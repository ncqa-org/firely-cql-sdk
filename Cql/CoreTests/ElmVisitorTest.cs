using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hl7.Cql.Elm.Visitor;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoreTests
{
    [TestClass]
    public class ElmVisitorTest
    {
        private static readonly IFhirSerializationEngine _serializer =
            FhirSerializationEngineFactory.Ostrich(ModelInfo.ModelInspector);

        [TestMethod]
        public void CheckRequirementsResultsValid()
        {
            var measures = new DirectoryInfo("C:/Users/achisholm/OneDrive - National Committee for Quality Assurance/Documents/ExternalTestData/TESTDONOTCOMMIT/Measures").GetFiles("*.bundle.json");
            var patients = new DirectoryInfo("C:/Users/achisholm/OneDrive - National Committee for Quality Assurance/Documents/ExternalTestData/TESTDONOTCOMMIT/PatientsAll").GetFiles("*.bundle.json");

            // Valueset -> resources
            Dictionary<string, HashSet<string>> result = new();

            foreach (var f in measures)
            {
                using var fs = f.OpenRead();
                using var sr = new StreamReader(fs); 

                var measureBundle = _serializer.DeserializeFromJson(sr.ReadToEnd()) as Bundle;

                var drb = new DataRequirementsBuilder(measureBundle);
                var requirements = drb.Build();

                foreach (var r in requirements)
                {
                    foreach (var cf in r.CodeFilter)
                    {
                        if (cf.ValueSet != null)
                        {
                            result.TryAdd(cf.ValueSet, new());
                            result[cf.ValueSet].Add(cf.Path.Split('.')[0]);
                        }
                    }
                }

                break;
            }

            File.Delete("valuesetmap.csv");
            List<string> header = new();
            foreach (var n in result)
            {
                foreach (var x in n.Value)
                {
                    if (header.Contains(x)) continue;

                    header.Add(x);
                }
            }
            using (var vs = File.OpenWrite("valuesetmap.csv"))
            {
                using (var vswr = new StreamWriter(vs))
                {
                    foreach (var n in result)
                    {
                        vswr.WriteLine($"{n.Key}, {string.Join(',', n.Value)}");
                    }
                }
            }

            Console.WriteLine(new FileInfo("valuesetmap.csv").FullName);
        }
    }
}
