#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Hl7.Cql.Fhir;
using Hl7.Cql.Packaging;
using Hl7.Fhir.Model;

#pragma warning disable CS8604
#pragma warning disable CS8767

namespace Hl7.Cql.Elm.Visitor
{
    public class LibraryVisitor: IExpressionVisitor
    {
        public readonly Dictionary<string, Library> Libraries;
        private readonly Dictionary<string, ValueSetDef> _valueSetDefLookup = new();
        private readonly Dictionary<string, CodeDef> _codeDefLookup = new();
        private readonly Dictionary<string, CodeSystemDef> _codeSystemDefLookup = new();
        private readonly  CqlTypeToFhirTypeMapper _typeMapper = new(FhirTypeResolver.Default);
        private readonly Stack<Tuple<Library, Element>> _visitStack = new();

        private Library? _current;

        protected Library? CurrentLibrary => _current;

        private Dictionary<string, Stack<Element>> _aliasMap = new();

        // State
        private Dictionary<string, OperandDef> _operandRefStack = new();


        public LibraryVisitor(Dictionary<string, Library> libraries)
        {
            Libraries = libraries;
        }

        public LibraryVisitor(Bundle measureBundle)
        {
            Libraries = new();
            foreach (var library in measureBundle.Entry.ByResourceType<Hl7.Fhir.Model.Library>())
            {
                var content = library.Content.Single(content =>
                    Regex.Unescape(content.ContentType) == "application/elm+json");

                using var stream = new MemoryStream(content.Data);
                var elm = Library.LoadFromJson(stream);

                var libNameNoVersion = elm.NameAndVersion!.Split('-')[0];

                Libraries.Add(libNameNoVersion, elm);

                foreach (var valueSet in elm?.valueSets ?? Enumerable.Empty<ValueSetDef>())
                {
                    if (valueSet?.name != null && valueSet?.id != null)
                        _valueSetDefLookup.Add($"{libNameNoVersion}.{valueSet.name}", valueSet);
                }

                foreach (var code in elm?.codes ?? Enumerable.Empty<CodeDef>())
                {
                    if (code?.name != null && code?.id != null)
                        _codeDefLookup.Add($"{libNameNoVersion}.{code.name}", code);
                }

                foreach (var codeSystem in elm?.codeSystems ?? Enumerable.Empty<CodeSystemDef>())
                {
                    /*if (codeSystem?.name != null && codeSystem?.id != null)
                        _codeSystemDefLookup.Add(codeSystem.name, codeSystem);*/
                }
            }
        }

        public LibraryVisitor(LibraryVisitor old) : this(old.Libraries)
        {

        }

        /// <summary>
        /// Gets a library's aliased name or real name if none exists
        /// </summary>
        /// <param name="source"></param>
        /// <param name="libraryName"></param>
        protected string GetLibraryName(Library source, string libraryName)
        {
            return GetLibrary(source, libraryName).Name!;
        }

        protected Library GetLibrary(Library source, string? libraryName)
        {
            if (string.IsNullOrEmpty(libraryName))
            {
                return source;
            }

            var alias = source.includes?.SingleOrDefault(_ => _.localIdentifier == libraryName);
            if (alias != null)
            {
                return Libraries[alias.path];
            }

            if (Libraries.ContainsKey(libraryName))
            {
                return Libraries[libraryName];
            }
            return source;
        }

        protected ValueSetDef? GetValueSetDef(string valueSetFullyQualifiedName)
        {
            if (string.IsNullOrEmpty(valueSetFullyQualifiedName))
                return null;

            if (_valueSetDefLookup.ContainsKey(valueSetFullyQualifiedName))
                return _valueSetDefLookup[valueSetFullyQualifiedName];

            var libraryName = valueSetFullyQualifiedName.Split('.')[0];
            var valueSetName = valueSetFullyQualifiedName.Split('.')[1];
            var library = GetLibrary(_current, libraryName);
            var aliasFullyQualifiedName = $"{library.Name}.{valueSetName}";

            if (_valueSetDefLookup.ContainsKey(aliasFullyQualifiedName))
                return _valueSetDefLookup[aliasFullyQualifiedName];

            return null;
        }

        protected CodeDef? GetCodeDef(string codeFullyQualifiedName)
        {
            if (string.IsNullOrEmpty(codeFullyQualifiedName))
                return null;

            if (_codeDefLookup.ContainsKey(codeFullyQualifiedName))
                return _codeDefLookup[codeFullyQualifiedName];

            var libraryName = codeFullyQualifiedName.Split('.')[0];
            var codeName = codeFullyQualifiedName.Split('.')[1];
            var library = GetLibrary(_current, libraryName);
            var aliasFullyQualifiedName = $"{library.Name}.{codeName}";

            if (_codeDefLookup.ContainsKey(aliasFullyQualifiedName))
                return _codeDefLookup[aliasFullyQualifiedName];

            return null;
        }

        protected CodeSystemDef? GetCodeSystemDef(string codeFullyQualifiedName)
        {
            var codeSystemName = GetCodeDef(codeFullyQualifiedName)?.codeSystem.name;
            return _codeSystemDefLookup.ContainsKey(codeSystemName) ? _codeSystemDefLookup[codeSystemName] : null;
        }

        /// <summary>
        /// Visits all expressions in library by name
        /// </summary>
        public void VisitLibrary(string name)
        {
            _current = Libraries[name];
            foreach (var statement in _current.statements)
            {
                if (statement.accessLevel == AccessModifier.Private) continue;

                VisitStatement(statement);
            }
            _current = null;
        }

        public void VisitExpression(string library, string expression)
        {
            _current = Libraries[library];
            _current.statements.Single(o => o.name == expression).Accept(this);
        }

        /// <summary>
        /// Visits all expressions in library by index
        /// </summary>
        /// <param name="index"></param>
        public void VisitLibrary(int index)
        {
            _current = Libraries.ElementAt(index).Value;
            foreach (var statement in _current.statements)
            {
                if (statement.accessLevel == AccessModifier.Private) continue;

                VisitStatement(statement);
            }
            _current = null;
        }

        /// <summary>
        /// Gets a string representation of an element type
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private string GetFhirTypeName(Hl7.Cql.Elm.Element type)
        {
            if (type == null)
            {
                throw new ArgumentException("Type cannot be null");
            }

            var resultType = (type is TypeSpecifier) ? type : type.resultTypeSpecifier;

            var fhirType = _typeMapper.TypeEntryFor(type);

            if (type.resultTypeName != null)
            {
                return type.resultTypeName.Name;
            }

            if (fhirType?.FhirType == FHIRAllTypes.List || resultType is ListTypeSpecifier)
            {
                if (resultType is NamedTypeSpecifier named)
                {
                    return named.name.Name;
                }

                if (resultType is ListTypeSpecifier listType)
                {
                    if (listType.elementType is NamedTypeSpecifier listTypeNamed)
                    {
                        return $"List<{listTypeNamed.name.Name}>";
                    }

                    if (listType.elementType is IntervalTypeSpecifier listInvervalType)
                    {
                        return $"List<{GetFhirTypeName(listInvervalType)}>";
                    }

                    return $"List<{GetFhirTypeName(listType.elementType)}>";

                }

                throw new ArgumentException($"List type cannot be resolved");
            }

            if (resultType is NamedTypeSpecifier named1)
            {
                return named1.name.Name;
            }

            if (resultType is IntervalTypeSpecifier interval)
            {
                if (interval.pointType is NamedTypeSpecifier named2)
                {
                    switch (named2.name.Name)
                    {
                        case "{urn:hl7-org:elm-types:r1}Date":
                        case "{urn:hl7-org:elm-types:r1}DateTime":
                        case "{urn:hl7-org:elm-types:r1}Time":
                            return "{urn:hl7-org:elm-types:r1}Period";
                    }

                    return $"Interval<{named2.name.Name}>";
                }
            }

            if (resultType is TupleTypeSpecifier tuple)
            {
                if (tuple.element.Length == 0)
                    throw new ArgumentException($"Tuple type cannot be resolved");

                var returnType = "";
                foreach (var element in tuple.element)
                {
                    if (string.IsNullOrEmpty(returnType))
                        returnType += "Tuple<";
                    else
                        returnType += ",";
                    returnType += GetFhirTypeName(element.elementType);
                }
                returnType += ">";
                return returnType;
            }

            if (resultType is ChoiceTypeSpecifier choice)
            {
                var r = $"Choice<{string.Join(", ", choice.choice.Select(GetFhirTypeName))}>";
                return r;
            }

            throw new ArgumentException($"Could not find type for {type.GetType().Name}");
        }

        /// <summary>
        /// Gets a string representation of the return type of an element.
        /// For literals or basic expressions this is just the type, for
        /// function refs this should follow the call to a concrete type.
        /// </summary>
        /// <param name="source">Library containing the source element for following
        /// function calls.</param>
        /// <param name="type"></param>
        /// <returns></returns>
        private string GetReturnType(Library source, Hl7.Cql.Elm.Element type)
        {
            switch (type)
            {
                case OperandDef operandDef:
                    return GetFhirTypeName(operandDef.operandTypeSpecifier);

                case ExpandValueSet:
                    return "List<{urn:hl7-org:elm-types:r1}Code>";

                case Flatten flatten:
                    return GetFhirTypeName(flatten);

                case Query query:
                    {
                        if (query.@return != null)
                        {

                            // Getting the return type of a query might require getting the type of the aliased
                            // source for the query, so add it to the known aliases temporarily
                            foreach (var n in query.source)
                            {
                                _aliasMap.TryAdd(n.alias, new());
                                _aliasMap[n.alias].Push(n);
                            }

                            var value = $"List<{GetReturnType(source, query.@return.expression)}>";

                            foreach (var n in query.source)
                            {
                                _aliasMap[n.alias].Pop();
                            }

                            return value;
                        }

                        return GetFhirTypeName(query);
                    }

                case AliasedQuerySource aliasedQuerySource:
                    {
                        if (aliasedQuerySource.resultTypeName != null)
                        {
                            return aliasedQuerySource.resultTypeName.Name;
                        }

                        if (aliasedQuerySource.resultTypeSpecifier != null)
                        {
                            return GetFhirTypeName(aliasedQuerySource.resultTypeSpecifier);
                        }

                        // If a query source is from a list, the actual returned value is the element
                        if (aliasedQuerySource.expression.resultTypeSpecifier is ListTypeSpecifier queryListResult)
                        {
                            return GetFhirTypeName(queryListResult.elementType);
                        }

                        var sourceType = GetReturnType(source, aliasedQuerySource.expression);

                        return sourceType;
                    }

                // Follow the alias refs to find the actual type
                case AliasRef aliasRef:
                    {
                        if (aliasRef.resultTypeName != null)
                        {
                            return aliasRef.resultTypeName.Name;
                        }

                        if (aliasRef.resultTypeSpecifier != null)
                        {
                            return GetFhirTypeName(aliasRef.resultTypeSpecifier);
                        }

                        return GetReturnType(source, _aliasMap[aliasRef.name].Peek());
                    }

                case FunctionRef functionRef:
                    var functionDef = GetFunctionDef(source, functionRef);
                    return GetReturnType(source, functionDef);

                case As @as:
                    {
                        if (@as.asType != null)
                        {
                            return @as.asType.Name;
                        }
                        return GetFhirTypeName(@as.asTypeSpecifier);
                    }

                case Property property:
                {
                    if (property.resultTypeName != null || property.resultTypeSpecifier != null)
                    {
                        return GetFhirTypeName(type);
                    }

                    if (string.IsNullOrWhiteSpace(property.path))
                    {
                        throw new ArgumentException("Cannot determine type of property");
                    }

                    return property.path;
                }

                case ExpressionRef exprRef:
                {
                    var def = GetFunctionDef(source, exprRef);
                    return GetReturnType(source, def.expression);
                }

                case SingletonFrom singletonFrom:
                {
                    return GetReturnType(source, singletonFrom.operand);
                }

                case Retrieve retrieve:
                {
                    if (retrieve.resultTypeName != null || retrieve.resultTypeSpecifier != null)
                    {
                        return GetFhirTypeName(type);
                    }

                    return retrieve.dataType.Name;
                }

                case Interval interval:
                {
                    if (interval.resultTypeSpecifier != null)
                    {
                        return GetFhirTypeName(type);
                    }

                    return "Interval<?>";
                }

                case ToDecimal:
                {
                    return "{urn:hl7-org:elm-types:r1}Decimal";
                }

                case ToInteger:
                {
                    return "{urn:hl7-org:elm-types:r1}Integer";
                }

                default:
                    return GetFhirTypeName(type);
            }
        }

        protected string GetElementReturnType(Library source, Hl7.Cql.Elm.Element type)
        {
            // TODO - Period is equivalent to an interval

            var returnType = GetReturnType(source, type);

            switch (returnType)
            {
                case "{http://hl7.org/fhir}Age":
                case "{http://hl7.org/fhir}SimpleQuantity":
                    return "{http://hl7.org/fhir}Quantity";
                default:
                    return returnType;
            }
        }

        protected string GetElementReturnType(Hl7.Cql.Elm.Element type) => GetElementReturnType(_current, type);

        protected ExpressionDef GetFunctionDef(
            Library librarySource,
            ExpressionRef expressionRef)
        {
            var lib = GetLibrary(librarySource, expressionRef.libraryName);
            return lib.statements.Single(_ => _.name == expressionRef.name);
        }

        /// <summary>
        /// Gets the function reference from a library, resolving any overloads
        /// </summary>
        protected FunctionDef GetFunctionDef(
            Library librarySource,
            FunctionRef functionRef)
        {
            var targetLib = GetLibrary(librarySource, functionRef.libraryName);
            // Get all functions in a library
            var functionDefs =
                targetLib.statements
                    .OfType<FunctionDef>()
                    .Where(_ => _.name == functionRef.name).ToArray();

            // Special case for ToString, which relies on runtime type info
            if (functionRef.name == "ToString")
            {
                return functionDefs[0];
            }

            if (functionDefs.Length == 1)
            {
                return functionDefs[0];
            }

            // Get signature of the function call we're trying to make
            var signature = functionRef.operand.Select(o => GetElementReturnType(librarySource, o)).ToArray();

            // Match signature to a single function in external lib


            var def = functionDefs.SingleOrDefault(fd =>
                fd.operand.Select(o => GetElementReturnType(targetLib, o)).SequenceEqual(signature));


            if (def == null)
            {
                StringBuilder sb = new();
                sb.AppendLine(CultureInfo.InvariantCulture,
                    $"Could not find one matching overload in library {targetLib.Name} from library source {librarySource.Name} " +
                    $"for function" +
                    $" {functionRef.name}({string.Join(",", functionRef.operand.Select(_ => GetElementReturnType(librarySource, _) ?? "null"))}) [{functionRef.locator}]");
                sb.AppendLine("Overloads are: ");
                foreach (var x in functionDefs)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture,
                        $"     {x.name}({string.Join(",", x.operand.Select(_ => GetElementReturnType(targetLib, _.operandTypeSpecifier) ?? "null"))}) [{x.expression.locator}]");
                }

                throw new InvalidOperationException(sb.ToString());
            }

            return def;
        }

        /// <summary>
        /// Gets a copy of the element visit history
        /// </summary>
        protected Stack<Tuple<Library, Element>> GetHistory() => new(new Stack<Tuple<Library, Element>>(_visitStack));

        /// <summary>
        /// Gets the nearest ancestor of type T
        /// </summary>
        /// <typeparam name="T">Type of element to look for</typeparam>
        /// <returns>Element if found, null if no ancestors or no matching ancestors.</returns>
        protected T? GetFirstAncestor<T>() where T : Element
        {
            var history = GetHistory();
            while (history.TryPop(out var e))
            {
                if (e.Item2 is T match) return match;
            }
            return null;
        }

        /// <summary>
        /// Called when about to enter any element
        /// </summary>
        /// <param name="element"></param>
        public virtual void VisitElement(Element element)
        {
            _visitStack.Push(new(_current, element));
        }

        /// <summary>
        /// Called when leaving any element
        /// </summary>
        /// <param name="expression"></param>
        public virtual void LeaveElement(Element expression)
        {
            _visitStack.Pop();
        }

        protected virtual void VisitStatement(ExpressionDef expression, string? library = null)
        {
            if (library != null)
            {
                _current = Libraries[library];
            }
            expression.expression.Accept(this);
        }

        public virtual void VisitAggregation(AggregateExpression expression, IEnumerable<Expression> parameters)
        {
            foreach (var parameter in parameters)
                parameter.Accept(this);
        }

        public virtual void VisitCollection(Expression expression, IEnumerable<Expression> elements)
        {
            foreach (var element in elements ?? Enumerable.Empty<Expression>())
                element.Accept(this);
        }

        public virtual void VisitControlFlow(Expression expression, IEnumerable<Expression> blocks)
        {
            foreach (var block in blocks)
                block.Accept(this);
        }

        public virtual void VisitDateExpression(Expression expression, IEnumerable<Expression> operands)
        {
            foreach (var operand in operands)
                operand.Accept(this);
        }

        public virtual void VisitMathExpression(Expression expression, Expression[] operands)
        {
            foreach (var operand in operands)
                operand.Accept(this);
        }

        public virtual void VisitOperator(OperatorExpression expression, IEnumerable<Expression> operands)
        {
            foreach (var operand in operands)
                operand.Accept(this);
        }

        public virtual void VisitProperty(Property property)
        {
            property.source.Accept(this);
        }

        public virtual void VisitQuery(Query query)
        {
            foreach (var statement in query.source ?? Enumerable.Empty<AliasedQuerySource>())
            {
                _aliasMap.TryAdd(statement.alias, new());
                _aliasMap[statement.alias].Push(statement);

                statement.Accept(this);
            }

            foreach (var let in query.let ?? Enumerable.Empty<LetClause>())
                let.expression.Accept(this);

            foreach (var relationship in query.relationship ?? Enumerable.Empty<RelationshipClause>())
                relationship.suchThat.Accept(this);

            query.where.Accept(this);
            query.@return?.expression?.Accept(this);
            query.aggregate?.expression?.Accept(this);
            query.aggregate?.starting?.Accept(this);

            foreach (var statement in query.source ?? Enumerable.Empty<AliasedQuerySource>())
            {
                _aliasMap[statement.alias].Pop();
            }
        }

        public virtual void VisitRef(OperandRef expression)
        { }

        public virtual void VisitRetrieve(Retrieve retrieve)
        {
            retrieve.id.Accept(this);
            retrieve.codes.Accept(this);
            retrieve.dateRange.Accept(this);
            retrieve.context.Accept(this);

            foreach (var codeFilter in retrieve.codeFilter ?? Enumerable.Empty<CodeFilterElement>())
                codeFilter.value.Accept(this);

            foreach (var dateFilter in retrieve.dateFilter ?? Enumerable.Empty<DateFilterElement>())
                dateFilter.value.Accept(this);

            foreach (var otherFilter in retrieve.otherFilter ?? Enumerable.Empty<OtherFilterElement>())
                otherFilter.value.Accept(this);
        }

        public virtual void VisitAliasQuerySource(AliasedQuerySource aliasedQuerySource)
        {
            aliasedQuerySource.expression.Accept(this);
        }

        public virtual void VisitCodeRef(CodeRef expression)
        { }

        public virtual void VisitCodeSystemRef(CodeSystemRef expression)
        { }

        public virtual void VisitConceptRef(ConceptRef expression)
        { }

        public virtual void VisitQueryLetRef(QueryLetRef expression)
        { }

        public virtual void VisitAliasRef(AliasRef expression)
        { }

        public virtual void VisitIdentifierRef(IdentifierRef expression)
        { }

        public virtual void VisitOperandRef(OperandRef expression)
        { }

        public virtual void VisitParameterRef(ParameterRef expression)
        { }

        public virtual void VisitValueSetRef(ValueSetRef expression)
        { }

        public virtual void VisitCode(Code code)
        {
            // points to CodeSystemRef
            code.system.Accept(this);
        }

        public virtual void VisitRef(ExpressionRef expression)
        {
            Library? current = _current;
            Library? referenced;

            // Null library names are a self reference
            if (string.IsNullOrEmpty(expression.libraryName))
            {
                referenced = current;
            }
            else
            {
                var name = GetLibraryName(_current, expression.libraryName);
                referenced = Libraries[name];
            }

            var refExpression = GetFunctionDef(referenced, expression).expression;

            _current = referenced;
            refExpression.Accept(this);
            _current = current;
        }

        public void VisitRef(FunctionRef expression)
        {
            // Visit all operand expressions
            foreach (var operand in expression.operand)
            {
                operand.Accept(this);
            }

            VisitFunctionRef(expression);
        }

        public virtual void VisitFunctionRef(FunctionRef expression)
        {
            Library? current = _current;

            var referenced = GetLibrary(current, expression.libraryName);

            _current = referenced;

            // Find referenced expression

            FunctionDef overload = GetFunctionDef(current, expression);
            overload?.expression.Accept(this);

            _current = current;
        }

        public virtual void VisitRef(ValueSetRef expression) { }

        public virtual void VisitRef(CodeRef expression) { }

        public virtual void VisitRef(AliasRef expression) { }

        public virtual void VisitConstant(Expression expression) { }
    }
}
