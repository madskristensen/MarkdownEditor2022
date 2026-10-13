using System.ComponentModel;
using System.Diagnostics;
using Markdig;
using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class PreviewRenderingTests
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public void BuildPreviewPage_SmallDocument_PreservesInlineInitialization()
        {
            (string page, bool hydrate) = Browser.BuildPreviewPage("<body>[content][scripts]</body>", "<p>Text</p>", "default", 7);
            Assert.IsFalse(hydrate);
            StringAssert.Contains(page, "<p>Text</p>");
            StringAssert.Contains(page, "__initializeMarkdownPreview('default', 7)");
        }

        [TestMethod]
        public void BuildPreviewPage_LargeDocument_UsesEmptyShell()
        {
            (string page, bool hydrate) = Browser.BuildPreviewPage("<body>[content][scripts]</body>", new string('x', 2 * 1024 * 1024), "default", 7);
            Assert.IsTrue(hydrate);
            Assert.AreEqual("<body></body>", page);
        }

        [TestMethod]
        public void BuildPreviewPage_OversizedTemplate_ReportsLimit()
        {
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                Browser.BuildPreviewPage(new string('x', 2 * 1024 * 1024) + "[content][scripts]", "", "default", 7));
        }

        [TestMethod]
        public void InjectPreviewHead_TemplateWithoutHead_AddsStylesBeforeBody()
        {
            string result = Browser.InjectPreviewHead("<html><body>profile-[content]</body></html>", "<style>#probe { color: red; }</style>");

            Assert.AreEqual(
                "<html><head><style>#probe { color: red; }</style></head><body>profile-[content]</body></html>",
                result);
        }

        [TestMethod]
        public void InjectPreviewHead_ExistingHead_PreservesExistingContent()
        {
            string result = Browser.InjectPreviewHead("<html><HEAD data-test=\"true\"><title>Custom</title></HEAD><body></body></html>", "<style>body { color: red; }</style>");

            StringAssert.Contains(result, "<HEAD data-test=\"true\"><style>body { color: red; }</style><title>Custom</title></HEAD>");
        }

        [TestMethod]
        public async Task PreviewContentScript_NodeBehaviorTestsPass()
        {
            string outputDirectory = AppContext.BaseDirectory;
            string harnessPath = Path.Combine(outputDirectory, "preview-content-script.test.cjs");
            string scriptPath = Path.Combine(outputDirectory, "Margin", "preview-content.js");
            Assert.IsTrue(File.Exists(harnessPath), $"JavaScript test harness was not copied to {harnessPath}.");
            Assert.IsTrue(File.Exists(scriptPath), $"Preview script was not copied to {scriptPath}.");

            ProcessStartInfo startInfo = new()
            {
                FileName = "node",
                Arguments = $"--test \"{harnessPath}\"",
                WorkingDirectory = outputDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.EnvironmentVariables["MARKDOWN_PREVIEW_SCRIPT"] = scriptPath;

            using (Process process = new() { StartInfo = startInfo })
            {
                bool started = false;
                try
                {
                    try
                    {
                        started = process.Start();
                    }
                    catch (Win32Exception exception)
                    {
                        Assert.Fail($"Node.js is required to run the preview JavaScript tests. Install Node.js with --test support and ensure node is on PATH. {exception.Message}");
                    }

                    Assert.IsTrue(started, "Could not start Node.js for the preview JavaScript tests.");
                    Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                    Task<string> stderr = process.StandardError.ReadToEndAsync();
                    Task completion = Task.WhenAll(
                        Task.Run(() => process.WaitForExit()), stdout, stderr);
                    Task finished = await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(60)));
                    if (finished != completion)
                    {
                        Assert.Fail("Preview JavaScript tests exceeded the 60-second timeout.");
                    }

                    await completion;
                    TestContext.WriteLine(await stdout);
                    Assert.AreEqual(0, process.ExitCode,
                        $"Preview JavaScript tests failed.\nSTDOUT:\n{await stdout}\nSTDERR:\n{await stderr}");
                }
                finally
                {
                    if (started && !process.HasExited)
                    {
                        // Node's test runner can spawn children; stop only this test's process tree.
                        using (Process cleanup = Process.Start(new ProcessStartInfo
                        {
                            FileName = Path.Combine(Environment.SystemDirectory, "taskkill.exe"),
                            Arguments = $"/PID {process.Id} /T /F",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        }))
                        {
                            if (!cleanup.WaitForExit(5000))
                            {
                                cleanup.Kill();
                            }
                        }

                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                    }
                }
            }
        }

        [TestMethod]
        public async Task RenderHtmlDocument_OnWorkerPreservesPreviewFeatures()
        {
            MarkdownDocument markdown = Markdown.Parse(
                "# Heading\n\n```js\nconst value = 1;\n```\n\n```mermaid\ngraph TD; A-->B;\n```\n\n$x + y$\n",
                Document.Pipeline);

            string html = await Task.Run(() => Browser.RenderHtmlDocument(markdown));

            StringAssert.Contains(html, "Heading");
            StringAssert.Contains(html, "pragma-line-");
            StringAssert.Contains(html, "language-javascript");
            StringAssert.Contains(html, "class=\"mermaid\"");
            StringAssert.Contains(html, "class=\"math\"");
        }

        [TestMethod]
        public async Task RenderHtmlDocument_ConcurrentDocumentsDoNotMixOutput()
        {
            Task<string>[] renders = Enumerable.Range(0, 16)
                .Select(index => Task.Run(() => Browser.RenderHtmlDocument(
                    Markdown.Parse($"# Document {index}\n\nParagraph {index}.", Document.Pipeline))))
                .ToArray();

            string[] results = await Task.WhenAll(renders);
            for (int index = 0; index < results.Length; index++)
            {
                StringAssert.Contains(results[index], $"Document {index}</h1>");
                StringAssert.Contains(results[index], $"Paragraph {index}.</p>");
            }
        }

        [TestMethod]
        public async Task RenderHtmlDocument_ReusingParsedDocumentIsStable()
        {
            MarkdownDocument markdown = Markdown.Parse(
                "# Contents\n\n[[_TOC_]]\n\n## One\n\nText\n\n## Two\n\nMore text",
                Document.Pipeline);

            string first = await Task.Run(() => Browser.RenderHtmlDocument(markdown));
            string second = await Task.Run(() => Browser.RenderHtmlDocument(markdown));

            Assert.AreEqual(first, second);
        }
    }
}
