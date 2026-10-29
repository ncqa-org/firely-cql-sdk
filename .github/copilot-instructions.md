# Copilot Instructions

.NET (net10.0, C# 14) SDK for [CQL](https://cql.hl7.org/): executes ELM or compiles ELM into .NET assemblies. Branching is Git Flow; PRs target `develop`.

## Build, test, lint

- Solutions: `Cql.sln` (engine + `PackagerCLI`) and `Demo\Demo.sln` (CQL → ELM → C# demo). Build `Cql.sln` first; Demo references its projects.
- Build: `dotnet build Cql.sln` (CI adds `/p:ContinuousIntegrationBuild=true`). Locally, `CoreTests` has package pins that trip NU1605 and the NuGet audit feed may be unreachable; add `-p:NuGetAudit=false -p:FirelySdkVersion=6.8.0 -p:TreatWarningsAsErrors=false -p:NoWarn=NU1605`.
- Tests (MSTest + FluentAssertions): CI runs `**/*Tests/*Tests.csproj`.
  - All: `dotnet test Cql\CoreTests\CoreTests.csproj` (also `Cql\Cql.Firely.Test`)
  - Single test: `dotnet test Cql\CoreTests\CoreTests.csproj --filter "FullyQualifiedName~ClassName.MethodName"`
- No separate lint step: `cql-sdk.props` sets `TreatWarningsAsErrors=true`, `Nullable=enable`, and doc-file generation, so warnings (including missing XML docs on public members) break the build. Style is in `.editorconfig` (CRLF line endings for `.cs`).
- Pre-release Firely packages come from the GitHub NuGet feed `https://nuget.pkg.github.com/FirelyTeam/index.json` (needs a PAT with `read:packages`; see README "Dev Start").
- Demo's CQL → ELM step needs JDK and Maven (see `docs\getting-started.md`).

## Architecture

Pipeline: **CQL → ELM (Java tool, external) → `Cql.Compiler` builds `System.Linq.Expressions` → C# source + compiled assemblies → packaged as FHIR `Library` resources → executed via `Cql.Runtime`.**

Projects under `Cql\`:
- `Elm` – ELM object model (all expression nodes public). `Cql.Model`, `Cql.Primitives` (CqlCode, CqlDate, CqlInterval<T>…), `Iso8601`, `Cql.Comparers`, `Cql.Conversion` – types/semantics.
- `Cql.Compiler` – `ExpressionBuilder` (partial class split per CQL operator category, e.g. `ExpressionBuilder.ListOperators.cs`) translates ELM to expressions; `IExpressionMutator` allows model-specific rewrites (`Cql.Compilers.Mutators.FhirR4`).
- `CodeGeneration.NET` – turns expressions into C#.
- `Cql.Packaging` / `PackagerCLI` – compile and package ELM + C# + assemblies into FHIR Libraries (`LibraryPackager`).
- `Cql.Runtime` – `CqlContext`, `DefinitionDictionary<T>`; `Cql.Operators` – `ICqlOperators`, `IDataRetriever`; `Cql.ValueSets`; `Cql.Firely` – Firely FHIR binding.
- `CoreTests` – unit tests including HL7 CQL spec test suites (`HL7Tests.*`) with XML inputs under `Input`.

`docs\CQL Engine v2.png` and `docs\public-surface.md` give the overview (may be dated).

## Conventions

- **Generated context libraries** implement `ICqlLibraryInstance` and take `(CqlContext, CqlLibraryScope)`; reuse them across patients with `MeasureSession` (see `docs\reusable-measures.md`). Cached definition values must be cleared via `ResetPatientValues`/`ResetSharedValues`, never evaluated lazily outside a session.
- **Public API is tracked**: projects use `PublicAPI.Shipped.txt` / `PublicAPI.Unshipped.txt`. New or changed public members must be added to `PublicAPI.Unshipped.txt`. Keep surface minimal—many assemblies are intentionally (mostly) internal; see `docs\public-surface.md` before making types public.
- Shared build settings (version, signing, package metadata, `FirelySdkVersion`) live in `cql-sdk.props`; Release builds are delay-signed with `cql-sdk.publickey`.
- PRs follow `.github\PULL_REQUEST_TEMPLATE\pull_request_template.md` (description, related issues, testing, label `breaking change` when applicable; short title).
