using System;
using Xunit;

namespace Kiota.Builder.Tests {
    public class CodeParameterTests {
        [Fact]
        public void Defensive() {
            var parameter = new CodeParameter {
                Name = "class",
            };
            Assert.False(parameter.IsOfKind((CodeParameterKind[])null));
            Assert.False(parameter.IsOfKind(Array.Empty<CodeParameterKind>()));
        }
        [Fact]
        public void IsOfKind() {
            var parameter = new CodeParameter {
                Name = "class",
            };
            Assert.False(parameter.IsOfKind(CodeParameterKind.Headers));
            parameter.ParameterKind = CodeParameterKind.RequestAdapter;
            Assert.True(parameter.IsOfKind(CodeParameterKind.RequestAdapter));
            Assert.True(parameter.IsOfKind(CodeParameterKind.RequestAdapter, CodeParameterKind.Headers));
            Assert.False(parameter.IsOfKind(CodeParameterKind.Headers));
        }
    }
}
