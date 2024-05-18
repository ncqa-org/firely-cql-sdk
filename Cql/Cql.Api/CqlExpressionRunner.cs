using Hl7.Cql.CqlToElm;
using Hl7.Cql.Fhir;
using Hl7.Cql.Runtime;
using Hl7.Cql.Elm;
using Hl7.Fhir.Model;
using Hl7.Fhir.Support;

using Hl7.Cql.Compiler;
using Hl7.Cql.CqlToElm.LibraryProviders;
using Hl7.Cql.Primitives;
using Hl7.Cql.Conversion;
using Hl7.Cql.CqlToElm.Builtin;
using static Hl7.Fhir.Model.OperationOutcome;

namespace Hl7.Cql.Api
{
    /// <summary>
    /// 
    /// </summary>
    public class CqlExpressionRunner
    {
        readonly CqlContext _cqlContext;
        readonly InvocationBuilder _invocationBuilder;
        readonly ElmFactory _elmFactory;
        readonly CqlToElmConverter _defaultConverter;
        readonly ExpressionBuilder _expressionBuilder;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="serviceProvider"></param>
        public CqlExpressionRunner(IServiceProvider serviceProvider)
        {
            _cqlContext = FhirCqlContext.ForBundle();
            _invocationBuilder = serviceProvider.GetRequiredService<InvocationBuilder>();
            _elmFactory = serviceProvider.GetRequiredService<ElmFactory>();
            _defaultConverter = serviceProvider.GetRequiredService<CqlToElmConverter>();

            var lib = new Elm.Library
            {
                identifier = new VersionedIdentifier { id = "Lambdas", version = "1.0.0" }
            };
            _expressionBuilder = ExpressionBuilderFor(lib, serviceProvider.GetRequiredService<ILogger<ExpressionBuilder>>());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Parameters RunExpression(string cqlExpression, string? cqlExpectation = null)
        {
            Parameters result = new Parameters()
            {
                Parameter = new List<Parameters.ParameterComponent>()
            };
            OperationOutcome outcome = new OperationOutcome()
            {
                Issue = new List<IssueComponent>()
            };
            try
            {
                // Build and validate the CQL expression
                var expression = Expression(cqlExpression);
                var expressionErrors = expression.GetErrors();
                if (expressionErrors.Any())
                {
                    var issues = expressionErrors.Select(s => new IssueComponent()
                    {
                        Code = IssueType.Invalid,
                        Severity = IssueSeverity.Error,
                        Details = new CodeableConcept()
                        {
                            Text = s.message
                        }
                    });
                    outcome.Issue.AddRange(issues);
                    result.Parameter.Add(new Parameters.ParameterComponent()
                    {
                        Resource = outcome
                    });
                    return result;
                }

                // Evaluate the CQL expression
                var expressionValue = _expressionBuilder.Lambda(expression).Compile().DynamicInvoke(_cqlContext);
                outcome.Issue.Add(new IssueComponent()
                {
                    Code = IssueType.Informational,
                    Severity = IssueSeverity.Information,
                    Details = new CodeableConcept()
                    {
                        Text = $"Executed CQL Expression: {cqlExpression}"
                    },
                    ElementId = "expression"
                });
                result.Parameter.Add(new Parameters.ParameterComponent()
                {
                    Name = "return",
                    Value = ConvertToFhirDataType(expressionValue!)
                });

                //Check if they provided an expectation
                if (cqlExpectation != null)
                {
                    // Build and validate the CQL expectation
                    var expectation = Expression(cqlExpectation);
                    var expectationErrors = expectation.GetErrors();
                    if (expectationErrors.Any())
                    {
                        var issues = expressionErrors.Select(s => new IssueComponent()
                        {
                            Code = IssueType.Invalid,
                            Severity = IssueSeverity.Error,                            
                            Details = new CodeableConcept()
                            {                                
                                Text = s.message
                            },
                            ElementId = "expectation"
                        });
                        outcome.Issue.AddRange(issues);
                        result.Parameter.Add(new Parameters.ParameterComponent()
                        {
                            Resource = outcome
                        });
                        return result;
                    }

                    // Evaluate the CQL expectation
                    var expectationValue = _expressionBuilder.Lambda(expectation).Compile().DynamicInvoke(_cqlContext);
                    outcome.Issue.Add(new IssueComponent()
                    {
                        Code = IssueType.Informational,
                        Severity = IssueSeverity.Information,
                        Details = new CodeableConcept()
                        {
                            Text = $"Executed CQL Expression: {cqlExpectation}"
                        }
                    });
                    result.Parameter.Add(new Parameters.ParameterComponent()
                    {
                        Name = "expected",
                        Value = ConvertToFhirDataType(expectationValue!)
                    });

                    // Compare the CQL expression and expectation
                    Elm.Expression equal = Equals(expression, expectation);
                    var equalLambda = _expressionBuilder.Lambda(equal);
                    var equalDelegate = equalLambda.Compile();
                    var equalResult = (bool?)equalDelegate.DynamicInvoke(_cqlContext);
                    
                    outcome.Issue.Add(new IssueComponent()
                    {
                        Code = IssueType.Informational,
                        Severity = IssueSeverity.Information,
                        Details = new CodeableConcept()
                        {
                            Text = $"Compared CQL Expression: {cqlExpectation} | Expectation: {cqlExpectation}"
                        }
                    });
                    result.Parameter.Add(new Parameters.ParameterComponent()
                    {
                        Name = "equal",
                        Value = new FhirBoolean(equalResult)
                    });

                }
                result.Parameter.Add(new Parameters.ParameterComponent()
                {
                    Resource = outcome
                });

                return result;
            }
            catch (Exception ex)
            {
                var message = ex.Message;
                outcome.Issue.Add(new IssueComponent()
                {
                    Code = IssueType.Exception,
                    Severity = IssueSeverity.Fatal,
                    Details = new CodeableConcept()
                    {
                        Text = $"Exception occurred: {ex.Message}"
                    }
                });
                return result;
            }

        }

        /// <summary>
        /// Convert the CQL Expression type to a FHIR data type
        /// </summary>
        /// <param name="expressionResult"></param>
        /// <returns></returns>
        public DataType ConvertToFhirDataType(object expressionResult)
        {
            try
            {
                var resultType = expressionResult.GetType();
                switch (resultType.Name)
                {
                    case "Boolean":
                        return new FhirBoolean((bool)expressionResult);
                    case "Int32":
                    case "Int64":
                        return new Hl7.Fhir.Model.Integer((int)expressionResult);
                    case "Decimal":
                        return new FhirDecimal((decimal)expressionResult);
                    case "String":
                        return new FhirString((string)expressionResult);
                    case nameof(CqlDateTime):
                        return new Hl7.Fhir.Model.FhirDateTime(expressionResult.ToString());
                    case "DateTimeOffset":
                        return new FhirDateTime((DateTimeOffset)expressionResult);
                    case nameof(CqlTime):
                        return new Hl7.Fhir.Model.Time(expressionResult.ToString());
                    case "Double":
                        return new FhirDecimal((decimal)expressionResult);
                    default:
                        return new FhirString(expressionResult.ToString());
                }
            }
            catch (Exception)
            {
                return new FhirString(null);
            }

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="expression"></param>
        /// <param name="memberName"></param>
        /// <returns></returns>
        protected Elm.Expression Expression(string expression, [System.Runtime.CompilerServices.CallerMemberName] string memberName = "func")
        {
            var lib = ConvertLibrary($@"
                library Test version '1.0.0'

                define private ""{memberName}"": {expression}");
            return lib.statements[0].expression;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cql"></param>
        /// <returns></returns>
        protected Elm.Library ConvertLibrary(string cql) => _defaultConverter.ConvertLibrary(cql);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="lib"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        ExpressionBuilder ExpressionBuilderFor(Elm.Library lib, ILogger<ExpressionBuilder> logger)
        {
            var tr = FhirTypeResolver.Default;
            var tc = FhirTypeConverter.Default;
            var tm = new TypeManager(tr);
            var binding = new CqlOperatorsBinding(tr, tc);
            var expressionBuilder = new ExpressionBuilder(binding, tm, lib, logger);
            return expressionBuilder;
        }

        private Elm.Expression Equals(Elm.Expression expression, Elm.Expression expectation)
        {
            var equal = _invocationBuilder.Invoke(SystemLibrary.Equal, expression, expectation);
            var @if = new If
            {
                condition = new And
                {
                    operand = new[]
                    {
                        new IsNull { operand = expression }.WithResultType(SystemTypes.BooleanType),
                        new IsNull { operand = expectation }.WithResultType(SystemTypes.BooleanType),
                    }
                }.WithResultType(SystemTypes.BooleanType),
                then = _elmFactory.Literal(true),
                @else = equal,
            }.WithResultType(SystemTypes.BooleanType);
            return @if;
        }
    }
}
