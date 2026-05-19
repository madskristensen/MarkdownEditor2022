using System.IO;
using System.Windows.Forms;
using MarkdownEditor2022.Services;

namespace MarkdownEditor2022
{
    [Command(PackageIds.ExportToPdf)]
    internal sealed class ExportToPdfCommand : BaseCommand<ExportToPdfCommand>
    {
        protected override Task InitializeCompletedAsync()
        {
            Command.Supported = false;
            return base.InitializeCompletedAsync();
        }

        protected override async Task ExecuteAsync(OleMenuCmdEventArgs e)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            DocumentView docView = await VS.Documents.GetActiveDocumentViewAsync();
            string markdownFile = docView?.FilePath;

            if (!HtmlGenerationService.IsMarkdownFile(markdownFile))
            {
                await VS.StatusBar.ShowMessageAsync("Export to PDF is only available for Markdown files");
                return;
            }

            string defaultName = Path.GetFileNameWithoutExtension(markdownFile) + ".pdf";
            string outputPath;

            using (SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export Markdown to PDF",
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = defaultName,
                InitialDirectory = Path.GetDirectoryName(markdownFile),
                OverwritePrompt = true
            })
            {
                if (dialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                outputPath = dialog.FileName;
            }

            try
            {
                await VS.StatusBar.ShowMessageAsync("Exporting to PDF…");
                await PdfExportService.ExportToPdfAsync(markdownFile, outputPath);
                await VS.StatusBar.ShowMessageAsync($"PDF exported: {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex)
            {
                await VS.StatusBar.ShowMessageAsync($"PDF export failed: {ex.Message}");
                await VS.MessageBox.ShowErrorAsync("Export to PDF", $"An error occurred while exporting to PDF:\n\n{ex.Message}");
            }
        }
    }
}
