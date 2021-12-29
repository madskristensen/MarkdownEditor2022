using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;

namespace MarkdownEditor2022.Commands
{
    public class PasteImageCommand : IOleCommandTarget
    {
        private readonly IOleCommandTarget _next;
        private string _lastPath;
        private readonly IWpfTextView _view;
        private readonly string _fileName;
        private const string _format = "![{1}]({0})";

        public PasteImageCommand(IVsTextView adapter, IWpfTextView textView)
        {
            _view = textView;
            _fileName = textView.TextBuffer.GetFileName();
            ErrorHandler.ThrowOnFailure(adapter.AddCommandFilter(this, out _next));
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (pguidCmdGroup == typeof(VSConstants.VSStd97CmdID).GUID && nCmdID == (uint)VSConstants.VSStd97CmdID.Paste)
            {
                if (HandlePaste())
                {
                    return VSConstants.S_OK;
                }
            }

            return _next.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            for (var i = 0; i < cCmds; i++)
            {
                if (pguidCmdGroup == typeof(VSConstants.VSStd97CmdID).GUID && prgCmds[i].cmdID == (uint)VSConstants.VSStd97CmdID.Paste)
                {
                    prgCmds[i].cmdf = (uint)(OLECMDF.OLECMDF_ENABLED | OLECMDF.OLECMDF_SUPPORTED);
                    return VSConstants.S_OK;
                }

            }

            return _next.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
        }

        protected bool HandlePaste()
        {
            System.Windows.Forms.IDataObject data = Clipboard.GetDataObject();

            if (data == null)
            {
                return false;
            }

            var formats = data.GetFormats();

            if (formats == null)
            {
                return false;
            }

            // This is to check if the image is text copied from PowerPoint etc.
            var trueBitmap = formats.Any(x => new[] { "DeviceIndependentBitmap", "PNG", "JPG", "System.Drawing.Bitmap" }.Contains(x));
            var textFormat = formats.Any(x => new[] { "Text", "Rich Text Format" }.Contains(x));
            var hasBitmap = data.GetDataPresent("System.Drawing.Bitmap") || data.GetDataPresent(DataFormats.FileDrop);

            if (!hasBitmap && !trueBitmap || textFormat)
            {
                return false;
            }

            string existingFile = null;

            try
            {
                if (!GetPastedFileName(data, out existingFile))
                {
                    return true;
                }

                _lastPath = Path.GetDirectoryName(existingFile);

                SaveClipboardImageToFile(data, existingFile);
                UpdateTextBuffer(existingFile, _fileName);
            }
            catch (Exception ex)
            {
                ex.Log();
            }

            return true;
        }

        private bool GetPastedFileName(System.Windows.Forms.IDataObject data, out string fileName)
        {
            string extension;
            fileName = "file";

            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var fullpath = ((string[])data.GetData(DataFormats.FileDrop))[0];
                fileName = Path.GetFileName(fullpath);
                extension = Path.GetExtension(fileName).TrimStart('.');
            }
            else
            {
                extension = GetMimeType((Bitmap)data.GetData("System.Drawing.Bitmap"));
            }

            var dialog = new SaveFileDialog
            {
                FileName = fileName,
                DefaultExt = "." + extension,
                Filter = extension.ToUpperInvariant() + " Files|*." + extension,
                InitialDirectory = _lastPath ?? Path.GetDirectoryName(_fileName)
            };

            if (dialog.ShowDialog() != DialogResult.OK)
            {
                return false;
            }

            fileName = dialog.FileName;

            return true;
        }

        private static string GetMimeType(Bitmap bitmap)
        {
            if (bitmap.RawFormat.Guid == ImageFormat.Bmp.Guid)
            {
                return "bmp";
            }

            if (bitmap.RawFormat.Guid == ImageFormat.Emf.Guid)
            {
                return "emf";
            }

            if (bitmap.RawFormat.Guid == ImageFormat.Exif.Guid)
            {
                return "exif";
            }

            if (bitmap.RawFormat.Guid == ImageFormat.Gif.Guid)
            {
                return "gif";
            }

            if (bitmap.RawFormat.Guid == ImageFormat.Icon.Guid)
            {
                return "icon";
            }

            if (bitmap.RawFormat.Guid == ImageFormat.Jpeg.Guid)
            {
                return "jpg";
            }

            if (bitmap.RawFormat.Guid == ImageFormat.Tiff.Guid)
            {
                return "tiff";
            }

            if (bitmap.RawFormat.Guid == ImageFormat.Wmf.Guid)
            {
                return "wmf";
            }

            return "png";
        }

        private void UpdateTextBuffer(string fileName, string relativeTo)
        {
            var position = _view.Caret.Position.BufferPosition.Position;
            var relative = PackageUtilities.MakeRelative(relativeTo, fileName)
                                          .Replace("\\", "/");

            var altText = MarkdownDropHandler.ToFriendlyName(fileName);
            var image = string.Format(CultureInfo.InvariantCulture, _format, relative, altText);

            using (ITextEdit edit = _view.TextBuffer.CreateEdit())
            {
                edit.Insert(position, image);
                edit.Apply();
            }
        }

        public void SaveClipboardImageToFile(System.Windows.Forms.IDataObject data, string existingFile)
        {
            if (data.GetDataPresent(DataFormats.FileDrop))
            {
                var original = ((string[])data.GetData(DataFormats.FileDrop))[0];

                if (File.Exists(original))
                {
                    File.Copy(original, existingFile, true);
                }
            }
            else
            {
                using (var image = (Bitmap)data.GetData("System.Drawing.Bitmap"))
                using (var ms = new MemoryStream())
                {
                    image.Save(existingFile, GetImageFormat(Path.GetExtension(existingFile)));
                }
            }
        }

        public static ImageFormat GetImageFormat(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => ImageFormat.Jpeg,
                ".gif" => ImageFormat.Gif,
                ".bmp" => ImageFormat.Bmp,
                ".ico" => ImageFormat.Icon,
                _ => ImageFormat.Png,
            };
        }
    }
}
