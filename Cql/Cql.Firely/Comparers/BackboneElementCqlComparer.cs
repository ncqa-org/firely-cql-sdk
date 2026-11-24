#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Comparers;
using Hl7.Fhir.Model;

namespace Hl7.Cql.Fhir.Comparers
{
    /// <summary>
    /// Compares FHIR backbone elements (e.g. <c>ExplanationOfBenefit.ItemComponent</c>) by value using
    /// <c>IsExactly</c>. Backbone elements have no identity of their own, so unlike resources
    /// they cannot be compared by id. The result only distinguishes equal from not equal; it does not define an order.
    /// </summary>
    internal class BackboneElementCqlComparer : CqlComparerBase<BackboneElement>
    {
        public override int? Compare(BackboneElement? x, BackboneElement? y, string? precision)
        {
            if (x == null || y == null)
                return null;
            return x.IsExactly(y) ? 0 : 1;
        }

        public override bool Equivalent(BackboneElement? x, BackboneElement? y, string? precision)
        {
            if (x == null || y == null)
                return x == null && y == null;
            return x.IsExactly(y);
        }

        // Must be consistent with IsExactly: equal elements are always the same type, so hash by type only.
        public override int GetHashCode(BackboneElement? x) =>
            x == null ? typeof(BackboneElement).GetHashCode() : x.GetType().GetHashCode();
    }
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
