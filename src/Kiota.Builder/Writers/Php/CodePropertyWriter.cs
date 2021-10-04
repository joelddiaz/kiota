﻿using System.Diagnostics;
using System.Linq;
using Kiota.Builder.Extensions;

namespace Kiota.Builder.Writers.Php
{
    public class CodePropertyWriter: BaseElementWriter<CodeProperty, PhpConventionService>
    {
        public CodePropertyWriter(PhpConventionService conventionService) : base(conventionService) { }

        public override void WriteCodeElement(CodeProperty codeElement, LanguageWriter writer)
        {
            
            var returnType = conventions.GetTypeString(codeElement.Type);
            var currentPathProperty = codeElement.Parent.GetChildElements(true)
                .OfType<CodeProperty>()
                .FirstOrDefault(x => x.IsOfKind(CodePropertyKind.CurrentPath));
            
            switch (codeElement.PropertyKind)
            {
                case CodePropertyKind.RequestBuilder:
                    conventions.WriteShortDescription(codeElement.Description, writer);
                    writer.WriteLine($"{conventions.GetAccessModifier(codeElement.Access)} function {codeElement.Name.ToFirstCharacterLowerCase()}(): {returnType} {{");
                    writer.IncreaseIndent();
                    conventions.AddRequestBuilderBody(currentPathProperty != null, returnType, writer);
                    writer.DecreaseIndent();
                    writer.WriteLine("}");
                    break;
                case CodePropertyKind.HttpCore:
                    writer.WriteLine($"{conventions.GetAccessModifier(codeElement.Access)} HttpCoreInterface ${codeElement.Name.ToFirstCharacterLowerCase()};");
                    break;
                case CodePropertyKind.AdditionalData:
                    writer.WriteLine($"{conventions.GetAccessModifier(codeElement.Access)} array ${codeElement.Name.ToFirstCharacterLowerCase()};");
                    break;
                default:
                    WritePropertyDocComment(codeElement, writer);
                    writer.WriteLine($"{conventions.GetAccessModifier(codeElement.Access)} {returnType} ${codeElement.Name.ToFirstCharacterLowerCase()};");
                    break;
            }
            writer.WriteLine("");
        }

        private void WritePropertyDocComment(CodeProperty codeProperty, LanguageWriter writer)
        {
            var propertyDescription = codeProperty.Description;
            var hasDescription = !string.IsNullOrEmpty(codeProperty.Description);
            writer.WriteLine($"{conventions.DocCommentStart} @var {conventions.GetTypeString(codeProperty.Type)} ${codeProperty.Name} " +
                             $"{(hasDescription ? propertyDescription : string.Empty)} {conventions.DocCommentEnd}");
        }
    }
}