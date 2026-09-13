using System.IO;

namespace MarkdownEditor2022
{
    internal static class LocalPathResolver
    {
        private static readonly string[] _markdownExtensions = [".md", ".markdown", ".mdown", ".mkd"];

        internal static bool TryResolveReference(
            string referencePath,
            string documentDirectory,
            string configuredRoot,
            string previewRoot,
            bool requireExistingFile,
            out string filePath)
        {
            filePath = null;
            if (string.IsNullOrWhiteSpace(referencePath) ||
                string.IsNullOrWhiteSpace(documentDirectory) ||
                string.IsNullOrWhiteSpace(previewRoot))
            {
                return false;
            }

            Browser.SplitUrlPathAndSuffix(referencePath, out string path, out _);
            if (path.StartsWith("/", System.StringComparison.Ordinal))
            {
                if (Browser.TryResolveExistingRootRelativePath(
                    path, documentDirectory, configuredRoot, previewRoot, out filePath))
                {
                    return true;
                }

                if (requireExistingFile)
                {
                    return false;
                }

                string effectiveRoot = Browser.ResolveConfiguredRootPath(configuredRoot, documentDirectory) ??
                                       Browser.FindRootPath(path, documentDirectory, previewRoot);
                if (string.IsNullOrEmpty(effectiveRoot))
                {
                    return false;
                }

                filePath = Browser.ResolvePreviewPath(path, effectiveRoot, previewRoot);
                return true;
            }

            string candidate = Browser.ResolvePreviewPath(path, documentDirectory, previewRoot);
            if (!requireExistingFile)
            {
                filePath = candidate;
                return true;
            }

            return TryResolveExistingCandidate(candidate, previewRoot, out filePath);
        }

        internal static bool TryResolveExistingCandidate(
            string candidate,
            string previewRoot,
            out string filePath)
        {
            filePath = null;
            if (!Browser.IsPathWithinPreviewRoot(candidate, previewRoot))
            {
                return false;
            }

            if (File.Exists(candidate))
            {
                filePath = Path.GetFullPath(candidate);
                return true;
            }

            if (string.IsNullOrEmpty(Path.GetExtension(candidate)))
            {
                foreach (string extension in _markdownExtensions)
                {
                    string markdownCandidate = candidate + extension;
                    if (File.Exists(markdownCandidate))
                    {
                        filePath = markdownCandidate;
                        return true;
                    }
                }

                return false;
            }

            filePath = Browser.TryResolveMissingHtmlToMarkdownSibling(candidate);
            filePath ??= Browser.TryResolveMissingHtmlToJekyllCollection(candidate, previewRoot);
            return !string.IsNullOrEmpty(filePath);
        }
    }
}
