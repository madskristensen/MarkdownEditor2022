using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Editor.DragDrop;

namespace MarkdownEditor2022
{
    internal class MarkdownDropHandler : IDropHandler
    {
        private readonly IWpfTextView _view;
        private string _draggedFileName;
        private readonly string _documentFileName;
        private const string _markdownTemplate = "![{0}]({1})";
        private static readonly string[] _imageExtensions = { ".jpg", ".jpeg", ".bmp", ".png", ".gif", ".svg", ".tif", ".tiff" };

        public MarkdownDropHandler(IWpfTextView view)
        {
            _view = view;
            _documentFileName = view.TextBuffer.GetFileName();
        }

        public DragDropPointerEffects HandleDataDropped(DragDropInfo dragDropInfo)
        {
            try
            {
                SnapshotPoint position = dragDropInfo.VirtualBufferPosition.Position;
                string relative = PackageUtilities.MakeRelative(_documentFileName, _draggedFileName)
                                              .Replace("\\", "/")
                                              .Replace(" ", "%20");

                string altText = ToFriendlyName(_draggedFileName);
                string image = string.Format(_markdownTemplate, altText, relative);

                _view.TextBuffer.Insert(position, image);
            }
            catch (Exception ex)
            {
                ex.Log();
            }

            return DragDropPointerEffects.Copy;
        }

        public void HandleDragCanceled()
        { }

        public DragDropPointerEffects HandleDragStarted(DragDropInfo dragDropInfo)
        {
            return DragDropPointerEffects.All;
        }

        public DragDropPointerEffects HandleDraggingOver(DragDropInfo dragDropInfo)
        {
            return DragDropPointerEffects.All;
        }

        public bool IsDropEnabled(DragDropInfo dragDropInfo)
        {
            _draggedFileName = GetImageFilename(dragDropInfo);
            string ext = Path.GetExtension(_draggedFileName);

            if (!_imageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                return false;
            }

            return File.Exists(_draggedFileName) || Directory.Exists(_draggedFileName);
        }

        private static string GetImageFilename(DragDropInfo info)
        {
            DataObject data = new(info.Data);

            if (info.Data.GetDataPresent("FileDrop"))
            {
                // The drag and drop operation came from the file system
                System.Collections.Specialized.StringCollection files = data.GetFileDropList();

                if (files != null && files.Count == 1)
                {
                    return files[0];
                }
            }
            else if (info.Data.GetDataPresent("CF_VSSTGPROJECTITEMS"))
            {
                // The drag and drop operation came from the VS solution explorer
                return data.GetText();
            }

            return null;
        }

        public static string ToFriendlyName(string fileName)
        {
            string text = Path.GetFileNameWithoutExtension(fileName)
                            .Replace("-", " ")
                            .Replace("_", " ");

            text = Regex.Replace(text, "(\\B[A-Z])", " $1");

            return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(text);
        }
    }
}