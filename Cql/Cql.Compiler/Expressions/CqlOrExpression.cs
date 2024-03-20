using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Hl7.Cql.Compiler.Expressions
{
    internal class CqlOrExpression : Expression
    {
        public CqlOrExpression(Expression left, Expression right)
        {
            LeftExpression = left;
            RightExpression = right;
        }

        public override Expression Reduce()
        {
            Expression ret = Expression.Or(LeftExpression, RightExpression);
            var converted = Expression.Convert(ret, typeof(bool?));
            return converted;
        }

        /* 
         * NOTE(agw): Not sure if we 100% need these overrides
         */
        public override bool CanReduce => true;
        public override ExpressionType NodeType => ExpressionType.OrElse;
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

        public override Type Type => typeof(bool?);


        public Expression LeftExpression;
        public Expression RightExpression;
    }
}
