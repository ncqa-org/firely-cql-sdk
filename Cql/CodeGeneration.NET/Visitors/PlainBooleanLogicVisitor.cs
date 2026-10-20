/*
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 *
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Compiler;
using Hl7.Cql.Operators;
using System;
using System.Linq.Expressions;

namespace Hl7.Cql.CodeGeneration.NET.Visitors
{
    /// <summary>
    /// Replaces three-valued CQL logic on operands that can never be null with plain C# logic, so generated code like
    /// <code>if ((context.Operators.Or((bool?)(eob is null), (bool?)(item is null)) ?? false))</code>
    /// reads as
    /// <code>if (((eob is null) || (item is null)))</code>
    /// </summary>
    /// <remarks>
    /// Only applies when both operands are non-nullable <see cref="bool"/> values lifted to <c>bool?</c>, where
    /// CQL <c>and</c>/<c>or</c>/<c>not</c> are exactly C# <c>&amp;&amp;</c>/<c>||</c>/<c>!</c>. Operands must also be free of side
    /// effects (null checks, type tests, comparisons of variables and members), so skipping the right operand when the
    /// left decides the result cannot change behavior. Must run before <see cref="SimplifyExpressionsVisitor"/>.
    /// </remarks>
    internal class PlainBooleanLogicVisitor : ExpressionVisitor
    {
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var visited = base.VisitMethodCall(node);
            if (visited is not MethodCallExpression call
                || call.Object is null
                || !typeof(ICqlOperators).IsAssignableFrom(call.Method.DeclaringType)
                || call.Method.ReturnType != typeof(bool?))
                return visited;

            switch (call.Method.Name)
            {
                case nameof(ICqlOperators.And) or nameof(ICqlOperators.Or) when call.Arguments.Count == 2
                    && TryUnlift(call.Arguments[0], out var left)
                    && TryUnlift(call.Arguments[1], out var right):
                    {
                        var logic = call.Method.Name == nameof(ICqlOperators.And)
                            ? Expression.AndAlso(left, right)
                            : Expression.OrElse(left, right);
                        return Expression.Convert(logic, typeof(bool?));
                    }
                case nameof(ICqlOperators.Not) when call.Arguments.Count == 1
                    && TryUnlift(call.Arguments[0], out var operand):
                    return Expression.Convert(Expression.Not(operand), typeof(bool?));
                default:
                    return visited;
            }
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var visited = base.VisitBinary(node);

            // (bool?)x ?? false  =>  x   (x cannot be null)
            if (visited is BinaryExpression
                {
                    NodeType: ExpressionType.Coalesce,
                    Left: UnaryExpression { NodeType: ExpressionType.Convert, Operand: { Type: var operandType } operand } lifted,
                    Right: ConstantExpression { Value: bool }
                } coalesce
                && operandType == typeof(bool)
                && lifted.Type == typeof(bool?)
                && coalesce.Type == typeof(bool))
            {
                return operand;
            }

            return visited;
        }

        /// <summary>Matches <c>(bool?)x</c> where <c>x</c> is a side-effect free <see cref="bool"/>.</summary>
        private static bool TryUnlift(Expression expression, out Expression unlifted)
        {
            if (expression is UnaryExpression { NodeType: ExpressionType.Convert, Operand: var operand }
                && expression.Type == typeof(bool?)
                && operand.Type == typeof(bool)
                && IsPure(operand))
            {
                unlifted = operand;
                return true;
            }

            unlifted = expression;
            return false;
        }

        private static bool IsPure(Expression expression) => expression switch
        {
            ParameterExpression or ConstantExpression or DefaultExpression => true,
            NullConditionalMemberExpression nullConditional => IsPure(nullConditional.MemberExpression),
            MemberExpression { Member: System.Reflection.FieldInfo } field => field.Expression is null || IsPure(field.Expression),
            // Property getters on FHIR/CQL model types are plain field accesses.
            MemberExpression { Member: System.Reflection.PropertyInfo } property => property.Expression is null || IsPure(property.Expression),
            TypeBinaryExpression typeBinary => IsPure(typeBinary.Expression),
            UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs or ExpressionType.Not } unary
                when unary.Method is null => IsPure(unary.Operand),
            BinaryExpression
            {
                NodeType: ExpressionType.Equal or ExpressionType.NotEqual or ExpressionType.AndAlso or ExpressionType.OrElse
            } binary when binary.Method is null => IsPure(binary.Left) && IsPure(binary.Right),
            _ => false,
        };
    }
}
