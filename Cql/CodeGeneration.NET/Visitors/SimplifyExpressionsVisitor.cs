/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Compiler;
using Hl7.Cql.Compiler.Expressions;
using Hl7.Cql.Operators;
using Hl7.Cql.Runtime;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Xml.Schema;

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
        private Stack<BlockExpression> blocks = new();

        public IReadOnlyCollection<BinaryExpression> Assignments => _assignments;

        PropertyInfo OperatorsProperty => typeof(CqlContext).GetProperty(nameof(CqlContext.Operators))!;
        System.Type OperatorsType => OperatorsProperty.PropertyType;

        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node)
        {
            if (node is null) return null;

            // We needs a different action at the "root" of the tree than at the nodes of the tree,
            // see toBlock().
            if (_atRoot)
            {
                _atRoot = false;

                var root = Expression.Block();
                blocks.Push(root);

                var visited = doVisit(node);

                var topBlock = blocks.Pop();
                var paramsFromAssignments = _assignments.Select(a => a.Left).Cast<ParameterExpression>();
                var blockParameters = paramsFromAssignments.Concat(topBlock.Variables).ToArray();
                List<Expression> expressions = topBlock.Expressions.ToList();
                if(isAndOr(node) == false)
                {
                    expressions = expressions.Append(visited).ToList();
                }
                var result = Expression.Block(blockParameters, expressions);
                return result;
                //return toBlock(topBlock);
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

                BlockExpression block => VisitBlock(block),
                MethodCallExpression methodCall => doTopLevelAndOr(methodCall),

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
            // transform complex into assignment to variable + variable
            var newLetVariable = Expression.Parameter(node.Type);
            var newAssign = Expression.Assign(newLetVariable, node);

            // TODO(agw): this is a lot of copying, do something better (stack of lists of expressions?)
            var parentBlock = blocks.Pop();

            ParameterExpression[] varArray = new ParameterExpression[parentBlock.Variables.Count()];
            Expression[] expArray = new Expression[parentBlock.Expressions.Count()];

            parentBlock.Variables.CopyTo(varArray, 0);
            parentBlock.Expressions.CopyTo(expArray, 0);

            var newBlock = Expression.Block(parentBlock.Variables.Append(newLetVariable), parentBlock.Expressions.Append(newAssign));

            blocks.Push(newBlock);

            return newLetVariable;
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            blocks.Push(node);
            base.VisitBlock(node);
            var result = blocks.Pop();
            return result;
        }

        protected bool isAndOr(Expression node)
        {
            bool result = false;
            if (node is MethodCallExpression methodCall)
            {
                string name = methodCall.Method.Name;

                string orName = nameof(ICqlOperators.Or);
                string andName = nameof(ICqlOperators.And);

                bool sameName = name == orName || name == andName;
                bool isOperatorCall = methodCall.Method.DeclaringType == OperatorsType;
                result = isOperatorCall && sameName;
            }
            return result;
        }

        protected bool expressionIsOrCall(Expression node)
        {
            bool result = false;
            if (node is MethodCallExpression methodCall)
            {
                string name = methodCall.Method.Name;

                string orName = nameof(ICqlOperators.Or);

                bool sameName = name == orName;
                bool isOperatorCall = methodCall.Method.DeclaringType == OperatorsType;
                result = isOperatorCall && sameName;
            }
            return result;
        }

        protected BlockExpression doOr(MethodCallExpression node, ParameterExpression resultParam)
        {
            BlockExpression? result = null;
            List<Expression> expressions = new List<Expression>();

            var assignTrue = Expression.Assign(resultParam, Expression.Constant(true, typeof(bool?)));
            var assignNull = Expression.Assign(resultParam, Expression.Constant(null, typeof(bool?)));

            bool leftIsOr = expressionIsOrCall(node.Arguments[0]);
            if (leftIsOr && node.Arguments[0] is MethodCallExpression methodCallExpression)
            {
                var expr = doOr(methodCallExpression, resultParam);
                expressions.Add(expr);
            }
            else
            {
                // do the actual left expression
                var leftExpression = node.Arguments[0];

                var tempLeft = Expression.Parameter(typeof(bool?));
                var leftConverted = Expression.Convert(leftExpression, typeof(bool?));
                var doLeft = Expression.Assign(tempLeft, leftConverted);

                expressions.Add(doLeft);

                var checkLeftTrue = Expression.Equal(tempLeft, Expression.Constant(true, typeof(bool?)));
                var checkLeftNull = Expression.Equal(tempLeft, Expression.Constant(null, typeof(bool?)));
                var checkLeftTrueIf = Expression.IfThen(checkLeftTrue, assignTrue);
                var checkLeftNullIf = Expression.IfThen(checkLeftNull, assignNull);

                expressions.Add(checkLeftTrueIf);
                expressions.Add(checkLeftNullIf);
            }

            bool rightIsOr = isAndOr(node.Arguments[1]);
            if (rightIsOr && node.Arguments[1] is MethodCallExpression rightMethodCallExpression)
            {
                BlockExpression rightBlock = doOr(rightMethodCallExpression, resultParam);
                // do the actual right expression

                /*
                    if(res == false)
                    {
                        do right block
                    }
                */

                var checkDoRight = Expression.IfThen(Expression.Equal(resultParam, Expression.Constant(false, typeof(bool?))), rightBlock);
                expressions.Add(checkDoRight);
            }
            else
            {
                /*
                    if(res == false)
                    {
                        do right expression (no more children calls)
                    }
                */

                BlockExpression? rightBlock = null;
                {
                    var tempRight = Expression.Parameter(typeof(bool?));
                    var rightConverted = Expression.Convert(node.Arguments[1], typeof(bool?));
                    var doRight = Expression.Assign(tempRight, rightConverted);

                    var checkRightTrue = Expression.Equal(tempRight, Expression.Constant(true, typeof(bool?)));
                    var checkRightNull = Expression.Equal(tempRight, Expression.Constant(null, typeof(bool?)));
                    var checkRightTrueIf = Expression.IfThen(checkRightTrue, assignTrue);
                    var checkRightNullIf = Expression.IfThen(checkRightNull, assignNull);
                    rightBlock = Expression.Block(doRight, checkRightTrueIf, checkRightNullIf, Expression.Empty());
                }

                var checkDoRight = Expression.IfThen(Expression.Equal(resultParam, Expression.Constant(false, typeof(bool?))), rightBlock);
                expressions.Add(checkDoRight);
            }

            expressions.Add(Expression.Empty());
            result = Expression.Block(expressions);
            return result;
        }

        protected Expression doTopLevelAndOr(MethodCallExpression node)
        {
            List<Expression> expressions = new List<Expression>();
                    
            var resultParam = Expression.Parameter(typeof(bool?));
            var assignFalse = Expression.Assign(resultParam, Expression.Convert(Expression.Constant(false, typeof(bool?)), typeof(bool?)));
            expressions.Add(assignFalse);

            if(expressionIsOrCall(node))
            {
                BlockExpression blockExpression = doOr(node, resultParam);
                expressions.Add(blockExpression);
            }

            expressions.Add(resultParam);

            // TODO(agw): add expressions to top level block rn
            var parentBlock = blocks.Peek();
            var updatedParent = Expression.Block(parentBlock.Variables.Append(resultParam), parentBlock.Expressions.Concat(expressions));
            blocks.Pop();
            blocks.Push(updatedParent);

            return resultParam;
        }

        protected Expression doVisitMethodCall(MethodCallExpression node)
        {
            string name = node.Method.Name;

            string orName = nameof(ICqlOperators.Or);
            string andName = nameof(ICqlOperators.And);

            /*
                var res1 = false;
                {
                    var a = A();
                    if(a == true)
                        res1 = true;
                    if(res1 == false)
                    {
                        var res2 = false;
                        {
                            var b = B();
                            if(b == true)
                                res2 = true;
                            if(res2 == false)
                            {
                                var c = C();
                                if(c == true)
                                    res2 = true;
                            }
                        }

                        res1 = res2;
                    }
                }

                // deep at the top
                var res = false;
                {
                    if(A() or B())
                        res = true;
                    else
                    {
                        C()
                    }
                }

                Or(Or(A(), B()), C())
            */

            bool sameName = name == orName || name == andName;
            bool isOperatorCall = node.Method.DeclaringType == OperatorsType;
            if(isOperatorCall && sameName)
            {


                var resultParam = Expression.Parameter(typeof(bool?));
                var assignTrue = Expression.Assign(resultParam, Expression.Constant(true, typeof(bool?)));
                var assignFalse = Expression.Assign(resultParam, Expression.Convert(Expression.Constant(false, typeof(bool?)), typeof(bool?)));
                var assignNull = Expression.Assign(resultParam, Expression.Constant(null, typeof(bool?)));

                // get left expression 
                BlockExpression parentBlock = blocks.Peek();

                List<Expression> parentExpressions = new List<Expression>();
                List<ParameterExpression> parentVariables = new List<ParameterExpression>();

                parentExpressions.AddRange(parentBlock.Expressions);
                parentVariables.AddRange(parentBlock.Variables);

                parentExpressions.Add(assignFalse);
                parentVariables.Add(resultParam);

                var updatedParentBlock = Expression.Block(parentVariables, parentExpressions);
                blocks.Pop();
                blocks.Push(updatedParentBlock);

                // add res to parentBlock

                // do children, setting res

                // return res?

                var childLogicBlock = Expression.Block();
                blocks.Push(childLogicBlock); // any children calls are going to go in this childLogicBlock

                var leftExpression = Visit(node.Arguments[0]);
                var rightExpression = Visit(node.Arguments[1]);

                var tempLeft = Expression.Parameter(typeof(bool?));
                var leftConverted = Expression.Convert(leftExpression, typeof(bool?));
                var doLeft = Expression.Assign(tempLeft, leftConverted);

                var checkLeftTrue = Expression.Equal(tempLeft, Expression.Constant(true, typeof(bool?)));
                var checkLeftNull = Expression.Equal(tempLeft, Expression.Constant(null, typeof(bool?)));
                var checkLeftTrueIf = Expression.IfThen(checkLeftTrue, assignTrue);
                var checkLeftNullIf = Expression.IfThen(checkLeftNull, assignNull);

                BlockExpression? rightBlock = null;
                {
                    var tempRight = Expression.Parameter(typeof(bool?));
                    var rightConverted = Expression.Convert(rightExpression, typeof(bool?));
                    var doRight = Expression.Assign(tempRight, rightConverted);

                    var checkRightTrue = Expression.Equal(tempRight, Expression.Constant(true, typeof(bool?)));
                    var checkRightNull = Expression.Equal(tempRight, Expression.Constant(null, typeof(bool?)));
                    var checkRightTrueIf = Expression.IfThen(checkRightTrue, assignTrue);
                    var checkRightNullIf = Expression.IfThen(checkRightNull, assignNull);
                    rightBlock = Expression.Block(doRight, checkRightTrueIf, checkRightNullIf, Expression.Empty());
                }

                var checkDoRight = Expression.IfThen(Expression.Equal(resultParam, Expression.Constant(false, typeof(bool?))), rightBlock);

                BlockExpression orBlock = Expression.Block(assignTrue, doLeft, checkLeftTrueIf, checkLeftNullIf, checkDoRight, Expression.Empty());

                // replace current top block with new top block

                ParameterExpression[] varArray = new ParameterExpression[parentBlock.Variables.Count()];
                Expression[] expArray = new Expression[parentBlock.Expressions.Count()];

                parentBlock.Variables.CopyTo(varArray, 0);
                parentBlock.Expressions.CopyTo(expArray, 0);

                List<ParameterExpression> variables = varArray.ToList();
                List<Expression> expressions = expArray.ToList();

                variables.Add(resultParam);
                expressions.Add(assignFalse);
                expressions.Add(orBlock);

                bool isTopBlock = blocks.Count() == 1;
                if(isTopBlock)
                {
                    expressions.Add(resultParam);
                }

                //var newBlock = parentBlock.Update(variables, expressions);

                //blocks.Pop();
                //blocks.Push(newBlock);


                // TODO(agw): not sure if this is right
                return resultParam;
            }

            Expression result = simplify(base.VisitMethodCall(node));
            return result;
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

                var testVisitor = new SimplifyExpressionsVisitor();
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
            var nestedVisitor = new SimplifyExpressionsVisitor();
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
                var thenVisitor = new SimplifyExpressionsVisitor();
                return c.Update(c.When, thenVisitor.Visit(c.Then));
            }

            var cases = node.WhenThenCases.Select(visitCase);

            // The final else case is treated just like the when/then
            var elseVisitor = new SimplifyExpressionsVisitor();
            var visitedElse = elseVisitor.Visit(node.ElseCase);

            var newCTW = node.Update(cases.ToList().AsReadOnly(), visitedElse);

            // To make sure the if block in C# (which is NOT an expression) can
            // be used everywhere, we place the block inside its own lambda.
            // This also ensures the lexical exits work correctly.
            var func = Expression.Lambda(Expression.Block(newCTW));
            var assign = simplify(func);

            // Finally, replace the whole statement with just an invocation
            // of the lambda we just created (which *is* an expression and can be
            // used everywhere).
            return Expression.Invoke(assign);
        }


    }
}

