using Hl7.Cql.Fhir;
using Hl7.Cql.Primitives;
using Hl7.Fhir.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hl7.Cql.Elm.Visitor
{
    public class DataRequirementsBuilder : LibraryVisitor
    {
        private HashSet<string> _properties = new();

        // Map of FHIR property code paths to the codes they might be for the current statement
        private HashSet<string> _codeProperties = new();
        private HashSet<Tuple<string, string>> _codeRequirements = new();
        private HashSet<string> _valueSetFilters = new();

        // Same map as above but accumulated for all statements
        private Dictionary<string, HashSet<Tuple<string, string>>> _codeRequirementsTotal = new();
        private Dictionary<string, HashSet<string>> _valueSetFiltersTotal = new();

        // Stack that holds a function scoped stack of Maps that 
        // associate variable names (aliases) to properties or child properties
        public Stack<Dictionary<string, string>> _path = new(new Dictionary<string, string>[] { new() });

        private readonly List<string> _resourceExclusions = new()
        {
            "Resource",
            "Reference",
            "Identifier",
            "Extension",
            "DomainResource",
            "Meta"
        };

        private void Clear()
        {
            _codeRequirements.Clear();
            _valueSetFilters.Clear();
            _codeRequirementsTotal.Clear();
            _valueSetFiltersTotal.Clear();
        }

        public DataRequirementsBuilder(Dictionary<string, Library> libraries) : base(libraries)
        {
        }

        public DataRequirementsBuilder(Bundle measureBundle) : base(measureBundle)
        {
        }

        private string GetSingle(string name)
        {
            if (name.Contains(">"))
            {
                return name.Split('>', '<')[1];
            }

            return name;
        }

        /// <summary>
        /// Generates data requirements for a single statement in the measure library
        /// </summary>
        /// <param name="defineName"></param>
        /// <returns></returns>
        public List<DataRequirement> BuildByDefine(string defineName)
        {
            Clear();
            
            var statement = Libraries.ElementAt(0).Value.statements.SingleOrDefault(_ => _.name == defineName);
            if (statement == null)
            {
                throw new ArgumentException($"Statement {defineName} does not exist!");
            }
            VisitStatement(statement, Libraries.ElementAt(0).Key);

            return BuildInternal();
        }

        /// <summary>
        /// Builds data requirement aggregate for all public defines in library.
        /// </summary>
        /// <returns></returns>
        public List<DataRequirement> Build()
        {
            Clear();

            VisitLibrary(0);
            return BuildInternal();
        }

        private List<DataRequirement> BuildInternal()
        {
            Hl7.Cql.Packaging.CqlTypeToFhirTypeMapper typeMapper = new(FhirTypeResolver.Default);
            List<DataRequirement> dataRequirements = new();
            foreach (var n in _properties)
            {
                var root = n.Split(".")[0];
                var fhirType = typeMapper.TypeEntryFor("{http://hl7.org/fhir}" + root);
                if (!Enum.TryParse<FHIRAllTypes>(root, out var fhirAllType)
                    || fhirType == null
                    || fhirType.CqlType != CqlPrimitiveType.Fhir
                    || _resourceExclusions.Contains(root))
                {
                    // Console.WriteLine($"Skipping invalid fhir type {n}");
                    continue;
                }

                var existing = dataRequirements.SingleOrDefault(o => o.Type == fhirAllType);

                if (existing == null)
                {
                    existing = new()
                    {
                        Type = fhirAllType,
                        MustSupport = new List<string>()
                    };
                    dataRequirements.Add(existing);
                }


                // Must support
                var currentMustSupport = existing.MustSupport.ToList();
                currentMustSupport.Add(n.Substring(n.IndexOf(".") + 1));
                existing.MustSupport = currentMustSupport;

                // Code filter
                if (_codeRequirementsTotal.TryGetValue(n, out var codeRequirements))
                {
                    var currentCodeFilter = existing.CodeFilter;
                    var codeFilter = new DataRequirement.CodeFilterComponent()
                    {
                        Path = n,
                    };

                    foreach (var codeRequirement in codeRequirements)
                    {
                        codeFilter.Code.Add(new Coding(codeRequirement.Item1, codeRequirement.Item2));
                    }
                    currentCodeFilter.Add(codeFilter);
                }

                // Add code filter for each value set
                if (_valueSetFiltersTotal.TryGetValue(n, out var valueSetFilter))
                {
                    foreach (var valueSet in valueSetFilter)
                    {
                        var currentCodeFilter = existing.CodeFilter;
                        var codeFilter = new DataRequirement.CodeFilterComponent()
                        {
                            Path = n,
                        };

                        codeFilter.ValueSet = valueSet;

                        currentCodeFilter.Add(codeFilter);
                    }
                }
            }

            return dataRequirements;

        }

        protected override void VisitStatement(ExpressionDef expression, string? Library = null)
        {

            base.VisitStatement(expression, Library);

            foreach (var n in _codeProperties)
            {
                _codeRequirementsTotal.TryAdd(n, new());

                foreach (var x in _codeRequirements)
                {
                    _codeRequirementsTotal[n].Add(x);
                }

                foreach (var v in _valueSetFilters)
                {
                    _valueSetFiltersTotal.TryAdd(n, new());
                    foreach (var x in n)
                    {
                        _valueSetFiltersTotal[n].Add(v);
                    }
                }
            }
            _valueSetFilters.Clear();
            _codeRequirements.Clear();
        }

        public override void VisitProperty(Property property)
        {

            string source;

            if (!string.IsNullOrEmpty(property.scope))
            {
                source = _path.Peek()[property.scope];
            }
            else
            {
                switch (property.source)
                {
                    case OperandRef operandRef:
                    {
                        source = _path.Peek()[operandRef.name];
                        break;
                    }

                    default:
                    {
                        source = GetElementReturnType(property.source);
                        break;
                    }
                }

            }

            // Tuple property sources cannot be resolved
            if (source.StartsWith("Tuple"))
            {
                base.VisitProperty(property);
                return;
            }

            var fullPath = $"{GetSingle(source)}.{property.path}";

            // Clean up path
            var cleanPath = fullPath.Split("}").Last();
            var props = cleanPath.Split(".");
            for (int i = 1; i < props.Length; i++)
            {
                props[i] = props[i][0].ToString().ToLower() + props[i].Substring(1);
            }

            cleanPath = string.Join(".", props);
            _properties.Add(cleanPath);

            var type = GetSingle(GetElementReturnType(property));

            if (
                type == "{http://hl7.org/fhir}Coding" ||
                type == "{http://hl7.org/fhir}Code" ||
                type == "{http://hl7.org/fhir}CodeableConcept")
            {
                _codeProperties.Add(cleanPath);
            }

            base.VisitProperty(property);
        }

        public override void VisitFunctionRef(FunctionRef expression)
        {

            _path.Push(new());
            var currentPath = _path.Peek();
            var functionDef = GetFunctionDef(CurrentLibrary, expression);
            for (int i = 0; i < functionDef.operand.Length; i++)
            {
                var r = GetElementReturnType(expression.operand[i]);
                currentPath[functionDef.operand[i].name] = r;
            }


            base.VisitFunctionRef(expression);

            _path.Pop();
        }

        public override void VisitQuery(Query query)
        {
            foreach (var n in query.source)
            {
                _path.Peek()[n.alias] = GetElementReturnType(n.expression);
            }

            if (query.let != null)
            {
                foreach (var n in query.let)
                {
                    _path.Peek()[n.identifier] = GetElementReturnType(n.expression);

                }
            }

            if (query.relationship != null)
            {
                foreach (var n in query.relationship)
                {
                    _path.Peek()[n.alias] = GetElementReturnType(n.expression);

                }
            }

            base.VisitQuery(query);
        }

        public override void VisitCodeRef(CodeRef expression)
        {
            _codeRequirements.Add(ResolveCode(expression));
            base.VisitCodeRef(expression);
        }

        public override void VisitValueSetRef(ValueSetRef expression)
        {
            var def = ResolveValueSet(expression);
            _valueSetFilters.Add(def.id);
            base.VisitValueSetRef(expression);
        }

        private string ResolvePropertySource(Property property)
        {
            if (!string.IsNullOrEmpty(property.scope))
            {
                return _path.Peek()[property.scope];
            }
            if (property.source is Property sourceProperty)
            {
                return $"{ResolvePropertySource(sourceProperty)}.{property.path}";
            }
            else
            {
                return GetSingle(GetElementReturnType(property.source));
            }
        }

        private Tuple<string, string> ResolveCode(CodeRef codeRef)
        {
            var lib = GetLibrary(CurrentLibrary, codeRef.libraryName);
            var codeInLib = lib.codes.Single(c => c.name == codeRef.name);
            var system = lib.codeSystems.Single(s => s.name == codeInLib.codeSystem.name);

            return new Tuple<string, string>(system.id, codeInLib.id);
        }

        private ValueSetDef ResolveValueSet(ValueSetRef valueSetRef)
        {
            var lib = GetLibrary(CurrentLibrary, valueSetRef.libraryName);
            var valueSetInLib = lib.valueSets.Single(v => v.name == valueSetRef.name);
            return valueSetInLib;

        }
    }
}
