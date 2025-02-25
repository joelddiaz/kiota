﻿using System;
using System.Linq;

namespace Kiota.Builder
{
    public enum CodePropertyKind
    {
        Custom,
        RequestBuilder,
        AdditionalData,
        BackingStore,
        UrlTemplate,
        /// <summary>
        /// The set of parameters to be carried over to the next request builder.
        /// </summary>
        PathParameters,
        RequestAdapter,
        /// <summary>
        /// The request body. Used when request parameters are wrapped in a classs.
        /// </summary>
        RequestBody,
        /// <summary>
        /// The request query parameters. Used when request parameters are wrapped in a classs.
        /// </summary>
        QueryParameter,
        /// <summary>
        /// The request headers. Used when request parameters are wrapped in a classs.
        /// </summary>
        Headers,
        /// <summary>
        /// The request middleware options. Used when request parameters are wrapped in a classs.
        /// </summary>
        Options,
        /// <summary>
        /// The request response handler. Used when request parameters are wrapped in a classs.
        /// </summary>
        ResponseHandler,
    }

    public class CodeProperty : CodeTerminal, IDocumentedElement
    {
        public CodePropertyKind PropertyKind {get;set;} = CodePropertyKind.Custom;
        public bool ReadOnly {get;set;} = false;
        public AccessModifier Access {get;set;} = AccessModifier.Public;
        private CodeTypeBase type;
        public CodeTypeBase Type {get => type ;set {
            EnsureElementsAreChildren(value);
            type = value;
        }}
        public string DefaultValue {get;set;}
        public string Description {get; set;}
        public string SerializationName { get; set; }
        public string NamePrefix { get; set; }
        public bool IsOfKind(params CodePropertyKind[] kinds) {
            return kinds?.Contains(PropertyKind) ?? false;
        }
    }
}
