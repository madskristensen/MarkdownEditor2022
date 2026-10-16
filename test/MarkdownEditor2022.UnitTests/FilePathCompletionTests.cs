using System.IO;
using System.Threading;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class FilePathCompletionTests
    {
        private string _directory = null!;
        private string _docs = null!;

        [TestInitialize]
        public void Initialize()
        {
            _directory = Path.Combine(AppContext.BaseDirectory, "CompletionTests", Guid.NewGuid().ToString("N"));
            _docs = Path.Combine(_directory, "docs");
            Directory.CreateDirectory(Path.Combine(_docs, "images"));
            Directory.CreateDirectory(Path.Combine(_docs, ".hidden"));
            File.WriteAllText(Path.Combine(_docs, "guide.md"), "# Guide");
            File.WriteAllText(Path.Combine(_docs, "photo.png"), "image");
            File.WriteAllText(Path.Combine(_docs, ".hidden.md"), "hidden");
            File.WriteAllText(Path.Combine(_docs, "images", "diagram.svg"), "image");
        }

        [TestCleanup]
        public void Cleanup() => Directory.Delete(_directory, recursive: true);

        [TestMethod]
        public void GetFileEntries_RelativePath_PreservesParentFoldersFilesAndHiddenFiltering()
        {
            IReadOnlyList<(string name, bool isDirectory, bool isParent)> entries =
                FilePathCompletionSource.GetFileEntries("./", _docs, _directory, CancellationToken.None);

            CollectionAssert.AreEquivalent(new[]
            {
                ("..", true, true), ("images", true, false), ("guide.md", false, false), ("photo.png", false, false)
            }, entries.ToArray());
        }

        [DataRow("images/")]
        [DataRow("../docs/images/")]
        [DataRow("/docs/images/")]
        [DataRow("~/docs/images/")]
        [TestMethod]
        public void GetFileEntries_NavigatesRelativeAndRootRelativeDirectories(string path)
        {
            IReadOnlyList<(string name, bool isDirectory, bool isParent)> entries =
                FilePathCompletionSource.GetFileEntries(path, _docs, _directory, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { ("diagram.svg", false, false) }, entries.ToArray());
        }

        [TestMethod]
        public void GetFileEntries_Root_DoesNotOfferParentNavigation()
        {
            IReadOnlyList<(string name, bool isDirectory, bool isParent)> entries =
                FilePathCompletionSource.GetFileEntries("/", _docs, _directory, CancellationToken.None);

            CollectionAssert.AreEqual(new[] { ("docs", true, false) }, entries.ToArray());
        }

        [TestMethod]
        public void GetFileEntries_MissingRoot_ReturnsNoEntries()
        {
            Assert.AreEqual(0, FilePathCompletionSource.GetFileEntries("/", _docs,
                Path.Combine(_directory, "missing"), CancellationToken.None).Count);
        }

        [TestMethod]
        public void ResolveTargetFilePath_ResolvesExtensionlessAndRootRelativeAnchors()
        {
            string expected = Path.Combine(_docs, "guide.md");

            Assert.AreEqual(expected, FilePathCompletionSource.ResolveTargetFilePath("guide", _docs, _directory, CancellationToken.None));
            Assert.AreEqual(expected, FilePathCompletionSource.ResolveTargetFilePath("/docs/guide.md", _docs, _directory, CancellationToken.None));
            Assert.IsNull(FilePathCompletionSource.ResolveTargetFilePath("missing", _docs, _directory, CancellationToken.None));
        }

        [TestMethod]
        public void CanceledRequest_StopsEnumerationAndAnchorResolution()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                FilePathCompletionSource.GetFileEntries("./", _docs, _directory, cancellation.Token));
            Assert.Throws<OperationCanceledException>(() =>
                FilePathCompletionSource.ResolveTargetFilePath("guide", _docs, _directory, cancellation.Token));
        }
    }
}