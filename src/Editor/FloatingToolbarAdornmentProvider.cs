using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace MarkdownEditor2022
{
    /// <summary>
    /// Listens for text view creation and instantiates the floating toolbar adornment
    /// for Markdown documents.
    /// </summary>
    [Export(typeof(IWpfTextViewCreationListener))]
    [ContentType(Constants.LanguageName)]
    [TextViewRole(PredefinedTextViewRoles.Document)]
    internal sealed class FloatingToolbarAdornmentProvider : IWpfTextViewCreationListener
    {
        /// <summary>
        /// Called when a text view having matching roles is created over a text data model
        /// having a matching content type.
        /// </summary>
        /// <param name="textView">The newly created text view.</param>
        public void TextViewCreated(IWpfTextView textView)
        {
            // Create the adornment as a singleton property on the text view
            textView.Properties.GetOrCreateSingletonProperty(() => new FloatingToolbarAdornment(textView));
        }
    }
}
