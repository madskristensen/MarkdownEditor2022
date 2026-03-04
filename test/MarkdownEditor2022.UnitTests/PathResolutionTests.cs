using System;
using System.IO;
using MarkdownEditor2022;
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

        #region Root-Relative Path Resolution Tests

        /// <summary>
        /// Mirrors the ResolveRelativePathsToAbsoluteUrls method from Browser.cs for testing root-relative paths.
        /// This simplified version only tests the root-relative path resolution logic.
        /// </summary>
        private static string ResolveRootRelativePath(string rootRelativePath, string rootPath, string driveRoot)
        {
            try
            {
                // Remove leading slash
                string pathWithoutLeadingSlash = rootRelativePath.TrimStart('/');

                // Normalize path separators
                pathWithoutLeadingSlash = pathWithoutLeadingSlash.Replace('/', Path.DirectorySeparatorChar);

                // Resolve against the root path
                string fullPath = Path.GetFullPath(Path.Combine(rootPath, pathWithoutLeadingSlash));

                // Convert to virtual host URL relative to drive root
                string relativeToDrive = fullPath;
                if (fullPath.StartsWith(driveRoot, StringComparison.OrdinalIgnoreCase))
                {
                    relativeToDrive = fullPath.Substring(driveRoot.Length);
                }
                string virtualUrl = "http://browsing-file-host/" + relativeToDrive.Replace(Path.DirectorySeparatorChar, '/');

                return virtualUrl;
            }
            catch
            {
                return null;
            }
        }

        [TestMethod]
        public void ResolveRootRelativePath_ImagePath_ReturnsCorrectVirtualUrl()
        {
            // Simulating Jekyll/GitHub Pages scenario where /images/test.png should resolve to
            // C:\Projects\blog\images\test.png when root_path is C:\Projects\blog
            string rootRelativePath = "/images/test.png";
            string rootPath = @"C:\Projects\blog";
            string driveRoot = @"C:\";

            string result = ResolveRootRelativePath(rootRelativePath, rootPath, driveRoot);

            Assert.AreEqual("http://browsing-file-host/Projects/blog/images/test.png", result);
        }

        [TestMethod]
        public void ResolveRootRelativePath_NestedPath_ReturnsCorrectVirtualUrl()
        {
            string rootRelativePath = "/assets/img/photo.jpg";
            string rootPath = @"C:\Users\Dev\website";
            string driveRoot = @"C:\";

            string result = ResolveRootRelativePath(rootRelativePath, rootPath, driveRoot);

            Assert.AreEqual("http://browsing-file-host/Users/Dev/website/assets/img/photo.jpg", result);
        }

        [TestMethod]
        public void ResolveRootRelativePath_SingleFile_ReturnsCorrectVirtualUrl()
        {
            string rootRelativePath = "/readme.md";
            string rootPath = @"C:\Projects";
            string driveRoot = @"C:\";

            string result = ResolveRootRelativePath(rootRelativePath, rootPath, driveRoot);

            Assert.AreEqual("http://browsing-file-host/Projects/readme.md", result);
        }

        [TestMethod]
        public void ResolveRootRelativePath_PathWithSpaces_ReturnsCorrectVirtualUrl()
        {
            string rootRelativePath = "/my docs/test file.md";
            string rootPath = @"C:\Projects\Site";
            string driveRoot = @"C:\";

            string result = ResolveRootRelativePath(rootRelativePath, rootPath, driveRoot);

            Assert.AreEqual("http://browsing-file-host/Projects/Site/my docs/test file.md", result);
        }

        #endregion

        #region HTML Generation Tests

        [TestMethod]
        public void IsMarkdownFile_MarkdownExtensions_ReturnsExpectedResult()
        {
            Assert.IsTrue(HtmlGenerationService.IsMarkdownFile(@"C:\Docs\file.md"));
            Assert.IsTrue(HtmlGenerationService.IsMarkdownFile(@"C:\Docs\file.rmd"));
            Assert.IsTrue(HtmlGenerationService.IsMarkdownFile(@"C:\Docs\file.mermaid"));
            Assert.IsTrue(HtmlGenerationService.IsMarkdownFile(@"C:\Docs\file.mmd"));
            Assert.IsFalse(HtmlGenerationService.IsMarkdownFile(@"C:\Docs\file.txt"));
        }

        [TestMethod]
        public void GetHtmlFileName_ReplacesExtensionWithHtml()
        {
            string result = HtmlGenerationService.GetHtmlFileName(@"C:\Docs\readme.md");

            Assert.AreEqual(@"C:\Docs\readme.html", result);
        }

        [TestMethod]
        public void CreateFromHtmlTemplate_WithTemplate_ReplacesTitleAndContent()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string markdownFile = Path.Combine(tempDir, "readme.md");
                string templateFile = Path.Combine(tempDir, "md-template.html");

                File.WriteAllText(markdownFile, "# Test");
                File.WriteAllText(templateFile, "<html><head><title>[title]</title></head><body>[content]</body></html>");

                string result = HtmlGenerationService.CreateFromHtmlTemplate(markdownFile, "My Title", "<p>Body</p>");

                Assert.AreEqual("<html><head><title>My Title</title></head><body><p>Body</p></body></html>", result);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CreateFromHtmlTemplate_WithoutContentToken_ReturnsHtmlContent()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string markdownFile = Path.Combine(tempDir, "readme.md");
                string templateFile = Path.Combine(tempDir, "md-template.html");

                File.WriteAllText(markdownFile, "# Test");
                File.WriteAllText(templateFile, "<html><body>no token</body></html>");

                string result = HtmlGenerationService.CreateFromHtmlTemplate(markdownFile, "Title", "<p>Body</p>");

                Assert.AreEqual("<p>Body</p>", result);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void BuildHtmlDocument_UsesFirstHeadingAsTitle()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string markdownFile = Path.Combine(tempDir, "readme.md");
                string templateFile = Path.Combine(tempDir, "md-template.html");

                File.WriteAllText(markdownFile, "# My Heading\n\nBody");
                File.WriteAllText(templateFile, "<html><head><title>[title]</title></head><body>[content]</body></html>");

                string result = HtmlGenerationService.BuildHtmlDocument(markdownFile);

                StringAssert.Contains(result, "<title>My Heading</title>");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void BuildHtmlDocument_NoHeading_UsesFileNameAsTitle()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string markdownFile = Path.Combine(tempDir, "notes.md");
                string templateFile = Path.Combine(tempDir, "md-template.html");

                File.WriteAllText(markdownFile, "plain text without heading");
                File.WriteAllText(templateFile, "<html><head><title>[title]</title></head><body>[content]</body></html>");

                string result = HtmlGenerationService.BuildHtmlDocument(markdownFile);

                StringAssert.Contains(result, "<title>notes</title>");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CreateFromHtmlTemplate_WhenTemplateResolutionThrows_FallsBackToRawHtml()
        {
            string markdownFile = "C:\\bad|path\\readme.md";

            string result = HtmlGenerationService.CreateFromHtmlTemplate(markdownFile, "Title", "<p>Body</p>");

            Assert.AreEqual("<p>Body</p>", result);
        }

        [TestMethod]
        public void CreateFromHtmlTemplate_PrefersNearestParentTemplate()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            string rootDir = Path.Combine(tempDir, "root");
            string nestedDir = Path.Combine(rootDir, "docs", "nested");
            Directory.CreateDirectory(nestedDir);

            try
            {
                string markdownFile = Path.Combine(nestedDir, "readme.md");
                string rootTemplate = Path.Combine(rootDir, "md-template.html");
                string nearestTemplate = Path.Combine(Path.Combine(rootDir, "docs"), "md-template.html");

                File.WriteAllText(markdownFile, "# Test");
                File.WriteAllText(rootTemplate, "<html><body>root-[content]</body></html>");
                File.WriteAllText(nearestTemplate, "<html><body>nearest-[content]</body></html>");

                string result = HtmlGenerationService.CreateFromHtmlTemplate(markdownFile, "Title", "<p>Body</p>");

                StringAssert.Contains(result, "nearest-<p>Body</p>");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void CreateFromHtmlTemplate_FallsBackToUserProfileTemplate()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string profileTemplate = Path.Combine(userProfile, "md-template.html");
            bool hadExistingProfileTemplate = File.Exists(profileTemplate);
            string originalProfileTemplateContent = hadExistingProfileTemplate ? File.ReadAllText(profileTemplate) : null;

            try
            {
                string markdownFile = Path.Combine(tempDir, "readme.md");
                File.WriteAllText(markdownFile, "# Test");
                File.WriteAllText(profileTemplate, "<html><body>profile-[content]</body></html>");

                string result = HtmlGenerationService.CreateFromHtmlTemplate(markdownFile, "Title", "<p>Body</p>");

                StringAssert.Contains(result, "profile-<p>Body</p>");
            }
            finally
            {
                if (hadExistingProfileTemplate)
                {
                    File.WriteAllText(profileTemplate, originalProfileTemplateContent);
                }
                else if (File.Exists(profileTemplate))
                {
                    File.Delete(profileTemplate);
                }

                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestMethod]
        public void HtmlGenerationEnabled_WhenHtmlSiblingExists_ReturnsTrue()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                string markdownFile = Path.Combine(tempDir, "readme.md");
                string htmlFile = Path.Combine(tempDir, "readme.html");

                File.WriteAllText(markdownFile, "# Title");
                File.WriteAllText(htmlFile, "<html></html>");

                bool result = HtmlGenerationService.HtmlGenerationEnabled(markdownFile);

                Assert.IsTrue(result);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        #endregion
    }
}
