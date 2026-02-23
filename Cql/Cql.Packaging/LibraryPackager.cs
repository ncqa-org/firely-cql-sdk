#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
/*
 * Copyright (c) 2023, NCQA and contributors
 * See the file CONTRIBUTORS for details.
 *
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/FirelyTeam/firely-cql-sdk/main/LICENSE
 */

using Hl7.Cql.Abstractions;
using Hl7.Cql.CodeGeneration.NET;
using Hl7.Cql.Compiler;
using Hl7.Cql.Conversion;
using Hl7.Cql.Elm;
using Hl7.Cql.Fhir;
using Hl7.Cql.Graph;
using Hl7.Cql.Iso8601;
using Hl7.Cql.Primitives;
using Hl7.Cql.Runtime;
using Hl7.Fhir.Model;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Runtime.Loader;
using System.Text;
using AccessModifier = Hl7.Cql.Elm.AccessModifier;
using FhirModelCode = Hl7.Fhir.Model.Code;
using Elm = Hl7.Cql.Elm;
using Library = Hl7.Fhir.Model.Library;

namespace Hl7.Cql.Packaging
{
    public class LibraryPackager
    {
        internal LibraryPackager()
        {
            FhirTypeConverter.InitializeCache(0);
            TypeConverter = FhirTypeConverter.Default;
        }

        internal LibraryPackager(TypeConverter? typeConverter)
        {
            FhirTypeConverter.InitializeCache(0);
            TypeConverter = typeConverter ?? FhirTypeConverter.Default;
        }

        public static IDictionary<string, Elm.Library> LoadLibraries(DirectoryInfo elmDir)
        {
            var dict = new ConcurrentDictionary<string, Elm.Library>();
            var files = elmDir.GetFiles("*.json", SearchOption.AllDirectories);
            Parallel.ForEach(files, file =>
            {
                var library = Elm.Library.LoadFromJson(file);
                if (library?.NameAndVersion != null)
                {
                    dict.TryAdd(library.NameAndVersion, library);
                }
            });
            return dict;
        }

        public static AssemblyLoadContext LoadResources(DirectoryInfo dir, string lib, string version)
        {
            var libFile = new FileInfo(Path.Combine(dir.FullName, $"{lib}-{version}.json"));
            using var fs = libFile.OpenRead();
            var library = fs.ParseFhir<Library>();
            var dependencies = library.GetDependencies(dir);
            var allLibs = dependencies.AllLibraries();
            var asmContext = new AssemblyLoadContext($"{lib}-{version}");
            allLibs.LoadAssemblies(asmContext);

            var tupleTypes = new FileInfo(Path.Combine(dir.FullName, "TupleTypes-Binary.json"));
            using var tupleFs = tupleTypes.OpenRead();
            var binaries = new[]
            {
                tupleFs.ParseFhir<Binary>()
            };

            binaries.LoadAssembles(asmContext);
            return asmContext;
        }

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public static AssemblyLoadContext LoadElm(DirectoryInfo elmDirectory,
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
          string lib,
          string version,
          LogLevel logLevel = LogLevel.Error,
          int cacheSize = 0)
        {
            var logFactory = LoggerFactory
                          .Create(logging =>
                          {
                              logging.AddFilter(level => level >= logLevel);
                              logging.AddConsole(console =>
                              {
                                  console.LogToStandardErrorThreshold = LogLevel.Error;
                              });
                          });

            return LoadElm(elmDirectory, lib, version, logFactory, cacheSize);
        }

        public static AssemblyLoadContext LoadElm(DirectoryInfo elmDirectory,
            string lib,
            string version,
            ILoggerFactory logFactory,
            int cacheSize)
        {
            var elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{lib}-{version}.json"));
            if (!elmFile.Exists)
                elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{lib}.json"));
            if (!elmFile.Exists)
                throw new ArgumentException($"Cannot find a matching ELM file for {lib} version {version} in {elmDirectory.FullName}", nameof(lib));
            var library = Elm.Library.LoadFromJson(elmFile)
                ?? throw new InvalidOperationException($"File {elmFile.FullName} is not a valid ELM package.");
            var dependencies = Elm.Library
                .GetIncludedLibraries(library, elmDirectory)
                .Packages()
                .ToArray();

            var typeResolver = new FhirTypeResolver(ModelInfo.ModelInspector);
            FhirTypeConverter.InitializeCache(cacheSize);
            var typeConverter = FhirTypeConverter.Default;
            var typeManager = new TypeManager(typeResolver);
            var operatorBinding = new CqlOperatorsBinding(typeResolver, typeConverter);
            var compiler = new AssemblyCompiler(typeResolver, typeManager, operatorBinding);

            var assemblyData = compiler.Compile(dependencies,
                logFactory);

            var asmContext = new AssemblyLoadContext($"{lib}-{version}");
            foreach (var kvp in assemblyData)
            {
                var assemblyBytes = kvp.Value.Binary;
                using var ms = new MemoryStream(assemblyBytes);
                asmContext.LoadFromStream(ms);
            }
            return asmContext;
        }

        internal IEnumerable<Resource> PackageResources(DirectoryInfo elmDirectory,
            DirectoryInfo cqlDirectory,
            DirectedGraph packageGraph,
            TypeResolver typeResolver,
            OperatorBinding operatorBinding,
            TypeManager typeManager,
            Func<string, string, string> canon,
            ILoggerFactory logFactory)
        {
            var builderLogger = logFactory.CreateLogger<ExpressionBuilder>();
            var codeWriterLogger = logFactory.CreateLogger<CSharpSourceCodeWriter>();

            var elmLibraries = packageGraph.Nodes.Values
                .Select(node => node.Properties?[Elm.Library.LibraryNodeProperty] as Elm.Library)
                .OfType<Elm.Library>()
                // Processing this deterministically to reduce different exceptions when running this repeatedly
                .OrderBy(lib => lib.NameAndVersion)
                .ToArray();

            var all = new DefinitionDictionary<LambdaExpression>();
            foreach (var library in elmLibraries)
            {
                builderLogger.LogInformation($"Building expressions for {library.NameAndVersion}");
                var builder = new ExpressionBuilder(operatorBinding, typeManager, library!, builderLogger, new(false));
                var expressions = builder.Build();
                all.Merge(expressions);
            }
            var scw = new CSharpSourceCodeWriter(codeWriterLogger);
            foreach (var @using in typeResolver.ModelNamespaces)
                scw.Usings.Add(@using);
            foreach (var alias in typeResolver.Aliases)
                scw.AliasedUsings.Add(alias);

            var navToLibraryStream = new Dictionary<string, Stream>();
            var compiler = new AssemblyCompiler(typeResolver, typeManager, operatorBinding);
            var assemblies = compiler.Compile(elmLibraries, logFactory);
            var libraries = new Dictionary<string, Library>();
            var typeCrosswalk = new CqlTypeToFhirTypeMapper(typeResolver);
            foreach (var library in elmLibraries)
            {
                var elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{library.NameAndVersion}.json"));
                if (!elmFile.Exists)
                    elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{library.identifier?.id ?? string.Empty}.json"));
                if (!elmFile.Exists)
                    throw new InvalidOperationException($"Cannot find ELM file for {library.NameAndVersion}");
                var cqlFiles = cqlDirectory.GetFiles($"{library.NameAndVersion}.cql", SearchOption.AllDirectories);
                if (cqlFiles.Length == 0)
                {
                    cqlFiles = cqlDirectory.GetFiles($"{library.identifier!.id}.cql", SearchOption.AllDirectories);
                    if (cqlFiles.Length == 0)
                        throw new InvalidOperationException($"{library.identifier!.id}.cql");
                }
                if (cqlFiles.Length > 1)
                    throw new InvalidOperationException($"More than 1 CQL file found.");
                var cqlFile = cqlFiles[0];
                if (library.NameAndVersion is null)
                    throw new InvalidOperationException("Library NameAndVersion should not be null.");
                if (!assemblies.TryGetValue(library.NameAndVersion, out var assembly))
                    throw new InvalidOperationException($"No assembly for {library.NameAndVersion}");
                var builder = new ExpressionBuilder(operatorBinding, typeManager, library, builderLogger, new(false));
                var fhirLibrary = createLibraryResource(elmFile, cqlFile, assembly, typeCrosswalk, canon, library, elmLibraries);
                libraries.Add(library.NameAndVersion, fhirLibrary);
            }

            var resources = new List<Resource>();
            var resourceDataValues = libraries.Values.Select(library =>
                {
                    library.Id = library.Id?.Replace('_', '-');
                    return library;
                }
            );

            resources.AddRange(resourceDataValues);

            //if (assemblies.TryGetValue("ICqlMeasure", out var cqlMeasureInterface))
            //{
            //    foreach (var sourceKvp in cqlMeasureInterface.SourceCode)
            //    {
            //        var icqlSourceBytes = Encoding.UTF8.GetBytes(sourceKvp.Value);

            //        var cqlMeasureBinary = new Binary
            //        {
            //            Id = "ICqlMeasure",
            //            ContentType = "text/plain",
            //            Data = icqlSourceBytes,
            //        };

            //        resources.Add(cqlMeasureBinary);
            //    }

            //}

            var tupleAssembly = assemblies["TupleTypes"];

            var tuplesBinary = new Binary
            {
                Id = "TupleTypes-Binary",
                ContentType = "application/octet-stream",
                Data = tupleAssembly.Binary,
            };
            resources.Add(tuplesBinary);
            foreach (var sourceKvp in tupleAssembly.SourceCode)
            {
                var tuplesSourceBytes = Encoding.UTF8.GetBytes(sourceKvp.Value);
                var tuplesCSharp = new Binary
                {
                    Id = sourceKvp.Key.Replace("_", "-"),
                    ContentType = "text/plain",
                    Data = tuplesSourceBytes,
                };
                resources.Add(tuplesCSharp);
            }

            foreach (var library in elmLibraries)
            {
                var elmFile = new FileInfo(Path.Combine(elmDirectory.FullName, $"{library.NameAndVersion}.json"));
                foreach (var def in library.statements ?? Enumerable.Empty<Hl7.Cql.Elm.ExpressionDef>())
                {
                    if (def.annotation != null)
                    {
                        var tags = new List<Tag>();
                        foreach (var a in def.annotation.OfType<Elm.Annotation>())
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
                            measure.Name = library.identifier?.id!;
                            measure.Title = measureAnnotation.value;
                            measure.Id = library.identifier?.id!.Replace('_', '-');
                            measure.Version = library.identifier?.version!;
                            measure.Status = PublicationStatus.Active;
                            measure.Date = new DateTimeIso8601(elmFile.LastWriteTimeUtc, Iso8601.DateTimePrecision.Millisecond)
                                .ToString();
                            measure.EffectivePeriod = new Period
                            {
                                Start = new DateTimeIso8601(measureYear, 1, 1, 0, 0, 0, 0, 0, 0).ToString(),
                                End = new DateTimeIso8601(measureYear, 12, 31, 23, 59, 59, 999, 0, 0).ToString(),
                            };
                            measure.Group = new List<Measure.GroupComponent>();
                            measure.Url = canon(measure.Id!, measure.TypeName);
                            if (library.NameAndVersion is null)
                                throw new InvalidOperationException("Library NameAndVersion should not be null.");
                            if (!libraries.TryGetValue(library.NameAndVersion, out var libForMeasure) || libForMeasure is null)
                                throw new InvalidOperationException($"We didn't create a measure for library {libForMeasure}");
                            measure.Library = new List<string> { $"{libForMeasure!.Url}|{libForMeasure!.Version}" };
                            AnnotateMeasurePopulations(measure, library);
                            resources.Add(measure);
                        }
                    }
                }
            }

            return resources;
        }

        private static readonly Dictionary<string, string> Populations = new()
        {
        { "initial-population", "Initial Population" },
        { "numerator", "Numerator" },
        { "denominator", "Denominator" },
        { "denominator-exclusion", "Denominator Exclusion" },
        { "initial-population-commercial", "Initial Population Commercial" },
        { "initial-population-exchange", "Initial Population Exchange" },
        { "initial-population-medicare", "Initial Population Medicare" },
        { "initial-population-medicaid", "Initial Population Medicaid" },
        { "denominator-commercial", "Denominator Commercial" },
        { "denominator-exchange", "Denominator Exchange" },
        { "denominator-medicare", "Denominator Medicare" },
        { "denominator-medicaid", "Denominator Medicaid" },
        { "denominator-exclusion-commercial", "Denominator Exclusion Commercial" },
        { "denominator-exclusion-exchange", "Denominator Exclusion Exchange" },
        { "denominator-exclusion-medicare", "Denominator Exclusion Medicare" },
        { "denominator-exclusion-medicaid", "Denominator Exclusion Medicaid" },
        { "numerator-commercial", "Numerator Commercial" },
        { "numerator-exchange", "Numerator Exchange" },
        { "numerator-medicare", "Numerator Medicare" },
        { "numerator-medicaid", "Numerator Medicaid" }
        };
        private static void AnnotateMeasurePopulations(Measure measure, Elm.Library library)
        {
            var defs = library.statements ?? Enumerable.Empty<Hl7.Cql.Elm.ExpressionDef>();
            foreach (var def in defs)
            {
                var annotations = (def.annotation?
                                      .OfType<Elm.Annotation>()
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
                    var productLine = annotations
                        .FirstOrDefault(t => t.name == "productline");

                    var tuples = from g in groups
                                 from p in populations
                                 select new { Group = g.value, Population = p.value };
                    foreach (var tuple in tuples)
                    {
                        if (!Populations.ContainsKey(tuple.Population))
                            throw new InvalidOperationException(
                                $"Definition {def.name} has a @population annotation whose value is {tuple.Population}.  @population must be one of: {string.Join(", ", Populations.Keys)}");

                        var rate = $"{tuple.Group}";
                        var groupsForRate = measure.Group?
                                                   .Where(g => g.ElementId == rate)
                                                   .ToArray() ?? new Measure.GroupComponent[0];
                        Measure.GroupComponent? group;
                        if (groupsForRate.Length == 1)
                        {
                            group = groupsForRate[0];
                        }
                        else if (groupsForRate.Length == 0)
                        {
                            group = new Measure.GroupComponent
                            {
                                ElementId = rate,
                                //Code = new CodeableConcept(rate, MeasureGroupCodeSystem),
                                Description = $"Rate {tuple.Group}",
                            };
                            measure.Group!.Add(group);
                        }
                        else throw new InvalidOperationException($"Rate {rate} is defined twice for this measure.");

                        var populationSuffix = productLine != null ? $"{tuple.Population}-{productLine.value}" : tuple.Population;
                        var pop = $"{populationSuffix}";
                        var populationsForGroup = group.Population
                                                       .Where(p => p.ElementId == pop)
                                                       .ToArray();
                        Measure.PopulationComponent? population;
                        if (populationsForGroup.Length == 1)
                        {
                            population = populationsForGroup[0];
                        }
                        else if (populationsForGroup.Length == 0)
                        {
                            population = new Measure.PopulationComponent
                            {
                                ElementId = pop,
                                Code = new CodeableConcept
                                {
                                    Coding = new List<Coding>
                                    {
                                        new Coding
                                        {
                                            System = "http://terminology.hl7.org/CodeSystem/measure-population",
                                            Code = populationSuffix,
                                            Display = Populations[populationSuffix]
                                        }
                                    }
                                },
                                Description = Populations[tuple.Population],
                                Criteria = new Hl7.Fhir.Model.Expression
                                {
                                    Language = "text/cql-identifier",
                                    ExpressionElement = new FhirString(def.name)
                                }
                            };
                            group.Population.Add(population);
                        }
                        else throw new InvalidOperationException($"Population {pop} is defined twice for this measure.");
                    }
                }
            }
        }

        private Hl7.Fhir.Model.Library createLibraryResource(FileInfo elmFile,
            FileInfo? cqlFile,
            AssemblyData assembly,
            CqlTypeToFhirTypeMapper typeCrosswalk,
            Func<string, string, string> canon,
            Elm.Library? elmLibrary = null,
            Elm.Library[]? elmLibraries = null)
        {
            if (elmFile.Exists)
            {
                if (elmLibrary is null)
                {
                    elmLibrary = Elm.Library.LoadFromJson(elmFile);
                    if (elmLibrary is null)
                        throw new ArgumentException($"File at {elmFile.FullName} is not valid ELM");
                }
                var bytes = File.ReadAllBytes(elmFile.FullName);
                var attachment = new Attachment
                {
                    ElementId = $"{elmLibrary.NameAndVersion}+elm",
                    ContentType = Elm.Library.JsonMimeType,
                    Data = bytes,
                };
                var library = new Hl7.Fhir.Model.Library();
                library.Content.Add(attachment);
                library.Type = LogicLibraryCodeableConcept;
                string libraryId = $"{elmLibrary!.NameAndVersion}"!.Replace('_', '-');
                library.Id = libraryId!;
                library.Version = elmLibrary!.identifier?.version!;
                library.Name = elmLibrary!.identifier?.id!;
                library.Status = PublicationStatus.Active;
                library.Date = new DateTimeIso8601(elmFile.LastWriteTimeUtc, Iso8601.DateTimePrecision.Millisecond).ToString();
                if (elmLibrary.contexts?.Any(context => nameof(ResourceType.Patient).Equals(context?.name)) ?? false)
                {
                    library.Subject = new CodeableConcept("http://hl7.org/fhir/resource-types", nameof(ResourceType.Patient));
                }
                var parameters = new List<ParameterDefinition>();
                var inParams = elmLibrary.parameters?
                    .Select(pd => ElmParameterToFhir(pd, typeCrosswalk));
                if (inParams is not null)
                    parameters.AddRange(inParams);
                var outParams = elmLibrary.statements?
                    .Where(def => def.name != "Patient" && def is not FunctionDef)
                    .Select(def => ElmDefinitionToParameter(def, typeCrosswalk));
                if (outParams is not null)
                    parameters.AddRange(outParams);
                //var valueSetParameterDefinitions = new List<ParameterDefinition>();
                //foreach (var valueSet in elmLibrary.valueSets ?? Enumerable.Empty<Elm.ValueSetDef>())
                //{
                //    var valueSetParameter = new ParameterDefinition
                //    {
                //        Type = FHIRAllTypes.ValueSet,
                //        Name = valueSet.id!,
                //        Use = OperationParameterUse.In,
                //    };
                //    valueSetParameterDefinitions.Add(valueSetParameter);
                //}
                //parameters.AddRange(valueSetParameterDefinitions);
                library.Parameter = parameters.Count > 0 ? parameters : null!;

                List<RelatedArtifact> result = new List<RelatedArtifact>();
                foreach (var include in elmLibrary?.includes ?? Enumerable.Empty<Elm.IncludeDef>())
                {
                    var includeId = $"{include.path}|{include.version}";
                    var ra = new RelatedArtifact
                    {
                        Display = $"Library {include.path}",
                        Type = RelatedArtifact.RelatedArtifactType.DependsOn,
                        Resource = canon(includeId.Replace("_","-"), "Library"),
                    };
                    if (!result.Any(r => r.IsExactly(ra)))
                        result.Add(ra);
                }
                foreach (ValueSetDef include in elmLibrary?.valueSets ?? Enumerable.Empty<Elm.ValueSetDef>())
                {
                    var ra = new RelatedArtifact
                    {
                        Display = $"{FHIRAllTypes.ValueSet} {include.name}",
                        Type = RelatedArtifact.RelatedArtifactType.DependsOn,
                        Resource = include.id,
                    };
                    if (!result.Any(r => r.IsExactly(ra)))
                        result.Add(ra);
                }
                var elmDir = elmFile.Directory;
                foreach (var include in elmLibrary?.includes ?? Enumerable.Empty<Elm.IncludeDef>())
                {
                    var childElmLibrary = elmLibraries?.FirstOrDefault(lib => lib.NameAndVersion == $"{include.path}-{include.version}");
                    //if (elmDir == null) continue;
                    //// Try to find the included library file by path and version
                    //string childFileName = !string.IsNullOrEmpty(include.version)
                    //    ? $"{include.path}-{include.version}.json"
                    //    : $"{include.path}.json";
                    //var childElmFile = new FileInfo(Path.Combine(elmDir.FullName, childFileName));
                    //if (!childElmFile.Exists)
                    //{
                    //    // Try fallback to just path.json if not found
                    //    childElmFile = new FileInfo(Path.Combine(elmDir.FullName, $"{include.path}.json"));
                    //}
                    if (childElmLibrary is not null)
                    {
                        //var childElmLibrary = Elm.Library.LoadFromJson(childElmFile);
                        if (childElmLibrary != null)
                        {
                            // Add RelatedArtifacts from child
                            foreach (var childInclude in childElmLibrary.includes ?? Enumerable.Empty<Elm.IncludeDef>())
                            {
                                var childIncludeId = $"{childInclude.path}|{childInclude.version}";
                                var childRa = new RelatedArtifact
                                {
                                    Display = $"Library {childInclude.path}",
                                    Type = RelatedArtifact.RelatedArtifactType.DependsOn,
                                    Resource = canon(childIncludeId.Replace("_", "-"), "Library"),
                                };
                                if (!result.Any(r => r.IsExactly(childRa)))
                                    result.Add(childRa);
                            }
                            foreach (ValueSetDef childVs in childElmLibrary.valueSets ?? Enumerable.Empty<Elm.ValueSetDef>())
                            {
                                var childRa = new RelatedArtifact
                                {
                                    Display = $"{FHIRAllTypes.ValueSet} {childVs.name}",
                                    Type = RelatedArtifact.RelatedArtifactType.DependsOn,
                                    Resource = childVs.id,
                                };
                                if (!result.Any(r => r.IsExactly(childRa)))
                                    result.Add(childRa);
                            }
                        }
                    }
                }
                library.RelatedArtifact.AddRange(result);
                library.RelatedArtifact.Sort((x, y) => string.Compare(x.Display, y.Display ?? "", StringComparison.Ordinal));

                if (cqlFile!.Exists)
                {
                    var cqlBytes = File.ReadAllBytes(cqlFile.FullName);

                    var cqlAttachment = new Attachment
                    {
                        ElementId = $"{elmLibrary!.NameAndVersion}+cql",
                        ContentType = "text/cql",
                        Data = cqlBytes,
                    };
                    library.Content.Add(cqlAttachment);
                }
                if (assembly != null)
                {
                    var assemblyBytes = assembly.Binary;
                    var assemblyAttachment = new Attachment
                    {
                        ElementId = $"{elmLibrary!.NameAndVersion}+dll",
                        ContentType = "application/octet-stream",
                        Data = assemblyBytes,
                    };
                    library.Content.Add(assemblyAttachment);
                    foreach (var kvp in assembly.SourceCode)
                    {
                        var sourceBytes = Encoding.UTF8.GetBytes(kvp.Value);
                        var sourceBase64 = System.Convert.ToBase64String(sourceBytes);
                        var sourceAttachment = new Attachment
                        {
                            ElementId = $"{kvp.Key}+csharp",
                            ContentType = "text/plain",
                            Data = sourceBytes,
                        };
                        library.Content.Add(sourceAttachment);
                    }

                }
                library.Url = canon(library.Name.Replace("_","-"), library.TypeName)!;
                return library;
            }
            else throw new ArgumentException($"Couldn't find library {elmFile.FullName}", nameof(elmFile));
        }
        
        private static readonly CodeableConcept LogicLibraryCodeableConcept = new CodeableConcept
        {
            Coding = new List<Coding>
            {
                new Coding
                {
                    Code = "logic-library"!,
                    System = "http://terminology.hl7.org/CodeSystem/library-type"!
                }
            }
        };

        internal TypeConverter TypeConverter { get; }

        private ParameterDefinition ElmParameterToFhir(Hl7.Cql.Elm.ParameterDef elmParameter,
            CqlTypeToFhirTypeMapper typeCrosswalk)
        {
            var typeSpecifier = elmParameter.resultTypeSpecifier ?? elmParameter.parameterTypeSpecifier;
            if (typeSpecifier is null)
                throw new ArgumentException($"{typeSpecifier} is missing on parameter: {elmParameter.name}", nameof(elmParameter));
            var type = typeCrosswalk.TypeEntryFor(typeSpecifier);
            if (type is null || type.FhirType is null)
                throw new ArgumentException($"Unable to identify a valid FHIR type for this parameter.", nameof(elmParameter));

            var annotations = (elmParameter.annotation?
                .OfType<Elm.Annotation>()
                .SelectMany(a => a.t ?? Enumerable.Empty<Tag>())
                ?? Enumerable.Empty<Tag>())
                .ToArray();

            var parameterDefinition = new ParameterDefinition
            {
                Name = elmParameter.name!,
                Use = OperationParameterUse.In,
                Min = elmParameter.@default is null ? 1 : 0,
                Max = "1"!,
                Type = type.FhirType,
            };
            return parameterDefinition;
        }

        private ParameterDefinition ElmDefinitionToParameter(Hl7.Cql.Elm.ExpressionDef definition,
            CqlTypeToFhirTypeMapper typeCrosswalk)
        {
            var resultTypeSpecifier = definition.resultTypeSpecifier;
            if (resultTypeSpecifier is null && !string.IsNullOrWhiteSpace(definition.resultTypeName.Name))
                resultTypeSpecifier = new Hl7.Cql.Elm.NamedTypeSpecifier
                {
                    name = definition.resultTypeName
                };
            var type = typeCrosswalk.TypeEntryFor(resultTypeSpecifier);
            if (type is null || type.FhirType is null)
                throw new ArgumentException($"Unable to identify a valid FHIR type for this definition.", nameof(definition));
            var parameterDefinition = new ParameterDefinition
            {
                Name = definition.name!,
                Use = OperationParameterUse.Out,
                Min = 0,
                Max = "1",
                Type = type.FhirType!,
            };

        AddParameterCqlTypeExtension(type, parameterDefinition);

        if (definition.accessLevel == AccessModifier.Private)
        {
            parameterDefinition.Extension.Add(new Extension
            {
                Value = new FhirModelCode("private"),
                Url = Constants.Hl7FhirStructureDefinitionCqlAccessModifier,
            });
        }

            return parameterDefinition;
        }
        private static void AddParameterCqlTypeExtension(CqlTypeToFhirMapping type, ParameterDefinition parameterDefinition)
        {
            var cqlType = type.CqlType;
            var cqlElementType = type.ElementType?.CqlType;
            switch (cqlType)
            {
                case null:
                    return;

                case CqlPrimitiveType.List
                    when type.ElementType?.FhirType is { } elementFhirType:
                    parameterDefinition.Type = elementFhirType;
                    parameterDefinition.Max = "*";
                    break;
            }

            var cqlTypeName =
                (cqlType, cqlElementType) switch
                {
                    // Don't show "generic" for List
                    (CqlPrimitiveType.List, _) => cqlType.ToString(),

                    // "Generic" display
                    (_, CqlPrimitiveType.Fhir) => $"{cqlType}<{cqlElementType}.{type.ElementType!.FhirType}>",
                    (_, { }) => $"{cqlType}<{cqlElementType}>",

                    // Non-"Generic" display
                    _ => cqlType.ToString(),
                };

            parameterDefinition.Extension = new List<Extension>
            {
                new Extension
                {
                    Url = Constants.Hl7FhirStructureDefinitionCqlType,
                    Value = new FhirString(cqlTypeName),
                }
            };
        }
    }

}
