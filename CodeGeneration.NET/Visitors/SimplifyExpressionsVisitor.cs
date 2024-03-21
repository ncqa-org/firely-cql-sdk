/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Compiler;
using Hl7.Cql.Compiler.Expressions;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;

namespace Hl7.Cql.CodeGeneration.NET.Visitors
{
    /// <summary>
    /// This Visitor will (in most cases) create a new variable for a nested expression, 
    /// and assign the visited node's expression to that variable, thus unwinding the deeply nested
    /// structure of Linq.Expression.
    /// e.g. exprA(exprB(4)) will be turned into
    ///     var b = exprB(4)
    ///     var a = exprA(b)
    ///     return a;
    /// </summary>
    internal class SimplifyExpressionsVisitor : ExpressionVisitor
    {
        private bool _atRoot = true;
        private readonly List<BinaryExpression> _assignments = new();

        public IReadOnlyCollection<BinaryExpression> Assignments => _assignments;
        private VariableNameGenerator _variableNameGenerator;

        public SimplifyExpressionsVisitor(VariableNameGenerator nameGenerator)
        {
            _variableNameGenerator = nameGenerator;
        }

        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node)
        {
            if (node is null) return null;

            // We needs a different action at the "root" of the tree than at the nodes of the tree,
            // see toBlock().
            if (_atRoot)
            {
                _atRoot = false;
                var visited = doVisit(node);
                return toBlock(visited);
            }
            else
                return doVisit(node);
        }

        private Expression doVisit(Expression node)
        {
            // This visit will, by default, call `simplify()` on every
            // type of node, which unwinds the nesting. Note that, even if you
            // override a specific visitor, simplify() will still be called on it.
            // If you do want to avoid a node to be simplified at all, you need
            // to include a special case in the switch below.
            return node switch
            {
                // Pass these expressions straight through
                ConstantExpression or
                ParameterExpression or
                NewExpression or
                MemberExpression or
                ElmAsExpression or
                NullConditionalMemberExpression => base.Visit(node),

                // These expressions require special handling
                ConditionalExpression cond => VisitConditional(cond),
                UnaryExpression unary => VisitUnary(unary),
                BinaryExpression binary => VisitBinary(binary),
                CaseWhenThenExpression cwt => VisitCaseWhenThenExpression(cwt),

                // Simplify all others.
                _ => simplify(base.Visit(node))
            };
        }

        private Expression simplify(Expression node)
        {
            if (node.Type == typeof(void)) return node;

            // transform complex into assignment to variable + variable
            var newLetVariable = Expression.Parameter(node.Type);
            var newAssign = Expression.Assign(newLetVariable, node);
            _assignments.Add(newAssign);

            return newLetVariable;
        }


        protected override Expression VisitConditional(ConditionalExpression node)
        {
            // Turn every nested conditional except the most simple ones into a Case/when/then
            if (isSimpleConditional(node))
                return node;

            var cwt = toCWT(node);
            return Visit(cwt);

            // simple a ? b : c, with simple b and c
            bool isSimpleConditional(ConditionalExpression node)
            {
                if (node.IfFalse is ConditionalExpression) return false;

                var testVisitor = new SimplifyExpressionsVisitor(_variableNameGenerator);
                _ = testVisitor.Visit(node.IfTrue);
                _ = testVisitor.Visit(node.IfFalse);
                return !testVisitor.Assignments.Any();
            }
        }

        private CaseWhenThenExpression toCWT(ConditionalExpression ce)
        {
            var exprs = unwind(ce).ToList();
            var cases = exprs
                .SkipLast(1)
                .Cast<ConditionalExpression>()
                .Select(expr => new CaseWhenThenExpression.WhenThenCase(expr.Test, expr.IfTrue));
            return new CaseWhenThenExpression(cases.ToList().AsReadOnly(), exprs.Last());

            static IEnumerable<Expression> unwind(ConditionalExpression ce)
            {
                if (ce.IfFalse is ConditionalExpression nestedCe)
                    return unwind(nestedCe).Prepend(ce);
                else
                    return new[] { ce, ce.IfFalse };
            }
        }


        protected override Expression VisitUnary(UnaryExpression node)
        {
            // Don't simplify simple converts.
            if (node.NodeType is ExpressionType.Convert or ExpressionType.TypeAs)
            {
                return base.VisitUnary(node);
            }
            else
                return simplify(base.VisitUnary(node));

        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            return node switch
            {
                // Simply comparing two values is something you can do by eye, we don't need to simplify that.
                { NodeType: ExpressionType.Equal or ExpressionType.NotEqual } => base.VisitBinary(node),

                // A null coalesce of two inspectable things is still inspectable.
                { NodeType: ExpressionType.Coalesce } => base.VisitBinary(node),

                // The interim value of an assignment is clear, we don't need to simplify
                { NodeType: ExpressionType.Assign } => base.VisitBinary(node),

                _ => simplify(base.VisitBinary(node))
            };
        }

        // This visitor builds up an expression (like any other), but also has a
        // set of Assignments, that cannot be seen in isolation from the expression. Therefore,
        // if there are any assignments, we need to, here at the root, create a
        // block to include those assignments.
        private Expression toBlock(Expression node)
        {
            // If there are no assignments (unlikely, we have introduced them to split large calls
            // into simpler ones + assignments, then we can return
            // the expression immediately.
            if (!_assignments.Any()) return node;

            // Otherwise introduce a block with the assignments translated to block variables +
            // assignments.
            var blockParameters = _assignments.Select(a => a.Left).Cast<ParameterExpression>().ToArray();
            var newBody = Expression.Block(blockParameters, _assignments.Append(node));

            return newBody;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            // Create a new visitor, since we're the new root that can hold
            // a block of assignments.
            var nestedVisitor = new SimplifyExpressionsVisitor(_variableNameGenerator);
            var body = nestedVisitor.Visit(node.Body);
            return node.Update(body, node.Parameters);
        }

        /// <summary>
        /// MemberInit is a special case: it's child `newExpression` cannot be
        /// rewritten to any other type than NewExpression (to put it otherwise:
        /// Expression.MemberInit only takes a NewExpression as a parameter,
        /// not an Expression). We need to prevent this from happening.
        /// </summary>
        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            return node.Update(visitNewExpression(node.NewExpression), Visit(node.Bindings, VisitMemberBinding));

            // Continue visiting the children of NewExpression, but don't rewrite it to a parameter.
            NewExpression visitNewExpression(NewExpression node) => node.Update(Visit(node.Arguments));
        }

        /// <summary>
        /// CaseWhenThen expressions cannot be represented in C# as an expression
        /// (well, as a switch expression, but that has its own limitations), so we need
        /// to make sure they get translated to a lambda containing the if/then/else block
        /// and then simplified to just a call to that lambda.
        /// </summary>
        protected Expression VisitCaseWhenThenExpression(CaseWhenThenExpression node)
        {
            // Each of the cases will be translated to blocks, which can hold their own
            // local variables and lexical return, just like the body of a Lambda. So,
            // we use a nested vistor here to create a nested block.
            CaseWhenThenExpression.WhenThenCase visitCase(CaseWhenThenExpression.WhenThenCase c)
            {
                var thenVisitor = new SimplifyExpressionsVisitor(_variableNameGenerator);
                return c.Update(c.When, thenVisitor.Visit(c.Then));
            }

            var cases = node.WhenThenCases.Select(visitCase);

            // The final else case is treated just like the when/then
            var elseVisitor = new SimplifyExpressionsVisitor(_variableNameGenerator);
            var visitedElse = elseVisitor.Visit(node.ElseCase);

            var newCTW = node.Update(cases.ToList().AsReadOnly(), visitedElse);


            // ~ Find duplicate left side when cases
            // ex. if      (A() == null) return null;
            //     else if (A() == true) return true;

            // Pull that out to become

            // var a = A();
            // if      (a == null) return null;
            // else if (a == true) return true;


            var deduplicatedCases = new List<CaseWhenThenExpression.WhenThenCase>();

            var possibleDuplicates = new Dictionary<string, List<CaseWhenThenExpression.WhenThenCase>>();
            foreach(var _case in (newCTW as CaseWhenThenExpression)!.WhenThenCases)
            {
                if (!(_case.When is BinaryExpression))
                {
                    deduplicatedCases.Add(_case);
                    continue;
                }

                var left = (_case.When as BinaryExpression)!.Left;
                var debugView = left.GetDebugView();

                List<CaseWhenThenExpression.WhenThenCase>? _list;
                if (!possibleDuplicates.TryGetValue(debugView, out _list))
                {
                    _list = new List<CaseWhenThenExpression.WhenThenCase>();
                    possibleDuplicates.Add(debugView, _list);
                }

                _list.Add(_case);
            }


            var parameterAssignmentExpressions = new List<Expression>();
            var localVariables = new List<ParameterExpression>();
            foreach(var kvp in possibleDuplicates)
            {
                if (kvp.Value.Count <= 1)
                {
                    // NOTE(agw): Wasn't a duplicate, just add it
                    deduplicatedCases.Add(kvp.Value.First());
                    continue;
                }

                var whenLeft = (kvp.Value[0].When as BinaryExpression)!.Left;

                var param = Expression.Parameter(whenLeft.Type, _variableNameGenerator.Next());

                var assignment = Expression.Assign(param, whenLeft);
                parameterAssignmentExpressions.Add(assignment);

                for (int i = 0; i < kvp.Value.Count; i++)
                {
                    var _case = kvp.Value[i];
                    var when = (_case.When as BinaryExpression)!;
                    var left = Expression.MakeBinary(when.NodeType, param, when.Right);

                    var newCase = new CaseWhenThenExpression.WhenThenCase(left, _case.Then);

                    deduplicatedCases.Add(newCase);
                }
            }

             newCTW = (newCTW as CaseWhenThenExpression)!.Update(deduplicatedCases.AsReadOnly(), visitedElse);


            var allExpressions = new List<Expression>();
            allExpressions.AddRange(parameterAssignmentExpressions);
            allExpressions.Add(newCTW);

            var block = Expression.Block(localVariables, allExpressions);

            // To make sure the if block in C# (which is NOT an expression) can
            // be used everywhere, we place the block inside its own lambda.
            // This also ensures the lexical exits work correctly.
            var func = Expression.Lambda(block);
            var assign = simplify(func);

            // Finally, replace the whole statement with just an invocation
            // of the lambda we just created (which *is* an expression and can be
            // used everywhere).
            return Expression.Invoke(assign);
        }


    }
}

