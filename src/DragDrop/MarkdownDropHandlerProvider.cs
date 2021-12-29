using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.DragDrop;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    [Export(typeof(IDropHandlerProvider))]
    [DropFormat("CF_VSSTGPROJECTITEMS")]
    [DropFormat("FileDrop")]
    [Name("IgnoreDropHandler")]
    [ContentType(Constants.LanguageName)]
    [Order(Before = "DefaultFileDropHandler")]
    internal class MarkdownDropHandlerProvider : IDropHandlerProvider
    {
        public IDropHandler GetAssociatedDropHandler(IWpfTextView view) =>
            view.Properties.GetOrCreateSingletonProperty(() => new MarkdownDropHandler(view));
    }
}