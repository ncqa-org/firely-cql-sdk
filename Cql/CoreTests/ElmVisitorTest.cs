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
            
        }
    }
}
