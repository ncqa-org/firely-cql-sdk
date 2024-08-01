using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#pragma warning disable CS1591

namespace Hl7.Cql.Elm
{
    /// <summary>
    /// 
    /// </summary>
    public interface IExpressionVisitor
    {
        void VisitElement(Element? element) { }
        void VisitAggregation(AggregateExpression expression, IEnumerable<Expression> parameters);
        void VisitCode(Code code);
        void VisitCollection(Expression expression, IEnumerable<Expression> elements);
        void VisitConstant(Expression expression);
        void VisitControlFlow(Expression expression, IEnumerable<Expression> blocks);
        void VisitDateExpression(Expression expression, IEnumerable<Expression> operands);
        void VisitMathExpression(Expression expression, Expression[] operands);
        void VisitOperator(OperatorExpression expression, IEnumerable<Expression> operands);
        void VisitProperty(Property property);
        void VisitQuery(Query query);
        void VisitRef(ExpressionRef expression);
        void VisitRef(FunctionRef expression);
        void VisitRef(ValueSetRef expression);
        void VisitRef(CodeRef expression);
        void VisitRef(AliasRef expression);
        void VisitRef(OperandRef expression);
        void VisitRetrieve(Retrieve retrieve);
        void VisitAliasQuerySource(AliasedQuerySource expression);
        void VisitCodeRef(CodeRef expression);
        void VisitCodeSystemRef(CodeSystemRef expression);
        void VisitConceptRef(ConceptRef expression);
        void VisitQueryLetRef(QueryLetRef expression);
        void VisitAliasRef(AliasRef expression);
        void VisitIdentifierRef(IdentifierRef expression);
        void VisitOperandRef(OperandRef expression);
        void VisitParameterRef(ParameterRef expression);
        void VisitValueSetRef(ValueSetRef expression);
        void LeaveElement(Element? expression) { }

    }

    public static class ExpressionVisitorExtensions
    {
        public static void Accept(this Element? expression, IExpressionVisitor visitor)
        {
            if (expression == null) return;

            visitor.VisitElement(expression);
            switch (expression)
            {
                #region VisitMathExpression
                case Ratio exp:
                    visitor.VisitMathExpression(exp, new[] { exp.numerator, exp.denominator });
                    break;
                case Round exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand, exp.precision });
                    break;
                case Interval exp:
                    visitor.VisitMathExpression(exp, new[] { exp.low, exp.high, exp.lowClosedExpression, exp.highClosedExpression });
                    break;
                case HighBoundary exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case LowBoundary exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Power exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Log exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Modulo exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case TruncatedDivide exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Divide exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Multiply exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Subtract exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Add exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case GreaterOrEqual exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case LessOrEqual exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Greater exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Less exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case NotEqual exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Equivalent exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Equal exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case ConvertQuantity exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Implies exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Xor exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Or exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case And exp:
                    visitor.VisitMathExpression(exp, exp.operand);
                    break;
                case Lower exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Upper exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Precision exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Predecessor exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Successor exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Exp exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Ln exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Abs exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Truncate exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Floor exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Ceiling exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                case Not exp:
                    visitor.VisitMathExpression(exp, new[] { exp.operand });
                    break;
                #endregion VisitMathExpression

                #region VisitDateExpression
                case Date exp:
                    visitor.VisitDateExpression(exp, new[] { exp.year, exp.month, exp.day });
                    break;
                case DateTime exp:
                    visitor.VisitDateExpression(exp, new[] { exp.year, exp.month, exp.day, exp.hour, exp.minute, exp.second, exp.millisecond, exp.timezoneOffset });
                    break;
                case Time exp:
                    visitor.VisitDateExpression(exp, new[] { exp.hour, exp.minute, exp.second, exp.millisecond });
                    break;
                case Now exp:
                    visitor.VisitDateExpression(exp, Enumerable.Empty<Expression>());
                    break;
                case Today exp:
                    visitor.VisitDateExpression(exp, Enumerable.Empty<Expression>());
                    break;
                case TimeOfDay exp:
                    visitor.VisitDateExpression(exp, Enumerable.Empty<Expression>());
                    break;
                case Ends exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case Starts exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case OverlapsAfter exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case OverlapsBefore exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case Overlaps exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case MeetsAfter exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case MeetsBefore exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case Meets exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case After exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case Before exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case ProperIncludedIn exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case ProperIncludes exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case IncludedIn exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case Includes exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case ProperIn exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case In exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case ProperContains exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case Contains exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case SameOrAfter exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case SameOrBefore exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case SameAs exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case DifferenceBetween exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case DurationBetween exp:
                    visitor.VisitDateExpression(exp, exp.operand);
                    break;
                case CalculateAge exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case DateTimeComponentFrom exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case TimezoneOffsetFrom exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case TimezoneFrom exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case TimeFrom exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case DateFrom exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case ToTime exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case ConvertsToTime exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case ToDateTime exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                case ConvertsToDateTime exp:
                    visitor.VisitDateExpression(exp, new[] { exp.operand });
                    break;
                #endregion VisitDateExpression

                #region VisitOperator
                case UnaryExpression exp:
                    visitor.VisitOperator(exp, new[] { exp.operand });
                    break;
                case BinaryExpression exp:
                    visitor.VisitOperator(exp, exp.operand);
                    break;
                case TernaryExpression exp:
                    visitor.VisitOperator(exp, exp.operand);
                    break;
                case NaryExpression exp:
                    visitor.VisitOperator(exp, exp.operand);
                    break;
                case AnyInValueSet exp:
                    visitor.VisitOperator(exp, new[] { exp.codes, exp.valueset, exp.valuesetExpression });
                    break;
                case InValueSet exp:
                    visitor.VisitOperator(exp, new[] { exp.code, exp.valueset, exp.valuesetExpression });
                    break;
                case AnyInCodeSystem exp:
                    visitor.VisitOperator(exp, new[] { exp.codes, exp.codesystem, exp.codesystemExpression });
                    break;
                case InCodeSystem exp:
                    visitor.VisitOperator(exp, new[] { exp.code, exp.codesystem, exp.codesystemExpression });
                    break;
                case Message exp:
                    visitor.VisitOperator(exp, new[] { exp.source, exp.condition, exp.code, exp.severity, exp.message });
                    break;
                case Descendents exp:
                    visitor.VisitOperator(exp, new[] { exp.source });
                    break;
                case Children exp:
                    visitor.VisitOperator(exp, new[] { exp.source });
                    break;
                case IndexOf exp:
                    visitor.VisitOperator(exp, new[] { exp.source, exp.element });
                    break;
                case Slice exp:
                    visitor.VisitOperator(exp, new[] { exp.source, exp.startIndex, exp.endIndex });
                    break;
                case Last exp:
                    visitor.VisitOperator(exp, new[] { exp.source });
                    break;
                case First exp:
                    visitor.VisitOperator(exp, new[] { exp.source });
                    break;
                case Substring exp:
                    visitor.VisitOperator(exp, new[] { exp.stringToSub, exp.startIndex, exp.length });
                    break;
                case LastPositionOf exp:
                    visitor.VisitOperator(exp, new[] { exp.pattern, exp.@string });
                    break;
                case PositionOf exp:
                    visitor.VisitOperator(exp, new[] { exp.pattern, exp.@string });
                    break;
                case SplitOnMatches exp:
                    visitor.VisitOperator(exp, new[] { exp.stringToSplit, exp.separatorPattern });
                    break;
                case Split exp:
                    visitor.VisitOperator(exp, new[] { exp.stringToSplit, exp.separator });
                    break;
                case Combine exp:
                    visitor.VisitOperator(exp, new[] { exp.source, exp.separator });
                    break;
                #endregion VisitOperator

                #region VisitAggregation
                case Aggregate exp:
                    visitor.VisitAggregation(exp, new[] { exp.source, exp.iteration, exp.initialValue });
                    break;
                case AggregateExpression exp:
                    visitor.VisitAggregation(exp, new[] { exp.source });
                    break;
                #endregion VisitAggregation

                #region VisitControlFlow
                case Total exp:
                    visitor.VisitControlFlow(exp, Enumerable.Empty<Expression>());
                    break;
                case Iteration exp:
                    visitor.VisitControlFlow(exp, Enumerable.Empty<Expression>());
                    break;
                case Current exp:
                    visitor.VisitControlFlow(exp, Enumerable.Empty<Expression>());
                    break;
                case MaxValue exp:
                    visitor.VisitControlFlow(exp, Enumerable.Empty<Expression>());
                    break;
                case MinValue exp:
                    visitor.VisitControlFlow(exp, Enumerable.Empty<Expression>());
                    break;
                case Sort exp:
                    visitor.VisitControlFlow(exp, new[] { exp.source });
                    break;
                case Filter exp:
                    visitor.VisitControlFlow(exp, new[] { exp.source, exp.condition });
                    break;
                case Repeat exp:
                    visitor.VisitControlFlow(exp, new[] { exp.source, exp.element });
                    break;
                case ForEach exp:
                    visitor.VisitControlFlow(exp, new[] { exp.source, exp.element });
                    break;
                case If exp:
                    visitor.VisitControlFlow(exp, new[] { exp.condition, exp.then, exp.@else });
                    break;
                case Case exp:
                    visitor.VisitControlFlow(exp, new[] { exp.comparand, exp.@else }
                        .Concat(exp.caseItem.Select(ci => ci.when))
                        .Concat(exp.caseItem.Select(ci => ci.then))
                    );
                    break;
                #endregion VisitControlFlow

                #region VisitDef

                case AliasedQuerySource exp:
                    visitor.VisitAliasQuerySource(exp);
                    break;

                #endregion

                #region VisitRef
                case CodeRef exp:
                    visitor.VisitCodeRef(exp);
                    break;
                case CodeSystemRef exp:
                    visitor.VisitCodeSystemRef(exp);
                    break;
                case ConceptRef exp:
                    visitor.VisitConceptRef(exp);
                    break;
                case QueryLetRef exp:
                    visitor.VisitQueryLetRef(exp);
                    break;
                case AliasRef exp:
                    visitor.VisitAliasRef(exp);
                    break;
                case IdentifierRef exp:
                    visitor.VisitIdentifierRef(exp);
                    break;
                case OperandRef exp:
                    visitor.VisitOperandRef(exp);
                    break;
                case ParameterRef exp:
                    visitor.VisitParameterRef(exp);
                    break;
                case FunctionRef exp:
                    visitor.VisitRef(exp);
                    break;
                case ExpressionRef exp:
                    visitor.VisitRef(exp);
                    break;
                case ValueSetRef exp:
                    visitor.VisitValueSetRef(exp);
                    break;
                #endregion VisitRef

                case Query exp:
                    visitor.VisitQuery(exp);
                    break;
                case Retrieve exp:
                    visitor.VisitRetrieve(exp);
                    break;
                case Code exp:
                    visitor.VisitCode(exp);
                    break;

                #region VisitCollection
                case Concept exp:
                    visitor.VisitCollection(exp, exp.code);
                    break;
                case List exp:
                    visitor.VisitCollection(exp, exp.element);
                    break;
                case Instance exp:
                    visitor.VisitCollection(exp, exp.element.Select(e => e.value));
                    break;
                case Tuple exp:
                    visitor.VisitCollection(exp, exp.element.Select(e => e.value));
                    break;
                #endregion VisitCollection

                case Property exp:
                    visitor.VisitProperty(exp);
                    break;

                #region VisitConstant
                case Quantity exp:
                    visitor.VisitConstant(exp);
                    break;
                case Literal exp:
                    visitor.VisitConstant(exp);
                    break;
                case Null exp:
                    visitor.VisitConstant(exp);
                    break;
                #endregion VisitConstant
                case null:
                    break;
                default:
                    throw new NotImplementedException($"Expression type {expression.GetType().Name} not supported by IExpressionVisitor.");

            }

            visitor.LeaveElement(expression);
        }
    }
}
