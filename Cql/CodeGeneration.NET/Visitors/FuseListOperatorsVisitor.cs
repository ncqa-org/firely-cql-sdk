/*
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 *
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Operators;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Hl7.Cql.CodeGeneration.NET.Visitors
{
    /// <summary>
    /// Fuses list operator pipelines that would otherwise materialize an intermediate list:
    /// <list type="bullet">
    /// <item><c>ExistsInList(WhereOrNull(source, p))</c> becomes <c>ExistsWhere(source, p)</c>, which stops at the first match.</item>
    /// <item><c>FlattenList(SelectOrNull(source, f))</c> becomes <c>FlattenSelect(source, f)</c>, which builds a single list.</item>
    /// </list>
    /// Must run before <see cref="SimplifyExpressionsVisitor"/>, while calls are still nested.
    /// </summary>
    internal class FuseListOperatorsVisitor : ExpressionVisitor
    {
        private static readonly MethodInfo ExistsWhereDefinition =
            typeof(ICqlOperators).GetMethod(nameof(ICqlOperators.ExistsWhere))!;

        private static readonly MethodInfo FlattenSelectDefinition =
            typeof(ICqlOperators).GetMethod(nameof(ICqlOperators.FlattenSelect))!;

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Rewrite bottom-up so nested pipelines are fused as well.
            var visited = base.VisitMethodCall(node);
            if (visited is not MethodCallExpression call || call.Object is null || !IsOperatorsMethod(call.Method))
                return visited;

            return call.Method.Name switch
            {
                nameof(ICqlOperators.ExistsInList) => TryFuseExistsWhere(call) ?? call,
                nameof(ICqlOperators.FlattenList) => TryFuseFlattenSelect(call) ?? call,
                _ => call,
            };
        }

        private static Expression? TryFuseExistsWhere(MethodCallExpression exists)
        {
            if (exists.Arguments.Count != 1 || !TryGetOperatorsCall(exists.Arguments[0], nameof(ICqlOperators.WhereOrNull), out var where))
                return null;

            var elementType = where.Method.GetGenericArguments()[0];
            var predicate = where.Arguments[1];
            if (!typeof(Func<,>).MakeGenericType(elementType, typeof(bool?)).IsAssignableFrom(predicate.Type))
                return null;

            var method = ExistsWhereDefinition.MakeGenericMethod(elementType);
            return Expression.Call(exists.Object!, method, where.Arguments[0], predicate);
        }

        private static Expression? TryFuseFlattenSelect(MethodCallExpression flatten)
        {
            if (flatten.Arguments.Count != 1 || !TryGetOperatorsCall(flatten.Arguments[0], nameof(ICqlOperators.SelectOrNull), out var select))
                return null;

            if (select.Arguments[1] is not LambdaExpression selector || selector.Parameters.Count != 1)
                return null;

            var genericArguments = select.Method.GetGenericArguments();
            var sourceElementType = genericArguments[0];
            var flattenedElementType = flatten.Method.GetGenericArguments()[0];
            var innerListType = typeof(IEnumerable<>).MakeGenericType(flattenedElementType);

            // The selector must produce something that is (reference-)assignable to IEnumerable<R>.
            var selectorResultType = genericArguments[1];
            if (selectorResultType.IsValueType || !innerListType.IsAssignableFrom(selectorResultType))
                return null;

            var retypedSelector = selector.ReturnType == innerListType
                ? selector
                : Expression.Lambda(
                    typeof(Func<,>).MakeGenericType(sourceElementType, innerListType),
                    selector.Body,
                    selector.Parameters);

            var method = FlattenSelectDefinition.MakeGenericMethod(sourceElementType, flattenedElementType);
            return Expression.Call(flatten.Object!, method, select.Arguments[0], retypedSelector);
        }

        private static bool TryGetOperatorsCall(Expression expression, string methodName, out MethodCallExpression call)
        {
            // Look through reference conversions the compiler may have inserted, e.g. (IEnumerable<T>)WhereOrNull(...)
            while (expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } unary
                   && !unary.Type.IsValueType)
            {
                expression = unary.Operand;
            }

            if (expression is MethodCallExpression mce
                && mce.Object is not null
                && mce.Method.Name == methodName
                && IsOperatorsMethod(mce.Method))
            {
                call = mce;
                return true;
            }

            call = null!;
            return false;
        }

        private static bool IsOperatorsMethod(MethodInfo method) =>
            method.IsGenericMethod && typeof(ICqlOperators).IsAssignableFrom(method.DeclaringType);
    }
}
