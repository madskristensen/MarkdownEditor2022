using Markdig;
using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class ParserCompletionTests
    {
        [TestMethod]
        public async Task PipelineParse_ComplexMarkdown_CompletesAndProducesHtml()
        {
            string markdown = "# Heading\n\n";
            for (int i = 0; i < 1000; i++)
            {
                markdown += "| Column | Value |\n| --- | --- |\n| A | B |\n";
            }

            markdown += "\n```mermaid\ngraph TD\n    A --> B\n```";

            Task<MarkdownDocument> parseTask = Task.Run(() => Markdown.Parse(markdown, Document.Pipeline));
            Task completedTask = await Task.WhenAny(parseTask, Task.Delay(TimeSpan.FromSeconds(5)));

            Assert.AreSame(parseTask, completedTask, "Markdown parsing did not complete.");
            MarkdownDocument document = await parseTask;
            string html = document.ToHtml(Document.Pipeline);

            StringAssert.Contains(html, "<h1 id=\"heading\">");
            StringAssert.Contains(html, "Heading</h1>");
            StringAssert.Contains(html, "graph TD");
        }

        [TestMethod]
        public void PipelineParse_TaskList_PreservesCheckedState()
        {
            MarkdownDocument document = Markdown.Parse("* [ ] Open\n* [x] Done\n* [X] Also done", Document.Pipeline);
            string html = document.ToHtml(Document.Pipeline);

            StringAssert.Contains(html, "type=\"checkbox\"");
            StringAssert.Contains(html, "checked");
        }
    }
}
