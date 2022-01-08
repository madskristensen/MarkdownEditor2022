global using System;
global using Community.VisualStudio.Toolkit;
global using Microsoft.VisualStudio.Shell;
global using Task = System.Threading.Tasks.Task;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;

namespace MarkdownEditor2022
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [InstalledProductRegistration(Vsix.Name, Vsix.Description, Vsix.Version)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [Guid(PackageGuids.MarkdownEditor2022String)]

    [ProvideLanguageService(typeof(MarkdownEditor), Constants.LanguageName, 0, ShowHotURLs = false, DefaultToNonHotURLs = true, EnableLineNumbers = true, EnableAsyncCompletion = true, EnableCommenting = true, ShowCompletion = true, ShowDropDownOptions = true)]
    [ProvideLanguageEditorOptionPage(typeof(OptionsProvider.AdvancedOptions), Constants.LanguageName, "", "Advanced", null, 0)]
    [ProvideLanguageExtension(typeof(MarkdownEditor), Constants.FileExtension)]
    [ProvideEditorExtension(typeof(MarkdownEditor), Constants.FileExtension, 50)]
    [ProvideFileIcon(Constants.FileExtension, "KnownMonikers.RegistrationScript")]
    [ProvideEditorFactory(typeof(MarkdownEditor), 0, false, CommonPhysicalViewAttributes = (int)__VSPHYSICALVIEWATTRIBUTES.PVA_SupportsPreview, TrustLevel = __VSEDITORTRUSTLEVEL.ETL_AlwaysTrusted)]
    [ProvideEditorLogicalView(typeof(MarkdownEditor), VSConstants.LOGVIEWID.TextView_string, IsTrusted = true)]
    public sealed class MarkdownEditor2022Package : ToolkitPackage
    {
        internal static MarkdownEditor MarkdownEditor { get; private set; }

        protected override Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            MarkdownEditor = new(this);
            RegisterEditorFactory(MarkdownEditor);

            ((IServiceContainer)this).AddService(typeof(MarkdownEditor), MarkdownEditor, true);

            SetInternetExplorerRegistryKey();

            return this.RegisterCommandsAsync();
        }

        // This is to enable DPI scaling in the preview browser instance
        private static void SetInternetExplorerRegistryKey()
        {
            try
            {
                using (RegistryKey featureControl = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Internet Explorer\\Main\\FeatureControl", true))
                using (RegistryKey pixel = featureControl.CreateSubKey("FEATURE_96DPI_PIXEL", true, RegistryOptions.Volatile))
                {
                    pixel.SetValue("devenv.exe", 1, RegistryValueKind.DWord);
                }
            }
            catch (Exception ex)
            {
                ex.Log();
            }
        }
    }
}