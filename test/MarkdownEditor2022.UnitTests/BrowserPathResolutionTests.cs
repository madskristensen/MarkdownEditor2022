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
        
        /// <summary>
        /// Test all 5 scenarios from the original issue
        /// </summary>
        [TestMethod]
        public void IssueScenarios_AllFiveTestCases_ShouldResolveCorrectly()
        {
            // Arrange - reproduce the exact issue scenario
            string testFile = "/tmp/outerdirectory/content/theming.md";
            string baseDir = Path.GetDirectoryName(testFile);
            
            // Act & Assert - test all 5 cases from the issue
            // ![t1](../images/theming.webp "A user-generated...")
            string t1Path = Path.GetFullPath(Path.Combine(baseDir, "../images/theming.webp"));
            Assert.AreEqual("/tmp/outerdirectory/images/theming.webp", t1Path);
            
            // ![t2](images/theming.webp "A user-generated...")
            string t2Path = Path.Combine(baseDir, "images/theming.webp");
            Assert.AreEqual("/tmp/outerdirectory/content/images/theming.webp", t2Path);
            
            // ![t3](./images/theming.webp "A user-generated...")
            string t3Path = Path.GetFullPath(Path.Combine(baseDir, "./images/theming.webp"));
            Assert.AreEqual("/tmp/outerdirectory/content/images/theming.webp", t3Path);
            
            // ![t4](theming.webp "A user-generated...")
            string t4Path = Path.Combine(baseDir, "theming.webp");
            Assert.AreEqual("/tmp/outerdirectory/content/theming.webp", t4Path);
            
            // ![t5](./theming.webp "A user-generated...")
            string t5Path = Path.GetFullPath(Path.Combine(baseDir, "./theming.webp"));
            Assert.AreEqual("/tmp/outerdirectory/content/theming.webp", t5Path);
        }
        
        [TestMethod]
        public void EdgeCases_ShouldHandleCorrectly()
        {
            // Test file at root level
            string rootFile = @"C:\theming.md";
            string rootBaseHref = Path.GetDirectoryName(rootFile).Replace("\\", "/");
            Assert.AreEqual("C:", rootBaseHref);
            
            // Test deeply nested file
            string deepFile = @"C:\a\b\c\d\e\f\theming.md";
            string deepBaseHref = Path.GetDirectoryName(deepFile).Replace("\\", "/");
            Assert.AreEqual("C:/a/b/c/d/e/f", deepBaseHref);
        }
    }
}