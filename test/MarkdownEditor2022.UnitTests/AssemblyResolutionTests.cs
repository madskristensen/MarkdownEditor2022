using System.Reflection;
using Microsoft.VisualStudio.Shell;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class AssemblyResolutionTests
    {
        private static readonly string[] _disallowedGlobalRedirects =
        [
            "Markdig",
            "System.Memory",
            "System.Buffers",
            "System.Numerics.Vectors",
            "System.Runtime.CompilerServices.Unsafe"
        ];

        [TestMethod]
        public void ExtensionAssembly_DoesNotRegisterGlobalBindingRedirects_ForMarkdigOrBclDependencies()
        {
            Assembly extensionAssembly = typeof(AssemblyResolution).Assembly;
            string[] redirectedNames = extensionAssembly
                .GetCustomAttributes<ProvideBindingRedirectionAttribute>()
                .Select(attr => attr.AssemblyName)
                .ToArray();

            foreach (string disallowed in _disallowedGlobalRedirects)
            {
                Assert.IsFalse(
                    redirectedNames.Contains(disallowed, StringComparer.OrdinalIgnoreCase),
                    $"Unexpected global ProvideBindingRedirection for '{disallowed}'.");
            }
        }

        [TestMethod]
        public void ShouldHandleRequest_ReturnsTrue_ForExtensionRequestingScopedDependency()
        {
            Assembly extensionAssembly = typeof(AssemblyResolution).Assembly;

            Assert.IsTrue(AssemblyResolution.ShouldHandleRequest(
                extensionAssembly,
                "System.Memory, Version=4.0.5.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"));

            Assert.IsTrue(AssemblyResolution.ShouldHandleRequest(
                extensionAssembly,
                "Markdig, Version=1.3.0.0, Culture=neutral, PublicKeyToken=null"));
        }

        [TestMethod]
        public void ShouldHandleRequest_ReturnsFalse_ForExternalOrNullRequester()
        {
            Assert.IsFalse(AssemblyResolution.ShouldHandleRequest(
                null!,
                "System.Memory, Version=4.0.5.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"));

            Assert.IsFalse(AssemblyResolution.ShouldHandleRequest(
                typeof(object).Assembly,
                "System.Memory, Version=4.0.5.0, Culture=neutral, PublicKeyToken=cc7b13ffcd2ddd51"));

            Assert.IsFalse(AssemblyResolution.ShouldHandleRequest(
                typeof(AssemblyResolutionTests).Assembly,
                "System.Runtime.CompilerServices.Unsafe, Version=6.0.3.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a"));
        }

        [TestMethod]
        public void ShouldHandleRequest_ReturnsFalse_ForUnscopedAssembly()
        {
            Assembly extensionAssembly = typeof(AssemblyResolution).Assembly;

            Assert.IsFalse(AssemblyResolution.ShouldHandleRequest(
                extensionAssembly,
                "Newtonsoft.Json, Version=13.0.0.0, Culture=neutral, PublicKeyToken=30ad4fe6b2a6aeed"));
        }
    }
}
