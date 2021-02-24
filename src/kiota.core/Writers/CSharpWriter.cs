﻿using System.Linq;
using Microsoft.OpenApi.Models;

namespace kiota.core
{
    public class CSharpWriter : LanguageWriter
    {
        public CSharpWriter(string rootPath, string clientNamespaceName)
        {
            segmenter = new CSharpPathSegmenter(rootPath, clientNamespaceName);
        }
        private readonly IPathSegmenter segmenter;
        public override IPathSegmenter PathSegmenter => segmenter;

        public override void WriteCodeClassDeclaration(CodeClass.Declaration code)
        {
            foreach (var codeUsing in code.Usings.Where(x => !string.IsNullOrEmpty(x.Name)).OrderBy(x => x.Name))
            {
                if(codeUsing.Declaration == null)
                    WriteLine($"using {codeUsing.Name};");
                else
                    WriteLine($"using {codeUsing.Name.Split('.').Select(x => x.ToFirstCharacterUpperCase()).Aggregate((x,y) => x + "." + y)};");
            }
            if(code?.Parent?.Parent is CodeNamespace) {
                WriteLine($"namespace {code.Parent.Parent.Name} {{");
                IncreaseIndent();
            }

            var derivation = code.Inherits?.Name +
                            (string.IsNullOrEmpty(code.Inherits?.Name) || !code.Implements.Any() ? string.Empty : ", ") +
                            (code.Implements.Any() ? code.Implements.Select(x => x.Name).Aggregate((x, y) => $"{x}, {y}") : string.Empty);
            if(!string.IsNullOrEmpty(derivation))
                derivation = ": " + derivation + " ";
            WriteLine($"public class {code.Name.ToFirstCharacterUpperCase()} {derivation}{{");
            IncreaseIndent();
        }

        public override void WriteCodeClassEnd(CodeClass.End code)
        {
            DecreaseIndent();
            WriteLine("}");
            if(code?.Parent?.Parent is CodeNamespace) {
                DecreaseIndent();
                WriteLine("}");
            }
        }

        public override void WriteProperty(CodeProperty code)
        {
            var simpleBody = "get;";
            if (!code.ReadOnly)
            {
                simpleBody = "get; set;";
            }
            var defaultValue = string.Empty;
            if (code.DefaultValue != null)
            {
                defaultValue = " = " + code.DefaultValue + ";";
            }
            var propertyType = GetTypeString(code.Type);
            switch(code.PropertyKind) {
                case CodePropertyKind.RequestBuilder:
                    WriteLine($"{GetAccessModifier(code.Access)} {propertyType} {code.Name.ToFirstCharacterUpperCase()} {{ get =>");
                    IncreaseIndent();
                    AddRequestBuilderBody(propertyType);
                    DecreaseIndent();
                    WriteLine("}");
                break;
                default:
                    WriteLine($"{GetAccessModifier(code.Access)} {propertyType} {code.Name.ToFirstCharacterUpperCase()} {{ {simpleBody} }}{defaultValue}");
                break;
            }
        }
        private const string pathSegmentPropertyName = "PathSegment";
        private const string currentPathPropertyName = "CurrentPath";
        private const string httpCorePropertyName = "HttpCore";
        private void AddRequestBuilderBody(string returnType, string suffix = default, string prefix = default) {
            WriteLine($"{prefix}new {returnType} {{ {httpCorePropertyName} = {httpCorePropertyName}, {currentPathPropertyName} = {currentPathPropertyName} + {pathSegmentPropertyName} {suffix}}};");
        }
        public override void WriteIndexer(CodeIndexer code)
        {
            var returnType = GetTypeString(code.ReturnType);
            WriteLine($"public {returnType} this[{GetTypeString(code.IndexType)} position] {{ get {{");
            IncreaseIndent();
            AddRequestBuilderBody(returnType, " + \"/\" + position", "return ");
            DecreaseIndent();
            WriteLine("} }");
        }

        public override void WriteMethod(CodeMethod code)
        {
            var staticModifier = code.IsStatic ? "static " : string.Empty;
            // Task type should be moved into the refiner
            WriteLine($"{GetAccessModifier(code.Access)} {staticModifier}async Task<{GetTypeString(code.ReturnType).ToFirstCharacterUpperCase()}> {code.Name}({string.Join(", ", code.Parameters.Select(p=> GetParameterSignature(p)).ToList())}) {{");
            IncreaseIndent();
            switch(code.MethodKind) {
                case CodeMethodKind.RequestExecutor:
                    var operationName = code.Name.Replace("Async", string.Empty);
                    WriteLine("var requestInfo = new RequestInfo {");
                    IncreaseIndent();
                    WriteLines($"HttpMethod = HttpMethod.{operationName.ToUpperInvariant()},",
                               $"URI = new Uri({currentPathPropertyName}),");
                    DecreaseIndent();
                    WriteLines("};",
                               "if (q != null) {");
                    IncreaseIndent();
                    WriteLines($"var qParams = new {operationName.ToFirstCharacterUpperCase()}QueryParameters();",
                                "q.Invoke(qParams);",
                                "qParams.AddQueryParameters(requestInfo.QueryParameters);");
                    DecreaseIndent();
                    WriteLines("}",
                               "h?.Invoke(requestInfo.Headers);",
                               "using var resultStream = await HttpCore.SendAsync(requestInfo);",
                               "// return await ResponseHandler?.Invoke(resultStream);",
                               "return null;");
                break;
                default:
                    WriteLine("return null;");
                break;
            }
            DecreaseIndent();
            WriteLine("}");

        }

        public override void WriteType(CodeType code)
        {
            Write(GetTypeString(code), includeIndent: false);

        }

        public override string GetTypeString(CodeType code)
        {
            var typeName = TranslateType(code.Name, code.Schema);
            if (code.ActionOf)
            {
                return $"Action<{typeName}>";
            }
            else
            {
                return typeName;
            }
        }

        public override string TranslateType(string typeName, OpenApiSchema schema)
        {
            switch (typeName)
            {
                case "integer": return "int";
                case "boolean": return "bool"; 
                case "array": return TranslateType(schema.Items.Type, schema.Items) + "[]";
                default: return typeName ?? "object";
            }
        }

        public override string GetParameterSignature(CodeParameter parameter)
        {
            var parameterType = GetTypeString(parameter.Type);
            return $"{parameterType} {parameter.Name}{(parameter.Optional ? $" = default({parameterType})": string.Empty)}";
        }

        public override string GetAccessModifier(AccessModifier access)
        {
            return (access == AccessModifier.Public ? "public" : (access == AccessModifier.Protected ? "protected" : "private"));
        }
    }
}
