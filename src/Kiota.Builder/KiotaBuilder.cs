﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Kiota.Builder.Extensions;
using Kiota.Builder.Writers;
using Microsoft.OpenApi.Any;
using Kiota.Builder.Refiners;
using System.Security;
using Microsoft.OpenApi.Services;

namespace Kiota.Builder
{
    public class KiotaBuilder
    {
        private readonly ILogger<KiotaBuilder> logger;
        private readonly GenerationConfiguration config;

        public KiotaBuilder(ILogger<KiotaBuilder> logger, GenerationConfiguration config)
        {
            this.logger = logger;
            this.config = config;
        }

        public async Task GenerateSDK()
        {
            var sw = new Stopwatch();
            // Step 1 - Read input stream
            string inputPath = config.OpenAPIFilePath;

            try {
                // doing this verification at the begining to give immediate feedback to the user
                Directory.CreateDirectory(config.OutputPath);
            } catch (Exception ex) {
                logger.LogError($"Could not open/create output directory {config.OutputPath}, reason: {ex.Message}");
                return;
            }
            
            sw.Start();
            using var input = await LoadStream(inputPath);
            if(input == null)
                return;
            StopLogAndReset(sw, "step 1 - reading the stream - took");

            // Step 2 - Parse OpenAPI
            sw.Start();
            var doc = CreateOpenApiDocument(input);
            StopLogAndReset(sw, "step 2 - parsing the document - took");

            SetApiRootUrl(doc);

            // Step 3 - Create Uri Space of API
            sw.Start();
            var openApiTree = CreateUriSpace(doc);
            StopLogAndReset(sw, "step 3 - create uri space - took");

            // Step 4 - Create Source Model
            sw.Start();
            var generatedCode = CreateSourceModel(openApiTree);
            StopLogAndReset(sw, "step 4 - create source model - took");

            // Step 5 - RefineByLanguage
            sw.Start();
            ApplyLanguageRefinement(config, generatedCode);
            StopLogAndReset(sw, "step 5 - refine by language - took");

            // Step 6 - Write language source 
            sw.Start();
            await CreateLanguageSourceFilesAsync(config.Language, generatedCode);
            StopLogAndReset(sw, "step 6 - writing files - took");
        }
        private void SetApiRootUrl(OpenApiDocument doc) {
            config.ApiRootUrl = doc.Servers.FirstOrDefault()?.Url.TrimEnd('/');
            if(string.IsNullOrEmpty(config.ApiRootUrl))
                throw new InvalidOperationException("A servers entry (v3) or host + basePath + schems properties (v2) must be present in the OpenAPI description.");
        }
        private void StopLogAndReset(Stopwatch sw, string prefix) {
            sw.Stop();
            logger.LogDebug($"{prefix} {sw.Elapsed}");
            sw.Reset();
        }


        private async Task<Stream> LoadStream(string inputPath)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Stream input;
            if (inputPath.StartsWith("http"))
                try {
                    using var httpClient = new HttpClient();
                    input = await httpClient.GetStreamAsync(inputPath);
                } catch (HttpRequestException ex) {
                    logger.LogError($"Could not download the file at {inputPath}, reason: {ex.Message}");
                    return null;
                }
            else
                try {
                    input = new FileStream(inputPath, FileMode.Open);
                } catch (Exception ex) when (ex is FileNotFoundException ||
                    ex is PathTooLongException ||
                    ex is DirectoryNotFoundException ||
                    ex is IOException ||
                    ex is UnauthorizedAccessException ||
                    ex is SecurityException ||
                    ex is NotSupportedException) {
                    logger.LogError($"Could not open the file at {inputPath}, reason: {ex.Message}");
                    return null;
                }
            stopwatch.Stop();
            logger.LogTrace("{timestamp}ms: Read OpenAPI file {file}", stopwatch.ElapsedMilliseconds, inputPath);
            return input;
        }


        public OpenApiDocument CreateOpenApiDocument(Stream input)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            logger.LogTrace("Parsing OpenAPI file");
            var reader = new OpenApiStreamReader();
            var doc = reader.Read(input, out var diag);
            stopwatch.Stop();
            if (diag.Errors.Count > 0)
            {
                logger.LogError($"{stopwatch.ElapsedMilliseconds}ms: OpenApi Parsing errors {string.Join(Environment.NewLine, diag.Errors.Select(e => e.Message))}");
            }
            else
            {
                logger.LogTrace("{timestamp}ms: Parsed OpenAPI successfully. {count} paths found.", stopwatch.ElapsedMilliseconds, doc.Paths.Count);
            }

            return doc;
        }

        /// <summary>
        /// Translate OpenApi PathItems into a tree structure that will define the classes
        /// </summary>
        /// <param name="doc">OpenAPI Document of the API to be processed</param>
        /// <returns>Root node of the API URI space</returns>
        public OpenApiUrlTreeNode CreateUriSpace(OpenApiDocument doc)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var node = OpenApiUrlTreeNode.Create(doc, Constants.DefaultOpenApiLabel);
            stopwatch.Stop();
            logger.LogTrace("{timestamp}ms: Created UriSpace tree", stopwatch.ElapsedMilliseconds);
            return node;
        }
        private CodeNamespace rootNamespace;
        private CodeNamespace modelsNamespace;

        /// <summary>
        /// Convert UriSpace of OpenApiPathItems into conceptual SDK Code model 
        /// </summary>
        /// <param name="root">Root OpenApiUriSpaceNode of API to be generated</param>
        /// <returns></returns>
        public CodeNamespace CreateSourceModel(OpenApiUrlTreeNode root)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            rootNamespace = CodeNamespace.InitRootNamespace();
            var codeNamespace = rootNamespace.AddNamespace(config.ClientNamespaceName);
            modelsNamespace = rootNamespace.AddNamespace($"{codeNamespace.Name}.models");
            CreateRequestBuilderClass(codeNamespace, root, root);
            StopLogAndReset(stopwatch, $"{nameof(CreateRequestBuilderClass)}");
            stopwatch.Start();
            MapTypeDefinitions(codeNamespace);
            StopLogAndReset(stopwatch, $"{nameof(MapTypeDefinitions)}");

            logger.LogTrace("{timestamp}ms: Created source model with {count} classes", stopwatch.ElapsedMilliseconds, codeNamespace.GetChildElements(true).Count());

            return rootNamespace;
        }

        /// <summary>
        /// Manipulate CodeDOM for language specific issues
        /// </summary>
        /// <param name="config"></param>
        /// <param name="generatedCode"></param>
        public void ApplyLanguageRefinement(GenerationConfiguration config, CodeNamespace generatedCode)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            ILanguageRefiner.Refine(config, generatedCode);

            stopwatch.Stop();
            logger.LogDebug("{timestamp}ms: Language refinement applied", stopwatch.ElapsedMilliseconds);
        }

        /// <summary>
        /// Iterate through Url Space and create request builder classes for each node in the tree
        /// </summary>
        /// <param name="root">Root node of URI space from the OpenAPI described API</param>
        /// <returns>A CodeNamespace object that contains request builder classes for the Uri Space</returns>

        public async Task CreateLanguageSourceFilesAsync(GenerationLanguage language, CodeNamespace generatedCode)
        {
            var languageWriter = LanguageWriter.GetLanguageWriter(language, this.config.OutputPath, this.config.ClientNamespaceName);
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            await new CodeRenderer(config).RenderCodeNamespaceToFilePerClassAsync(languageWriter, generatedCode);
            stopwatch.Stop();
            logger.LogTrace("{timestamp}ms: Files written to {path}", stopwatch.ElapsedMilliseconds, config.OutputPath);
        }
        private static readonly string requestBuilderSuffix = "RequestBuilder";
        private static readonly string voidType = "void";
        private static readonly string coreInterfaceType = "IRequestAdapter";
        private static readonly string requestAdapterParameterName = "requestAdapter";
        private static readonly string constructorMethodName = "constructor";
        /// <summary>
        /// Create a CodeClass instance that is a request builder class for the OpenApiUrlTreeNode
        /// </summary>
        private void CreateRequestBuilderClass(CodeNamespace currentNamespace, OpenApiUrlTreeNode currentNode, OpenApiUrlTreeNode rootNode)
        {
            // Determine Class Name
            CodeClass codeClass;
            var isApiClientClass = currentNode == rootNode;
            if (isApiClientClass)
                codeClass = currentNamespace.AddClass(new CodeClass { 
                Name = config.ClientClassName,
                ClassKind = CodeClassKind.RequestBuilder,
                Description = "The main entry point of the SDK, exposes the configuration and the fluent API."
            }).First();
            else
            {
                var targetNS = currentNode.DoesNodeBelongToItemSubnamespace() ? currentNamespace.EnsureItemNamespace() : currentNamespace;
                var className = currentNode.GetClassName(requestBuilderSuffix);
                codeClass = targetNS.AddClass(new CodeClass {
                    Name = className, 
                    ClassKind = CodeClassKind.RequestBuilder,
                    Description = currentNode.GetPathItemDescription(Constants.DefaultOpenApiLabel, $"Builds and executes requests for operations under {currentNode.Path}"),
                }).First();
            }

            logger.LogTrace("Creating class {class}", codeClass.Name);

            // Add properties for children
            foreach (var child in currentNode.Children)
            {
                var propIdentifier = child.Value.GetClassName();
                var propType = propIdentifier + requestBuilderSuffix;
                if (child.Value.IsPathSegmentWithSingleSimpleParameter())
                {
                    var prop = CreateIndexer($"{propIdentifier}-indexer", propType, child.Value, currentNode);
                    codeClass.SetIndexer(prop);
                }
                else if (child.Value.IsComplexPathWithAnyNumberOfParameters())
                {
                    CreateMethod(propIdentifier, propType, codeClass, child.Value);
                }
                else
                {
                    var prop = CreateProperty(propIdentifier, propType, kind: CodePropertyKind.RequestBuilder); // we should add the type definition here but we can't as it might not have been generated yet
                    codeClass.AddProperty(prop);
                }
            }

            // Add methods for Operations
            if (currentNode.HasOperations(Constants.DefaultOpenApiLabel))
            {
                foreach(var operation in currentNode
                                        .PathItems[Constants.DefaultOpenApiLabel]
                                        .Operations
                                        .Where(x => x.Value.RequestBody?.Content?.Any(y => !config.IgnoredRequestContentTypes.Contains(y.Key)) ?? true))
                    CreateOperationMethods(currentNode, operation.Key, operation.Value, codeClass);
            }
            CreateUrlManagement(codeClass, currentNode, isApiClientClass);
           
            Parallel.ForEach(currentNode.Children.Values, childNode =>
            {
                var targetNamespaceName = childNode.GetNodeNamespaceFromPath(config.ClientNamespaceName);
                var targetNamespace = rootNamespace.FindNamespaceByName(targetNamespaceName) ?? rootNamespace.AddNamespace(targetNamespaceName);
                CreateRequestBuilderClass(targetNamespace, childNode, rootNode);
            });
        }
        private static void CreateMethod(string propIdentifier, string propType, CodeClass codeClass, OpenApiUrlTreeNode currentNode)
        {
            var methodToAdd = new CodeMethod {
                Name = propIdentifier,
                MethodKind = CodeMethodKind.RequestBuilderWithParameters,
                Description = currentNode.GetPathItemDescription(Constants.DefaultOpenApiLabel, $"Builds and executes requests for operations under {currentNode.Path}"),
                Access = AccessModifier.Public,
                IsAsync = false,
                IsStatic = false,
            };
            methodToAdd.ReturnType = new CodeType {
                Name = propType,
                ActionOf = false,
                CollectionKind = CodeTypeBase.CodeTypeCollectionKind.None,
                IsExternal = false,
                IsNullable = false,
            };
            AddPathParametersToMethod(currentNode, methodToAdd, false);
            codeClass.AddMethod(methodToAdd);
        }
        private static void AddPathParametersToMethod(OpenApiUrlTreeNode currentNode, CodeMethod methodToAdd, bool asOptional) {
            foreach(var parameter in currentNode.GetPathParametersForCurrentSegment()) {
                var mParameter = new CodeParameter {
                    Name = parameter.Name,
                    Optional = asOptional,
                    Description = parameter.Description,
                    ParameterKind = CodeParameterKind.Path,
                    UrlTemplateParameterName = parameter.Name,
                };
                mParameter.Type = GetPrimitiveType(parameter.Schema);
                methodToAdd.AddParameter(mParameter);
            }
        }
        private static readonly string PathParametersParameterName = "pathParameters";
        private void CreateUrlManagement(CodeClass currentClass, OpenApiUrlTreeNode currentNode, bool isApiClientClass) {
            var pathProperty = new CodeProperty {
                Access = AccessModifier.Private,
                Name = "urlTemplate",
                DefaultValue = $"\"{currentNode.GetUrlTemplate()}\"",
                ReadOnly = true,
                Description = "Url template to use to build the URL for the current request builder",
                PropertyKind = CodePropertyKind.UrlTemplate,
                Type = new CodeType {
                    Name = "string",
                    IsNullable = false,
                    IsExternal = true,
                },
            };
            currentClass.AddProperty(pathProperty);

            var requestAdapterProperty = new CodeProperty {
                Name = requestAdapterParameterName,
                Description = "The request adapter to use to execute the requests.",
                PropertyKind = CodePropertyKind.RequestAdapter,
                Access = AccessModifier.Private,
                ReadOnly = true,
            };
            requestAdapterProperty.Type = new CodeType {
                Name = coreInterfaceType,
                IsExternal = true,
                IsNullable = false,
            };
            currentClass.AddProperty(requestAdapterProperty);
            var constructor = currentClass.AddMethod(new CodeMethod {
                Name = constructorMethodName,
                MethodKind = isApiClientClass ? CodeMethodKind.ClientConstructor : CodeMethodKind.Constructor,
                IsAsync = false,
                IsStatic = false,
                Description = $"Instantiates a new {currentClass.Name.ToFirstCharacterUpperCase()} and sets the default values.",
                Access = AccessModifier.Public,
            }).First();
            constructor.ReturnType = new CodeType { Name = voidType, IsExternal = true };
            var pathParametersProperty = new CodeProperty {
                Name = PathParametersParameterName,
                Description = "Path parameters for the request",
                PropertyKind = CodePropertyKind.PathParameters,
                Access = AccessModifier.Private,
                ReadOnly = true,
                Type = new CodeType {
                    Name = "Dictionary<string, object>",
                    IsExternal = true,
                    IsNullable = false,
                },
            };
            currentClass.AddProperty(pathParametersProperty);
            if(isApiClientClass) {
                constructor.SerializerModules = config.Serializers;
                constructor.DeserializerModules = config.Deserializers;
                constructor.BaseUrl = config.ApiRootUrl;
                pathParametersProperty.DefaultValue = $"new {pathParametersProperty.Type.Name}()";
            } else {
                constructor.AddParameter(new CodeParameter {
                    Name = PathParametersParameterName,
                    Type = pathParametersProperty.Type,
                    Optional = false,
                    Description = pathParametersProperty.Description,
                    ParameterKind = CodeParameterKind.PathParameters,
                });
                AddPathParametersToMethod(currentNode, constructor, true);
            }
            constructor.AddParameter(new CodeParameter {
                Name = requestAdapterParameterName,
                Type = requestAdapterProperty.Type,
                Optional = false,
                Description = requestAdapterProperty.Description,
                ParameterKind = CodeParameterKind.RequestAdapter,
            });
            if(isApiClientClass && config.UsesBackingStore) {
                var factoryInterfaceName = $"{BackingStoreInterface}Factory";
                var backingStoreParam = new CodeParameter {
                    Name = "backingStore",
                    Optional = true,
                    Description = "The backing store to use for the models.",
                    ParameterKind = CodeParameterKind.BackingStore,
                    Type = new CodeType {
                        Name = factoryInterfaceName,
                        IsNullable = true,
                    }
                };
                constructor.AddParameter(backingStoreParam);
            }
        }
        private static readonly Func<CodeClass, int> shortestNamespaceOrder = (x) => x.GetNamespaceDepth();
        /// <summary>
        /// Remaps definitions to custom types so they can be used later in generation or in refiners
        /// </summary>
        private void MapTypeDefinitions(CodeElement codeElement) {
            var unmappedTypes = GetUnmappedTypeDefinitions(codeElement).Distinct();
            
            var unmappedTypesWithNoName = unmappedTypes.Where(x => string.IsNullOrEmpty(x.Name)).ToList();
            
            unmappedTypesWithNoName.ForEach(x => {
                logger.LogWarning($"Type with empty name and parent {x.Parent.Name}");
            });

            var unmappedTypesWithName = unmappedTypes.Except(unmappedTypesWithNoName);

            var unmappedRequestBuilderTypes = unmappedTypesWithName
                                    .Where(x => 
                                    x.Parent is CodeProperty property && property.IsOfKind(CodePropertyKind.RequestBuilder) ||
                                    x.Parent is CodeIndexer ||
                                    x.Parent is CodeMethod method && method.IsOfKind(CodeMethodKind.RequestBuilderWithParameters))
                                    .ToList();
            
            Parallel.ForEach(unmappedRequestBuilderTypes, x => {
                var parentNS = x.Parent.Parent.Parent as CodeNamespace;
                x.TypeDefinition = parentNS.FindChildrenByName<CodeClass>(x.Name)
                                            .OrderBy(shortestNamespaceOrder)
                                            .FirstOrDefault();
                // searching down first because most request builder properties on a request builder are just sub paths on the API
                if(x.TypeDefinition == null) {
                    parentNS = parentNS.Parent as CodeNamespace;
                    x.TypeDefinition = parentNS
                        .FindNamespaceByName($"{parentNS.Name}.{x.Name.Substring(0, x.Name.Length - requestBuilderSuffix.Length).ToFirstCharacterLowerCase()}".TrimEnd(nsNameSeparator))
                        ?.FindChildrenByName<CodeClass>(x.Name)
                        ?.OrderBy(shortestNamespaceOrder)
                        ?.FirstOrDefault();
                    // in case of the .item namespace, going to the parent and then down to the target by convention
                    // this avoid getting the wrong request builder in case we have multiple request builders with the same name in the parent branch
                    // in both cases we always take the uppermost item (smaller numbers of segments in the namespace name)
                }
            });

            Parallel.ForEach(unmappedTypesWithName.Where(x => x.TypeDefinition == null).GroupBy(x => x.Name), x => {
                if (rootNamespace.FindChildByName<ITypeDefinition>(x.First().Name) is CodeElement definition)
                    foreach (var type in x)
                    {
                        type.TypeDefinition = definition;
                        logger.LogWarning($"Mapped type {type.Name} for {type.Parent.Name} using the fallback approach.");
                    }
            });
        }
        private static readonly char nsNameSeparator = '.';
        private static IEnumerable<CodeType> filterUnmappedTypeDefitions(IEnumerable<CodeTypeBase> source) =>
        source.OfType<CodeType>()
                .Union(source
                        .OfType<CodeUnionType>()
                        .SelectMany(x => x.Types))
                .Where(x => !x.IsExternal && x.TypeDefinition == null);
        private IEnumerable<CodeType> GetUnmappedTypeDefinitions(CodeElement codeElement) {
            var childElementsUnmappedTypes = codeElement.GetChildElements(true).SelectMany(x => GetUnmappedTypeDefinitions(x));
            return codeElement switch
            {
                CodeMethod method => filterUnmappedTypeDefitions(method.Parameters.Select(x => x.Type).Union(new CodeTypeBase[] { method.ReturnType })).Union(childElementsUnmappedTypes),
                CodeProperty property => filterUnmappedTypeDefitions(new CodeTypeBase[] { property.Type }).Union(childElementsUnmappedTypes),
                CodeIndexer indexer => filterUnmappedTypeDefitions(new CodeTypeBase[] { indexer.ReturnType }).Union(childElementsUnmappedTypes),
                _ => childElementsUnmappedTypes,
            };
        }
        private CodeIndexer CreateIndexer(string childIdentifier, string childType, OpenApiUrlTreeNode currentNode, OpenApiUrlTreeNode parentNode)
        {
            logger.LogTrace("Creating indexer {name}", childIdentifier);
            return new CodeIndexer
            {
                Name = childIdentifier,
                Description = $"Gets an item from the {currentNode.GetNodeNamespaceFromPath(config.ClientNamespaceName)} collection",
                IndexType = new CodeType { Name = "string", IsExternal = true, },
                ReturnType = new CodeType { Name = childType },
                ParameterName = currentNode.Segment.SanitizeUrlTemplateParameterName().TrimStart('{').TrimEnd('}'),
                PathSegment = parentNode.GetNodeNamespaceFromPath(string.Empty).Split('.').Last(),
            };
        }

        private CodeProperty CreateProperty(string childIdentifier, string childType, string defaultValue = null, OpenApiSchema typeSchema = null, CodeElement typeDefinition = null, CodePropertyKind kind = CodePropertyKind.Custom)
        {
            var propertyName = childIdentifier;
            config.PropertiesPrefixToStrip.ForEach(x => propertyName = propertyName.Replace(x, string.Empty));
            var prop = new CodeProperty
            {
                Name = propertyName,
                DefaultValue = defaultValue,
                PropertyKind = kind,
                Description = typeSchema?.Description,
            };
            if(propertyName != childIdentifier)
                prop.SerializationName = childIdentifier;
            
            var propType = GetPrimitiveType(typeSchema, childType);
            propType.TypeDefinition = typeDefinition;
            propType.CollectionKind = typeSchema.IsArray() ? CodeType.CodeTypeCollectionKind.Complex : default;
            prop.Type = propType;
            logger.LogTrace("Creating property {name} of {type}", prop.Name, prop.Type.Name);
            return prop;
        }
        private static readonly HashSet<string> typeNamesToSkip = new() {"object", "array"};
        private static CodeType GetPrimitiveType(OpenApiSchema typeSchema, string childType = default) {
            var typeNames = new List<string>{typeSchema?.Items?.Type, childType, typeSchema?.Type};
            if(typeSchema?.AnyOf?.Any() ?? false)
                typeNames.AddRange(typeSchema.AnyOf.Select(x => x.Type)); // double is sometimes an anyof string, number and enum
            // first value that's not null, and not "object" for primitive collections, the items type matters
            var typeName = typeNames.FirstOrDefault(x => !string.IsNullOrEmpty(x) && !typeNamesToSkip.Contains(x));
           
            if(string.IsNullOrEmpty(typeName))
                return null;
            var format = typeSchema?.Format ?? typeSchema?.Items?.Format;
            var isExternal = false;
            if (typeSchema?.Items?.Enum?.Any() ?? false)
                typeName = childType;
            else if("string".Equals(typeName, StringComparison.OrdinalIgnoreCase)) {
                    isExternal = true;
                if("date-time".Equals(format, StringComparison.OrdinalIgnoreCase))
                    typeName = "DateTimeOffset";
                else if ("base64url".Equals(format, StringComparison.OrdinalIgnoreCase))
                    typeName = "binary";
            } else if ("double".Equals(format, StringComparison.OrdinalIgnoreCase) || 
                    "float".Equals(format, StringComparison.OrdinalIgnoreCase) ||
                    "int64".Equals(format, StringComparison.OrdinalIgnoreCase) ||
                    "decimal".Equals(format, StringComparison.OrdinalIgnoreCase)) {
                isExternal = true;
                typeName = format.ToLowerInvariant();
            } else if ("boolean".Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                        "integer".Equals(typeName, StringComparison.OrdinalIgnoreCase))
                isExternal = true;
            return new CodeType {
                Name = typeName,
                IsExternal = isExternal,
            };
        }
        private const string RequestBodyBinaryContentType = "application/octet-stream";
        private static readonly HashSet<string> noContentStatusCodes = new() { "201", "202", "204" };
        private void CreateOperationMethods(OpenApiUrlTreeNode currentNode, OperationType operationType, OpenApiOperation operation, CodeClass parentClass)
        {
            var parameterClass = CreateOperationParameter(currentNode, operationType, operation);

            var schema = operation.GetResponseSchema();
            var method = (HttpMethod)Enum.Parse(typeof(HttpMethod), operationType.ToString());
            var executorMethod = new CodeMethod {
                Name = operationType.ToString(),
                MethodKind = CodeMethodKind.RequestExecutor,
                HttpMethod = method,
                Description = operation.Description ?? operation.Summary,
            };
            parentClass.AddMethod(executorMethod);
            if (schema != null)
            {
                var returnType = CreateModelDeclarations(currentNode, schema, operation, executorMethod, "Response");
                executorMethod.ReturnType = returnType ?? throw new InvalidOperationException("Could not resolve return type for operation");
            } else {
                var returnType = voidType;
                if(operation.Responses.Any(x => x.Value.Content.ContainsKey(RequestBodyBinaryContentType)))
                    returnType = "binary";
                else if(!operation.Responses.Any(x => noContentStatusCodes.Contains(x.Key)))
                    logger.LogWarning($"could not find operation return type {operationType} {currentNode.Path}");
                executorMethod.ReturnType = new CodeType { Name = returnType, IsExternal = true, };
            }

            
            AddRequestBuilderMethodParameters(currentNode, operation, parameterClass, executorMethod);

            var handlerParam = new CodeParameter {
                Name = "responseHandler",
                Optional = true,
                ParameterKind = CodeParameterKind.ResponseHandler,
                Description = "Response handler to use in place of the default response handling provided by the core service",
                Type = new CodeType { Name = "IResponseHandler", IsExternal = true },
            };
            executorMethod.AddParameter(handlerParam);// Add response handler parameter

            var cancellationParam = new CodeParameter{
                Name = "cancellationToken",
                Optional = true,
                ParameterKind = CodeParameterKind.Cancellation,
                Description = "Cancellation token to use when cancelling requests",
                Type = new CodeType { Name = "CancellationToken", IsExternal = true },
            };
            executorMethod.AddParameter(cancellationParam);// Add cancellation token parameter
            logger.LogTrace("Creating method {name} of {type}", executorMethod.Name, executorMethod.ReturnType);

            var generatorMethod = new CodeMethod {
                Name = $"Create{operationType.ToString().ToFirstCharacterUpperCase()}RequestInformation",
                MethodKind = CodeMethodKind.RequestGenerator,
                IsAsync = false,
                HttpMethod = method,
                Description = operation.Description ?? operation.Summary,
                ReturnType = new CodeType { Name = "RequestInformation", IsNullable = false, IsExternal = true},
            };
            parentClass.AddMethod(generatorMethod);
            AddRequestBuilderMethodParameters(currentNode, operation, parameterClass, generatorMethod);
            logger.LogTrace("Creating method {name} of {type}", generatorMethod.Name, generatorMethod.ReturnType);
        }
        private void AddRequestBuilderMethodParameters(OpenApiUrlTreeNode currentNode, OpenApiOperation operation, CodeClass parameterClass, CodeMethod method) {
            var nonBinaryRequestBody = operation.RequestBody?.Content?.FirstOrDefault(x => !RequestBodyBinaryContentType.Equals(x.Key, StringComparison.OrdinalIgnoreCase));
            if (nonBinaryRequestBody.HasValue && nonBinaryRequestBody.Value.Value != null)
            {
                var requestBodySchema = nonBinaryRequestBody.Value.Value.Schema;
                var requestBodyType = CreateModelDeclarations(currentNode, requestBodySchema, operation, method, "RequestBody");
                method.AddParameter(new CodeParameter {
                    Name = "body",
                    Type = requestBodyType,
                    Optional = false,
                    ParameterKind = CodeParameterKind.RequestBody,
                    Description = requestBodySchema.Description
                });
                method.ContentType = nonBinaryRequestBody.Value.Key;
            } else if (operation.RequestBody?.Content?.ContainsKey(RequestBodyBinaryContentType) ?? false) {
                var nParam = new CodeParameter {
                    Name = "body",
                    Optional = false,
                    ParameterKind = CodeParameterKind.RequestBody,
                    Description = $"Binary request body",
                    Type = new CodeType {
                        Name = "binary",
                        IsExternal = true,
                        IsNullable = false,
                    },
                };
                method.AddParameter(nParam);
            }
            if(parameterClass != null) {
                var qsParam = new CodeParameter
                {
                    Name = "q",
                    Optional = true,
                    ParameterKind = CodeParameterKind.QueryParameter,
                    Description = "Request query parameters",
                    Type = new CodeType { Name = parameterClass.Name, ActionOf = true, TypeDefinition = parameterClass },
                };
                method.AddParameter(qsParam);
            }
            var headersParam = new CodeParameter {
                Name = "h",
                Optional = true,
                ParameterKind = CodeParameterKind.Headers,
                Description = "Request headers",
                Type = new CodeType { Name = "IDictionary<string, string>", ActionOf = true, IsExternal = true },
            };
            method.AddParameter(headersParam);
            var optionsParam = new CodeParameter {
                Name = "o",
                Optional = true,
                ParameterKind = CodeParameterKind.Options,
                Description = "Request options",
                Type = new CodeType { Name = "IEnumerable<IRequestOption>", ActionOf = false, IsExternal = true },
            };
            method.AddParameter(optionsParam);
        }
        private string GetModelsNamespaceNameFromReferenceId(string referenceId) {
            if (string.IsNullOrEmpty(referenceId)) return referenceId;
            if(referenceId.StartsWith(config.ClientClassName, StringComparison.OrdinalIgnoreCase))
                referenceId = referenceId[config.ClientClassName.Length..];
            referenceId = referenceId.Trim(nsNameSeparator);
            var lastDotIndex = referenceId.LastIndexOf(nsNameSeparator);
            var namespaceSuffix = lastDotIndex != -1 ? referenceId[..lastDotIndex] : referenceId;
            return $"{modelsNamespace.Name}.{namespaceSuffix}";
        }
        private CodeType CreateModelDeclarationAndType(OpenApiUrlTreeNode currentNode, OpenApiSchema schema, OpenApiOperation operation, CodeNamespace codeNamespace, string classNameSuffix = "") {
            var className = currentNode.GetClassName(operation: operation, suffix: classNameSuffix);
            var codeDeclaration = AddModelDeclarationIfDoesntExit(currentNode, schema, className, codeNamespace);
            return new CodeType {
                TypeDefinition = codeDeclaration,
                Name = className,
            };
        }
        private CodeTypeBase CreateInheritedModelDeclaration(OpenApiUrlTreeNode currentNode, OpenApiSchema schema, OpenApiOperation operation) {
            var allOfs = schema.AllOf.FlattenEmptyEntries(x => x.AllOf);
            CodeElement codeDeclaration = null;
            var className = string.Empty;
            foreach(var currentSchema in allOfs) {
                var referenceId = GetReferenceIdFromOriginalSchema(currentSchema, schema);
                var shortestNamespaceName = string.IsNullOrEmpty(referenceId) ? currentNode.GetNodeNamespaceFromPath(config.ClientNamespaceName) : GetModelsNamespaceNameFromReferenceId(referenceId);
                var shortestNamespace = rootNamespace.FindNamespaceByName(shortestNamespaceName);
                if(shortestNamespace == null)
                    shortestNamespace = rootNamespace.AddNamespace(shortestNamespaceName);
                className = currentSchema.GetSchemaTitle() ?? currentNode.GetClassName(operation: operation);
                codeDeclaration = AddModelDeclarationIfDoesntExit(currentNode, currentSchema, className, shortestNamespace, codeDeclaration as CodeClass, true);
            }

            return new CodeType {
                TypeDefinition = codeDeclaration,
                Name = className,
            };
        }
        private static string GetReferenceIdFromOriginalSchema(OpenApiSchema schema, OpenApiSchema parentSchema) {
            var title = schema.Title;
            if(!string.IsNullOrEmpty(schema.Reference?.Id)) return schema.Reference.Id;
            if(parentSchema.Reference?.Id?.EndsWith(title, StringComparison.OrdinalIgnoreCase) ?? false) return parentSchema.Reference.Id;
            if(parentSchema.Items?.Reference?.Id?.EndsWith(title, StringComparison.OrdinalIgnoreCase) ?? false) return parentSchema.Items.Reference.Id;
            return (parentSchema.
                            AllOf
                            .FirstOrDefault(x => x.Reference?.Id?.EndsWith(title, StringComparison.OrdinalIgnoreCase) ?? false) ??
                    parentSchema.
                            AnyOf
                            .FirstOrDefault(x => x.Reference?.Id?.EndsWith(title, StringComparison.OrdinalIgnoreCase) ?? false) ??
                    parentSchema.
                            OneOf
                            .FirstOrDefault(x => x.Reference?.Id?.EndsWith(title, StringComparison.OrdinalIgnoreCase) ?? false))
                ?.Reference?.Id;
        }
        private CodeTypeBase CreateUnionModelDeclaration(OpenApiUrlTreeNode currentNode, OpenApiSchema schema, OpenApiOperation operation, string suffixForInlineSchema) {
            var schemas = schema.AnyOf.Union(schema.OneOf);
            var unionType = new CodeUnionType {
                Name = currentNode.GetClassName(operation: operation, suffix: suffixForInlineSchema),
            };
            foreach(var currentSchema in schemas) {
                var shortestNamespaceName = currentSchema.Reference == null ? currentNode.GetNodeNamespaceFromPath(config.ClientNamespaceName) : GetModelsNamespaceNameFromReferenceId(currentSchema.Reference.Id);
                var shortestNamespace = rootNamespace.FindNamespaceByName(shortestNamespaceName);
                if(shortestNamespace == null)
                    shortestNamespace = rootNamespace.AddNamespace(shortestNamespaceName);
                var className = currentSchema.GetSchemaTitle();
                var codeDeclaration = AddModelDeclarationIfDoesntExit(currentNode, currentSchema, className, shortestNamespace);
                unionType.AddType(new CodeType {
                    TypeDefinition = codeDeclaration,
                    Name = className,
                });
            }
            return unionType;
        }
        private CodeTypeBase CreateModelDeclarations(OpenApiUrlTreeNode currentNode, OpenApiSchema schema, OpenApiOperation operation, CodeElement parentElement, string suffixForInlineSchema)
        {
            var codeNamespace = parentElement.GetImmediateParentOfType<CodeNamespace>();
            
            if (!schema.IsReferencedSchema() && schema.Properties.Any()) { // Inline schema, i.e. specific to the Operation
                return CreateModelDeclarationAndType(currentNode, schema, operation, codeNamespace, suffixForInlineSchema);
            } else if(schema.IsAllOf()) {
                return CreateInheritedModelDeclaration(currentNode, schema, operation);
            } else if(schema.IsAnyOf() || schema.IsOneOf()) {
                return CreateUnionModelDeclaration(currentNode, schema, operation, suffixForInlineSchema);
            } else if(schema.IsObject()) {
                // referenced schema, no inheritance or union type
                var targetNamespace = GetShortestNamespace(codeNamespace, schema);
                return CreateModelDeclarationAndType(currentNode, schema, operation, targetNamespace);
            } else if (schema.IsArray()) {
                // collections at root
                var type = GetPrimitiveType(schema?.Items, string.Empty);
                if(type == null)
                    type = CreateModelDeclarationAndType(currentNode, schema?.Items, operation, codeNamespace);
                type.CollectionKind = CodeTypeBase.CodeTypeCollectionKind.Array;
                return type;
            } else if(!string.IsNullOrEmpty(schema.Type))
                return GetPrimitiveType(schema, string.Empty);
            else throw new InvalidOperationException("un handled case, might be object type or array type");
        }
        private CodeElement GetExistingDeclaration(bool checkInAllNamespaces, CodeNamespace currentNamespace, OpenApiUrlTreeNode currentNode, string declarationName) {
            var searchNameSpace = GetSearchNamespace(checkInAllNamespaces, currentNode, currentNamespace);
            return searchNameSpace.FindChildByName<ITypeDefinition>(declarationName, checkInAllNamespaces) as CodeElement;
        }
        private CodeNamespace GetSearchNamespace(bool checkInAllNamespaces, OpenApiUrlTreeNode currentNode, CodeNamespace currentNamespace) {
            if(checkInAllNamespaces) return rootNamespace;
            else if (currentNode.DoesNodeBelongToItemSubnamespace()) return currentNamespace.EnsureItemNamespace();
            else return currentNamespace;
        }
        private CodeElement AddModelDeclarationIfDoesntExit(OpenApiUrlTreeNode currentNode, OpenApiSchema schema, string declarationName, CodeNamespace currentNamespace, CodeClass inheritsFrom = null, bool checkInAllNamespaces = false) {
            var existingDeclaration = GetExistingDeclaration(checkInAllNamespaces, currentNamespace, currentNode, declarationName);
            if(existingDeclaration == null) // we can find it in the components
            {
                if(schema.Enum.Any()) {
                    var newEnum = new CodeEnum { 
                        Name = declarationName,
                        Options = schema.Enum.OfType<OpenApiString>().Select(x => x.Value).Where(x => !"null".Equals(x)).ToList(),//TODO set the flag property
                        Description = currentNode.GetPathItemDescription(Constants.DefaultOpenApiLabel),
                    };
                    return currentNamespace.AddEnum(newEnum).First();
                } else 
                    return AddModelClass(currentNode, schema, declarationName, currentNamespace, inheritsFrom);
            } else
                return existingDeclaration;
        }
        private CodeNamespace GetShortestNamespace(CodeNamespace currentNamespace, OpenApiSchema currentSchema) {
            if(!string.IsNullOrEmpty(currentSchema.Reference?.Id)) {
                var parentClassNamespaceName = GetModelsNamespaceNameFromReferenceId(currentSchema.Reference.Id);
                return rootNamespace.AddNamespace(parentClassNamespaceName);
            }
            return currentNamespace;
        }
        private CodeClass AddModelClass(OpenApiUrlTreeNode currentNode, OpenApiSchema schema, string declarationName, CodeNamespace currentNamespace, CodeClass inheritsFrom = null) {
            var referencedAllOfs = schema.AllOf.Where(x => x.Reference != null);
            if(inheritsFrom == null && referencedAllOfs.Any()) {// any non-reference would be the current class in some description styles
                var parentSchema = referencedAllOfs.FirstOrDefault();
                if(parentSchema != null) {
                    var parentClassNamespace = GetShortestNamespace(currentNamespace, parentSchema);
                    inheritsFrom = AddModelDeclarationIfDoesntExit(currentNode, parentSchema, parentSchema.GetSchemaTitle(), parentClassNamespace, null, true) as CodeClass;
                }
            }
            var newClass = currentNamespace.AddClass(new CodeClass {
                Name = declarationName,
                ClassKind = CodeClassKind.Model,
                Description = currentNode.GetPathItemDescription(Constants.DefaultOpenApiLabel)
            }).First();
            if(inheritsFrom != null) {
                var declaration = newClass.StartBlock as CodeClass.Declaration;
                declaration.Inherits = new CodeType { TypeDefinition = inheritsFrom, Name = inheritsFrom.Name };
            }
            CreatePropertiesForModelClass(currentNode, schema, currentNamespace, newClass);
            return newClass;
        }
        private void CreatePropertiesForModelClass(OpenApiUrlTreeNode currentNode, OpenApiSchema schema, CodeNamespace ns, CodeClass model) {
            AddSerializationMembers(model, schema?.AdditionalPropertiesAllowed ?? false, config.UsesBackingStore);
            if(schema?.Properties?.Any() ?? false)
            {
                model.AddProperty(schema
                                    .Properties
                                    .Select(x => {
                                        var propertyDefinitionSchema = x.Value.GetNonEmptySchemas().FirstOrDefault();
                                        var className = propertyDefinitionSchema.GetSchemaTitle();
                                        CodeElement definition = default;
                                        if(propertyDefinitionSchema != null) {
                                            if(string.IsNullOrEmpty(className))
                                                className = $"{model.Name}_{x.Key}";
                                            var shortestNamespaceName = GetModelsNamespaceNameFromReferenceId(propertyDefinitionSchema.Reference?.Id);
                                            var targetNamespace = string.IsNullOrEmpty(shortestNamespaceName) ? ns : 
                                                                    (rootNamespace.FindNamespaceByName(shortestNamespaceName) ?? rootNamespace.AddNamespace(shortestNamespaceName));
                                            definition = AddModelDeclarationIfDoesntExit(currentNode, propertyDefinitionSchema, className, targetNamespace, null, true);
                                        }
                                        return CreateProperty(x.Key, className ?? x.Value.Type, typeSchema: x.Value, typeDefinition: definition);
                                    })
                                    .ToArray());
            }
            else if(schema?.AllOf?.Any(x => x.IsObject()) ?? false)
                CreatePropertiesForModelClass(currentNode, schema.AllOf.Last(x => x.IsObject()), ns, model);
        }
        private const string FieldDeserializersMethodName = "GetFieldDeserializers<T>";
        private const string SerializeMethodName = "Serialize";
        private const string AdditionalDataPropName = "AdditionalData";
        private const string BackingStorePropertyName = "BackingStore";
        private const string BackingStoreInterface = "IBackingStore";
        private const string BackedModelInterface = "IBackedModel";
        internal static void AddSerializationMembers(CodeClass model, bool includeAdditionalProperties, bool usesBackingStore) {
            var serializationPropsType = $"IDictionary<string, Action<T, IParseNode>>";
            if(!model.ContainsMember(FieldDeserializersMethodName)) {
                var deserializeProp = new CodeMethod {
                    Name = FieldDeserializersMethodName,
                    MethodKind = CodeMethodKind.Deserializer,
                    Access = AccessModifier.Public,
                    Description = "The deserialization information for the current model",
                    IsAsync = false,
                    ReturnType = new CodeType {
                        Name = serializationPropsType,
                        IsNullable = false,
                        IsExternal = true,
                    },
                };
                model.AddMethod(deserializeProp);
            }
            if(!model.ContainsMember(SerializeMethodName)) {
                var serializeMethod = new CodeMethod {
                    Name = SerializeMethodName,
                    MethodKind = CodeMethodKind.Serializer,
                    IsAsync = false,
                    Description = $"Serializes information the current object",
                    ReturnType = new CodeType { Name = voidType, IsNullable = false, IsExternal = true },
                };
                var parameter = new CodeParameter {
                    Name = "writer",
                    Description = "Serialization writer to use to serialize this model",
                    ParameterKind = CodeParameterKind.Serializer,
                    Type = new CodeType { Name = "ISerializationWriter", IsExternal = true, IsNullable = false },
                };
                serializeMethod.AddParameter(parameter);
                
                model.AddMethod(serializeMethod);
            }
            if(!model.ContainsMember(AdditionalDataPropName) &&
                includeAdditionalProperties && 
                !(model.GetGreatestGrandparent(model)?.ContainsMember(AdditionalDataPropName) ?? false)) {
                // we don't want to add the property if the parent already has it
                var additionalDataProp = new CodeProperty {
                    Name = AdditionalDataPropName,
                    Access = AccessModifier.Public,
                    DefaultValue = "new Dictionary<string, object>()",
                    PropertyKind = CodePropertyKind.AdditionalData,
                    Description = "Stores additional data not described in the OpenAPI description found when deserializing. Can be used for serialization as well.",
                    Type = new CodeType {
                        Name = "IDictionary<string, object>",
                        IsNullable = false,
                        IsExternal = true,
                    },
                };
                model.AddProperty(additionalDataProp);
            }
            if(!model.ContainsMember(BackingStorePropertyName) &&
               usesBackingStore &&
               !(model.GetGreatestGrandparent(model)?.ContainsMember(BackingStorePropertyName) ?? false)) {
                var backingStoreProperty = new CodeProperty {
                    Name = BackingStorePropertyName,
                    Access = AccessModifier.Public,
                    DefaultValue = $"BackingStoreFactorySingleton.Instance.CreateBackingStore()",
                    PropertyKind = CodePropertyKind.BackingStore,
                    Description = "Stores model information.",
                    ReadOnly = true,
                    Type = new CodeType {
                        Name = BackingStoreInterface,
                        IsNullable = false,
                        IsExternal = true,
                    },
                };
                model.AddProperty(backingStoreProperty);
                (model.StartBlock as CodeClass.Declaration).AddImplements(new CodeType {
                    Name = BackedModelInterface,
                    IsExternal = true,
                });
            }
        }
        private CodeClass CreateOperationParameter(OpenApiUrlTreeNode node, OperationType operationType, OpenApiOperation operation)
        {
            var parameters = node.PathItems[Constants.DefaultOpenApiLabel].Parameters.Union(operation.Parameters).Where(p => p.In == ParameterLocation.Query);
            if(parameters.Any()) {
                var parameterClass = new CodeClass
                {
                    Name = operationType.ToString() + "QueryParameters",
                    ClassKind = CodeClassKind.QueryParameters,
                    Description = operation.Description ?? operation.Summary
                };
                foreach (var parameter in parameters)
                {
                    var prop = new CodeProperty
                    {
                        Name = FixQueryParameterIdentifier(parameter),
                        Description = parameter.Description,
                        Type = new CodeType
                        {
                            IsExternal = true,
                            Name = parameter.Schema.Items?.Type ?? parameter.Schema.Type,
                            CollectionKind = parameter.Schema.IsArray() ? CodeType.CodeTypeCollectionKind.Array : default,
                        },
                    };

                    if (!parameterClass.ContainsMember(parameter.Name))
                    {
                        parameterClass.AddProperty(prop);
                    }
                    else
                    {
                        logger.LogWarning("Ignoring duplicate parameter {name}", parameter.Name);
                    }
                }

                return parameterClass;
            } else return null;
        }

        private static string FixQueryParameterIdentifier(OpenApiParameter parameter)
        {
            // Replace with regexes pulled from settings that are API specific

            return parameter.Name.Replace("$","").ToCamelCase();
        }
    }
}
