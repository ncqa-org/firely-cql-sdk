/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Comparers;
using Hl7.Cql.Primitives;
using System;

namespace Hl7.Cql.Runtime
{
    /// <summary>
    /// Computes hash codes that are consistent with CQL equality (as implemented by the default
    /// <see cref="CqlComparers"/> registrations), so <c>distinct</c> can bucket elements instead of comparing
    /// every pair. Two elements that are CQL-equal always get the same hash; elements in the same bucket are
    /// still compared with the CQL comparer, so the result is identical to the pairwise algorithm.
    /// </summary>
    /// <remarks>
    /// Only types whose registered comparer is one of the known defaults are hashed. For any other type
    /// (resources, intervals, lists, custom registrations) <see cref="TryGetHash"/> returns
    /// <see langword="false"/> and the caller falls back to pairwise comparison.
    /// </remarks>
    internal sealed class DistinctHasher
    {
        private const int NullMemberHash = 0x5bd1e995;
        private const int OpaqueMemberHash = 17;

        private readonly CqlComparers _comparers;

        public DistinctHasher(CqlComparers comparers)
        {
            _comparers = comparers;
        }

        /// <summary>Gets a CQL-equality-consistent hash for a top-level (non-null) list element.</summary>
        public bool TryGetHash(object item, out int hash) => TryGetMemberHash(item, depth: 0, out hash);

        private bool TryGetMemberHash(object item, int depth, out int hash)
        {
            hash = 0;
            var type = item.GetType();
            var registered = _comparers.Comparers.TryGetValue(type, out var comparer);

            switch (item)
            {
                case string s when registered
                                   && comparer is StringCqlComparer sc
                                   && ReferenceEquals(sc.InnerComparer, StringComparer.Ordinal):
                    hash = StringComparer.Ordinal.GetHashCode(s.Normalize());
                    return true;
                case int i when registered && comparer is DefaultCqlComparer<int>:
                    hash = i;
                    return true;
                case long l when registered && comparer is DefaultCqlComparer<long>:
                    hash = l.GetHashCode();
                    return true;
                case bool b when registered && comparer is DefaultCqlComparer<bool>:
                    hash = b ? 1 : 2;
                    return true;
                case decimal d when registered && comparer is DecimalCqlComparer:
                    hash = DecimalCqlComparer.Truncate(d).GetHashCode();
                    return true;
                case CqlDate date when registered && comparer is InterfaceCqlComparer<CqlDate>:
                    // CqlDate.CompareTo (no precision) is 0 only when both dates have the same
                    // components at the same precision.
                    hash = HashCode.Combine(date.Value.Year, date.Value.Month, date.Value.Day);
                    return true;
                case TupleBaseType tuple when !registered && depth < 8 && UsesDefaultTupleComparer():
                    hash = HashTuple(tuple, depth);
                    return true;
                default:
                    return false;
            }
        }

        private bool UsesDefaultTupleComparer() =>
            _comparers.Comparers.TryGetValue(typeof(TupleBaseType), out var tupleComparer)
            && tupleComparer is TupleBaseTypeComparer tc
            && ReferenceEquals(tc.MemberComparer, _comparers);

        private int HashTuple(TupleBaseType tuple, int depth)
        {
            // Tuples are CQL-equal only when they have the same type and every member compares equal,
            // so combining member hashes is consistent. Members we can't hash contribute a constant.
            var hash = new HashCode();
            hash.Add(tuple.GetType());
            foreach (var member in TupleBaseTypeComparer.GetMembers(tuple.GetType()))
            {
                var value = member.Getter(tuple);
                if (value is null)
                    hash.Add(NullMemberHash);
                else if (TryGetMemberHash(value, depth + 1, out var memberHash))
                    hash.Add(memberHash);
                else
                    hash.Add(OpaqueMemberHash);
            }
            return hash.ToHashCode();
        }
    }
}
