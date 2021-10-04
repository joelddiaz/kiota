﻿using System;
using System.Linq;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.Php
{
    public class PhpConventionService: CommonLanguageConventionService
    {
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

        public override string PathSegmentPropertyName => "$pathSegment";

        public override string CurrentPathPropertyName => "$currentPath";

        public override string HttpCorePropertyName => "$httpCore";
        public override string RawUrlPropertyName
        {
            get;
        }

        public override string ParseNodeInterfaceName => "ParseNode";

        public string DocCommentStart = "/**";
        public string DocCommentEnd = "*/";

        public override string GetTypeString(CodeTypeBase code)
        {
            return TranslateType(code.Name);
        }

        public override string TranslateType(CodeType type)
        {
            throw new NotImplementedException();
        }

        public string TranslateType(string typeName)
        {
            return (typeName) switch
            {
                "boolean" => "bool",
                "double" or "decimal" => "float",
                "integer" => "int",
                "object" or "string" or "array" or "float" or "void" => typeName,
                _ => typeName.ToFirstCharacterUpperCase()
            };
        }

        public override string GetParameterSignature(CodeParameter parameter)
        {
            
            var typeString = GetTypeString(parameter.Type);
            var parameterSuffix = parameter.ParameterKind switch
            {
                CodeParameterKind.Headers => "array $headers",
                CodeParameterKind.RequestBody => $"{typeString} $body",
                CodeParameterKind.HttpCore => "HttpCoreInterface $httpCore",
                CodeParameterKind.Options => "array $options",
                CodeParameterKind.ResponseHandler => "ResponseHandlerInterface $responseHandler",
                _ => $"{GetTypeString(parameter.Type)} ${parameter.Name.ToFirstCharacterLowerCase()}"

            };
            return $"{(parameter.Optional ? String.Empty : "?")}{parameterSuffix}";
        }

        public string GetParameterSignature(CodeParameter parameter, CodeMethod codeMethod)
        {
            if (codeMethod?.AccessedProperty != null && codeMethod.AccessedProperty.IsOfKind(CodePropertyKind.AdditionalData))
            {
                return "array $value";
            }
            return GetParameterSignature(parameter);
        }

        public string GetParameterDocNullable(CodeParameter parameter)
        {
            var parameterSignature = GetParameterSignature(parameter).Trim().Split(' ');
            return parameter.Optional switch
            {
                true => $"{parameterSignature[0]}|null {parameterSignature[1]}",
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

        public void AddRequestBuilderBody(bool addCurrentPathProperty, string returnType, LanguageWriter writer, string suffix = default)
        {
            var currentPath = addCurrentPathProperty ? $"$this->{RemoveDollarSignFromPropertyName(CurrentPathPropertyName)} . " : string.Empty;
            writer.WriteLines($"return new {returnType}({currentPath}$this->{RemoveDollarSignFromPropertyName(PathSegmentPropertyName)}{suffix}, $this->{RemoveDollarSignFromPropertyName(HttpCorePropertyName)});");
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
            if (current.StartsWith("\"", StringComparison.OrdinalIgnoreCase))
            {
                return current.Replace('\"', '\'');
            }
            return current;
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

        private static string ReplaceDotsWithSlashInNamespaces(string namespaced)
        {
            var parts = namespaced.Split('.');
            return string.Join('\\', parts.Select(x => x.ToFirstCharacterUpperCase()));
        }
    }
}