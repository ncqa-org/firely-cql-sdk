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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Hl7.Cql.CodeGeneration.NET.Visitors
{
    /// <summary>
    /// Makes the CQL <c>and</c>, <c>or</c> and <c>implies</c> operators short-circuit in generated code.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="SimplifyExpressionsVisitor"/> hoists every operand into a local variable before the operator is called,
    /// so both sides are always evaluated, even when the left operand already decides the result.
    /// This visitor runs after it and, for every assignment <c>t = And(a, b)</c> in a block, moves the assignments
    /// that only exist to compute <c>b</c> into a local function guarded by the left operand:
    /// </para>
    /// <code>
    /// bool? f_()
    /// {
    ///     if ((a_ is false))
    ///     {
    ///         return false;
    ///     }
    ///     else
    ///     {
    ///         var b_ = ...;          // only evaluated when needed
    ///         return context.Operators.And(a_, b_);
    ///     }
    /// };
    /// var t_ = f_();
    /// </code>
    /// <para>
    /// The three-valued semantics are unchanged: <c>false and x</c> is <c>false</c>, <c>true or x</c> is <c>true</c>,
    /// and <c>false implies x</c> is <c>true</c>, whatever <c>x</c> is (including <see langword="null"/>).
    /// The operator itself is still called in the non-short-circuit branch, so null handling stays in one place.
    /// </para>
    /// <para>
    /// The rewrite relies on the shape produced by <see cref="SimplifyExpressionsVisitor"/>: each hoisted variable is
    /// assigned once and used once. Only variables that are referenced exactly once (i.e. exclusively by the right
    /// operand) are moved; anything shared stays where it is. Directly invoked local functions do not allocate a
    /// closure in C#, so the guard costs no allocation.
    /// </para>
    /// </remarks>
    internal class ShortCircuitLogicalOperatorsVisitor : ExpressionVisitor
    {
        protected override Expression VisitBlock(BlockExpression node)
        {
            // Bottom-up: nested lambdas and blocks are rewritten first.
            var visited = (BlockExpression)base.VisitBlock(node);
            return Rewrite(visited);
        }

        private static BlockExpression Rewrite(BlockExpression block)
        {
            while (TryRewriteOne(block, out var rewritten))
                block = rewritten;

            return block;
        }

        private static bool TryRewriteOne(BlockExpression block, out BlockExpression rewritten)
        {
            rewritten = block;
            var expressions = block.Expressions;
            var blockVariables = new HashSet<ParameterExpression>(block.Variables);

            // Where (and whether uniquely) each block variable is assigned at the top level of this block.
            var assignmentIndex = new Dictionary<ParameterExpression, int>();
            for (var i = 0; i < expressions.Count; i++)
            {
                if (expressions[i] is BinaryExpression { NodeType: ExpressionType.Assign, Left: ParameterExpression assigned }
                    && blockVariables.Contains(assigned))
                {
                    assignmentIndex[assigned] = assignmentIndex.ContainsKey(assigned) ? -1 : i;
                }
            }

            var referenceCounts = CountReferences(expressions);

            // Start from the end so the outermost operator in a chain is handled first; the nested ones are
            // then handled when the guarded block is rewritten recursively.
            for (var i = expressions.Count - 1; i >= 0; i--)
            {
                if (!TryGetLogicalOperatorAssignment(expressions[i], out var target, out var call))
                    continue;

                var left = call.Arguments[0];
                var right = call.Arguments[1];

                if (left is ConstantExpression)
                    continue;

                var moved = CollectExclusiveDependencies(right, i, expressions, assignmentIndex, referenceCounts);
                if (moved.Count == 0 && !HasWork(right))
                    continue;

                rewritten = Build(block, i, target, call, moved);
                return true;
            }

            return false;
        }

        private static BlockExpression Build(BlockExpression block, int index, ParameterExpression target,
            MethodCallExpression call, SortedSet<int> moved)
        {
            var expressions = block.Expressions;
            var variables = block.Variables.ToList();
            var newExpressions = new List<Expression>(expressions.Count);

            var (shortCircuitWhen, shortCircuitResult) = call.Method.Name switch
            {
                nameof(ICqlOperators.And) => (false, false),
                nameof(ICqlOperators.Or) => (true, true),
                nameof(ICqlOperators.Implies) => (false, true),
                _ => throw new InvalidOperationException($"Unexpected operator {call.Method.Name}."),
            };

            // The left operand is evaluated once, before the guard. Hoist it unless it is already a variable.
            Expression left = call.Arguments[0];
            Expression? leftAssignment = null;
            if (left is not ParameterExpression)
            {
                var leftVariable = Expression.Parameter(typeof(bool?));
                variables.Add(leftVariable);
                leftAssignment = Expression.Assign(leftVariable, left);
                left = leftVariable;
            }

            // Everything only needed by the right operand moves into the guarded branch, in its original order.
            var movedAssignments = moved.Select(i => expressions[i]).ToList();
            var movedVariables = movedAssignments
                .Select(a => (ParameterExpression)((BinaryExpression)a).Left)
                .ToList();
            variables.RemoveAll(movedVariables.Contains);

            var evaluateOperator = call.Update(call.Object, new[] { left, call.Arguments[1] });
            Expression otherwise = movedAssignments.Count == 0
                ? evaluateOperator
                : Rewrite(Expression.Block(movedVariables, movedAssignments.Append(evaluateOperator)));

            var guard = new CaseWhenThenExpression(
                new[]
                {
                    new CaseWhenThenExpression.WhenThenCase(
                        Expression.Equal(left, Expression.Constant(shortCircuitWhen, typeof(bool?))),
                        Expression.Constant(shortCircuitResult, typeof(bool?))),
                }.ToList().AsReadOnly(),
                otherwise);

            var function = Expression.Parameter(typeof(Func<bool?>));
            variables.Add(function);

            for (var i = 0; i < expressions.Count; i++)
            {
                if (moved.Contains(i))
                    continue;

                if (i == index)
                {
                    if (leftAssignment is not null)
                        newExpressions.Add(leftAssignment);
                    newExpressions.Add(Expression.Assign(function, Expression.Lambda<Func<bool?>>(Expression.Block(guard))));
                    newExpressions.Add(Expression.Assign(target, Expression.Invoke(function)));
                }
                else
                {
                    newExpressions.Add(expressions[i]);
                }
            }

            return Expression.Block(block.Type, variables, newExpressions);
        }

        private static bool TryGetLogicalOperatorAssignment(Expression expression,
            out ParameterExpression target, out MethodCallExpression call)
        {
            if (expression is BinaryExpression
                {
                    NodeType: ExpressionType.Assign,
                    Left: ParameterExpression t,
                    Right: MethodCallExpression mce,
                }
                && mce.Object is not null
                && mce.Method.Name is nameof(ICqlOperators.And) or nameof(ICqlOperators.Or) or nameof(ICqlOperators.Implies)
                && typeof(ICqlOperators).IsAssignableFrom(mce.Method.DeclaringType)
                && mce.Method.ReturnType == typeof(bool?)
                && mce.Arguments.Count == 2
                && mce.Arguments[0].Type == typeof(bool?)
                && mce.Arguments[1].Type == typeof(bool?))
            {
                target = t;
                call = mce;
                return true;
            }

            target = null!;
            call = null!;
            return false;
        }

        /// <summary>
        /// Finds the top-level assignments (before <paramref name="limit"/>) whose variables are used only
        /// by <paramref name="root"/>, directly or through other such assignments.
        /// </summary>
        private static SortedSet<int> CollectExclusiveDependencies(Expression root, int limit,
            IReadOnlyList<Expression> expressions,
            Dictionary<ParameterExpression, int> assignmentIndex,
            Dictionary<ParameterExpression, int> referenceCounts)
        {
            var result = new SortedSet<int>();
            var pending = new Stack<Expression>();
            pending.Push(root);

            while (pending.Count > 0)
            {
                foreach (var parameter in ParameterCollector.Collect(pending.Pop()))
                {
                    if (assignmentIndex.TryGetValue(parameter, out var index)
                        && index >= 0
                        && index < limit
                        && referenceCounts.TryGetValue(parameter, out var count)
                        && count == 1
                        && result.Add(index))
                    {
                        pending.Push(((BinaryExpression)expressions[index]).Right);
                    }
                }
            }

            return result;
        }

        private static Dictionary<ParameterExpression, int> CountReferences(IEnumerable<Expression> expressions)
        {
            var counter = new ReferenceCounter();
            foreach (var expression in expressions)
            {
                // The left-hand side of a top-level assignment is the definition, not a use.
                if (expression is BinaryExpression { NodeType: ExpressionType.Assign, Left: ParameterExpression } assignment)
                    counter.Visit(assignment.Right);
                else
                    counter.Visit(expression);
            }
            return counter.Counts;
        }

        /// <summary>True if evaluating the expression involves a call, i.e. is worth skipping.</summary>
        private static bool HasWork(Expression expression)
        {
            var finder = new WorkFinder();
            finder.Visit(expression);
            return finder.Found;
        }

        private sealed class ReferenceCounter : ExpressionVisitor
        {
            public Dictionary<ParameterExpression, int> Counts { get; } = new();

            protected override Expression VisitParameter(ParameterExpression node)
            {
                Counts[node] = Counts.TryGetValue(node, out var count) ? count + 1 : 1;
                return node;
            }
        }

        private sealed class ParameterCollector : ExpressionVisitor
        {
            private readonly HashSet<ParameterExpression> _found = new();

            public static IEnumerable<ParameterExpression> Collect(Expression expression)
            {
                var collector = new ParameterCollector();
                collector.Visit(expression);
                return collector._found;
            }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                _found.Add(node);
                return node;
            }
        }

        private sealed class WorkFinder : ExpressionVisitor
        {
            public bool Found { get; private set; }

            public override Expression? Visit(Expression? node) => Found ? node : base.Visit(node);

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                Found = true;
                return node;
            }

            protected override Expression VisitInvocation(InvocationExpression node)
            {
                Found = true;
                return node;
            }

            protected override Expression VisitExtension(Expression node)
            {
                Found = true;
                return node;
            }
        }
    }
}
