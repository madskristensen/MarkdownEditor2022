using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Markdig;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.VisualStudio.Commanding;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.Commanding.Commands;
using Microsoft.VisualStudio.Threading;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(ICommandHandler))]
    [Name(nameof(GoToDefinitionCommand))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.PrimaryDocument)]
    internal sealed class GoToDefinitionCommand : ICommandHandler<GoToDefinitionCommandArgs>
    {
        private static readonly string[] _markdownExtensions = [".md", ".markdown", ".mdown", ".mkd"];

        public string DisplayName => nameof(GoToDefinitionCommand);

        public CommandState GetCommandState(GoToDefinitionCommandArgs args)
        {
            return CommandState.Available;
        }

        public bool ExecuteCommand(GoToDefinitionCommandArgs args, CommandExecutionContext executionContext)
        {
            Document document = args.SubjectBuffer.GetDocument();
            int caretPosition = GetCaretPosition(args);
            MarkdownDocument markdown = document?.Markdown;
            if (!TryGetFileReference(markdown, caretPosition, out string reference))
            {
                markdown = Markdown.Parse(args.SubjectBuffer.CurrentSnapshot.GetText(), Document.Pipeline);
                if (!TryGetFileReference(markdown, caretPosition, out reference))
                {
                    return false;
                }
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await NavigateAsync(args.TextView, markdown, reference, executionContext.OperationContext.UserCancellationToken);
            }).FireAndForget();

            return true;
        }

        internal static bool TryGetFileReference(MarkdownDocument markdown, int position, out string reference)
        {
            reference = null;
            LinkInline link = markdown?.Descendants<LinkInline>()
                .FirstOrDefault(candidate =>
                    candidate.Span.Start <= position &&
                    candidate.Span.End >= position &&
                    !string.IsNullOrWhiteSpace(candidate.Url));

            if (link == null || !IsLocalReference(link.Url))
            {
                return false;
            }

            reference = link.Url;
            return true;
        }

        internal static bool TryResolveFileReference(
            string reference,
            string documentFile,
            string configuredRoot,
            string workspaceRoot,
            out string filePath,
            out string fragment)
        {
            filePath = null;
            fragment = null;
            if (!IsLocalReference(reference) || string.IsNullOrWhiteSpace(documentFile))
            {
                return false;
            }

            SplitReference(reference, out string path, out fragment);
            string documentDirectory = Path.GetDirectoryName(documentFile);
            string resolvedRoot = Browser.ResolveConfiguredRootPath(configuredRoot, documentDirectory);
            string previewRoot = Browser.GetPreviewRoot(documentDirectory, resolvedRoot, workspaceRoot);

            if (string.IsNullOrEmpty(path))
            {
                filePath = Path.GetFullPath(documentFile);
                return true;
            }

            if (Uri.TryCreate(path, UriKind.Absolute, out Uri uri) && uri.IsFile)
            {
                string localPath = uri.LocalPath;
                if (File.Exists(localPath) && Browser.IsPathWithinPreviewRoot(localPath, previewRoot))
                {
                    filePath = Path.GetFullPath(localPath);
                    return true;
                }

                return false;
            }

            string decodedPath = WebUtility.UrlDecode(path);
            if (Path.IsPathRooted(decodedPath) && !decodedPath.StartsWith("/", StringComparison.Ordinal))
            {
                string absolutePath = Path.GetFullPath(decodedPath);
                if (File.Exists(absolutePath) && Browser.IsPathWithinPreviewRoot(absolutePath, previewRoot))
                {
                    filePath = absolutePath;
                    return true;
                }

                return false;
            }

            if (decodedPath.StartsWith("/", StringComparison.Ordinal))
            {
                return LocalPathResolver.TryResolveReference(
                    decodedPath,
                    documentDirectory,
                    configuredRoot,
                    previewRoot,
                    requireExistingFile: true,
                    out filePath);
            }

            return LocalPathResolver.TryResolveReference(
                decodedPath,
                documentDirectory,
                configuredRoot,
                previewRoot,
                requireExistingFile: true,
                out filePath);
        }

        internal static bool TryGetMarkdownFragmentPosition(
            MarkdownDocument markdown,
            string fragment,
            out int position)
        {
            position = 0;
            if (markdown == null || string.IsNullOrWhiteSpace(fragment))
            {
                return false;
            }

            string decodedFragment = WebUtility.UrlDecode(fragment.TrimStart('#'));
            HeadingBlock heading = markdown.Descendants<HeadingBlock>()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.GetAttributes()?.Id, decodedFragment, StringComparison.Ordinal));
            if (heading == null)
            {
                return false;
            }

            position = heading.Span.Start;
            return true;
        }

        private static int GetCaretPosition(GoToDefinitionCommandArgs args)
        {
            SnapshotPoint? point = args.TextView.Caret.Position.Point.GetPoint(
                args.SubjectBuffer, PositionAffinity.Predecessor);
            return point?.Position ?? args.TextView.Caret.Position.BufferPosition.Position;
        }

        private static bool IsLocalReference(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference) ||
                reference.StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            SplitReference(reference, out string path, out _);
            return Path.IsPathRooted(path) ||
                   !Uri.TryCreate(reference, UriKind.Absolute, out Uri uri) ||
                   uri.IsFile;
        }

        private static void SplitReference(string reference, out string path, out string fragment)
        {
            int fragmentIndex = reference.IndexOf('#');
            fragment = fragmentIndex < 0 ? null : reference.Substring(fragmentIndex + 1);
            string pathAndQuery = fragmentIndex < 0 ? reference : reference.Substring(0, fragmentIndex);
            int queryIndex = pathAndQuery.IndexOf('?');
            path = queryIndex < 0 ? pathAndQuery : pathAndQuery.Substring(0, queryIndex);
        }

        private static async Task NavigateAsync(
            ITextView sourceView,
            MarkdownDocument sourceMarkdown,
            string reference,
            CancellationToken cancellationToken)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            string documentFile = sourceView.TextBuffer.GetFileName();
            IVsSolution solution = await VS.GetRequiredServiceAsync<SVsSolution, IVsSolution>();
            string workspaceRoot = Browser.GetWorkspaceRoot(solution);
            string configuredRoot = RootPathResolver.GetEffectiveRootPath(sourceMarkdown, sourceView);

            if (!TryResolveFileReference(
                reference, documentFile, configuredRoot, workspaceRoot, out string filePath, out string fragment))
            {
                await VS.StatusBar.ShowMessageAsync($"Could not find file referenced by '{reference}'.");
                return;
            }

            DocumentView documentView = await VS.Documents.OpenInPreviewTabAsync(filePath);
            IWpfTextView targetView = documentView?.TextView;
            if (targetView == null || string.IsNullOrEmpty(fragment) || !IsMarkdownFile(filePath))
            {
                return;
            }

            Document targetDocument = targetView.TextBuffer.GetDocument();
            await targetDocument.WaitForInitialParseAsync(cancellationToken);
            int position;
            if (Browser.TryParseLineFragment(fragment, out int line, out int column))
            {
                ITextSnapshot lineSnapshot = targetView.TextSnapshot;
                int lineIndex = Math.Min(line - 1, lineSnapshot.LineCount - 1);
                ITextSnapshotLine snapshotLine = lineSnapshot.GetLineFromLineNumber(lineIndex);
                position = snapshotLine.Start.Position + Math.Min(Math.Max(column - 1, 0), snapshotLine.Length);
            }
            else if (!TryGetMarkdownFragmentPosition(targetDocument.Markdown, fragment, out position))
            {
                await VS.StatusBar.ShowMessageAsync($"Could not find fragment '#{fragment}' in '{Path.GetFileName(filePath)}'.");
                return;
            }

            ITextSnapshot snapshot = targetView.TextSnapshot;
            SnapshotPoint point = new(snapshot, Math.Min(position, snapshot.Length));
            targetView.Caret.MoveTo(point);
            targetView.ViewScroller.EnsureSpanVisible(
                new SnapshotSpan(point, 0), EnsureSpanVisibleOptions.AlwaysCenter);
            targetView.VisualElement.Focus();
        }

        private static bool IsMarkdownFile(string filePath)
        {
            string extension = Path.GetExtension(filePath);
            return _markdownExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }
    }
}
