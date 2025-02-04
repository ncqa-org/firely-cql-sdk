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
                if(curr is BinaryExpression binaryExpression)
                {
                    bool rightIsConstantExprssion = binaryExpression.Right is ConstantExpression;
                    bool rightIsNullableBool = typeof(bool?).IsAssignableFrom(binaryExpression.Right.Type);
                    bool leftIsParameter = binaryExpression.Left.NodeType == ExpressionType.Parameter;
                    if (binaryExpression.NodeType == ExpressionType.Assign && rightIsNullableBool && rightIsConstantExprssion == false && leftIsParameter)
                    {
                        // wrap right side in a cached bool
                        ConstructorInfo constructorInfo = typeof(CachedBool).GetConstructor(new[] { typeof(Func<bool?>) })!;

                        Expression func = Expression.Lambda<Func<bool?>>(Expression.Block(binaryExpression.Right));
                        NewExpression newCachedBool = Expression.New(constructorInfo, func);

                        int paramIdx = node.Variables.IndexOf((ParameterExpression)binaryExpression.Left);
                        ParameterExpression newParam = Expression.Parameter(typeof(CachedBool), ((ParameterExpression)binaryExpression.Left).Name);
                        parameterExpressions[paramIdx] = newParam;

                        Expression newResult = Expression.Assign(newParam, newCachedBool);
                        visitedExpression = newResult;
                    }
                }

                resultExpressions[i] = visitedExpression;
            }

            result = Expression.Block(parameterExpressions, resultExpressions);

            return result;
        }

    }
}
