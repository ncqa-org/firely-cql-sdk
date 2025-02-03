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
using System;
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
        private Stack<BlockExpression> blocks = new(); // TODO0(agw): may not need this stack...

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


        protected bool expressionIsAndCall(Expression node)
        {
            bool result = false;
            if (node is MethodCallExpression methodCall)
            {
                string name = methodCall.Method.Name;

                string orName = nameof(ICqlOperators.And);

                bool sameName = name == orName;
                bool isOperatorCall = methodCall.Method.DeclaringType == OperatorsType;
                result = isOperatorCall && sameName;
            }
            return result;
        }

        protected BlockExpression doAndOr(MethodCallExpression node, ParameterExpression resultParam)
        {
            bool isOr = expressionIsOrCall(node);
            bool initialValue = isOr ? false : true;

            BlockExpression? result = null;
            List<Expression> expressions = new List<Expression>();
            List<ParameterExpression> parameterExpressions = new List<ParameterExpression>();

            var assignResultTrue = Expression.Assign(resultParam, Expression.Constant(true, typeof(bool?)));
            var assignResultFalse = Expression.Assign(resultParam, Expression.Constant(false, typeof(bool?)));
            var assignResultNull = Expression.Assign(resultParam, Expression.Constant(null, typeof(bool?)));

            ////////////////////////////////////////
            // ~ Do Left Side

            bool leftIsCall = isAndOr(node.Arguments[0]);
            if(leftIsCall)
            {
                // create new block with local variable
                MethodCallExpression leftExpression = (MethodCallExpression)node.Arguments[0];

                var childInitial = expressionIsOrCall(leftExpression) ? false : true;

                var childResult = Expression.Parameter(typeof(bool?));
                var assignChild = Expression.Assign(childResult, Expression.Constant(childInitial, typeof(bool?)));
                expressions.Add(assignChild);
                parameterExpressions.Add(childResult);

                BlockExpression leftBlock = doAndOr(leftExpression, childResult);

                // assign parent to child local, empty at end so we don't return anything
                var assignParent = Expression.Assign(resultParam, childResult);
                var endingExpressions = new Expression[] {assignParent, Expression.Empty()};
                leftBlock = Expression.Block(leftBlock.Variables, leftBlock.Expressions.Concat(endingExpressions));

                expressions.Add(leftBlock);
            }
            else
            {
                // actually do left
                var tempLeft = Expression.Parameter(typeof(bool?));
                parameterExpressions.Add(tempLeft);
                var doLeft = Expression.Assign(tempLeft, node.Arguments[0]);

                expressions.Add(doLeft);

                BinaryExpression? assignExpression = null;
                bool checkValue = false;
                if(expressionIsOrCall(node))
                {
                    checkValue = true;
                    assignExpression = assignResultTrue;
                }
                else
                {
                    checkValue = false;
                    assignExpression = assignResultFalse;
                }

                Expression checkExpression = Expression.Equal(tempLeft, Expression.Constant(checkValue, typeof(bool?)));
                var checkLeftTrueIf = Expression.IfThen(checkExpression, assignExpression);
                expressions.Add(checkLeftTrueIf);

                // add null check
                var checkLeftNull = Expression.Equal(tempLeft, Expression.Constant(null, typeof(bool?)));
                var checkLeftNullIf = Expression.IfThen(checkLeftNull, assignResultNull);
                expressions.Add(checkLeftNullIf);
            }


            ////////////////////////////////////////
            // ~ Do Right Side

            bool rightIsCall = isAndOr(node.Arguments[1]);
            if (rightIsCall)
            {
                // right is call, create a new variable for local result
                MethodCallExpression rightExpression = (MethodCallExpression)node.Arguments[1];
                var childResult = Expression.Parameter(typeof(bool?));

                var childInitial = expressionIsOrCall(rightExpression) ? false : true;
                var assignChild = Expression.Assign(childResult, Expression.Constant(childInitial, typeof(bool?)));

                expressions.Add(assignChild);
                parameterExpressions.Add(childResult);

                // get right block
                BlockExpression rightBlock = doAndOr(rightExpression, childResult);

                // assign parent to child result
                var assignParent = Expression.Assign(resultParam, childResult);
                var endingExpressions = new Expression[] {assignParent, Expression.Empty()};
                rightBlock = Expression.Block(rightBlock.Variables, rightBlock.Expressions.Concat(endingExpressions));

                // if/then block to continue
                bool checkValue = expressionIsOrCall(rightExpression) ? false : true;
                BinaryExpression checkExpression = Expression.Equal(resultParam, Expression.Constant(checkValue, typeof(bool?)));
                var checkDoRight = Expression.IfThen(checkExpression, rightBlock);
                expressions.Add(checkDoRight);
            }
            else
            {
                BlockExpression? rightBlock = null;
                {
                    var tempRight = Expression.Parameter(typeof(bool?));
                    var doRight = Expression.Assign(tempRight, node.Arguments[1]);

                    BinaryExpression? assignExpression = null;
                    bool rightCheckValue = false;
                    if(expressionIsOrCall(node))
                    {
                        rightCheckValue = true;
                        assignExpression = assignResultTrue;
                    }
                    else
                    {
                        rightCheckValue = false;
                        assignExpression = assignResultFalse;
                    }

                    Expression rightCheckExpression = Expression.Equal(tempRight, Expression.Constant(rightCheckValue, typeof(bool?)));
                    var checkRightIf = Expression.IfThen(rightCheckExpression, assignExpression);

                    var checkRightTrue = Expression.Equal(tempRight, Expression.Constant(true, typeof(bool?)));
                    var checkRightNull = Expression.Equal(tempRight, Expression.Constant(null, typeof(bool?)));
                    var checkRightNullIf = Expression.IfThen(checkRightNull, assignResultNull);

                    rightBlock = Expression.Block(new[] { tempRight }, doRight, checkRightIf, checkRightNullIf, Expression.Empty());
                }

                bool checkValue = expressionIsOrCall(node) ? false : true;
                BinaryExpression checkExpression = Expression.Equal(resultParam, Expression.Constant(checkValue, typeof(bool?)));

                var checkDoRight = Expression.IfThen(checkExpression, rightBlock);
                expressions.Add(checkDoRight);
            }

            expressions.Add(Expression.Empty());
            result = Expression.Block(parameterExpressions, expressions);
            return result;
        }

        protected Expression doTopLevelAndOr(MethodCallExpression node)
        {
            List<Expression> expressions = new List<Expression>();
                    
            var resultParam = Expression.Parameter(typeof(bool?));

            BlockExpression? blockExpression = null;
            if(expressionIsOrCall(node))
            {
                var assignFalse = Expression.Assign(resultParam, Expression.Constant(false, typeof(bool?)));
                expressions.Add(assignFalse);
            }
            else if(expressionIsAndCall(node))
            {
                var assignTrue = Expression.Assign(resultParam, Expression.Constant(true, typeof(bool?)));
                expressions.Add(assignTrue);
            }
            else
            {
                throw new NotImplementedException();
            }

            blockExpression = doAndOr(node, resultParam);

            expressions.Add(blockExpression);
            expressions.Add(resultParam);

            var parentBlock = blocks.Peek();
            var updatedParent = Expression.Block(parentBlock.Variables.Append(resultParam), parentBlock.Expressions.Concat(expressions));
            blocks.Pop();
            blocks.Push(updatedParent);

            return resultParam;
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            Expression result = node;
            // Turn every nested conditional except the most simple ones into a Case/when/then
            if (true || isSimpleConditional(node))
            {
                // TODO(agw): pull out and/or statements to parent block
                Expression inner = node.Test;
                if(node.Test.NodeType == ExpressionType.Coalesce)
                {
                    var coalesceExpression = (BinaryExpression)node.Test;
                    inner = coalesceExpression.Left;
                }

                bool testIsAndOrCall = isAndOr(inner);
                if(testIsAndOrCall)
                {
                    BlockExpression parentBlock = blocks.Peek();

                    ParameterExpression resultParam = Expression.Parameter(typeof(bool?));
                    BlockExpression andOrBlock = doAndOr((MethodCallExpression)inner, resultParam);

                    Expression? assignExpression = null;
                    if(expressionIsOrCall(inner))
                    {
                        assignExpression = Expression.Assign(resultParam, Expression.Constant(false, typeof(bool?)));
                    }
                    else
                    {
                        assignExpression = Expression.Assign(resultParam, Expression.Constant(true, typeof(bool?)));
                    }


                    var newTest = Expression.Equal(resultParam, Expression.Constant(true, typeof(bool?)));

                    var visitedIfTrue = doVisit(node.IfTrue);
                    var visitedIfFalse = doVisit(node.IfFalse);

                    var newConditional = Expression.Condition(newTest, visitedIfTrue, visitedIfFalse);

                    Expression toAdd = newConditional;

                    if(newConditional.Type == typeof(bool?))
                    {
                        toAdd = Expression.Assign(resultParam, newConditional);
                    }

                    var expressions = new List<Expression>(parentBlock.Expressions.Count() + 1);

                    int idxOfConditional = parentBlock.Expressions.IndexOf(node);

                    // copy until conditional node
                    for(int i = 0; i < idxOfConditional; i += 1)
                    {
                        expressions.Add(parentBlock.Expressions[i]);
                    }

                    expressions.Add(assignExpression);
                    expressions.Add(andOrBlock);
                    expressions.Add(toAdd);

                    result = resultParam;

                    // add remaining
                    for (int i = idxOfConditional + 1; i < parentBlock.Expressions.Count(); i += 1)
                    {
                        expressions.Add(parentBlock.Expressions[i]);
                    }

                    var parameters = parentBlock.Variables.Append(resultParam);
                    BlockExpression newBlock = Expression.Block(parameters, expressions);

                    blocks.Pop();
                    blocks.Push(newBlock);
                }
            }
            else
            {
                result = toCWT(node);
                result = Visit(result);
            }


            return result;

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

