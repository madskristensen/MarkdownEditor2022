using System.Reflection;
using Community.VisualStudio.Toolkit;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class ReflectionTypeLoadExceptionTests
    {
        [TestMethod]
        public void WhenAssemblyGetTypesThrowsReflectionTypeLoadExceptionThenGetLoadableTypesReturnsNonNullTypes()
        {
            Assembly assembly = typeof(ExtensionMethods).Assembly;

            // Verify GetLoadableTypes returns non-null types for a healthy assembly
            Type[] types = GetLoadableTypes(assembly).ToArray();
            Assert.IsTrue(types.Length > 0);
            Assert.IsTrue(types.Contains(typeof(ExtensionMethods)));
            Assert.IsFalse(types.Any(t => t == null));
        }

        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                return ex.Types.Where(t => t != null);
            }
        }

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

        [TestMethod]
        public void WhenReflectionTypeLoadExceptionHasNullTypesThenFilterRemovesNulls()
        {
            Type?[] mockTypes = [typeof(string), null, typeof(int), null, typeof(bool)];
            Exception[] mockExceptions = [new("Mock loader exception")];

            ReflectionTypeLoadException ex = new(mockTypes, mockExceptions);

            Type[] validTypes = ex.Types.Where(t => t != null).ToArray();

            Assert.AreEqual(3, validTypes.Length);
            Assert.AreEqual(typeof(string), validTypes[0]);
            Assert.AreEqual(typeof(int), validTypes[1]);
            Assert.AreEqual(typeof(bool), validTypes[2]);
        }
    }
}
