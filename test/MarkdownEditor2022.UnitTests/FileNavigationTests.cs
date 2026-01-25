using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    /// <summary>
    /// Tests for file navigation logic used in Browser.HandleFileNavigationAsync.
    /// These test the algorithms without VS API dependencies.
    /// </summary>
    [TestClass]
    public class FileNavigationTests
    {
        private static readonly string[] _markdownExtensions = { ".md", ".markdown", ".mdown", ".mkd" };

        #region Internal Anchor Detection Tests

        /// <summary>
        /// Determines if a navigation is to an internal anchor (same file with fragment).
        /// Mirrors logic from HandleFileNavigationAsync.
        /// </summary>
        private static bool IsInternalAnchor(string filePath, string currentFile, string fragment)
        {
            bool hasFragment = !string.IsNullOrEmpty(fragment?.TrimStart('#'));
            return hasFragment && filePath.Equals(currentFile, StringComparison.OrdinalIgnoreCase);
        }

        [TestMethod]
        public void IsInternalAnchor_SameFileWithFragment_ReturnsTrue()
        {
            string filePath = @"C:\Projects\Docs\readme.md";
            string currentFile = @"C:\Projects\Docs\readme.md";
            string fragment = "#section-1";

            bool result = IsInternalAnchor(filePath, currentFile, fragment);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsInternalAnchor_SameFileCaseInsensitive_ReturnsTrue()
        {
            string filePath = @"C:\Projects\Docs\README.MD";
            string currentFile = @"C:\Projects\Docs\readme.md";
            string fragment = "#section-1";

            bool result = IsInternalAnchor(filePath, currentFile, fragment);

            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsInternalAnchor_DifferentFileWithFragment_ReturnsFalse()
        {
            string filePath = @"C:\Projects\Docs\other.md";
            string currentFile = @"C:\Projects\Docs\readme.md";
            string fragment = "#section-1";

            bool result = IsInternalAnchor(filePath, currentFile, fragment);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsInternalAnchor_SameFileNoFragment_ReturnsFalse()
        {
            string filePath = @"C:\Projects\Docs\readme.md";
            string currentFile = @"C:\Projects\Docs\readme.md";
            string fragment = null;

            bool result = IsInternalAnchor(filePath, currentFile, fragment);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsInternalAnchor_SameFileEmptyFragment_ReturnsFalse()
        {
            string filePath = @"C:\Projects\Docs\readme.md";
            string currentFile = @"C:\Projects\Docs\readme.md";
            string fragment = "";

            bool result = IsInternalAnchor(filePath, currentFile, fragment);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsInternalAnchor_FragmentWithHashOnly_ReturnsFalse()
        {
            string filePath = @"C:\Projects\Docs\readme.md";
            string currentFile = @"C:\Projects\Docs\readme.md";
            string fragment = "#";

            bool result = IsInternalAnchor(filePath, currentFile, fragment);

            Assert.IsFalse(result);
        }

        #endregion

        #region Markdown Extension Fallback Tests

        /// <summary>
        /// Tries to find a file by adding markdown extensions when no extension is specified.
        /// Mirrors logic from HandleFileNavigationAsync.
        /// </summary>
        private static string TryFindWithMarkdownExtension(string filePath, Func<string, bool> fileExists)
        {
            if (!string.IsNullOrEmpty(Path.GetExtension(filePath)))
            {
                return null; // Already has extension, no fallback needed
            }

            foreach (string ext in _markdownExtensions)
            {
                string withExt = filePath + ext;
                if (fileExists(withExt))
                {
                    return withExt;
                }
            }

            return null;
        }

        [TestMethod]
        public void TryFindWithMarkdownExtension_NoExtension_FindsMdFile()
        {
            string filePath = @"C:\Projects\Docs\readme";
            bool fileExists(string path) => path == @"C:\Projects\Docs\readme.md";

            string result = TryFindWithMarkdownExtension(filePath, fileExists);

            Assert.AreEqual(@"C:\Projects\Docs\readme.md", result);
        }

        [TestMethod]
        public void TryFindWithMarkdownExtension_NoExtension_FindsMarkdownFile()
        {
            string filePath = @"C:\Projects\Docs\readme";
            bool fileExists(string path) => path == @"C:\Projects\Docs\readme.markdown";

            string result = TryFindWithMarkdownExtension(filePath, fileExists);

            Assert.AreEqual(@"C:\Projects\Docs\readme.markdown", result);
        }

        [TestMethod]
        public void TryFindWithMarkdownExtension_NoExtension_PrioritizesMd()
        {
            string filePath = @"C:\Projects\Docs\readme";
            // Both .md and .markdown exist - should return .md (first in list)
            bool fileExists(string path) =>
                path == @"C:\Projects\Docs\readme.md" ||
                path == @"C:\Projects\Docs\readme.markdown";

            string result = TryFindWithMarkdownExtension(filePath, fileExists);

            Assert.AreEqual(@"C:\Projects\Docs\readme.md", result);
        }

        [TestMethod]
        public void TryFindWithMarkdownExtension_NoExtension_NoFileFound_ReturnsNull()
        {
            string filePath = @"C:\Projects\Docs\readme";
            bool fileExists(string path) => false;

            string result = TryFindWithMarkdownExtension(filePath, fileExists);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void TryFindWithMarkdownExtension_HasExtension_ReturnsNull()
        {
            string filePath = @"C:\Projects\Docs\readme.txt";
            bool fileExists(string path) => true;

            string result = TryFindWithMarkdownExtension(filePath, fileExists);

            Assert.IsNull(result); // No fallback when extension already present
        }

        #endregion

        #region Virtual Host Path Conversion Tests

        /// <summary>
        /// Converts a virtual host URI path to an absolute file system path.
        /// Mirrors logic from BrowserNavigationStarting for browsing-file-host URIs.
        /// </summary>
        private static string ConvertVirtualHostPathToAbsolute(string localPath, string currentFile)
        {
            string driveRoot = Path.GetPathRoot(currentFile);
            string normalizedPath = localPath.Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(driveRoot, normalizedPath);
        }

        [TestMethod]
        public void ConvertVirtualHostPath_SimplePath_ResolvesToDriveRoot()
        {
            string localPath = "Projects/Docs/file.md";
            string currentFile = @"C:\Users\Dev\readme.md";

            string result = ConvertVirtualHostPathToAbsolute(localPath, currentFile);

            Assert.AreEqual(@"C:\Projects\Docs\file.md", result);
        }

        [TestMethod]
        public void ConvertVirtualHostPath_PathWithSpaces_Preserved()
        {
            string localPath = "My Projects/My Docs/file.md";
            string currentFile = @"D:\Work\readme.md";

            string result = ConvertVirtualHostPathToAbsolute(localPath, currentFile);

            Assert.AreEqual(@"D:\My Projects\My Docs\file.md", result);
        }

        [TestMethod]
        public void ConvertVirtualHostPath_DifferentDrive_UsesDriveFromCurrentFile()
        {
            string localPath = "folder/file.md";
            string currentFile = @"E:\Projects\readme.md";

            string result = ConvertVirtualHostPathToAbsolute(localPath, currentFile);

            Assert.AreEqual(@"E:\folder\file.md", result);
        }

        #endregion

        #region Should Create Markdown File Tests

        /// <summary>
        /// Determines if a non-existent file should be offered for creation.
        /// Only markdown files (or files without extension) should be offered.
        /// </summary>
        private static bool ShouldOfferToCreateFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);

            bool isMarkdownFile = !string.IsNullOrEmpty(extension) &&
                                  Array.IndexOf(_markdownExtensions, extension.ToLowerInvariant()) >= 0;
            bool noExtension = string.IsNullOrEmpty(extension);

            return isMarkdownFile || noExtension;
        }

        [DataRow(@"C:\Docs\file.md", true)]
        [DataRow(@"C:\Docs\file.markdown", true)]
        [DataRow(@"C:\Docs\file.mdown", true)]
        [DataRow(@"C:\Docs\file.mkd", true)]
        [DataRow(@"C:\Docs\file.MD", true)]
        [DataRow(@"C:\Docs\file", true)]  // No extension - will add .md
        [DataRow(@"C:\Docs\file.txt", false)]
        [DataRow(@"C:\Docs\file.html", false)]
        [DataRow(@"C:\Docs\file.pdf", false)]
        [DataRow(@"C:\Docs\file.png", false)]
        [TestMethod]
        public void ShouldOfferToCreateFile_VariousExtensions_ReturnsExpected(string filePath, bool expected)
        {
            bool result = ShouldOfferToCreateFile(filePath);

            Assert.AreEqual(expected, result);
        }

        #endregion

        #region Fragment Extraction Tests

        /// <summary>
        /// Extracts and normalizes the fragment from a URI fragment string.
        /// </summary>
        private static string ExtractFragment(string fragment)
        {
            return fragment?.TrimStart('#');
        }

        [TestMethod]
        public void ExtractFragment_WithHash_ReturnsContent()
        {
            string result = ExtractFragment("#section-1");

            Assert.AreEqual("section-1", result);
        }

        [TestMethod]
        public void ExtractFragment_WithoutHash_ReturnsUnchanged()
        {
            string result = ExtractFragment("section-1");

            Assert.AreEqual("section-1", result);
        }

        [TestMethod]
        public void ExtractFragment_EmptyString_ReturnsEmpty()
        {
            string result = ExtractFragment("");

            Assert.AreEqual("", result);
        }

        [TestMethod]
        public void ExtractFragment_Null_ReturnsNull()
        {
            string result = ExtractFragment(null);

            Assert.IsNull(result);
        }

        [TestMethod]
        public void ExtractFragment_MultipleHashes_RemovesAllLeading()
        {
            // TrimStart removes ALL leading occurrences, not just the first
            string result = ExtractFragment("##heading");

            Assert.AreEqual("heading", result);
        }

        #endregion
    }
}
