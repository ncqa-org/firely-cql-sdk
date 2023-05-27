﻿using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using ElmPackage = Hl7.Cql.Elm.ElmPackage;
using Hl7.Cql.Graph;
using Hl7.Cql.Compiler;
using Hl7.Cql.Runtime.FhirR4;
using Hl7.Cql.Poco.Fhir.R4.Model;
using Hl7.Cql.CodeGeneration.NET;
using Hl7.Cql.Runtime;
using Hl7.Cql.Iso8601;
using Hl7.Cql.Elm;
using Hl7.Cql.Primitives;
using Hl7.Cql.Poco.Fhir.R4;

namespace MeasurePackager
{
    public class Packager
    {
        public IDictionary<string, ElmPackage> LoadPackages(DirectoryInfo elmDir)
        {
            var dict = new ConcurrentDictionary<string, ElmPackage>();
            var files = elmDir.GetFiles("*.json", SearchOption.AllDirectories);
            Parallel.ForEach(files, file =>
            {
                var package = ElmPackage.LoadFrom(file);
                if (package != null)
                {
                    dict.TryAdd(package.NameAndVersion, package);
                }
            });
            return dict;
        }

        public DirectedGraph GetPackageGraph(IDictionary<string, ElmPackage> packages, ILogger<Packager> logger)
        {
            var buildOrder = new DirectedGraph();
            foreach (var package in packages.Values)
            {
                var includes = ElmPackage.GetIncludedLibraries(package, (name, version) =>
                {
                    var id = ElmPackage.NameAndVersionFor(name, version);
                    if (id != null && packages.TryGetValue(id, out var dependency))
                        return dependency;
                    else
                    {
                        var ex = new InvalidOperationException($"Cannot find library {id} referenced in {package.NameAndVersion}");
                        logger.Log(LogLevel.Critical, ex, null);
                        throw ex;
                    }
                });
                MergeGraphInfo(includes, buildOrder);
            }
            return buildOrder;
        }

        public IEnumerable<Resource> PackageResources(DirectoryInfo elmDirectory,
            DirectoryInfo cqlDirectory,
            DirectedGraph packageGraph,
            OperatorBinding operatorBinding,
            TypeManager typeManager,
            FhirCqlCrosswalk typeCrosswalk,
            Func<Resource, string> canon,
            IEnumerable<Assembly> references,
            IEnumerable<string> usings,
            ILogger<ExpressionBuilder> builderLogger,
            ILogger<CSharpSourceCodeWriter> writerLogger)
        {
            var elmPackages = packageGraph.Nodes.Values
                .Select(node => node.Properties?[ElmPackage.PackageNodeProperty] as ElmPackage)
                .Where(p => p is not null)
                .ToArray();

            var all = new DefinitionDictionary<LambdaExpression>();
            foreach (var package in elmPackages)
            {
                var builder = new ExpressionBuilder(operatorBinding, typeManager, package, builderLogger);
                var expressions = builder.Build();
                all.Merge(expressions);
            }
            var scw = new CSharpSourceCodeWriter(writerLogger);
            foreach (var @using in usings)
                scw.Usings.Add(@using);
            scw.AliasedUsings.Add(("Range", typeof(Hl7.Cql.Poco.Fhir.R4.Model.Range).FullName!));

            var navToLibraryStream = new Dictionary<string, Stream>();
            var assemblies = Generate(all, typeManager, packageGraph, scw, navToLibraryStream, references);
            var libraries = new Dictionary<string, Hl7.Cql.Poco.Fhir.R4.Model.Library>();
            foreach (var package in elmPackages)
            {
                var elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{package.NameAndVersion}.json"));
                if (!elmFile.Exists)
                    elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{package.library.identifier.id}.json"));
                if (!elmFile.Exists)
                    throw new InvalidOperationException($"Cannot find ELM file for {package.NameAndVersion}");
                var cqlFiles = cqlDirectory.GetFiles($"{package.NameAndVersion}.cql", SearchOption.AllDirectories);
                if (cqlFiles.Length == 0)
                {
                    cqlFiles = cqlDirectory.GetFiles($"{package.library!.identifier!.id}.cql", SearchOption.AllDirectories);
                    if (cqlFiles.Length == 0)
                        throw new InvalidOperationException($"{package.library!.identifier!.id}.cql");
                }
                if (cqlFiles.Length > 1)
                    throw new InvalidOperationException($"More than 1 CQL file found.");
                var cqlFile = cqlFiles[0];
                if (!assemblies.TryGetValue(package.NameAndVersion, out var assembly))
                    throw new InvalidOperationException($"No assembly for {package.NameAndVersion}");
                var builder = new ExpressionBuilder(operatorBinding, typeManager, package, builderLogger);
                var library = CreateLibraryResource(elmFile, cqlFile, assembly, typeCrosswalk, builder, canon, package);
                libraries.Add(package.NameAndVersion, library);
            }
            var resources = new List<Resource>();
            resources.AddRange(libraries.Values);

            var tupleAssembly = assemblies["TupleTypes"];
            var assemblyBytes = File.ReadAllBytes(tupleAssembly.Location.FullName);
            var asm = Assembly.Load(assemblyBytes);
            var assemblyBase64 = Convert.ToBase64String(assemblyBytes);

            var tuplesBinary = new Binary
            {
                id = "TupleTypes-Binary",
                contentType = "application/octet-stream",
                data = new Base64BinaryElement { value = assemblyBase64 },
            };
            resources.Add(tuplesBinary);
            foreach (var sourceKvp in tupleAssembly.SourceCode)
            {
                var tuplesSourceBytes = Encoding.UTF8.GetBytes(sourceKvp.Value);
                var tuplesSourceBase64 = Convert.ToBase64String(tuplesSourceBytes);
                var tuplesCSharp = new Binary
                {
                    id = sourceKvp.Key,
                    contentType = "text/plain",
                    data = new Base64BinaryElement { value = tuplesSourceBase64 },
                };
                resources.Add(tuplesCSharp);
            }

            foreach (var package in elmPackages)
            {
                var elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{package.NameAndVersion}.json"));
                foreach (var def in package.library?.statements?.def ?? Enumerable.Empty<Hl7.Cql.Elm.Expressions.DefinitionExpression>())
                {
                    if (def.annotation != null)
                    {
                        var tags = new List<Tag>();
                        foreach (var a in def.annotation)
                        {
                            if (a.t != null)
                            {
                                foreach (var t in a.t)
                                {
                                    if (t != null)
                                        tags.Add(t);
                                }
                            }
                        }
                        var measureAnnotation = tags
                            .SingleOrDefault(t => t?.name == "measure");
                        var yearAnnotation = tags
                            .SingleOrDefault(t => t?.name == "year");
                        if (measureAnnotation != null
                            && !string.IsNullOrWhiteSpace(measureAnnotation.value)
                            && yearAnnotation != null
                            && !string.IsNullOrWhiteSpace(yearAnnotation.value)
                            && int.TryParse(yearAnnotation.value, out var measureYear))
                        {
                            var measure = new Measure();
                            measure.name = measureAnnotation.value;
                            measure.id = package.library?.identifier?.id!;
                            measure.version = package.library?.identifier?.version!;
                            measure.status = "active"!;
                            measure.date = new DateTimeIso8601(elmFile.LastWriteTimeUtc, DateTimePrecision.Millisecond)!;
                            measure.effectivePeriod = new Period
                            {
                                start = new DateTimeElement
                                {
                                    value = new DateTimeIso8601(measureYear, 1, 1, 0, 0, 0, 0, 0, 0)
                                },
                                end = new DateTimeElement
                                {
                                    value = new DateTimeIso8601(measureYear, 12, 31, 23, 59, 59, 999, 0, 0)
                                }
                            };
                            measure.group = new List<Measure.GroupComponent>();
                            measure.url = canon(measure)!;
                            if (!libraries.TryGetValue(package.NameAndVersion, out var libForMeasure) || libForMeasure is null)
                                throw new InvalidOperationException($"We didn't create a measure for library {libForMeasure}");
                            measure.library = new List<CanonicalElement> { libForMeasure!.url.value! };
                            resources.Add(measure);
                        }
                    }
                }
            }

            return resources;
        }

        private static readonly Dictionary<string, string> Populations = new Dictionary<string, string>
        {
            { "initial-population", "Initial Population" },
            { "numerator", "Numerator" },
            { "denominator", "Denominator" },
            { "denominator-exclusion", "Denominator Exclusion" }
        };

        private void DiscoverPopulations(Measure measure, ElmPackage package)
        {
            var defs = package.library?.statements?.def ?? Enumerable.Empty<Hl7.Cql.Elm.Expressions.DefinitionExpression>();
            foreach (var def in defs)
            {
                var annotations = (def.annotation?
                    .SelectMany(a => a.t ?? Enumerable.Empty<Tag>())
                    ?? Enumerable.Empty<Tag>())
                    .ToArray();
                if (annotations.Length > 0)
                {
                    var groups = annotations
                        .Where(t => t.name == "group")
                        .ToArray();
                    var populations = annotations
                        .Where(t => t.name == "population")
                        .ToArray();
                    var tuples = from g in groups
                                 from p in populations
                                 select new { Group = g.value, Population = p.value };
                    foreach (var tuple in tuples)
                    {
                        if (!int.TryParse(tuple.Group, out var groupNumber))
                            throw new InvalidOperationException($"Definition {def.name} has a @group annotation whose value is {tuple.Group}.  Groups must be positive integers.");
                        if (!Populations.ContainsKey(tuple.Population))
                            throw new InvalidOperationException($"Definition {def.name} has a @population annotation whose value is {tuple.Population}.  @population must be one of: {string.Join(", ", Populations.Keys)}");

                        var rate = $"rate-{tuple.Group}";
                        var groupsForRate = measure.group?
                            .Where(g => g.id == rate)
                            .ToArray() ?? new Measure.GroupComponent[] { };
                        Measure.GroupComponent? group;
                        if (groupsForRate.Length == 1)
                        {
                            group = groupsForRate[0];
                        }
                        else if (groupsForRate.Length == 0)
                        {
                            group = new Measure.GroupComponent
                            {
                                id = rate,
                                code = CodeableConcept((rate, Constants.MeasureGroupCodeSystem)),
                                description = $"Rate {tuple.Group}",
                                population = new List<Measure.GroupComponent.PopulationComponent>()
                            };
                            measure.group!.Add(group);
                        }
                        else throw new InvalidOperationException($"Rate {rate} is defined twice for this measure.");

                        var pop = $"{rate}-{tuple.Population}";
                        var populationsForGroup = group.population
                            .Where(p => p.id == pop)
                            .ToArray();
                        Measure.GroupComponent.PopulationComponent? population;
                        if (populationsForGroup.Length == 1)
                        {
                            population = populationsForGroup[0];
                        }
                        else if (populationsForGroup.Length == 0)
                        {
                            population = new Measure.GroupComponent.PopulationComponent
                            {
                                id = pop,
                                code = CodeableConcept((tuple.Population, Constants.MeasureGroupCodeSystem)),
                                description = Populations[tuple.Population],
                                criteria = new Hl7.Cql.Poco.Fhir.R4.Model.Expression
                                {
                                    language = "text/cql-identifier"!,
                                    expression = def.name
                                }
                            };
                            group.population.Add(population);
                        }
                        else throw new InvalidOperationException($"Population {pop} is defined twice for this measure.");
                    }
                }
            }
        }

        private CodeableConcept CodeableConcept(params (string code, string system)[] tuples) =>
            new CodeableConcept
            {
                coding = tuples
                    .Select(t => new Coding { code = t.code!, system = t.system! })
                    .ToList(),
            };


        private IDictionary<string, AssemblyData> Generate(DefinitionDictionary<LambdaExpression> expressions,
            TypeManager typeManager,
            DirectedGraph dependencies,
            CSharpSourceCodeWriter writer,
            Dictionary<string, Stream> navToLibraryStream,
            IEnumerable<Assembly> references)
        {
            Stream getStreamForLibrary(string nav)
            {
                if (!navToLibraryStream.TryGetValue(nav, out var stream))
                {
                    stream = new MemoryStream();
                    navToLibraryStream.Add(nav, stream);
                }
                return stream;
            }
            writer.Write(expressions,
                typeManager.TupleTypes,
                dependencies,
                getStreamForLibrary,
                closeStream: false);

            var assemblies = new Dictionary<string, AssemblyData>();
            var tupleStreams = navToLibraryStream
                .Where(kvp => kvp.Key.StartsWith("Tuples\\"));
            var tupleAssembly = CompileTuples(tupleStreams, references);
            var additionalReferences = new[]
            {
                tupleAssembly
            };
            assemblies.Add("TupleTypes", tupleAssembly);
            var buildOrder = DetermineBuildOrder(dependencies);
            foreach (var node in buildOrder)
            {
                if (!navToLibraryStream.TryGetValue(node.NodeId, out var sourceCodeStream))
                    throw new InvalidOperationException($"Library {node.NodeId} doesn't exist in the source code dictionary.");
                CompileNode(sourceCodeStream, assemblies, node, references, additionalReferences);
            }
            return assemblies;
        }

        private AssemblyData CompileTuples(IEnumerable<KeyValuePair<string, Stream>> tupleStreams,
            IEnumerable<Assembly> assemblyReferences,
            DirectoryInfo? csOutput = null)
        {
            var metadataReferences = new List<MetadataReference>();
            AddNetCoreReferences(metadataReferences);
            foreach (var asm in assemblyReferences)
            {
                metadataReferences.Add(MetadataReference.CreateFromFile(asm.Location));
            }
            var compilation = CSharpCompilation.Create($"Tuples")
                .WithOptions(new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release))
                .WithReferences(metadataReferences);

            var sources = new Dictionary<string, string>();

            foreach (var kvp in tupleStreams)
            {
                var sourceCodeStream = kvp.Value;
                sourceCodeStream.Flush();
                sourceCodeStream.Seek(0, SeekOrigin.Begin);
                var reader = new StreamReader(sourceCodeStream);
                var sourceCode = reader.ReadToEnd().Trim();
                sources.Add(kvp.Key.Substring("Tuples\\".Length), sourceCode);
                var tree = SyntaxFactory.ParseSyntaxTree(sourceCode);

                compilation = compilation.AddSyntaxTrees(tree);
            }
            var codeStream = new MemoryStream();
            var compilationResult = compilation.Emit(codeStream);
            var errors = new List<Diagnostic>();
            var warnings = new List<Diagnostic>();
            if (!compilationResult.Success)
            {
                var sb = new StringBuilder();
                foreach (var diag in compilationResult.Diagnostics)
                {
                    switch (diag.Severity)
                    {
                        case DiagnosticSeverity.Warning:
                            warnings.Add(diag);
                            break;
                        case DiagnosticSeverity.Error:
                            errors.Add(diag);
                            break;
                        case DiagnosticSeverity.Hidden:
                        case DiagnosticSeverity.Info:
                        default:
                            break;
                    }
                    sb.AppendLine(diag.ToString());
                }
                var ex = new InvalidOperationException($"The following compilation errors were detected when compiling Tuples:{Environment.NewLine}{sb}");
                ex.Data["Errors"] = errors;
                ex.Data["Warnings"] = warnings;

                throw ex;
            }
            var bytes = codeStream.ToArray();
            var tempFile = new FileInfo(Path.Combine(Path.GetTempPath(), $"Tuples.dll"));
            File.WriteAllBytes(tempFile.FullName, bytes);
            var data = new AssemblyData(tempFile, sources);
            return data;
        }

        private void CompileNode(Stream sourceCodeStream,
            Dictionary<string, AssemblyData> assemblies,
            DirectedGraphNode node,
            IEnumerable<Assembly> assemblyReferences,
            IEnumerable<AssemblyData>? assemblyDataReferences)
        {
            sourceCodeStream.Flush();
            sourceCodeStream.Seek(0, SeekOrigin.Begin);
            var reader = new StreamReader(sourceCodeStream);
            var sourceCode = reader.ReadToEnd().Trim();
            var tree = SyntaxFactory.ParseSyntaxTree(sourceCode);
            var metadataReferences = new List<MetadataReference>();
            AddNetCoreReferences(metadataReferences);
            foreach (var asm in assemblyReferences)
            {
                metadataReferences.Add(MetadataReference.CreateFromFile(asm.Location));
            }
            foreach (var edge in node.ForwardEdges)
            {
                if (assemblies.TryGetValue(edge.ToId, out var referencedDll))
                {
                    metadataReferences.Add(MetadataReference.CreateFromFile(referencedDll.Location.FullName));
                }
            }
            if (assemblyDataReferences != null)
            {
                foreach (var @ref in assemblyDataReferences)
                {
                    metadataReferences.Add(MetadataReference.CreateFromFile(@ref.Location.FullName));
                }
            }
            var compilation = CSharpCompilation.Create($"{node.NodeId}")
                .WithOptions(new CSharpCompilationOptions(outputKind: OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Release))
                .WithReferences(metadataReferences)
                .AddSyntaxTrees(tree);
            var codeStream = new MemoryStream();
            var compilationResult = compilation.Emit(codeStream);
            var errors = new List<Diagnostic>();
            var warnings = new List<Diagnostic>();
            if (!compilationResult.Success)
            {
                var sb = new StringBuilder();
                foreach (var diag in compilationResult.Diagnostics)
                {
                    switch (diag.Severity)
                    {
                        case DiagnosticSeverity.Warning:
                            warnings.Add(diag);
                            break;
                        case DiagnosticSeverity.Error:
                            errors.Add(diag);
                            break;
                        case DiagnosticSeverity.Hidden:
                        case DiagnosticSeverity.Info:
                        default:
                            break;
                    }
                    sb.AppendLine(diag.ToString());
                }
                var ex = new InvalidOperationException($"The following compilation errors were detected when compiling {node.NodeId}:{Environment.NewLine}{sb}");
                ex.Data["Errors"] = errors;
                ex.Data["Warnings"] = warnings;

                throw ex;
            }
            var bytes = codeStream.ToArray();
            //var loadedAsm = Assembly.Load(bytes);
            var tempFile = new FileInfo(Path.Combine(Path.GetTempPath(), $"{node.NodeId}.dll"));
            File.WriteAllBytes(tempFile.FullName, bytes);
            var sources = new Dictionary<string, string> {
                { node.NodeId, sourceCode }
            };
            var data = new AssemblyData(tempFile, sources);
            assemblies.Add(node.NodeId, data);
        }

        private IList<DirectedGraphNode> DetermineBuildOrder(DirectedGraph minimalGraph)
        {
            var sorted = minimalGraph.TopologicalSort()
                .Where(n => n.NodeId != minimalGraph.StartNode.NodeId && n.NodeId != minimalGraph.EndNode.NodeId)
                .ToList();
            return sorted;
        }

        private void AddNetCoreReferences(List<MetadataReference> metadataReferences)
        {
            var rtPath = Path.GetDirectoryName(typeof(object).Assembly.Location);

            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Private.CoreLib.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Runtime.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Console.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "netstandard.dll")));

            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Text.RegularExpressions.dll"))); // IMPORTANT!
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Linq.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Linq.Expressions.dll"))); // IMPORTANT!

            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.IO.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Net.Primitives.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Net.Http.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Private.Uri.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Reflection.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.ComponentModel.Primitives.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Globalization.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Collections.Concurrent.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Collections.NonGeneric.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "Microsoft.CSharp.dll")));

            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Diagnostics.Tools.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Diagnostics.Debug.dll")));
            metadataReferences.Add(MetadataReference.CreateFromFile(Path.Combine(rtPath, "System.Collections.dll")));
        }

        private void MergeGraphInfo(DirectedGraph graph, DirectedGraph into)
        {
            foreach (var sourceNode in graph.Nodes)
            {
                if (!into.Nodes.ContainsKey(sourceNode.Key))
                    into.Add(sourceNode.Value);
            }
            foreach (var edge in graph.Edges)
            {
                if (!into.Edges.ContainsKey(edge.Key))
                    into.Add(edge.Value);
            }
            var orphaned = true;
            foreach (var edge in into.Edges)
            {
                if (edge.Value.ToId == graph.StartNode.NodeId)
                {
                    orphaned = false;
                    break;
                }
            }
            if (orphaned)
            {
                into.Add(new DirectedGraphEdge(into.StartNode.NodeId, graph.StartNode.NodeId));
            }
        }

        private Hl7.Cql.Poco.Fhir.R4.Model.Library CreateLibraryResource(FileInfo elmFile,
            FileInfo? cqlFile,
            AssemblyData? assembly,
            FhirCqlCrosswalk typeCrosswalk,
            ExpressionBuilder builder,
            Func<Resource, string> canon,
            ElmPackage? libPackage = null)
        {
            if (elmFile.Exists)
            {
                if (libPackage is null)
                {
                    libPackage = ElmPackage.LoadFrom(elmFile);
                    if (libPackage is null)
                        throw new ArgumentException($"File at {elmFile.FullName} is not valid ELM");
                }
                var bytes = File.ReadAllBytes(elmFile.FullName);
                var base64 = Convert.ToBase64String(bytes);
                var attachment = new Attachment
                {
                    id = $"{libPackage.NameAndVersion}+elm",
                    contentType = new CodeElement { value = ElmPackage.JsonMimeType },
                    data = new Base64BinaryElement { value = base64 },
                };
                var library = new Hl7.Cql.Poco.Fhir.R4.Model.Library();
                library.content = new List<Attachment> { attachment };
                library.type = LogicLibraryCodeableConcept;
                string libraryId = $"{libPackage!.NameAndVersion}";
                library.id = libraryId!;
                library.version = libPackage!.library?.identifier?.version!;
                library.name = libPackage!.library?.identifier?.id!;
                library.status = new CodeElement { value = "active" };
                library.date = new DateTimeElement { value = new DateTimeIso8601(elmFile.LastWriteTimeUtc, DateTimePrecision.Millisecond) };
                var parameters = new List<ParameterDefinition>();
                var inParams = libPackage.library?.parameters?.def?
                    .Select(pd => ElmParameterToFhir(pd, typeCrosswalk, builder));
                if (inParams is not null)
                    parameters.AddRange(inParams);
                var outParams = libPackage.library?.statements?.def?
                    .Where(def => def.name != "Patient" && (def.operand is null || def.operand.Length == 0))
                    .Select(def => ElmDefinitionToParameter(def, typeCrosswalk));
                if (outParams is not null)
                    parameters.AddRange(outParams);
                var valueSetParameterDefinitions = new List<ParameterDefinition>();
                foreach (var valueSet in libPackage.library?.valueSets?.def ?? Enumerable.Empty<ValueSetDefinitionExpression>())
                {
                    var valueSetParameter = new ParameterDefinition
                    {
                        type = "ValueSet"!,
                        name = valueSet.id!,
                        use = "in"!,
                    };
                    valueSetParameterDefinitions.Add(valueSetParameter);
                }
                parameters.AddRange(valueSetParameterDefinitions);
                library.parameter = parameters.Count > 0 ? parameters : null!;

                if (cqlFile!.Exists)
                {
                    var cqlBytes = File.ReadAllBytes(cqlFile.FullName);
                    var cqlBase64 = Convert.ToBase64String(cqlBytes);

                    var cqlAttachment = new Attachment
                    {
                        id = $"{libPackage.NameAndVersion}+cql",
                        contentType = new CodeElement { value = "text/cql" },
                        data = new Base64BinaryElement { value = cqlBase64 },
                    };
                    library.content.Add(cqlAttachment);
                }
                if (assembly != null)
                {
                    var assemblyBytes = File.ReadAllBytes(assembly.Location.FullName);
                    var assemblyBase64 = Convert.ToBase64String(assemblyBytes);
                    var assemblyAttachment = new Attachment
                    {
                        id = $"{libPackage.NameAndVersion}+dll",
                        contentType = new CodeElement { value = "application/octet-stream" },
                        data = new Base64BinaryElement { value = assemblyBase64 },
                    };
                    library.content.Add(assemblyAttachment);
                    foreach (var kvp in assembly.SourceCode)
                    {
                        var sourceBytes = Encoding.UTF8.GetBytes(kvp.Value);
                        var sourceBase64 = Convert.ToBase64String(sourceBytes);
                        var sourceAttachment = new Attachment
                        {
                            id = $"{kvp.Key}+csharp",
                            contentType = new CodeElement { value = "text/plain" },
                            data = new Base64BinaryElement { value = sourceBase64 },
                        };
                        library.content.Add(sourceAttachment);
                    }

                }
                library.url = canon(library)!;
                return library;
            }
            else throw new ArgumentException($"Couldn't find library {elmFile.FullName}", nameof(elmFile));
        }
        private static readonly CodeableConcept LogicLibraryCodeableConcept = new CodeableConcept
        {
            coding = new List<Coding>
            {
                new Coding
                {
                    code = "logic-library"!,
                    system = "http://terminology.hl7.org/CodeSystem/library-type"!
                }
            }
        };
        private static RuntimeContext RuntimeContext => FhirRuntimeContext.Create();

        private ParameterDefinition ElmParameterToFhir(Hl7.Cql.Elm.Expressions.ParameterDeclarationExpression elmParameter,
            FhirCqlCrosswalk typeCrosswalk,
            ExpressionBuilder builder)
        {
            var typeSpecifier = elmParameter.resultTypeSpecifier ?? elmParameter.parameterTypeSpecifier;
            if (typeSpecifier is null)
                throw new ArgumentException($"{typeSpecifier} is missing on parameter: {elmParameter.name}", nameof(elmParameter));
            var type = typeCrosswalk.TypeEntryFor(typeSpecifier);
            if (type is null || type.FhirType is null)
                throw new ArgumentException($"Unable to identify a valid FHIR type for this parameter.", nameof(elmParameter));
            var typeCode = Enum.GetName(typeof(FhirAllTypes), type.FhirType);

            var cqlTypeExtensions = new List<Extension> {
                new Extension()
                {
                    id = Constants.ParameterCqlType,
                    value = new StringElement() { value = type.CqlType!.ToString() }
                }
            };

            if (type.ElementType is not null)
            {
                cqlTypeExtensions.Add(new Extension()
                {
                    id = Constants.ParameterCqlElementType,
                    valueString = new StringElement() { value = type.ElementType.CqlType!.ToString() }
                });
            }

            if (elmParameter.@default is not null)
            {
                var typeEntry = typeCrosswalk.TypeEntryFor(elmParameter.@default);
                var lambda = builder.Lambda(elmParameter.@default);
                var func = lambda.Compile();
                var value = func.DynamicInvoke(RuntimeContext);
                AddDefaultValueToExtensions(cqlTypeExtensions, value, typeEntry);
            }

            var annotations = (elmParameter.annotation?
                .SelectMany(a => a.t ?? Enumerable.Empty<Tag>())
                ?? Enumerable.Empty<Tag>())
                .ToArray();

            if (annotations.Length > 0)
            {
                var uris = annotations
                    .Where(t => t.name == "uri")
                    .ToArray();

                foreach (var uri in uris)
                {
                    cqlTypeExtensions.Add(new Extension()
                    {
                        id = Constants.ParameterCanonicalUri,
                        valueString = uri.value
                    });
                }
            }

            var parameterDefinition = new ParameterDefinition
            {
                name = elmParameter.name!,
                use = "in"!,
                min = elmParameter.@default is null ? 1 : 0,
                max = "1"!,
                type = typeCode!,
                extension = cqlTypeExtensions,
            };
            return parameterDefinition;
        }

        private static CqlDate ConvertExpressionToDate(Hl7.Cql.Elm.Expressions.ParameterDeclarationExpression elmParameter,
            Hl7.Cql.Elm.Expressions.DateExpression defaultDateValue)
        {
            var yearExpr = defaultDateValue.year ?? throw new InvalidOperationException($"Date expressions must have a year value on parameter: {elmParameter.name}");
            var monthExpr = defaultDateValue.month ?? throw new InvalidOperationException($"Date expressions must have a month value on parameter: {elmParameter.name}");
            var dayExpr = defaultDateValue.day ?? throw new InvalidOperationException($"Date expressions must have a day value on parameter: {elmParameter.name}");

            int year = ConvertExpressionToLiteral<int>(elmParameter, yearExpr, "year");
            int month = ConvertExpressionToLiteral<int>(elmParameter, monthExpr, "month");
            int day = ConvertExpressionToLiteral<int>(elmParameter, dayExpr, "day");

            var dateValue = new CqlDate(year, month, day);
            return dateValue;
        }

        private static T ConvertExpressionToLiteral<T>(Hl7.Cql.Elm.Expressions.ParameterDeclarationExpression elmParameter,
            Hl7.Cql.Elm.Expressions.Expression expr, string name)
        {
            T val;
            if (expr is Hl7.Cql.Elm.Expressions.LiteralExpression literal)
            {
                var (literalValue, _) = ExpressionBuilder.ConvertLiteral(literal, typeof(T));
                if (literalValue is T)
                {
                    val = (T)literalValue!;
                    if (val is null)
                    {
                        throw new InvalidOperationException($"Parameter '{elmParameter.name}' {name} value was null");
                    }
                }
                else throw new InvalidOperationException($"Parameter '{elmParameter.name}' could not convert {name} value to int literal");
            }
            else throw new InvalidOperationException($"Parameter '{elmParameter.name}' must have a literal '{name}' expresssion");
            return val;
        }


        private static void AddDefaultValueToExtensions(List<Extension> cqlTypeExtensions, object? value, TypeEntry? defaultType)
        {
            var defaultValueExt = new Extension()
            {
                id = Constants.ParameterCqlDefaultValue
            };
            MapValueToExtension(defaultValueExt, value, defaultType);
            cqlTypeExtensions.Add(defaultValueExt);

            var defaultTypeCode = Enum.GetName(typeof(FhirAllTypes), defaultType.FhirType);

            cqlTypeExtensions.Add(new Extension()
            {
                id = Constants.ParameterCqlDefaultValueType,
                valueString = new StringElement() { value = defaultTypeCode }
            });
        }

        private static void MapValueToExtension(Extension ext, object? value, TypeEntry mappedType)
        {
            switch (mappedType.FhirType)
            {
                case FhirAllTypes.Boolean:
                    ext.valueBoolean = new BooleanElement() { value = value as bool? };
                    break;
                case FhirAllTypes.Integer:
                    ext.valueInteger = new IntegerElement() { value = value as int? };
                    break;
                case FhirAllTypes.Decimal:
                    ext.valueDecimal = new DecimalElement() { value = value as decimal? };
                    break;
                case FhirAllTypes.String:
                    ext.valueString = new StringElement() { value = value as string };
                    break;
                case FhirAllTypes.Date:
                    ext.valueDate = FhirTypeConverter.Default.Convert<DateElement>(value);
                    break;
                case FhirAllTypes.DateTime:
                    ext.valueDateTime = FhirTypeConverter.Default.Convert<DateTimeElement>(value);
                    break;
                case FhirAllTypes.Time:
                    ext.valueTime = FhirTypeConverter.Default.Convert<TimeElement>(value);
                    break;
                case FhirAllTypes.Quantity:
                    ext.valueQuantity = FhirTypeConverter.Default.Convert<Quantity>(value);
                    break;
                case FhirAllTypes.Range:
                    ext.valueRange = FhirTypeConverter.Default.Convert<Hl7.Cql.Poco.Fhir.R4.Model.Range>(value);
                    break;
                case FhirAllTypes.Ratio:
                    ext.valueRatio = FhirTypeConverter.Default.Convert<Ratio>(value);
                    break;
                case FhirAllTypes.Period:
                    ext.valuePeriod = FhirTypeConverter.Default.Convert<Period>(value);
                    break;
                default:
                    throw new InvalidOperationException($"Type '{mappedType.FhirType}' is not supported for Extension mappings");
            }
        }

        private ParameterDefinition ElmDefinitionToParameter(Hl7.Cql.Elm.Expressions.DefinitionExpression definition,
            FhirCqlCrosswalk typeCrosswalk)
        {
            var resultTypeSpecifier = definition.resultTypeSpecifier;
            if (resultTypeSpecifier is null && !string.IsNullOrWhiteSpace(definition.resultTypeName))
                resultTypeSpecifier = new Hl7.Cql.Elm.Expressions.TypeSpecifierExpression
                {
                    type = "NamedTypeSpecifier",
                    name = definition.resultTypeName
                };
            var type = typeCrosswalk.TypeEntryFor(resultTypeSpecifier);
            if (type is null || type.FhirType is null)
                throw new ArgumentException($"Unable to identify a valid FHIR type for this definition.", nameof(definition));
            var typeCode = Enum.GetName(typeof(FhirAllTypes), type.FhirType);
            var parameterDefinition = new ParameterDefinition
            {
                name = new CodeElement { value = definition.name! },
                use = new CodeElement { value = "out" },
                min = new IntegerElement { value = 0 },
                max = new StringElement { value = "1" },
                type = typeCode!,
            };
            if (type.ElementType is not null && type.ElementType.FhirType is not null)
            {
                var elementTypeCode = Enum.GetName(typeof(FhirAllTypes), type.ElementType.FhirType);
                parameterDefinition.extension = new List<Extension>()
                {
                    new Extension
                    {
                       valueCode = elementTypeCode!,
                       url = Constants.ParameterElementTypeExtensionUri
                    }
                };
            }

            if (!string.IsNullOrWhiteSpace(definition.accessLevel) && definition.accessLevel.Equals("private", StringComparison.CurrentCultureIgnoreCase))
            {
                if (parameterDefinition.extension == null)
                {
                    parameterDefinition.extension = new List<Extension>();
                }

                parameterDefinition.extension.Add(new Extension
                {
                    valueCode = definition.accessLevel.ToLower(),
                    url = Constants.ParameterAccessLevel,
                });
            }

            return parameterDefinition;
        }


        static void EnsureDirectory(DirectoryInfo directory, int timeoutMs = 5000)
        {
            var now = DateTime.Now;
            var loop = true;
            var timeout = TimeSpan.FromMilliseconds(timeoutMs);
            while (!directory.Exists && loop)
            {
                directory.Create();
                directory.Refresh();
                if (DateTime.Now.Subtract(now) > timeout)
                    throw new InvalidOperationException($"Unable to create directory {directory.FullName} after {timeout}");
            }
        }

    }
}
