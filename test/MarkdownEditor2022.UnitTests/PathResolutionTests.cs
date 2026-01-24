using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MarkdownEditor2022.UnitTests
{
    /// <summary>
    /// Tests for path resolution logic used in browser navigation and file creation.
    /// These test the same algorithms used in Browser.cs without VS API dependencies.
    /// </summary>
    [TestClass]
    public class PathResolutionTests
    {
        #region GetRelativePathForDisplay Tests

        /// <summary>
        /// Mirrors the GetRelativePathForDisplay method from Browser.cs for testing.
        /// </summary>
        private static string GetRelativePathForDisplay(string targetPath, string currentDir)
        {
            try
            {
                // Ensure currentDir ends with directory separator for proper URI construction
                string normalizedCurrentDir = currentDir;
                if (!normalizedCurrentDir.EndsWith(Path.DirectorySeparatorChar.ToString()))
                {
                    normalizedCurrentDir += Path.DirectorySeparatorChar;
                }

                Uri targetUri = new Uri(targetPath);
                Uri currentUri = new Uri(normalizedCurrentDir);
                Uri relativeUri = currentUri.MakeRelativeUri(targetUri);
                return Uri.UnescapeDataString(relativeUri.ToString().Replace('/', Path.DirectorySeparatorChar));
            }
            catch (UriFormatException)
            {
                return Path.GetFileName(targetPath);
            }
            catch (InvalidOperationException)
            {
                return Path.GetFileName(targetPath);
            }
        }

        [TestMethod]
        public void GetRelativePathForDisplay_FileInSameDirectory_ReturnsFileName()
        {
            string targetPath = @"C:\Projects\Docs\NewFile.md";
            string currentDir = @"C:\Projects\Docs";

            string result = GetRelativePathForDisplay(targetPath, currentDir);

            Assert.AreEqual("NewFile.md", result);
        }

        [TestMethod]
        public void GetRelativePathForDisplay_FileInSubdirectory_ReturnsRelativePath()
        {
            string targetPath = @"C:\Projects\Docs\SubFolder\NewFile.md";
            string currentDir = @"C:\Projects\Docs";

            string result = GetRelativePathForDisplay(targetPath, currentDir);

            Assert.AreEqual(@"SubFolder\NewFile.md", result);
        }

        [TestMethod]
        public void GetRelativePathForDisplay_FileInParentDirectory_ReturnsRelativePath()
        {
            string targetPath = @"C:\Projects\NewFile.md";
            string currentDir = @"C:\Projects\Docs";

            string result = GetRelativePathForDisplay(targetPath, currentDir);

            Assert.AreEqual(@"..\NewFile.md", result);
        }

        [TestMethod]
        public void GetRelativePathForDisplay_CurrentDirWithTrailingSeparator_ReturnsCorrectPath()
        {
            string targetPath = @"C:\Projects\Docs\NewFile.md";
            string currentDir = @"C:\Projects\Docs\";

            string result = GetRelativePathForDisplay(targetPath, currentDir);

            Assert.AreEqual("NewFile.md", result);
        }

        #endregion

        #region Virtual Host Path Resolution Tests

        /// <summary>
        /// Resolves a file path from a virtual host mapping, similar to Browser.cs logic.
        /// The virtual host maps to the parent of the current document's directory.
        /// </summary>
        private static string ResolvePathFromVirtualHost(string localPath, string currentDir)
        {
            // The browsing-file-host virtual host is mapped to the parent directory
            DirectoryInfo parentDir = new DirectoryInfo(currentDir).Parent;
            string baseDir = parentDir?.FullName ?? currentDir;
            return Path.GetFullPath(Path.Combine(baseDir, localPath));
        }

        [TestMethod]
        public void ResolvePathFromVirtualHost_FileInCurrentDir_ResolvesCorrectly()
        {
            // When clicking a link like "NewFile.md" from a file in C:\Projects\Docs\readme.md
            // The virtual host maps to C:\Projects (parent of Docs)
            // The browser URI path becomes "Docs/NewFile.md"
            string localPath = @"Docs\NewFile.md";
            string currentDir = @"C:\Projects\Docs";

            string result = ResolvePathFromVirtualHost(localPath, currentDir);

            // Should resolve to C:\Projects\Docs\NewFile.md, NOT C:\Projects\Docs\Docs\NewFile.md
            Assert.AreEqual(@"C:\Projects\Docs\NewFile.md", result);
        }

        [TestMethod]
        public void ResolvePathFromVirtualHost_FileInSubDir_ResolvesCorrectly()
        {
            // Link to "SubFolder/NewFile.md" from file in Docs
            // Browser path becomes "Docs/SubFolder/NewFile.md"
            string localPath = @"Docs\SubFolder\NewFile.md";
            string currentDir = @"C:\Projects\Docs";

            string result = ResolvePathFromVirtualHost(localPath, currentDir);

            Assert.AreEqual(@"C:\Projects\Docs\SubFolder\NewFile.md", result);
        }

        [TestMethod]
        public void ResolvePathFromVirtualHost_FileWithParentRef_ResolvesCorrectly()
        {
            // Link to "../OtherFolder/File.md" from file in Docs
            // Browser path becomes "OtherFolder/File.md" (resolved relative to Projects)
            string localPath = @"OtherFolder\File.md";
            string currentDir = @"C:\Projects\Docs";

            string result = ResolvePathFromVirtualHost(localPath, currentDir);

            Assert.AreEqual(@"C:\Projects\OtherFolder\File.md", result);
        }

        [TestMethod]
        public void ResolvePathFromVirtualHost_RootDirectory_FallsBackToCurrentDir()
        {
            // Edge case: current directory has no parent (root)
            string localPath = @"NewFile.md";
            string currentDir = @"C:\";

            string result = ResolvePathFromVirtualHost(localPath, currentDir);

            Assert.AreEqual(@"C:\NewFile.md", result);
        }

        #endregion

        #region Markdown Extension Detection Tests

        private static readonly string[] MarkdownExtensions = { ".md", ".markdown", ".mdown", ".mkd" };

        private static bool IsMarkdownExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return false;
            }

            return Array.IndexOf(MarkdownExtensions, extension.ToLowerInvariant()) >= 0;
        }

        [DataRow(".md", true)]
        [DataRow(".MD", true)]
        [DataRow(".Md", true)]
        [DataRow(".markdown", true)]
        [DataRow(".MARKDOWN", true)]
        [DataRow(".mdown", true)]
        [DataRow(".mkd", true)]
        [DataRow(".txt", false)]
        [DataRow(".html", false)]
        [DataRow("", false)]
        [DataRow(null, false)]
        [TestMethod]
        public void IsMarkdownExtension_VariousExtensions_ReturnsExpected(string extension, bool expected)
        {
            bool result = IsMarkdownExtension(extension);

            Assert.AreEqual(expected, result);
        }

        #endregion

        #region File Target Path Calculation Tests

        /// <summary>
        /// Calculates the target file path for creating a new file from a link.
        /// Adds .md extension if no extension is provided.
        /// </summary>
        private static string CalculateTargetFilePath(string file, string currentDir)
        {
            string extension = Path.GetExtension(file);
            string targetFile = string.IsNullOrEmpty(extension) ? file + ".md" : file;

            // Resolve relative to the parent directory (where virtual host maps)
            DirectoryInfo parentDir = new DirectoryInfo(currentDir).Parent;
            string baseDir = parentDir?.FullName ?? currentDir;
            return Path.GetFullPath(Path.Combine(baseDir, targetFile));
        }

        [TestMethod]
        public void CalculateTargetFilePath_NoExtension_AddsMdExtension()
        {
            string file = @"Docs\NewFile";
            string currentDir = @"C:\Projects\Docs";

            string result = CalculateTargetFilePath(file, currentDir);

            Assert.AreEqual(@"C:\Projects\Docs\NewFile.md", result);
        }

        [TestMethod]
        public void CalculateTargetFilePath_WithExtension_PreservesExtension()
        {
            string file = @"Docs\NewFile.markdown";
            string currentDir = @"C:\Projects\Docs";

            string result = CalculateTargetFilePath(file, currentDir);

            Assert.AreEqual(@"C:\Projects\Docs\NewFile.markdown", result);
        }

        [TestMethod]
        public void CalculateTargetFilePath_RelativePathUp_ResolvesCorrectly()
        {
            // "..\SiblingDir\File.md" relative to virtual host root
            string file = @"SiblingDir\File.md";
            string currentDir = @"C:\Projects\Docs";

            string result = CalculateTargetFilePath(file, currentDir);

            Assert.AreEqual(@"C:\Projects\SiblingDir\File.md", result);
        }

        #endregion
    }
}
