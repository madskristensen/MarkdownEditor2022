using System.Diagnostics;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Imaging;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Helper service for calling Image Optimizer extension commands.
    /// See: https://github.com/madskristensen/ImageOptimizer#api-for-extenders
    /// </summary>
    public static class ImageOptimizerService
    {
        private const string _optimizeLosslessCommand = "ImageOptimizer.OptimizeLossless";
        private const string _optimizeLossyCommand = "ImageOptimizer.OptimizeLossy";
        private const string _marketplaceUrl = "https://marketplace.visualstudio.com/items?itemName=MadsKristensen.ImageOptimizer64bit";

        private static bool _infoBarShown;

        /// <summary>
        /// Optimizes an image file using lossless compression (best quality).
        /// </summary>
        /// <param name="filePath">The full path to the image file to optimize.</param>
        /// <param name="showMessageBox">If true, shows a message box when Image Optimizer is not installed. If false, shows an info bar.</param>
        /// <returns>True if the command was executed successfully, false if Image Optimizer is not installed or command failed.</returns>
        public static async System.Threading.Tasks.Task<bool> OptimizeLosslessAsync(string filePath, bool showMessageBox = false)
        {
            return await OptimizeAsync(filePath, _optimizeLosslessCommand, showMessageBox);
        }

        /// <summary>
        /// Optimizes an image file using lossy compression (best compression).
        /// </summary>
        /// <param name="filePath">The full path to the image file to optimize.</param>
        /// <param name="showMessageBox">If true, shows a message box when Image Optimizer is not installed. If false, shows an info bar.</param>
        /// <returns>True if the command was executed successfully, false if Image Optimizer is not installed or command failed.</returns>
        public static async System.Threading.Tasks.Task<bool> OptimizeLossyAsync(string filePath, bool showMessageBox = false)
        {
            return await OptimizeAsync(filePath, _optimizeLossyCommand, showMessageBox);
        }

        /// <summary>
        /// Checks if the Image Optimizer extension is installed and available.
        /// </summary>
        /// <returns>True if Image Optimizer is available, false otherwise.</returns>
        public static async System.Threading.Tasks.Task<bool> IsAvailableAsync()
        {
            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                DTE2 dte = await VS.GetRequiredServiceAsync<DTE, DTE2>();
                Command command = dte.Commands.Item(_optimizeLosslessCommand);
                return command != null && command.IsAvailable;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Shows a message box prompting the user to install Image Optimizer with a link to the marketplace.
        /// </summary>
        public static async System.Threading.Tasks.Task ShowInstallMessageBoxAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            bool confirmed = await VS.MessageBox.ShowConfirmAsync(
                "Image Optimizer required",
                "The Image Optimizer extension is required for image optimization.\n\nWould you like to download it from the Visual Studio Marketplace?");

            if (confirmed)
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo(_marketplaceUrl) { UseShellExecute = true });
            }
        }

        /// <summary>
        /// Shows an info bar prompting the user to install Image Optimizer if not already shown.
        /// </summary>
        public static async Task ShowInstallInfoBarAsync()
        {
            if (_infoBarShown)
            {
                return;
            }

            _infoBarShown = true;

            try
            {
                InfoBarModel model = new(
                    [
                        new InfoBarTextSpan("Image Optimizer extension is required for image optimization. "),
                        new InfoBarHyperlink("Download Image Optimizer", "download")
                    ],
                    KnownMonikers.StatusInformation,
                    isCloseButtonVisible: true);

                InfoBar infoBar = await VS.InfoBar.CreateAsync(model);

                if (infoBar != null)
                {
                    infoBar.ActionItemClicked += OnInfoBarActionItemClicked;
                    await infoBar.TryShowInfoBarUIAsync();
                }
            }
            catch (Exception ex)
            {
                await ex.LogAsync();
            }
        }

        private static void OnInfoBarActionItemClicked(object sender, InfoBarActionItemEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            if (e.ActionItem.ActionContext?.ToString() == "download")
            {
                System.Diagnostics.Process.Start(new ProcessStartInfo(_marketplaceUrl) { UseShellExecute = true });
            }

            // Close the info bar after clicking
            if (sender is InfoBar infoBar)
            {
                infoBar.Close();
            }
        }

        private static async System.Threading.Tasks.Task<bool> OptimizeAsync(string filePath, string commandName, bool showMessageBox)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                DTE2 dte = await VS.GetRequiredServiceAsync<DTE, DTE2>();

                Command command;
                try
                {
                    command = dte.Commands.Item(commandName);
                }
                catch (ArgumentException)
                {
                    // Command doesn't exist - Image Optimizer is not installed
                    if (showMessageBox)
                    {
                        await ShowInstallMessageBoxAsync();
                    }
                    else
                    {
                        await ShowInstallInfoBarAsync();
                    }
                    return false;
                }

                if (command != null && command.IsAvailable)
                {
                    object customIn = filePath;
                    object customOut = null;
                    dte.Commands.Raise(command.Guid, command.ID, ref customIn, ref customOut);
                    return true;
                }
                else
                {
                    // Image Optimizer not available
                    if (showMessageBox)
                    {
                        await ShowInstallMessageBoxAsync();
                    }
                    else
                    {
                        await ShowInstallInfoBarAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Unexpected error
                await ex.LogAsync();
            }

            return false;
        }
    }
}
