#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Abstractions;
using Hl7.Cql.Primitives;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Hl7.Cql.Comparers
{
    internal class TupleBaseTypeComparer : ICqlComparer<TupleBaseType?>, ICqlComparer
    {
        public TupleBaseTypeComparer(ICqlComparer memberComparer)
        {
            MemberComparer = memberComparer;
        }

        public ICqlComparer MemberComparer { get; }

        /// <summary>
        /// A tuple property and a compiled getter for it.
        /// </summary>
        internal sealed class TupleMember
        {
            public TupleMember(PropertyInfo property, Func<object, object?> getter)
            {
                Property = property;
                Getter = getter;
            }

            public PropertyInfo Property { get; }
            public Func<object, object?> Getter { get; }
        }

        // Tuple types are generated and fixed for the lifetime of the process, so their members
        // (in GetProperties() order) and compiled getters can be cached instead of reflecting
        // on every comparison.
        private static readonly ConcurrentDictionary<Type, TupleMember[]> MembersByType = new();

        internal static TupleMember[] GetMembers(Type tupleType) =>
            MembersByType.GetOrAdd(tupleType, static type => type
                .GetProperties()
                .Select(p => new TupleMember(p, CreateGetter(type, p)))
                .ToArray());

        private static Func<object, object?> CreateGetter(Type type, PropertyInfo property)
        {
            if (property.GetIndexParameters().Length > 0 || property.GetMethod is null)
                return instance => property.GetValue(instance);

            var instanceParameter = Expression.Parameter(typeof(object), "instance");
            var body = Expression.Convert(
                Expression.Property(Expression.Convert(instanceParameter, type), property),
                typeof(object));
            return Expression.Lambda<Func<object, object?>>(body, instanceParameter).Compile();
        }

        public int? Compare(TupleBaseType? x, TupleBaseType? y, string? precision = null)
        {
            if (x == null || y == null)
                return null;
            var xType = x.GetType();
            var yType = y.GetType();
            if (xType != yType)
                return null;
            var members = GetMembers(xType);
            var result = 0;
            foreach (var member in members)
            {
                var compare = MemberComparer.Compare(member.Getter(x), member.Getter(y), precision);
                if (compare == null)
                    return null;
                if (compare.Value != 0)
                {
                    result += compare.Value;
                    break;
                }
            }
            return result;
        }



        public int? Compare(object? x, object? y, string? precision = null) =>
            Compare(x as TupleBaseType, y as TupleBaseType, null);

        public bool? Equals(TupleBaseType? x, TupleBaseType? y, string? precision = null) =>
            Compare(x, y, null) == 0;

        public bool? Equals(object? x, object? y, string? precision = null) =>
            Compare(x, y, null) == 0;

        public bool Equivalent(TupleBaseType? x, TupleBaseType? y, string? precision = null)
        {
            if (x == null)
            {
                if (y == null)
                    return true;
                else return false;
            }
            else if (y == null)
                return false;

            var xType = x.GetType();
            var yType = y.GetType();
            if (xType != yType)
                return false;
            var members = GetMembers(xType);
            foreach (var member in members)
            {
                if (!MemberComparer.Equivalent(member.Getter(x), member.Getter(y), precision))
                    return false;
            }
            return true;
        }

        public bool Equivalent(object? x, object? y) =>
            Equivalent(x as TupleBaseType, y as TupleBaseType);

        public bool Equivalent(object? x, object? y, string? precision = null) =>
            Equivalent(x as TupleBaseType, y as TupleBaseType);

        public int GetHashCode(TupleBaseType? obj) => obj?.GetHashCode() ?? typeof(TupleBaseType).GetHashCode() ^ 098174506;
        public int GetHashCode(object obj) => GetHashCode(obj as TupleBaseType);
    }
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member