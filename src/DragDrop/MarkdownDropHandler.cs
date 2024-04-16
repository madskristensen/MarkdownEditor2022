using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        private const string _markdownLink = "[{0}]({1})";
        private static readonly string[] _imageExtensions = { ".jpg", ".jpeg", ".bmp", ".png", ".gif", ".svg", ".tif", ".tiff", ".webm" };

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

                string ext = Path.GetExtension(_draggedFileName);
                bool isImage = _imageExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase);

                string altText = ToFriendlyName(_draggedFileName);
                string link = string.Format(_markdownLink, altText, relative);

                if (isImage)
                {
                    link = "!" + link;
                }
                _view.TextBuffer.Insert(position, link);

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
                MemoryStream ms = (MemoryStream)data.GetData("CF_VSSTGPROJECTITEMS", false);
                return GetFileData(ms);
            }

            return null;
        }

        private static string GetFileData(MemoryStream ms)
        {
            string uuidPattern = @"\{(.*?)\}";
            string projectUUID = "";
            string content = Encoding.Unicode.GetString(ms.ToArray());
            //Get the Project UUID and remove it from the data object
            Match match = Regex.Match(content, uuidPattern, RegexOptions.Singleline);
            if (match.Success)
            {
                projectUUID = match.Value.ToString();
            }

            content = content.Replace(projectUUID, "").Substring(match.Index);
            //Split the file list: Part1 => Project Name, Part2 => File name
            string[] projectFiles = content.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < projectFiles.Length; i += 2)
            {
                return projectFiles[i + 1].Substring(0, projectFiles[i + 1].IndexOf("\0"));
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