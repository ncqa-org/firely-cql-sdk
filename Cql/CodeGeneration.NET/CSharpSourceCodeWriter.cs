/* 
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Abstractions;
using Hl7.Cql.CodeGeneration.NET.Visitors;
using Hl7.Cql.Graph;
using Hl7.Cql.Primitives;
using Hl7.Cql.Runtime;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Hl7.Cql.CodeGeneration.NET
{
    /// <summary>
    /// Writes <see cref="LambdaExpression"/>s as members of a .NET class.
    /// </summary>
    internal class CSharpSourceCodeWriter
    {
        /// <summary>
        /// Creates an instance.
        /// </summary>
        /// <param name="log">The <see cref="ILogger{TCategoryName}"/> to report output.</param>
        public CSharpSourceCodeWriter(ILogger<CSharpSourceCodeWriter> log)
        {
            Log = log;
        }

        /// <summary>
        /// Gets or sets the namespace for generated .NET types.
        /// </summary>
        public string? Namespace { get; set; }
        /// <summary>
        /// If <see langword="true"/>, classes will be declared with the <see langword="partial"/> keyword.
        /// </summary>
        public bool PartialClass { get; set; }

        /// <summary>
        /// The <see cref="AccessModifier"/> to use for the <see cref="CqlContext"/> class member; its default value is <see cref="AccessModifier.Internal"/>.
        /// </summary>
        public AccessModifier ContextAccessModifier { get; set; } = AccessModifier.Internal;
        /// <summary>
        /// The <see cref="AccessModifier"/> to use for all CQL defines; its default value is <see cref="AccessModifier.Internal"/>.
        /// </summary>
        public AccessModifier DefinesAccessModifier { get; set; } = AccessModifier.Internal;

        /// <summary>
        /// Gets the <see langword="using"/> statements to be included in the generated code.
        /// </summary>
        public IList<string> Usings { get; } = new List<string>
        {
            nameof(System),
            typeof(Enumerable).Namespace!, // System.Linq
            typeof(ICollection<>).Namespace!, // System.Collections.Generic
            typeof(CqlContext).Namespace!,
            typeof(CqlPrimitiveType).Namespace!,
        };
        /// <summary>
        /// Gets the aliased <see langword="using"/> statements to be included in the generated code.
        /// For example,
        /// <code>
        /// using Item1 = Item2;
        /// </code>
        /// </summary>
        public IList<(string, string)> AliasedUsings { get; } = new List<(string, string)>
        {
        };

        /// <summary>
        /// Gets the version of this <see cref="CSharpSourceCodeWriter"/> as will appear in the <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute.Version"/>.
        /// </summary>
        public string Version => GetType().Assembly?.GetName()?.Version?.ToString() ?? "1.0.0";
        /// <summary>
        /// Gets the product of this <see cref="CSharpSourceCodeWriter"/> as will appear in the <see cref="System.CodeDom.Compiler.GeneratedCodeAttribute.Tool"/>.
        /// </summary>
        public string Tool =>
                GetType()
                .Assembly?
                .GetCustomAttributes(false)
                .OfType<AssemblyProductAttribute>()
                .SingleOrDefault()?
                .Product
            ?? "ELM-to-CSharp";

        internal ILogger<CSharpSourceCodeWriter> Log { get; }

        internal IList<string> ContextLibraries { get; set; } = new List<string>();

        /// <summary>
        /// Writes C# source code from inputs.
        /// </summary>
        /// <param name="definitions">The lambda expressions to write.</param>
        /// <param name="tupleTypes">Tuple types generated during lambda creation.</param>
        /// <param name="dependencyGraph">A dependency graph containing dependent libraries.</param>
        /// <param name="libraryNameToStream">A function that provides a <see cref="Stream"/> to write the source code given the name of the library being generated.</param>
        /// <param name="closeStream">When <see langword="true"/>, <see cref="Stream"/>s provided by <paramref name="libraryNameToStream"/> will be closed when writing is done.  Default value is <see langword="true"/></param>
        /// <param name="writeFile">A function that determines whether the given library should be generated or not; default is <see langword="null" />.  When <see langword="null" />, all libraries are written.</param>
        /// 
        public void Write(DefinitionDictionary<LambdaExpression> definitions,
            IEnumerable<Type> tupleTypes,
            DirectedGraph dependencyGraph,
            Func<string, Stream> libraryNameToStream,
            bool closeStream = true,
            Predicate<string>? writeFile = null)
        {
            var libraryNameToClassName = VariableNameGenerator.NormalizeIdentifier;
            bool defaultWriteFile(string nodeId) => true;
            writeFile ??= defaultWriteFile;

            //writeInterface(libraryNameToStream, closeStream);

            writeTupleTypes(tupleTypes, libraryNameToStream, closeStream);

            writeLibraries(definitions, dependencyGraph, libraryNameToStream, closeStream, writeFile!, libraryNameToClassName);
        }

        //private void writeInterface(Func<string, Stream> libraryNameToStream, bool closeStream)
        //{
        //    var stream = libraryNameToStream("ICqlMeasure");
        //    try
        //    {
        //        int indentLevel = 0;
        //        using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
        //        WriteUsings(writer);

        //        writer.WriteLine(indentLevel, $"public interface ICqlMeasure");
        //        writer.WriteLine(indentLevel, "{");
        //        indentLevel += 1;
        //        writer.WriteLine(indentLevel, $"IDictionary<string, object?> RunAll();");
        //        writer.WriteLine(indentLevel, $"IDictionary<string, object?> RunPopulationOnly();");
        //        writer.WriteLine(indentLevel, $"IDictionary<string, object?> Run();");
        //        indentLevel -= 1;
        //        writer.WriteLine(indentLevel, "}");
        //    }
        //    finally
        //    {
        //        if (closeStream && stream != null)
        //        {
        //            stream.Close();
        //        }
        //    }
        //}

        private void writeLibraries(DefinitionDictionary<LambdaExpression> definitions,
            DirectedGraph dependencyGraph,
            Func<string, Stream> libraryNameToStream,
            bool closeStream, Predicate<string> writeFile, Func<string?, string?> libraryNameToClassName)
        {
            var buildOrder = DetermineBuildOrder(dependencyGraph);

            foreach (var library in buildOrder)
            {
                string libraryName = library.NodeId;

                if (!writeFile(libraryName))
                    continue;

                if (!definitions.Libraries.Contains(libraryName))
                {
                    Log.LogInformation($"Skipping library {libraryName}, which has no definitions");
                    continue;
                }

                var stream = libraryNameToStream(libraryName);

                try
                {
                    using var writer = new StreamWriter(stream, Encoding.UTF8, 1024, leaveOpen: true);
                    int indentLevel = 0;
                    WriteUsings(writer);

                    var cacheLibraryName = ""; //cacheLibrary?.NodeId.Replace("-", "_").Replace(".", "_");

                    var hasContext = false;
                    var node = dependencyGraph.Nodes[libraryName];
                    if (node.Properties != null
                        && node.Properties.TryGetValue("Library", out var nodeLibrary))
                    {
                        var requiredUsesContext = false;
                        var requiredLibraries = node.ForwardEdges?
                            .Select(edge => edge.ToId)
                            .Except(new[] { dependencyGraph.EndNode.NodeId })
                            .Distinct();

                        foreach (var dependentLibrary in requiredLibraries!)
                        {
                            var dependentNode = dependencyGraph.Nodes[dependentLibrary];
                            // dependency uses a constructor so we will need make the entire class scoped vs singleton
                            if (dependentNode.Properties != null &&
                                dependentNode.Properties.TryGetValue("Library", out var dependNodeLibrary))
                            {
                                var elmLibrary = (Elm.Library)dependNodeLibrary;
                                if (elmLibrary?.contexts != null)
                                {
                                    requiredUsesContext = true;

                                    if (!ContextLibraries.Contains(dependentLibrary))
                                        ContextLibraries.Add(dependentLibrary);
                                }

                                if (dependentLibrary.StartsWith("Cache", StringComparison.Ordinal))
                                {
                                    cacheLibraryName = dependentLibrary.Replace("-", "_").Replace(".", "_");
                                }
                            }
                        }

                        hasContext = ((Elm.Library)nodeLibrary)?.contexts != null || requiredUsesContext;
                    }

                    // Namespace
                    if (!string.IsNullOrWhiteSpace(Namespace))
                    {
                        writer.WriteLine(indentLevel, $"namespace {Namespace}");
                        writer.WriteLine(indentLevel, "{");
                        writer.WriteLine();
                        indentLevel += 1;
                    }

                    writeClass(definitions, dependencyGraph, libraryNameToClassName, libraryName, writer, indentLevel, hasContext, cacheLibraryName);

                    if (!string.IsNullOrWhiteSpace(Namespace))
                    {
                        writer.WriteLine(indentLevel, "}");
                        indentLevel -= 1;
                    }
                }
                finally
                {
                    if (closeStream && stream != null)
                    {
                        stream.Close();
                    }
                }
            }
        }

        private void writeClass(DefinitionDictionary<LambdaExpression> definitions,
            DirectedGraph dependencyGraph,
            Func<string?, string?> libraryNameToClassName,
            string libraryName, StreamWriter writer,
            int indentLevel,
            bool hasContext,
            string? cacheLibraryName)
        {
            writer.WriteLine(indentLevel, $"[System.CodeDom.Compiler.GeneratedCode(\"{Tool}\", \"{Version}\")]");

            var libraryAttribute = libraryName;
            var versionAttribute = string.Empty;
            var nameAndVersion = libraryName.Split('-');
            if (nameAndVersion.Length == 2)
            {
                if (System.Version.TryParse(nameAndVersion[1], out var version))
                {
                    libraryAttribute = nameAndVersion[0];
                    versionAttribute = nameAndVersion[1];
                }
            }

            writer.WriteLine(indentLevel, $"[CqlLibrary(\"{libraryAttribute}\", \"{versionAttribute}\")]");
            var className = VariableNameGenerator.NormalizeIdentifier(libraryName);
            if (PartialClass)
                writer.WriteLine(indentLevel, $"public partial class {className}");
            //else if (hasContext)
            //    writer.WriteLine(indentLevel, $"public class {className} : ICqlMeasure");
            else
                writer.WriteLine(indentLevel, $"public class {className}");

            writer.WriteLine(indentLevel, "{");
            writer.WriteLine();
            indentLevel += 1;

            // Class
            {
                // singleton for libraries, constructors for measures
                // files that have a context defined will get the constructor with lazy for caching
                if (!hasContext)
                {
                    writer.WriteLine(indentLevel, $"public static {className} Instance {{ get; }}  = new();");
                    writer.WriteLine();
                }
                else
                {
                    if (!ContextLibraries.Contains(libraryName))
                        ContextLibraries.Add(libraryName);

                    writer.WriteLine(indentLevel, $"{AccessModifierString(ContextAccessModifier)} CqlContext context;");
                    writer.WriteLine();

                    // Write constructor
                    if (!string.IsNullOrEmpty(cacheLibraryName))
                    {
                        writer.WriteLine(indentLevel, $"{AccessModifierString(ContextAccessModifier)} {cacheLibraryName} cache;");
                        writer.WriteLine();
                    }

                    writeCachedValues(definitions, libraryName, writer, indentLevel);

                    // Write constructor
                    if (!string.IsNullOrEmpty(cacheLibraryName))
                        writer.WriteLine(indentLevel, $"public {className}(CqlContext context, {cacheLibraryName} cache)");
                    else
                        writer.WriteLine(indentLevel, $"public {className}(CqlContext context)");
                    writer.WriteLine(indentLevel, "{");
                    {
                        indentLevel += 1;

                        writer.WriteLine(indentLevel, "this.context = context ?? throw new ArgumentNullException(\"context\");");
                        writer.WriteLine();

                        if (!string.IsNullOrEmpty(cacheLibraryName))
                        {
                            writer.WriteLine(indentLevel, "this.cache = cache ?? throw new ArgumentNullException(\"cache\");");
                            writer.WriteLine();
                        }

                        writeDependencies(dependencyGraph, libraryNameToClassName, libraryName, writer, indentLevel);
                        indentLevel -= 1;
                    }

                    writer.WriteLine(indentLevel, "}");
                }

                WriteLibraryMembers(writer, dependencyGraph, libraryName, libraryNameToClassName!, indentLevel);

                //if (hasContext)
                //{
                //    writeICqlMeasureInterface(definitions, libraryName, writer, indentLevel);
                //    writer.WriteLine();
                //}

                writeMethods(definitions, libraryName, writer, indentLevel, hasContext, cacheLibraryName);

                indentLevel -= 1;
                writer.WriteLine(indentLevel, "}");
            }
        }

        //private void writeICqlMeasureInterface(DefinitionDictionary<LambdaExpression> definitions, string libraryName, StreamWriter writer, int indentLevel)
        //{
        //    var libDef = definitions.DefinitionsForLibrary(libraryName);

        //    var populationDefines = new List<string>();
        //    var allDefines = new List<string>();
        //    var ipDefines = new Dictionary<string, Type>();
        //    var exclusionDefines = new Dictionary<string, Type>();

        //    foreach (var kvp in libDef)
        //    {
        //        foreach (var overload in kvp.Value)
        //        {
        //            definitions.TryGetTags(libraryName, kvp.Key, overload.Signature, out var tags);
        //            if (isDefinition(overload.T))
        //            {
        //                allDefines.Add(kvp.Key);

        //                if (kvp.Key == "Patient")
        //                {
        //                    populationDefines.Add(kvp.Key);
        //                }

        //                var popTag = tags?.FirstOrDefault(t => t.Key == "population");
        //                var groupTag = tags?.FirstOrDefault(t => t.Key == "group");
        //                if (popTag != null && groupTag != null)
        //                {
        //                    var popTagValue = popTag.First();
        //                    populationDefines.Add(kvp.Key);

        //                    if (popTagValue == "initial-population")
        //                    {
        //                        ipDefines.Add(kvp.Key, overload.T.ReturnType);
        //                    }
        //                    else if (popTagValue == "denominator-exclusion")
        //                    {
        //                        exclusionDefines.Add(kvp.Key, overload.T.ReturnType);
        //                    }
        //                }
        //            }
        //        }
        //    }

        //    #region RunAll
        //    writer.WriteLine(indentLevel, "public IDictionary<string, object> RunAll()");
        //    writer.WriteLine(indentLevel, "{");
        //    indentLevel += 1;
        //    writer.WriteLine(indentLevel, "var result = new Dictionary<string, object>");
        //    writer.WriteLine(indentLevel, "{");
        //    indentLevel += 1;
        //    foreach (var def in allDefines)
        //    {
        //        var methodName = VariableNameGenerator.NormalizeIdentifier(def);
        //        writer.WriteLine(indentLevel, "{ " + $"\"{def}\", this.{methodName}()" + " },");
        //    }
        //    indentLevel -= 1;
        //    writer.WriteLine(indentLevel, "};");
        //    writer.WriteLine(indentLevel, "");
        //    writer.WriteLine(indentLevel, "return result;");
        //    indentLevel -= 1;
        //    writer.WriteLine(indentLevel, "}");
        //    #endregion
        //    #region RunPopulationOnly
        //    writer.WriteLine(indentLevel, "public IDictionary<string, object> RunPopulationOnly()");
        //    writer.WriteLine(indentLevel, "{");
        //    indentLevel += 1;
        //    writer.WriteLine(indentLevel, "var result = new Dictionary<string, object>");
        //    writer.WriteLine(indentLevel, "{");
        //    indentLevel += 1;
        //    foreach (var def in populationDefines)
        //    {
        //        var methodName = VariableNameGenerator.NormalizeIdentifier(def);
        //        writer.WriteLine(indentLevel, "{ " + $"\"{def}\", this.{methodName}()" + " },");
        //    }
        //    indentLevel -= 1;
        //    writer.WriteLine(indentLevel, "};");
        //    writer.WriteLine(indentLevel, "");
        //    writer.WriteLine(indentLevel, "return result;");
        //    indentLevel -= 1;
        //    writer.WriteLine(indentLevel, "}");
        //    #endregion
        //    #region Run
        //    writer.WriteLine(indentLevel, "public IDictionary<string, object> Run()");
        //    writer.WriteLine(indentLevel, "{");
        //    indentLevel += 1;

        //    if (ipDefines.Any())
        //    {
        //        writer.WriteLine(indentLevel, "var hasIpTrue = false;");
        //        foreach (var def in ipDefines)
        //        {
        //            var methodName = VariableNameGenerator.NormalizeIdentifier(def.Key);
        //            if (def.Value == typeof(bool) || def.Value == typeof(bool?))
        //                writer.WriteLine(indentLevel, $"hasIpTrue = hasIpTrue || (this.{methodName}() ?? false);");
        //            else if (def.Value == typeof(int) || def.Value == typeof(int?))
        //                writer.WriteLine(indentLevel, $"hasIpTrue = hasIpTrue || (this.{methodName}() ?? 0) > 0;");
        //            else
        //                writer.WriteLine(indentLevel, $"hasIpTrue = hasIpTrue || (this.{methodName}()?.Any() ?? false);");
        //        }
        //        writer.WriteLine(indentLevel, "if (!hasIpTrue) return new Dictionary<string, object>();");
        //    }

        //    if (exclusionDefines.Any())
        //    {
        //        writer.WriteLine(indentLevel, "var allExclusionsTrue = true;");

        //        foreach (var def in exclusionDefines)
        //        {
        //            var methodName = VariableNameGenerator.NormalizeIdentifier(def.Key);
        //            if (def.Value == typeof(bool) || def.Value == typeof(bool?))
        //                writer.WriteLine(indentLevel, $"allExclusionsTrue = allExclusionsTrue && (this.{methodName}() ?? false);");
        //            else if (def.Value == typeof(int) || def.Value == typeof(int?))
        //                writer.WriteLine(indentLevel, $"allExclusionsTrue = allExclusionsTrue && (this.{methodName}() ?? 0) > 0;");
        //            else
        //                writer.WriteLine(indentLevel, $"allExclusionsTrue = allExclusionsTrue && (this.{methodName}()?.Any() ?? false);");
        //        }
        //        writer.WriteLine(indentLevel, "if (allExclusionsTrue) return new Dictionary<string, object>();");
        //    }

        //    writer.WriteLine(indentLevel, "");
        //    writer.WriteLine(indentLevel, "return RunAll();");
        //    indentLevel -= 1;
        //    writer.WriteLine(indentLevel, "}");
        //    #endregion
        //}

        private void writeMethods(DefinitionDictionary<LambdaExpression> definitions, string libraryName, StreamWriter writer, int indentLevel, bool useLazy, string? cacheLibraryName)
        {
            foreach (var kvp in definitions.DefinitionsForLibrary(libraryName))
            {
                foreach (var overload in kvp.Value)
                {
                    definitions.TryGetTags(libraryName, kvp.Key, overload.Signature, out var tags);
                    writeMethod(libraryName, writer, indentLevel, useLazy, kvp.Key, overload.T, cacheLibraryName, tags);
                    writer.WriteLine();
                }
            }
        }

        private void writeDependencies(DirectedGraph dependencyGraph,
            Func<string?, string?> libraryNameToClassName,
            string libraryName, StreamWriter writer,
            int indentLevel)
        {
            var node = dependencyGraph.Nodes[libraryName];
            var requiredLibraries = node.ForwardEdges?
                .Select(edge => edge.ToId)
                .Except(new[] { dependencyGraph.EndNode.NodeId })
                .Distinct();

            foreach (var dependentLibrary in requiredLibraries!)
            {
                if (ContextLibraries.Contains(dependentLibrary) && !dependentLibrary.StartsWith("Cache", StringComparison.Ordinal))
                {
                    var typeName = libraryNameToClassName!(dependentLibrary);
                    var memberName = typeName;
                    writer.WriteLine(indentLevel, $"{memberName} = new {typeName}(context);");
                }
            }
        }

        private void writeCachedValues(DefinitionDictionary<LambdaExpression> definitions, string libraryName, StreamWriter writer, int indentLevel)
        {
            // Each definition is evaluated at most once per instance (one patient context), on first access.
            // A plain field plus an "evaluated" flag replaces Lazy<T>: instances are used by a single thread,
            // so Lazy's locking, and the Lazy + closure + delegate allocated per definition in the constructor,
            // are not needed. Definitions that are never used now cost nothing.
            writer.WriteLine(indentLevel, "#region Cached values");
            writer.WriteLine();

            foreach (var kvp in definitions.DefinitionsForLibrary(libraryName))
            {
                foreach (var overload in kvp.Value)
                {
                    if (isDefinition(overload.T))
                    {
                        var methodName = VariableNameGenerator.NormalizeIdentifier(kvp.Key);
                        var cachedValueName = DefinitionCacheKeyForMethod(methodName!);
                        var returnType = ExpressionConverter.PrettyTypeName(overload.T.ReturnType);
                        writer.WriteLine(indentLevel, $"private {returnType} {cachedValueName};");
                        writer.WriteLine(indentLevel, $"private bool {cachedValueName}_evaluated;");
                    }
                }
            }
            writer.WriteLine();
            writer.WriteLine(indentLevel, "#endregion");
        }

        private void writeTupleTypes(IEnumerable<Type> tupleTypes, Func<string, Stream> libraryNameToStream, bool closeStream)
        {
            if (tupleTypes.Any())
            {
                foreach (var tupleType in tupleTypes!)
                {
                    if (tupleType == null)
                        continue;
                    var tupleTypeStream = libraryNameToStream(Path.Combine(tupleType.Namespace!, tupleType.Name));
                    try
                    {
                        var writer = new StreamWriter(tupleTypeStream);
                        WriteUsings(writer);
                        var indentLevel = 0;
                        writer.WriteLine();
                        writer.WriteLine(indentLevel, $"namespace {tupleType.Namespace}");
                        writer.WriteLine(indentLevel, "{");
                        indentLevel += 1;
                        WriteTupleType(writer, indentLevel, tupleType);
                        indentLevel -= 1;
                        writer.WriteLine(indentLevel, "}");
                        writer.Flush();
                    }
                    finally
                    {
                        if (closeStream && tupleTypeStream != null)
                        {
                            tupleTypeStream.Close();
                            tupleTypeStream = null;
                        }
                    }
                }
            }
            else
            {
                Log.LogInformation($"No tuple types detected; skipping.");
            }
        }

        private static bool isDefinition(LambdaExpression overload) =>
            overload.Parameters.Count == 1
                && overload.Parameters[0].Type == typeof(CqlContext);

        private void WriteLibraryMembers(TextWriter writer,
            DirectedGraph dependencyGraph,
            string libraryName,
            Func<string, string> libraryNameToClassName,
            int indent)
        {
            var node = dependencyGraph.Nodes[libraryName];
            var requiredLibraries = node.ForwardEdges?
                .Select(edge => edge.ToId)
                .Except(new[] { dependencyGraph.EndNode.NodeId })
                .Distinct();
            if (requiredLibraries != null)
            {

                writer.WriteLine(indent, "#region Dependencies");
                writer.WriteLine();

                foreach (var dependentLibrary in requiredLibraries)
                {
                    // library exists and it's not the "Cache" library
                    //if (ContextLibraries.Contains(dependentLibrary))
                    if (ContextLibraries.Contains(dependentLibrary) && !dependentLibrary.StartsWith("Cache", StringComparison.Ordinal))
                    {
                        var typeName = libraryNameToClassName(dependentLibrary);
                        var memberName = typeName;
                        writer.WriteLine(indent, $"public {typeName} {memberName} {{ get; }}");
                    }
                }

                writer.WriteLine();
                writer.WriteLine(indent, "#endregion");
                writer.WriteLine();
            }
        }

        private IList<DirectedGraphNode> DetermineBuildOrder(DirectedGraph minimalGraph)
        {
            var sorted = minimalGraph.TopologicalSort()
                .Where(n => n.NodeId != minimalGraph.StartNode.NodeId
                    && n.NodeId != minimalGraph.EndNode.NodeId)
                .ToList();
            return sorted;
        }

        private string DefinitionCacheKeyForMethod(string methodName)
        {
            if (methodName[0] == '@')
                return "__" + methodName.Substring(1);
            else return "__" + methodName;
        }
        private string PrivateMethodNameFor(string methodName) => methodName + "_Value";

        private void writeMethod(string libraryName, TextWriter writer, int indentLevel,
            bool useLazy,
            string cqlName,
            LambdaExpression overload,
            string? cacheLibraryName,
            ILookup<string, string>? tags)
        {
            var methodName = VariableNameGenerator.NormalizeIdentifier(cqlName);
            var isDef = isDefinition(overload);

            var vng = new VariableNameGenerator(Enumerable.Empty<string>(), postfix: "_");

            var visitedBody = Transform(overload.Body,
                new RedundantCastsTransformer(),
                new FuseListOperatorsVisitor(),
                new PlainBooleanLogicVisitor(),
                new SimplifyExpressionsVisitor(),
                new ShortCircuitLogicalOperatorsVisitor(),
                new InlineTailLocalFunctionVisitor(),
                new RenameVariablesVisitor(vng),
                new LocalVariableDeduper()
            );

            // One dedupe pass can expose more duplicates: e.g. "End(a_)" and "End(g_)" only become identical after
            // g_ has been replaced by a_. Repeat until no more assignments disappear.
            for (var pass = 0; pass < 5; pass++)
            {
                var before = CountAssignments(visitedBody);
                var deduped = new LocalVariableDeduper().Visit(visitedBody);
                if (CountAssignments(deduped) >= before)
                    break;
                visitedBody = deduped;
            }

            var expressionConverter = new ExpressionConverter(libraryName, ContextLibraries);

            // Skip CqlContext
            var parameters = overload.Parameters.Skip(1);

            overload = Expression.Lambda(visitedBody, parameters);

            if (isDef)
            {
                if (useLazy)
                {
                    var privateMethodName = PrivateMethodNameFor(methodName!);

                    var func = expressionConverter.ConvertTopLevelFunctionDefinition(indentLevel, overload, privateMethodName, "private", true);

                    if (!string.IsNullOrEmpty(cacheLibraryName))
                    {
                        // convert from
                        // var a_ = Cache_2025_0_0.Has_hospice_during();
                        // to
                        // var a_ = cache.Has_hospice_during();
                        // (the shared Cache instance caches the value itself)
                        func = func.Replace($"{cacheLibraryName}.", "cache.");
                    }
                    writer.Write(func);
                    writer.WriteLine();
                }

                writer.WriteLine(indentLevel, $"[CqlDeclaration(\"{cqlName}\")]");
                WriteTags(writer, indentLevel, tags);

                if (overload.ReturnType == typeof(CqlValueSet))
                {
                    if (overload.Body is NewExpression @new)
                    {
                        var arg = @new.Arguments[0];
                        if (arg is ConstantExpression constant)
                        {
                            if (constant.Value is string valueSetId)
                            {
                                writer.WriteLine(indentLevel, $"[CqlValueSet(\"{valueSetId}\")]");
                            }
                        }
                    }
                }

                if (useLazy)
                {
                    var cachedValueName = DefinitionCacheKeyForMethod(methodName!);
                    var privateMethodName = PrivateMethodNameFor(methodName!);
                    var returnType = ExpressionConverter.PrettyTypeName(visitedBody.Type);

                    // [DebuggerStepThrough] lets "Step Into" go straight to the definition body (X_Value)
                    // instead of stopping in this caching accessor.
                    writer.WriteLine(indentLevel, "[System.Diagnostics.DebuggerStepThrough]");
                    writer.WriteLine(indentLevel, $"public {returnType} {methodName}()");
                    writer.WriteLine(indentLevel, "{");
                    writer.WriteLine(indentLevel + 1, $"if (!{cachedValueName}_evaluated)");
                    writer.WriteLine(indentLevel + 1, "{");
                    writer.WriteLine(indentLevel + 2, $"{cachedValueName} = this.{privateMethodName}(context);");
                    writer.WriteLine(indentLevel + 2, $"{cachedValueName}_evaluated = true;");
                    writer.WriteLine(indentLevel + 1, "}");
                    writer.WriteLine();
                    writer.WriteLine(indentLevel + 1, $"return {cachedValueName};");
                    writer.WriteLine(indentLevel, "}");
                }
                else if (TryGetConstantVocabulary(overload.Body, out var vocabulary))
                {
                    // Value sets, codes and code lists are immutable constants: create them once instead of
                    // allocating a new instance on every call.
                    var cachedValueName = DefinitionCacheKeyForMethod(methodName!);
                    var returnType = ExpressionConverter.PrettyTypeName(overload.ReturnType);
                    var initializer = expressionConverter.ConvertExpression(indentLevel + 1, vocabulary, leadingIndent: false);
                    writer.WriteLine(indentLevel, $"public {returnType} {methodName}(CqlContext context) => {cachedValueName};");
                    writer.WriteLine();
                    writer.WriteLine(indentLevel, $"private static readonly {returnType} {cachedValueName} = {initializer};");
                }
                else
                {
                    writer.Write(expressionConverter.ConvertTopLevelFunctionDefinition(indentLevel, overload, methodName!, "public", true));
                }
            }
            else
            {
                writer.WriteLine(indentLevel, $"[CqlDeclaration(\"{cqlName}\")]");
                WriteTags(writer, indentLevel, tags);
                writer.Write(expressionConverter.ConvertTopLevelFunctionDefinition(indentLevel, overload, methodName!, "public", !useLazy));
            }
        }

        private static readonly HashSet<Type> VocabularyTypes = new()
        {
            typeof(CqlValueSet), typeof(CqlCode), typeof(CqlCodeSystem), typeof(CqlConcept),
        };

        /// <summary>
        /// Recognizes definition bodies that only construct vocabulary from constants
        /// (<c>new CqlValueSet("...", null)</c>, <c>new CqlCode(...)</c>, arrays of those), which can be hoisted to a static field.
        /// The simplified body is a block that assigns the array to a local before returning it, so look through that.
        /// </summary>
        private static bool TryGetConstantVocabulary(Expression body, out Expression vocabulary)
        {
            vocabulary = body;
            if (body is BlockExpression { Expressions.Count: 2 } block
                && block.Expressions[0] is BinaryExpression { NodeType: ExpressionType.Assign, Left: ParameterExpression assigned } assign
                && block.Expressions[1] == assigned)
            {
                vocabulary = assign.Right;
            }

            return IsConstantVocabulary(vocabulary);

            static bool IsConstantVocabulary(Expression e) => e switch
            {
                NewExpression @new => VocabularyTypes.Contains(@new.Type) && @new.Arguments.All(IsConstantArgument),
                NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array =>
                    VocabularyTypes.Contains(array.Type.GetElementType()!) && array.Expressions.All(IsConstantVocabulary),
                _ => false,
            };

            static bool IsConstantArgument(Expression e) => e switch
            {
                ConstantExpression => true,
                UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.TypeAs } u => IsConstantArgument(u.Operand),
                NewArrayExpression { NodeType: ExpressionType.NewArrayInit } array => array.Expressions.All(IsConstantVocabularyOrConstant),
                _ => IsConstantVocabularyOrConstant(e),
            };

            static bool IsConstantVocabularyOrConstant(Expression e) =>
                e is ConstantExpression || IsConstantVocabulary(e);
        }

        private static void WriteTags(TextWriter writer, int indentLevel, ILookup<string, string>? tags)
        {
            if (tags != null)
            {
                foreach (var group in tags)
                {
                    foreach (var tag in group)
                    {
                        writer.WriteLine(indentLevel, $"[CqlTag(\"{group.Key}\", \"{tag}\")]");
                    }
                }
            }
        }

        private void WriteUsings(TextWriter writer)
        {
            foreach (var @using in Usings.Distinct())
            {
                writer.WriteLine($"using {@using};");
            }
            foreach (var @using in AliasedUsings)
            {
                writer.WriteLine($"using {@using.Item1} = {@using.Item2};");
            }
        }

        private int WriteTupleType(TextWriter writer, int indentLevel, Type tupleType)
        {
            writer.WriteLine(indentLevel, $"[System.CodeDom.Compiler.GeneratedCode(\"{Tool}\", \"{Version}\")]");
            writer.WriteLine(indentLevel, $"public class {tupleType.Name}: {ExpressionConverter.PrettyTypeName(tupleType.BaseType!)}");
            writer.WriteLine(indentLevel, "{");

            indentLevel++;
            foreach (var property in tupleType.GetProperties())
            {
                var normalizedName = VariableNameGenerator.NormalizeIdentifier(property.Name);
                var propertyType = ExpressionConverter.PrettyTypeName(property.PropertyType);
                var cqlDeclarationAttribute = property.GetCustomAttribute<CqlDeclarationAttribute>();
                if (cqlDeclarationAttribute != null)
                {
                    writer.WriteLine(indentLevel, $"[CqlDeclaration(\"{cqlDeclarationAttribute.Name}\")]");
                }
                writer.WriteLine(indentLevel, $"public {propertyType} {normalizedName} {{ get; set; }}");
            }
            indentLevel--;
            writer.WriteLine(indentLevel, $"}}");
            return indentLevel;
        }

        private static int CountAssignments(Expression expression)
        {
            var counter = new AssignmentCounter();
            counter.Visit(expression);
            return counter.Count;
        }

        private sealed class AssignmentCounter : ExpressionVisitor
        {
            public int Count { get; private set; }

            protected override Expression VisitBinary(BinaryExpression node)
            {
                if (node.NodeType == ExpressionType.Assign)
                    Count++;
                return base.VisitBinary(node);
            }
        }

        private static Expression Transform(Expression body, params ExpressionVisitor[] visitors)
        {
            foreach (var visitor in visitors)
            {
                body = visitor.Visit(body);
            }
            return body;
        }

        private static string AccessModifierString(AccessModifier modifier) => modifier switch
        {
            AccessModifier.Public => "public",
            AccessModifier.Private => "private",
            AccessModifier.Internal => "internal",
            AccessModifier.Protected => "protected",
            AccessModifier.ProtectedInternal => "protected internal",
            _ => throw new ArgumentException("Invalid access modifier", nameof(modifier)),
        };


    }
}

