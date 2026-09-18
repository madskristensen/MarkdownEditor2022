using System.Reflection;
using Community.VisualStudio.Toolkit;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class ReflectionTypeLoadExceptionTests
    {
        [TestMethod]
        public void WhenFindingInitializeAsyncMethodOnCommandTypeThenGetMethodReturnsMethodInfo()
        {
            Type commandType = typeof(GenerateTocCommand);

            MethodInfo method = commandType.GetMethod(
                nameof(BaseCommand<object>.InitializeAsync),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

            Assert.IsNotNull(method);
            Assert.AreEqual("InitializeAsync", method.Name);
            Assert.IsTrue(method.IsStatic);
        }
    }
}
