using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Hl7.Cql.Compiler;
using Hl7.Cql.Operators;
using Hl7.Cql.Runtime;

namespace Hl7.Cql.CodeGeneration.NET
{
    /// <summary>
    /// Finds the definitions whose value cannot depend on the patient being evaluated: they never retrieve data, never
    /// raise messages, and only reference other such definitions. Their cached values can survive from one patient to the
    /// next, as long as the parameters and evaluation time of the context stay the same.
    /// </summary>
    /// <remarks>
    /// The analysis is conservative: anything it does not recognize is treated as patient-dependent.
    /// </remarks>
    internal sealed class PatientIndependenceAnalyzer
    {
        private readonly DefinitionDictionary<LambdaExpression> _definitions;
        private readonly Dictionary<(string, string, string), bool> _memo = new();
        private readonly HashSet<(string, string, string)> _inProgress = new();

        public PatientIndependenceAnalyzer(DefinitionDictionary<LambdaExpression> definitions)
        {
            _definitions = definitions;
        }

        /// <summary>
        /// Whether the definition (a lambda whose only parameter is the <see cref="CqlContext"/>) is patient-independent.
        /// </summary>
        public bool IsPatientIndependentDefinition(string libraryName, string definitionName, LambdaExpression lambda) =>
            IsIndependent(libraryName, definitionName, SignatureKey(Array.Empty<Type>()), lambda);

        private static string SignatureKey(IEnumerable<Type> signature) =>
            string.Join(",", signature.Select(t => t.AssemblyQualifiedName));

        private bool IsIndependent(string libraryName, string name, string signatureKey, LambdaExpression lambda)
        {
            var key = (libraryName, name, signatureKey);
            if (_memo.TryGetValue(key, out var known))
                return known;
            if (!_inProgress.Add(key))
                return false;

            try
            {
                var visitor = new TaintVisitor(this);
                visitor.Visit(lambda.Body);
                var result = !visitor.Tainted;
                _memo[key] = result;
                return result;
            }
            finally
            {
                _inProgress.Remove(key);
            }
        }

        private bool DefinitionIsIndependent(string libraryName, string definitionName) =>
            _definitions.TryGetValue(libraryName, definitionName, Array.Empty<Type>(), out var lambda)
            && lambda is not null
            && IsIndependent(libraryName, definitionName, SignatureKey(Array.Empty<Type>()), lambda);

        private bool FunctionIsIndependent(string libraryName, string functionName, Type[] signature) =>
            _definitions.TryGetValue(libraryName, functionName, signature, out var lambda)
            && lambda is not null
            && IsIndependent(libraryName, functionName, SignatureKey(signature), lambda);

        private sealed class TaintVisitor : ExpressionVisitor
        {
            private readonly PatientIndependenceAnalyzer _owner;

            public TaintVisitor(PatientIndependenceAnalyzer owner)
            {
                _owner = owner;
            }

            public bool Tainted { get; private set; }

            public override Expression? Visit(Expression? node) =>
                Tainted ? node : base.Visit(node);

            private static bool IsContext(Expression? e) => e is ParameterExpression p && p.Type == typeof(CqlContext);

            private static bool IsOperatorsOfContext(Expression? e) =>
                e is MemberExpression { Member.Name: nameof(CqlContext.Operators), Expression: { } inner } && IsContext(inner);

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (IsContext(node.Object))
                {
                    // Parameters are fixed for the lifetime of the cached values.
                    if (node.Method.Name != nameof(CqlContext.ResolveParameter))
                        Tainted = true;
                    foreach (var argument in node.Arguments)
                        Visit(argument);
                    return node;
                }

                if (IsOperatorsOfContext(node.Object))
                {
                    var name = node.Method.Name;
                    if (name.StartsWith("Retrieve", StringComparison.Ordinal)
                        || name == nameof(ICqlOperators.Message))
                        Tainted = true;
                    foreach (var argument in node.Arguments)
                        Visit(argument);
                    return node;
                }

                return base.VisitMethodCall(node);
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (IsOperatorsOfContext(node))
                    return node;
                return base.VisitMember(node);
            }

            // Any other use of the context (passed as an argument, other members) is not understood.
            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (IsContext(node))
                    Tainted = true;
                return node;
            }

            protected override Expression VisitExtension(Expression node)
            {
                switch (node)
                {
                    case DefinitionCallExpression dce:
                        if (!_owner.DefinitionIsIndependent(dce.LibraryName, dce.DefinitionName))
                            Tainted = true;
                        return node;
                    case FunctionCallExpression fce:
                        // The first argument is the context.
                        var signature = fce.Arguments.Skip(1).Select(a => a.Type).ToArray();
                        if (!_owner.FunctionIsIndependent(fce.LibraryName, fce.FunctionName, signature))
                            Tainted = true;
                        foreach (var argument in fce.Arguments.Skip(1))
                            Visit(argument);
                        return node;
                    default:
                        return base.VisitExtension(node);
                }
            }
        }
    }
}
