using System.Xml.Linq;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class VsctToolbarContractTests
    {
        private static readonly XNamespace _ns = "http://schemas.microsoft.com/VisualStudio/2005-10-18/CommandTable";

        [TestMethod]
        public void MarkdownToolbar_ContainsViewAndFormattingGroups()
        {
            XDocument document = LoadCommandTable();
            XElement toolbar = FindElement(document, "Menu", "MarkdownDocumentToolbar");

            Assert.AreEqual("Toolbar", (string)toolbar.Attribute("type"));
            AssertParent(document, "Group", "ToolbarViewGroup", "MarkdownDocumentToolbar");
            AssertParent(document, "Group", "ToolbarFormattingGroup", "MarkdownDocumentToolbar");
            AssertParent(document, "Group", "ToolbarListGroup", "MarkdownDocumentToolbar");
            AssertParent(document, "Button", "ShowSource", "ToolbarViewGroup");
            AssertParent(document, "Button", "ShowSplit", "ToolbarViewGroup");
            AssertParent(document, "Button", "ShowPreview", "ToolbarViewGroup");
        }

        [TestMethod]
        public void HeadingMenu_UsesDynamicAnchorText()
        {
            XElement menu = FindElement(LoadCommandTable(), "Menu", "HeadingMenu");
            string[] flags = menu.Elements(_ns + "CommandFlag").Select(element => element.Value).ToArray();

            CollectionAssert.IsSubsetOf(
                new[] { "TextOnly", "TextChanges", "TextIsAnchorCommand", "DontCache" },
                flags);
            Assert.AreEqual("Paragraph", menu.Element(_ns + "Strings")?.Element(_ns + "ButtonText")?.Value);
        }

        [DataRow("MakeHighlight", "HighlightText")]
        [DataRow("MakeSubscript", "Subscript")]
        [DataRow("MakeSuperscript", "Superscript")]
        [TestMethod]
        public void FormattingButton_UsesExpectedMoniker(string commandId, string moniker)
        {
            XElement button = FindElement(LoadCommandTable(), "Button", commandId);
            XElement icon = button.Element(_ns + "Icon");

            Assert.IsNotNull(icon);
            Assert.AreEqual("ImageCatalogGuid", (string)icon.Attribute("guid"));
            Assert.AreEqual(moniker, (string)icon.Attribute("id"));
            Assert.IsTrue(button.Elements(_ns + "CommandFlag").Any(flag => flag.Value == "IconIsMoniker"));
        }

        [DataRow("TogglePreview", "")]
        [DataRow("CyclePreviewBackward", "Shift")]
        [TestMethod]
        public void ViewCycleKeyBinding_UsesF7(string commandId, string modifier)
        {
            XDocument document = LoadCommandTable();
            XElement binding = document.Descendants(_ns + "KeyBinding")
                .Single(element => (string)element.Attribute("id") == commandId);

            Assert.AreEqual("EditorFactory", (string)binding.Attribute("editor"));
            Assert.AreEqual("VK_F7", (string)binding.Attribute("key1"));
            Assert.AreEqual(modifier, (string)binding.Attribute("mod1"));
        }

        private static void AssertParent(XDocument document, string elementName, string id, string parentId)
        {
            XElement element = FindElement(document, elementName, id);
            Assert.AreEqual(parentId, (string)element.Element(_ns + "Parent")?.Attribute("id"));
        }

        private static XElement FindElement(XDocument document, string elementName, string id)
        {
            XElement element = document.Descendants(_ns + elementName)
                .SingleOrDefault(candidate => (string)candidate.Attribute("id") == id);
            Assert.IsNotNull(element, $"{elementName} '{id}' was not found in VSCommandTable.vsct.");
            return element;
        }

        private static XDocument LoadCommandTable()
        {
            DirectoryInfo directory = new(AppContext.BaseDirectory);
            while (directory != null)
            {
                string path = Path.Combine(directory.FullName, "src", "VSCommandTable.vsct");
                if (File.Exists(path))
                {
                    return XDocument.Load(path);
                }

                directory = directory.Parent;
            }

            Assert.Fail("Could not locate src\\VSCommandTable.vsct.");
            return null;
        }
    }
}
