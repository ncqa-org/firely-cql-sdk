# Reusable measures: evaluating many patients efficiently

This describes how to evaluate generated measures for many patients without allocating a context or measure instances per patient, and how to use that safely from parallel code.

## What changed

| Piece | Where | Purpose |
|---|---|---|
| `CqlLibraryScope`, `ICqlLibraryInstance` | `Cql.Runtime` | Owns all library instances of a worker. A library included by several libraries or measures exists once, so its definitions are evaluated once per patient. |
| Generated constructors | `CSharpSourceCodeWriter` | Each generated library has `(CqlContext)` and `(CqlContext, CqlLibraryScope)` constructors (plus a `cache` parameter variant when a Cache library is used), implements `ICqlLibraryInstance`, and gets dependencies through `scope.GetOrAdd`. |
| Patient-independent caching | `PatientIndependenceAnalyzer` | Definitions that never retrieve data, raise messages, or reference patient-dependent definitions (value sets, parameters, anything derived only from them) keep their cached value across patients. |
| `MeasureSession` | `Cql.Firely` | One `CqlContext` + one scope + the measure instances of one worker. `Evaluate(bundle, ...)` sets the bundle, runs your callback, then resets patient values in a `finally`. |
| `MeasureSessionPool` | `Cql.Firely` | Thread-safe pool of sessions for async or task-based work. |

Reset levels:

- `scope.Reset()` / end of `MeasureSession.Evaluate`: discards values that depend on the patient. Values that depend only on parameters and value sets are kept.
- `scope.ResetAll()` / `session.SetParameters(...)`: discards everything. Use when parameters or the evaluation time (`now`) change.

> Measures must be regenerated with this version of the generator. Previously generated C# does not implement `ICqlLibraryInstance` and has no `(context, scope)` constructor, so `MeasureSession.Get<T>()` will not work with it.

## Rules

1. **One session per thread at a time.** Sessions, contexts and library instances are not thread-safe. Never share one across threads.
2. **Create sessions once per worker, not per patient.** Creating the context (operators, comparers, unit conversion) is the expensive part.
3. **Materialize results inside the `Evaluate` callback.** Many definitions return deferred `IEnumerable`s over the patient's bundle. After `Evaluate` returns, the cached values are cleared and the context points at the next patient. Convert to `bool?`, counts, lists, or DTOs before returning.
4. **Parameters and `now` are fixed per session.** For a different measurement period, use `SetParameters` (clears all caches) or a separate session.
5. **Subscribe to `context.MessageReceived` once per session**, when you create it, not per patient.
6. **Value sets can be shared** between sessions only if the `IValueSetDictionary` is not modified after load. Load once, pass to every session.

## Patterns

### One session per worker with `Parallel.ForEach` (CPU-bound batch)

```csharp
var valueSets = LoadValueSets();                 // load once, read-only afterwards
var parameters = new Dictionary<string, object> { ["Measurement Period"] = period };

Parallel.ForEach(
    patientSource,                               // e.g. IEnumerable<Func<Bundle>> or file paths
    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
    () => new MeasureSession(FhirCqlContext.ForBundle(null, parameters, valueSets)),   // localInit: once per worker
    (patient, state, index, session) =>
    {
        var bundle = Load(patient);
        var result = session.Evaluate(bundle, s =>
        {
            var bcs = s.Get<BCSEHEDISMY2022_1_0_0>();   // created on first use, then reused
            var col = s.Get<COLEHEDISMY2022_1_0_0>();   // shares FHIRHelpers, NCQA libraries with bcs
            return new PatientResult(patient.Id, bcs.Numerator(), col.Numerator());
        });
        results.Add(result);
        return session;
    },
    _ => { });
```

Notes:
- `Parallel.ForEach` with `localInit` gives each worker its own session. Do not use `ThreadLocal<T>` with `async`; the continuation can run on another thread.
- Evaluate **all measures for a patient in the same `Evaluate` call**: shared libraries are computed once for that patient.

### Pool for async or task-based work

```csharp
var pool = new MeasureSessionPool(() =>
    new MeasureSession(FhirCqlContext.ForBundle(null, parameters, valueSets)));

await Parallel.ForEachAsync(patients, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (p, ct) =>
{
    var bundle = await LoadAsync(p, ct);                          // I/O outside the session
    var result = pool.Evaluate(bundle, s => Compute(s));          // CPU inside, synchronous
    await SaveAsync(result, ct);
});
```

Keep the callback synchronous: a session must not be used by two callers at once, and it is returned to the pool when `Evaluate` returns. Bound concurrency with `MaxDegreeOfParallelism`; the pool grows to the highest concurrent use and no further.

### Multiple processes

Nothing is shared between processes, so each process builds its own sessions as above. Partition patients across processes (by file range, queue, or hash of patient id). Per process, set `MaxDegreeOfParallelism` to roughly `cores / processesPerMachine` so processes do not oversubscribe CPUs.

### Measures loaded from assemblies at run time

```csharp
var measure = (ICqlLibraryInstance)session.Get(measureType);   // measureType from the loaded assembly
```

## Process settings worth trying for batch runs

Measure, do not assume. In the host application's project file or `runtimeconfig`:

```xml
<ServerGarbageCollector>true</ServerGarbageCollector>
<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>
<TieredPGO>true</TieredPGO>
```

Server GC uses a heap per core, which suits one-session-per-core workloads. With several processes per machine, Workstation non-concurrent GC may use less memory; compare both.

## Verifying a change

- Run a sample of patients through the old path (new context + new measure instances per patient) and the session path and compare every result.
- Measure allocations and throughput with `GC.GetAllocatedBytesForCurrentThread()` around a loop of N patients, or BenchmarkDotNet with `[MemoryDiagnoser]`.
- Watch for results that differ only when patients run back to back; that points at a deferred sequence escaping `Evaluate` (rule 3).

## Known limits and ideas not done yet

- `BundleDataSource` still builds an `IndexedBundle` per patient. Reusing its dictionaries between patients is a further saving.
- `CqlContext.ResolveParameter` concatenates a key string on every call; it is only called a few times per library now that parameter values are cached.
- Libraries that use a Cache library (`(context, cache, scope)` constructors) are generated but have not been exercised by a test.
- The patient-independence analysis is conservative. It treats any unrecognized use of the context, `Retrieve*`, and `Message` as patient-dependent. If a definition you expect to be cached across patients is not, check `ResetPatientValues` in the generated class: it lists the patient-dependent values.

---

# Picking this up again in the measures repository

This repo (`firely-cql-sdk`) is a submodule of the measures repository.

## State of the work

All changes are **uncommitted** in the submodule working tree. Before moving on:

1. Commit them inside the submodule (`cd` into it, `git add`, `git commit`) on a branch, e.g. a branch off `release/2_5_4`.
2. In the parent repo, `git add` the submodule path to record the new commit pointer.

Changed or added files:

- `Cql\Cql.Runtime\CqlLibraryScope.cs` (new), `Cql\Cql.Runtime\PublicAPI.Unshipped.txt`
- `Cql\CodeGeneration.NET\PatientIndependenceAnalyzer.cs` (new), `Cql\CodeGeneration.NET\CSharpSourceCodeWriter.cs`
- `Cql\Cql.Firely\MeasureSession.cs` (new), `Cql\Cql.Firely\MeasureSessionPool.cs` (new), `Cql\Cql.Firely\FhirCqlContext.cs` (doc comment), `Cql\Cql.Firely\PublicAPI.Unshipped.txt`
- `Cql\CoreTests\MeasureSessionTests.cs` (new)
- `.github\copilot-instructions.md` (new), `docs\reusable-measures.md` (this file)

## Build and test (commands that worked locally)

`Cql.sln` fails on its own because of pre-existing package pins in `CoreTests` (Fhir 5.8.2, Logging 8.0.0), and the NuGet audit feed can be unreachable. These flags work around both without editing project files:

```powershell
dotnet build Cql.sln -p:NuGetAudit=false -p:FirelySdkVersion=6.8.0 -p:TreatWarningsAsErrors=false -p:NoWarn=NU1605
dotnet test Cql\CoreTests -p:NuGetAudit=false -p:FirelySdkVersion=6.8.0 -p:TreatWarningsAsErrors=false -p:NoWarn=NU1605 --filter "FullyQualifiedName~MeasureSessionTests"
```

Result at handoff: solution builds with 0 errors; the 6 `MeasureSessionTests` pass. The rest of `CoreTests` was not run.

## Next steps in the measures repository

1. Build the SDK solution (above), then build the measures repository so it picks up the new SDK. Building generates the ELM for all measures.
2. **Regenerate the measures' C#** with the new generator. Confirm the generated classes contain `ICqlLibraryInstance`, a `(CqlContext, CqlLibraryScope)` constructor, and `ResetPatientValues` / `ResetSharedValues`.
3. Compare results: for a representative set of patients, assert identical outputs between the old per-patient-instance path and the `MeasureSession` path, for every measure.
4. Switch the host application to `MeasureSession` per worker (see Patterns). Evaluate all measures for a patient in one `Evaluate` call.
5. Benchmark allocations and throughput before and after; try the GC settings.
6. Check the measures' Cache libraries (if any) compile with the new constructors.
7. Look at the "Known limits" list for further savings once the numbers show where time goes.

## Prompt to give Copilot in the parent repository

> This repo includes `firely-cql-sdk` as a submodule. Read `<submodule path>\docs\reusable-measures.md` and `<submodule path>\.github\copilot-instructions.md`. The SDK now supports reusable measure instances through `MeasureSession` / `CqlLibraryScope`. Build the SDK solution with the flags in the guide, regenerate the measures with the new generator, then (1) verify the generated classes implement `ICqlLibraryInstance`, (2) write a comparison test that runs sample patients through the old per-patient path and the `MeasureSession` path and asserts identical results for every measure, and (3) update our parallel processing code to use one `MeasureSession` per worker. Follow the rules in the guide, especially materializing results inside `Evaluate`.

Useful context to give Copilot as well: how patients are loaded (files, queue, database), how many processes and cores you run, which measures run per patient, and where the current per-patient instantiation happens.
