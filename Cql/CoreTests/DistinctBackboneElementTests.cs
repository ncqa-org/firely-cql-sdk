using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Hl7.Cql.Abstractions;
using Hl7.Cql.Fhir;
using Hl7.Cql.Runtime;
using Hl7.Fhir.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CoreTests
{
    [TestClass]
    public class DistinctBackboneElementTests
    {
        public class EobItemTuple : TupleBaseType
        {
            public ExplanationOfBenefit? eob { get; set; }
            public ExplanationOfBenefit.ItemComponent? item { get; set; }
        }

        private static CqlContext GetNewContext() => FhirCqlContext.WithDataSource();

        private static ExplanationOfBenefit NewEob(string id, params int[] sequences) => new()
        {
            Id = id,
            Item = sequences.Select(s => new ExplanationOfBenefit.ItemComponent { Sequence = s }).ToList(),
        };

        [TestMethod]
        public void Distinct_SameEobDifferentItems_KeepsAll()
        {
            var ctx = GetNewContext();
            var eob = NewEob("eob-1", 1, 2);
            var tuples = eob.Item.Select(i => new EobItemTuple { eob = eob, item = i }).ToList();

            var result = ctx.Operators.ListDistinct(tuples)!.ToList();

            result.Should().HaveCount(2);
        }

        [TestMethod]
        public void Distinct_SameEobEqualItems_RemovesDuplicate()
        {
            var ctx = GetNewContext();
            var eob = NewEob("eob-1", 1);
            var tuples = new[]
            {
                new EobItemTuple { eob = eob, item = new ExplanationOfBenefit.ItemComponent { Sequence = 1 } },
                new EobItemTuple { eob = eob, item = new ExplanationOfBenefit.ItemComponent { Sequence = 1 } },
            };

            var result = ctx.Operators.ListDistinct(tuples)!.ToList();

            result.Should().HaveCount(1);
        }
    }
}
