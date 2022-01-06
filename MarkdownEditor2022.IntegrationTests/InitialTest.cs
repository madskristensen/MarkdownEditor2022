using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace MarkdownEditor2022.IntegrationTests
{
    public class InitialTest : AbstractIntegrationTest
    {
        [IdeFact]
        public async Task TestOpenVisualStudioAsync()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "markdown-tests", Path.GetRandomFileName());
            Directory.CreateDirectory(tempDirectory);

            string filePath = Path.Combine(tempDirectory, "Simple.md");
            string contents = @"# Title
";
            File.WriteAllText(filePath, contents);

            await TestServices.SolutionExplorer.OpenFileAsync(filePath, HangMitigatingCancellationToken);
            await TestServices.SolutionExplorer.CloseFileAsync(filePath, saveFile: false, HangMitigatingCancellationToken);
        }
    }
}
