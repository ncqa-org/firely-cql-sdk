/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Hl7.Cql.CodeGeneration.NET.Visitors
{
    /// <summary>
    /// Removes the local-function wrapper around an <c>if</c>/<c>else</c> that is the last thing a block does.
    /// </summary>
    /// <remarks>
    /// <see cref="SimplifyExpressionsVisitor"/> wraps every case/when (and <see cref="ShortCircuitLogicalOperatorsVisitor"/>
    /// every guard) in a local function so it can be used as an expression:
    /// <code>
    /// bool? a_()
    /// {
    ///     if (x) { return ...; } else { ...; return ...; }
    /// };
    /// return a_();
    /// </code>
    /// When the result is simply returned (directly, or through a variable used only for that), the <c>if</c>/<c>else</c>
    /// can be the tail of the enclosing block, because its <c>return</c>s produce exactly the value the wrapper would have:
    /// <code>
    /// if (x) { return ...; } else { ...; return ...; }
    /// </code>
    /// Runs after <see cref="ShortCircuitLogicalOperatorsVisitor"/> and before <see cref="RenameVariablesVisitor"/>.
    /// </remarks>
    internal class InlineTailLocalFunctionVisitor : ExpressionVisitor
    {
        protected override Expression VisitBlock(BlockExpression node)
        {
            var block = (BlockExpression)base.VisitBlock(node);
            return TryInline(block) ?? block;
        }

        private static BlockExpression? TryInline(BlockExpression block)
        {
            var expressions = block.Expressions;
            var count = expressions.Count;

            // Pattern 1: [..., f = () => { cwt }, f()]
            // Pattern 2: [..., f = () => { cwt }, t = f(), t]
            ParameterExpression function;
            ParameterExpression? result = null;
            int functionIndex;
            if (count >= 2 && IsInvocationOf(expressions[count - 1], out function))
            {
                functionIndex = count - 2;
            }
            else if (count >= 3
                && expressions[count - 1] is ParameterExpression returned
                && expressions[count - 2] is BinaryExpression { NodeType: ExpressionType.Assign, Left: ParameterExpression assigned, Right: var invoked }
                && assigned == returned
                && IsInvocationOf(invoked, out function))
            {
                result = returned;
                functionIndex = count - 3;
            }
            else
            {
                return null;
            }

            if (expressions[functionIndex] is not BinaryExpression
                {
                    NodeType: ExpressionType.Assign,
                    Left: ParameterExpression definedFunction,
                    Right: LambdaExpression { Parameters.Count: 0, Body: BlockExpression { Variables.Count: 0, Expressions.Count: 1 } body } lambda
                }
                || definedFunction != function
                || body.Expressions[0] is not CaseWhenThenExpression caseWhenThen
                || caseWhenThen.Type != block.Type
                || lambda.ReturnType != block.Type)
                return null;

            // The function (and the result variable) must not be used anywhere else.
            var counter = new ReferenceCounter();
            foreach (var e in expressions)
                counter.Visit(e);
            if (counter.Count(function) != 2 || (result is not null && counter.Count(result) != 2))
                return null;

            var newExpressions = expressions.Take(functionIndex).Append(caseWhenThen).ToList();
            var newVariables = block.Variables.Where(v => v != function && v != result).ToList();
            return Expression.Block(block.Type, newVariables, newExpressions);
        }

        private static bool IsInvocationOf(Expression expression, out ParameterExpression function)
        {
            if (expression is InvocationExpression { Arguments.Count: 0, Expression: ParameterExpression f })
            {
                function = f;
                return true;
            }
            function = null!;
            return false;
        }

        private sealed class ReferenceCounter : ExpressionVisitor
        {
            private readonly Dictionary<ParameterExpression, int> _counts = new();

            public int Count(ParameterExpression parameter) => _counts.TryGetValue(parameter, out var c) ? c : 0;

            protected override Expression VisitParameter(ParameterExpression node)
            {
                _counts[node] = Count(node) + 1;
                return node;
            }
        }
    }
}
