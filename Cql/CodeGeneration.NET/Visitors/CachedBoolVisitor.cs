using Hl7.Cql.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Hl7.Cql.CodeGeneration.NET.Visitors
{
    internal class CachedBoolVisitor : ExpressionVisitor
    {

        private static ConstructorInfo constructorInfo = typeof(CachedBool).GetConstructor(new[] { typeof(Func<bool?>) })!;

        protected override Expression VisitBlock(BlockExpression node)
        {
            Expression? result = null;

            Expression[] resultExpressions = new Expression[node.Expressions.Count()];
            ParameterExpression[] parameterExpressions = new ParameterExpression[node.Variables.Count()];

            // copy over
            for(int i = 0; i < node.Variables.Count(); i += 1)
            {
                parameterExpressions[i] = node.Variables[i];
            }

            // convert assignments to assignement to cached bool
            for(int i = 0; i < resultExpressions.Length; i += 1)
            {
                Expression curr = node.Expressions[i];
                Expression visitedExpression = curr;
                if(curr is BinaryExpression currBe)
                {
                    bool rightIsConstantExpression = currBe.Right is ConstantExpression;
                    bool rightIsNullableBool = currBe.Right.Type == typeof(bool?);
                    bool leftIsParameter = currBe.Left.NodeType == ExpressionType.Parameter;
                    if (currBe.NodeType == ExpressionType.Assign && rightIsNullableBool && rightIsConstantExpression == false && leftIsParameter)
                    {
                        // wrap right side in a cached bool

                        // TODO(agw): 
                        // for the right expression, find any references to local ParameterExpression. 
                        // if we are the only reference, we can move inside the cached bool function
                        List<Expression> pulledOut = new();

                        List<Expression> blockExpressions = new();
                        ParameterExpression blockResult = Expression.Parameter(typeof(bool?));
                        //blockExpressions.AddRange(pulledOut);
                        blockExpressions.Add(Expression.Assign(blockResult, currBe.Right));
                        blockExpressions.Add(blockResult);

                        Expression func = Expression.Lambda<Func<bool?>>(Expression.Block(blockExpressions));
                        NewExpression newCachedBool = Expression.New(constructorInfo, func);

                        int paramIdx = node.Variables.IndexOf((ParameterExpression)currBe.Left);
                        ParameterExpression newParam = Expression.Parameter(typeof(CachedBool), ((ParameterExpression)currBe.Left).Name);
                        parameterExpressions[paramIdx] = newParam;

                        Expression newResult = Expression.Assign(newParam, newCachedBool);
                        visitedExpression = newResult;
                    }
                    else if(currBe.NodeType == ExpressionType.Assign && currBe.Right is LambdaExpression lambdaExpression)
                    {
                        var lambdaBody = Visit(lambdaExpression.Body);
                        var newLambda = Expression.Lambda(lambdaBody, lambdaExpression.Parameters);

                        visitedExpression = Expression.Assign(currBe.Left, newLambda);
                    }
                    else
                    {
                        visitedExpression = base.Visit(node.Expressions[i]);
                    }
                }
                else
                {
                    visitedExpression = base.Visit(node.Expressions[i]);
                }

                resultExpressions[i] = visitedExpression;
            }

            result = Expression.Block(parameterExpressions, resultExpressions);

            return result;
        }

    }
}
