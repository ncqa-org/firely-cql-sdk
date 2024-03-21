using Hl7.Cql.Compiler;
using Hl7.Cql.Compiler.Expressions;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using static Hl7.Cql.Compiler.CaseWhenThenExpression;

namespace Hl7.Cql.CodeGeneration.NET.Visitors
{
    internal class ShortCircuitVisitor : ExpressionVisitor
    {

        public override Expression? Visit(Expression? node)
        {
            if (node is null) return null;
            if(node is CqlOrExpression)
            {
                return doVisit(node);
            }

            return node;
        }

        private Expression doVisit(Expression node)
        {
            return node switch
            {
                CqlOrExpression expr => VisitCqlOrExpression(expr),
                _ => base.Visit(node)
            };
        }

        /*
         NOTE(agw): This should take all consecutive CqlOrExpressions and convert it into if statements

         var a = ExpensiveA();
         if(a ?? false) return true;
         else if(a == null) return null;
         else
         {
            var b = ExpensiveB();
            if(b ?? false) return true;
            else if(b == null) return null;
            else
            {
                var c = ExpensiveC();
                if(c ?? false) return true;
                else if(c == null) return null;
                else return false;  
            }
         }
         */
        protected Expression VisitCqlOrExpression(CqlOrExpression expr)
        {
            // ~ Helpers
            var convertTrueExpression = Expression.Convert(Expression.Constant(true), typeof(bool?));
            var convertedNull = Expression.Convert(Expression.Constant(null), typeof(bool?));

            // ~ Get all bottom level, non-or expressions
            List<Expression> leafExpressions = new List<Expression>();
            dfsGetLeafExpressions(expr, leafExpressions);

            var leafStack = new Stack<Expression>();
            for (int i = 0; i < leafExpressions.Count; i++) { leafStack.Push(leafExpressions[i]); }

            // ~ Combine Statements into one nested CaseWhenThenExpression
            var falseAsNullable = Expression.Convert(Expression.Constant(false), typeof(bool?));
            Expression prev = falseAsNullable;

            while (leafStack.Count > 0)
            {
                var cond = leafStack.Pop();
                var topLevelIfs = new List<WhenThenCase>();

                if (cond.Type == typeof(bool?)) {
                    var equalNull = Expression.Equal(cond, convertedNull);
                    topLevelIfs.Add(new WhenThenCase(equalNull, convertedNull));

                    var coalescedConditional = Expression.Coalesce(cond, Expression.Constant(false));
                    topLevelIfs.Add(new WhenThenCase(coalescedConditional, convertTrueExpression));
                }
                else if(cond.Type == typeof(bool))
                {
                    var _case = new WhenThenCase(cond, convertTrueExpression);
                    topLevelIfs.Add(_case);
                }
                else { throw new Exception("Cql Or Must work on either bool? or bool types, not: " + cond.Type); }

                var caseWhen = new CaseWhenThenExpression(topLevelIfs, prev);
                prev = caseWhen;
            }

            var ret = prev;

            return ret;
        }


        // TODO(agw): have this return a stack rather than a list
        void dfsGetLeafExpressions(Expression expression, List<Expression> expressions)
        {
            if (expression == null) return;

            if (expression.NodeType == ExpressionType.OrElse)
            {
                Expression? left = null;
                Expression? right = null;
                if (expression is BinaryExpression)
                { 
                    var asBinary = expression as BinaryExpression;
                    if (asBinary == null) return; // NOTE(agw): not sure when this would happen...
                    left = asBinary.Left;
                    right = asBinary.Right;
                }
                else if(expression is CqlOrExpression)
                {
                    var asOr = expression as CqlOrExpression;
                    if (asOr == null) return; // NOTE(agw): not sure when this would happen...

                    left = asOr.LeftExpression; 
                    right = asOr.RightExpression; 
                }

                if(left != null && right != null)
                {
                    dfsGetLeafExpressions(left, expressions);
                    dfsGetLeafExpressions(right, expressions);
                }
            }
            else
            {
                expressions.Add(expression);
            }
                
        }
    }
}
