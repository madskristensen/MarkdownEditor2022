global using System;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using Task = System.Threading.Tasks.Task;
using System.ComponentModel.Design;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using EnvDTE;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace MarkdownEditor2022
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.MarkdownEditor2022String)]

    [ProvideLanguageService(typeof(MarkdownEditorV2), Constants.LanguageName, 0, ShowHotURLs = false, DefaultToNonHotURLs = true, EnableLineNumbers = true, EnableAsyncCompletion = true, ShowCompletion = true, ShowDropDownOptions = true)]
    [ProvideLanguageEditorOptionPage(typeof(OptionsProvider.AdvancedOptions), Constants.LanguageName, "", "Advanced", null, ["mark", "md", "mdown"])]
    [ProvideLanguageExtension(typeof(MarkdownEditorV2), Constants.FileExtensionMd)]
    [ProvideLanguageExtension(typeof(MarkdownEditorV2), Constants.FileExtensionRmd)]

    [ProvideEditorFactory(typeof(MarkdownEditorV2), 0, false, CommonPhysicalViewAttributes = (int)__VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorLogicalView(typeof(MarkdownEditorV2), VSConstants.LOGVIEWID.TextView_string, IsTrusted = true)]
    [ProvideEditorExtension(typeof(MarkdownEditorV2), Constants.FileExtensionMd, 1000)]
    [ProvideEditorExtension(typeof(MarkdownEditorV2), Constants.FileExtensionRmd, 1000)]

    [ProvideFileIcon(Constants.FileExtensionMd, "KnownMonikers.MarkdownFile")]
    public sealed class MarkdownEditor2022Package : ToolkitPackage
    {
        private static DTE _dte;

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            _dte = await VS.GetRequiredServiceAsync<DTE, DTE>();

            MarkdownEditorV2 language = new(this);
            RegisterEditorFactory(language);
            ((IServiceContainer)this).AddService(typeof(MarkdownEditorV2), language, true);

            await this.RegisterCommandsAsync();
            await Commenting.InitializeAsync();
            await ToggleTaskCommand.InitializeAsync();
        }

        public static bool IsActiveDocumentMarkdown()
        {
            string ext = Path.GetExtension(_dte?.ActiveDocument?.FullName ?? "").ToLowerInvariant();

            return ext is Constants.FileExtensionMd or Constants.FileExtensionRmd;
        }
    }
}
