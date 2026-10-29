#nullable enable
using FluentAssertions;
using Hl7.Cql.Abstractions;
using Hl7.Cql.Fhir;
using Hl7.Cql.Packaging;
using Hl7.Cql.Primitives;
using Hl7.Cql.Runtime;
using Hl7.Cql.ValueSets;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CoreTests
{
    /// <summary>
    /// Compiles the Demo BCS measure from its ELM, and checks that reusing one <see cref="MeasureSession"/>
    /// for many patients gives the same results as creating new instances.
    /// </summary>
    [TestClass]
    public class MeasureSessionTests
    {
        private static readonly DirectoryInfo Demo = FindDemo();
        private static Type? _bcsType;
        private static Bundle? _pass;
        private static Bundle? _fail;
        private static IValueSetDictionary? _valueSets;

        private static readonly IDictionary<string, object> MY2022 = new Dictionary<string, object>
        {
            ["Measurement Period"] = new CqlInterval<CqlDateTime>(
                new CqlDateTime(2022, 1, 1, 0, 0, 0, 0, 0, 0),
                new CqlDateTime(2022, 12, 31, 0, 0, 0, 0, 0, 0),
                true,
                true),
        };

        private static DirectoryInfo FindDemo()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Cql.sln")))
                dir = dir.Parent;
            return new DirectoryInfo(Path.Combine(dir!.FullName, "Demo"));
        }

        [ClassInitialize]
        public static void Initialize(TestContext _)
        {
            var asmContext = LibraryPackager.LoadElm(
                new DirectoryInfo(Path.Combine(Demo.FullName, "Elm", "Json")),
                "BCSEHEDISMY2022", "1.0.0",
                LoggerFactory.Create(_ => { }),
                100);

            _bcsType = asmContext.Assemblies
                .SelectMany(a => a.GetTypes())
                .Single(t => t.GetCustomAttribute<CqlLibraryAttribute>()?.Identifier == "BCSEHEDISMY2022");

            var options = new JsonSerializerOptions().ForFhir(new FhirJsonPocoDeserializerSettings { Validator = null });
            var engine = FhirSerializationEngineFactory.Ostrich(ModelInfo.ModelInspector);
            Bundle load(string name) => (Bundle)engine.DeserializeFromJson(
                File.ReadAllText(Path.Combine(Demo.FullName, "Test", "Inputs", "Bundles", name)));
            _pass = load("95029_pass.json");
            _fail = load("95029_fail.json");

            var valueSets = new List<ValueSet>();
            foreach (var file in new DirectoryInfo(Path.Combine(Demo.FullName, "Test", "Inputs", "ValueSets")).GetFiles("*.json"))
            {
                using var fs = file.OpenRead();
                var resource = JsonSerializer.Deserialize<Resource>(fs, options);
                if (resource is Bundle b)
                    valueSets.AddRange(b.Entry.Select(e => e.Resource).OfType<ValueSet>());
                else if (resource is ValueSet vs)
                    valueSets.Add(vs);
            }
            _valueSets = valueSets.ToValueSetDictionary(false);
        }

        private static bool? Numerator(object measure) =>
            (bool?)_bcsType!.GetMethod("Numerator")!.Invoke(measure, null);

        private static bool? FreshNumerator(Bundle bundle)
        {
            var context = FhirCqlContext.ForBundle(bundle, MY2022, _valueSets);
            return Numerator(Activator.CreateInstance(_bcsType!, context)!);
        }

        private MeasureSession NewSession() =>
            new(FhirCqlContext.ForBundle(null, MY2022, _valueSets));

        [TestMethod]
        public void ReusedSession_GivesSameResultsAsNewInstances()
        {
            var expectedPass = FreshNumerator(_pass!);
            var expectedFail = FreshNumerator(_fail!);
            expectedPass.Should().BeTrue();
            expectedFail.Should().BeFalse();

            var session = NewSession();
            var bcs = session.Get(_bcsType!);
            foreach (var (bundle, expected) in new[] { (_pass!, expectedPass), (_fail!, expectedFail), (_pass!, expectedPass), (_fail!, expectedFail) })
                session.Evaluate(bundle, _ => Numerator(bcs)).Should().Be(expected);
        }

        [TestMethod]
        public void Session_ReturnsSameInstanceForSameType()
        {
            var session = NewSession();
            session.Get(_bcsType!).Should().BeSameAs(session.Get(_bcsType!));
        }

        [TestMethod]
        public void SharedLibraries_ExistOncePerSession()
        {
            var session = NewSession();
            var root = session.Get(_bcsType!);

            var found = new List<ICqlLibraryInstance>();
            void walk(ICqlLibraryInstance instance)
            {
                foreach (var property in instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (typeof(ICqlLibraryInstance).IsAssignableFrom(property.PropertyType)
                        && property.GetValue(instance) is ICqlLibraryInstance dependency)
                    {
                        found.Add(dependency);
                        walk(dependency);
                    }
                }
            }
            walk(root);

            found.Should().NotBeEmpty();
            foreach (var group in found.GroupBy(f => f.GetType()))
                group.Distinct().Should().HaveCount(1, $"{group.Key.Name} should be shared");
        }

        [TestMethod]
        public void PatientValues_AreDiscardedAndParameterValuesAreKept()
        {
            var session = NewSession();
            var bcs = session.Get(_bcsType!);
            object? get(string name) => _bcsType!.GetMethod(name)!.Invoke(bcs, null);

            var (period1, patient1) = session.Evaluate(_pass!, _ => (get("Measurement_Period"), get("Patient")));
            var (period2, patient2) = session.Evaluate(_fail!, _ => (get("Measurement_Period"), get("Patient")));

            period2.Should().BeSameAs(period1, "the measurement period does not depend on the patient");
            patient1.Should().NotBeNull();
            patient2.Should().NotBeNull();
            patient2.Should().NotBeSameAs(patient1);
        }

        [TestMethod]
        public void SetParameters_DiscardsParameterValues()
        {
            var session = NewSession();
            var bcs = session.Get(_bcsType!);
            object? period() => _bcsType!.GetMethod("Measurement_Period")!.Invoke(bcs, null);

            var before = session.Evaluate(_pass!, _ => period());
            session.SetParameters(new Dictionary<string, object>
            {
                ["Measurement Period"] = new CqlInterval<CqlDateTime>(
                    new CqlDateTime(2023, 1, 1, 0, 0, 0, 0, 0, 0),
                    new CqlDateTime(2023, 12, 31, 0, 0, 0, 0, 0, 0),
                    true,
                    true),
            });
            var after = session.Evaluate(_pass!, _ => period());

            after.Should().NotBeSameAs(before);
        }

        [TestMethod]
        public void Pool_ReusesSessionsAcrossParallelWork()
        {
            var created = 0;
            var pool = new MeasureSessionPool(() =>
            {
                Interlocked.Increment(ref created);
                return NewSession();
            });

            var results = new bool?[40];
            Parallel.For(0, results.Length, new ParallelOptions { MaxDegreeOfParallelism = 4 }, i =>
            {
                var bundle = i % 2 == 0 ? _pass! : _fail!;
                results[i] = pool.Evaluate(bundle, s => Numerator(s.Get(_bcsType!)));
            });

            for (var i = 0; i < results.Length; i++)
                results[i].Should().Be(i % 2 == 0);
            created.Should().BeLessThanOrEqualTo(4);
        }
    }
}
