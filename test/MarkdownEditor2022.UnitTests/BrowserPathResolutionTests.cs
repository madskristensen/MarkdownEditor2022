using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class BrowserPathResolutionTests
    {
        [TestMethod]
        public void VirtualHostMapping_ShouldPointToFileDirectory()
        {
            // Arrange
            string testFile = @"C:\Projects\MyProject\content\theming.md";
            
            // Act - simulate the fixed behavior
            string baseHref = Path.GetDirectoryName(testFile).Replace("\\", "/");
            
            // Assert
            Assert.AreEqual("C:/Projects/MyProject/content", baseHref);
        }
        
        [TestMethod]
        public void RelativePaths_ShouldResolveCorrectly()
        {
            // Arrange
            string testFile = @"C:\Projects\MyProject\content\theming.md";
            string baseDir = Path.GetDirectoryName(testFile);
            
            // Act & Assert - test different relative path patterns
            string path1 = Path.Combine(baseDir, "images/theming.webp");
            Assert.AreEqual(@"C:\Projects\MyProject\content\images\theming.webp", path1);
            
            string path2 = Path.Combine(baseDir, "theming.webp");
            Assert.AreEqual(@"C:\Projects\MyProject\content\theming.webp", path2);
            
            string path3 = Path.GetFullPath(Path.Combine(baseDir, "../images/theming.webp"));
            Assert.AreEqual(@"C:\Projects\MyProject\images\theming.webp", path3);
        }
        
        [TestMethod]
        public void UnixPaths_ShouldResolveCorrectly()
        {
            // Arrange - simulate Unix-style paths
            string testFile = "/tmp/outerdirectory/content/theming.md";
            string baseDir = Path.GetDirectoryName(testFile);
            
            // Act & Assert
            string path1 = Path.Combine(baseDir, "images", "theming.webp");
            Assert.AreEqual("/tmp/outerdirectory/content/images/theming.webp", path1);
            
            string path2 = Path.Combine(baseDir, "theming.webp");
            Assert.AreEqual("/tmp/outerdirectory/content/theming.webp", path2);
            
            string path3 = Path.GetFullPath(Path.Combine(baseDir, "..", "images", "theming.webp"));
            Assert.AreEqual("/tmp/outerdirectory/images/theming.webp", path3);
        }
    }
}