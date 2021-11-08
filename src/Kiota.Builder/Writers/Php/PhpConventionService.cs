﻿using System;
using System.Collections.Generic;
using System.Linq;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.Php
{
    public class PhpConventionService: CommonLanguageConventionService
    {
        public override string TempDictionaryVarName => "urlTplParams";

        public override string GetAccessModifier(AccessModifier access)
        {
            return (access) switch
            {
                (AccessModifier.Public) => "public",
                (AccessModifier.Protected) => "protected",
                _ => "private"
            };
        }
        public override string StreamTypeName => "StreamInterface";

        public override string VoidTypeName => "void";

        public override string DocCommentPrefix => " * ";

        private static string PathSegmentPropertyName => "$pathSegment";

        private static string PathParametersPropertyName => "$pathParameters";

        private static string RequestAdapterPropertyName => "$requestAdapter";

        public override string ParseNodeInterfaceName => "ParseNode";

        public static string ResponseHandlerPropertyName => "$responseHandler";

        public string DocCommentStart = "/**";
        public string DocCommentEnd = "*/";

        public override string GetTypeString(CodeTypeBase code, CodeElement targetElement, bool includeCollectionInformation = true)
        {
            if (code.IsCollection || code.IsArray || code.CollectionKind == CodeTypeBase.CodeTypeCollectionKind.Complex)
            {
                return "array";
            }

            if (targetElement is CodeProperty propertyVar && propertyVar.IsOfKind(CodePropertyKind.PathParameters))
            {
                return "array";
            }
            return TranslateType(code);
        }

        public override string TranslateType(CodeType type)
        {
            string typeName = type.Name;
            return (typeName.ToLowerInvariant()) switch
            {
                "boolean" => "bool",
                "double" or "decimal" => "float",
                "integer" or "int32" or "int64" => "int",
                "object" or "string" or "array" or "float" or "void" => typeName,
                "binary" => "StreamInterface",
                _ => typeName.ToFirstCharacterUpperCase()
            };
        }

        public string GetParameterName(CodeParameter parameter)
        {
            return (parameter.ParameterKind) switch
            {
                CodeParameterKind.Headers => "$headers",
                CodeParameterKind.Options => "$options",
                CodeParameterKind.BackingStore => "$backingStore",
                CodeParameterKind.QueryParameter => "$queryParameters",
                CodeParameterKind.PathParameters => "$pathParameters",
                CodeParameterKind.RequestAdapter => RequestAdapterPropertyName,
                CodeParameterKind.RequestBody => "$body",
                CodeParameterKind.RawUrl => "$rawUrl",
                CodeParameterKind.Serializer => "$writer",
                CodeParameterKind.ResponseHandler => "$responseHandler",
                CodeParameterKind.SetterValue => "$value",
                CodeParameterKind.Path => "$urlTemplate",
                _ => $"${parameter.Name.ToFirstCharacterLowerCase()}"
            };
        }
        public override string GetParameterSignature(CodeParameter parameter, CodeElement targetElement)
        {
            
            var typeString = GetTypeString(parameter.Type, parameter);
            var parameterSuffix = parameter.ParameterKind switch
            {
                CodeParameterKind.Headers or CodeParameterKind.Options => $"array ${(parameter.ParameterKind == CodeParameterKind.Options ? "options" : "headers")}",
                CodeParameterKind.RequestBody => $"{typeString} $body",
                CodeParameterKind.RequestAdapter => $"RequestAdapter {GetParameterName(parameter)}",
                CodeParameterKind.ResponseHandler => $"ResponseHandler {GetParameterName(parameter)}",
                CodeParameterKind.QueryParameter => $"GetQueryParameters {GetParameterName(parameter)}",
                CodeParameterKind.Serializer => $"SerializationWriter {GetParameterName(parameter)}",
                CodeParameterKind.BackingStore => $"BackingStore {GetParameterName(parameter)}",
                CodeParameterKind.PathParameters => $"array {GetParameterName(parameter)}",
                _ => $"{typeString} {GetParameterName(parameter)}"

            };
            return $"{(!parameter.Optional ? String.Empty : "?")}{parameterSuffix}";
        }
        public string GetParameterSignature(CodeParameter parameter, CodeMethod codeMethod)
        {
            if (codeMethod?.AccessedProperty != null && codeMethod.AccessedProperty.IsOfKind(CodePropertyKind.AdditionalData))
            {
                return "array $value";
            }
            
            return GetParameterSignature(parameter, codeMethod as CodeElement);
        }

        public string GetParameterDocNullable(CodeParameter parameter, CodeElement codeElement)
        {
            var parameterSignature = GetParameterSignature(parameter, codeElement).Trim().Split(' ');
            return parameter.Optional switch
            {
                true => $"{parameterSignature[0].Trim('?')}|null {parameterSignature[1]}",
                _ => string.Join(' ', parameterSignature)
            };
        }

        private static string RemoveInvalidDescriptionCharacters(string originalDescription) => originalDescription?.Replace("\\", "/");
        public override void WriteShortDescription(string description, LanguageWriter writer)
        {
            
            if (!String.IsNullOrEmpty(description))
            {
                writer.WriteLine(DocCommentStart);
                writer.WriteLine(
                    $"{DocCommentPrefix}{RemoveInvalidDescriptionCharacters(description)}");
                writer.WriteLine(DocCommentEnd);
            }
        }

        public void AddRequestBuilderBody(bool addCurrentPathProperty, string returnType, LanguageWriter writer, string suffix = default, string additionalPathParameters = default)
        {
            writer.WriteLines($"return new {returnType}($this->{RemoveDollarSignFromPropertyName(PathParametersPropertyName)}{suffix}, $this->{RemoveDollarSignFromPropertyName(RequestAdapterPropertyName)});");
        }

        private static string RemoveDollarSignFromPropertyName(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName) || propertyName.Length < 2)
            {
                throw new ArgumentException(nameof(propertyName) + " must not be null and have at least 2 characters.");
            }
            
            return propertyName[1..];
        }

        public void WritePhpDocumentStart(LanguageWriter writer)
        {
            writer.WriteLines("<?php", string.Empty);
        }

        public void WriteCodeBlockEnd(LanguageWriter writer)
        {
            writer.DecreaseIndent();
            writer.WriteLine("}");
        }
        
        /**
         * For Php strings, having double quotes around strings might cause an issue
         * if the string contains valid variable name.
         * For example $variable = "$value" will try too set the value of
         * $variable to the variable named $value rather than the string '$value'
         * around quotes as expected.
         */
        public string ReplaceDoubleQuoteWithSingleQuote(string current)
        {
            if (string.IsNullOrEmpty(current))
            {
                return current;
            }
            return current.StartsWith("\"", StringComparison.OrdinalIgnoreCase) ? current.Replace('\"', '\'') : current;
        }

        public void WriteNamespaceAndImports(CodeClass.Declaration codeElement, LanguageWriter writer)
        {
            bool hasUse = false;
            if (codeElement?.Parent?.Parent is CodeNamespace codeNamespace)
            {
                writer.WriteLine($"namespace {ReplaceDotsWithSlashInNamespaces(codeNamespace.Name)};");
                writer.WriteLine();
                codeElement.Usings?
                    .Where(x => x.Declaration.IsExternal ||
                                !x.Declaration.Name.Equals(codeElement.Name, StringComparison.OrdinalIgnoreCase))
                    .Select(x =>
                        x.Declaration.IsExternal
                            ? $"use {ReplaceDotsWithSlashInNamespaces(x.Declaration.Name)}\\{ReplaceDotsWithSlashInNamespaces(x.Name)};"
                            : $"use {ReplaceDotsWithSlashInNamespaces(x.Name)}\\{ReplaceDotsWithSlashInNamespaces(x.Declaration.Name)};")
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList()
                    .ForEach(x =>
                    {
                        hasUse = true;
                        writer.WriteLine(x);
                    });
            }
            if (hasUse)
            {
                writer.WriteLine(string.Empty);
            }
        }

        public string ReplaceDotsWithSlashInNamespaces(string namespaced)
        {
            var parts = namespaced.Split('.');
            return string.Join('\\', parts.Select(x => x.ToFirstCharacterUpperCase()));
        }
        internal void AddRequestBuilderBody(CodeClass parentClass, string returnType, LanguageWriter writer, string urlTemplateVarName = default, IEnumerable<CodeParameter> pathParameters = default) {
            var codeParameters = pathParameters as CodeParameter[] ?? pathParameters?.ToArray();
            var codePathParametersSuffix = !(codeParameters?.Any() ?? false) ? string.Empty : $", {string.Join(", ", codeParameters.Select(x => $"{x.Name.ToFirstCharacterLowerCase()}"))}";
            var pathParametersProperty = parentClass.GetPropertyOfKind(CodePropertyKind.PathParameters);
            var requestAdapterProp = parentClass.GetPropertyOfKind(CodePropertyKind.RequestAdapter);
            var urlTemplateParams = urlTemplateVarName ?? $"$this->{pathParametersProperty.Name}";
            writer.WriteLines($"return new {returnType}(${urlTemplateParams}, $this->{requestAdapterProp.Name}{codePathParametersSuffix});");
        }
        internal void AddParametersAssignment(LanguageWriter writer, CodeTypeBase pathParametersType, string pathParametersReference, params (CodeTypeBase, string, string)[] parameters) {
            if(pathParametersType == null) return;
            writer.WriteLine($"${TempDictionaryVarName} = {pathParametersReference};");
            if(parameters.Any())
                writer.WriteLines(parameters.Select(p => 
                    $"${TempDictionaryVarName}['{p.Item2}'] = {p.Item3};"
                ).ToArray());
        }
    }
}