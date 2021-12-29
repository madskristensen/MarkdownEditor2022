using System.ComponentModel.Composition;
using MarkdownEditor2022.Commands;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IVsTextViewCreationListener))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    public class CommandRegistration : IVsTextViewCreationListener
    {
        [Import] internal IVsEditorAdaptersFactoryService _editorAdaptersFactoryService = null;

        public void VsTextViewCreated(IVsTextView textViewAdapter)
        {
            IWpfTextView textView = _editorAdaptersFactoryService.GetWpfTextView(textViewAdapter);
            textView.Properties.GetOrCreateSingletonProperty(() => new PasteImageCommand(textViewAdapter, textView));

        }
    }
}
