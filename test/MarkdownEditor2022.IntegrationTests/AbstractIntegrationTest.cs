using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility.Testing;
using Xunit;

namespace MarkdownEditor2022.IntegrationTests
{
    [IdeSettings(MinVersion = VisualStudioVersion.VS2022, MaxVersion = VisualStudioVersion.VS2022)]
    public abstract class AbstractIntegrationTest : AbstractIdeIntegrationTest
    {
        public AbstractIntegrationTest()
        {
            TemporaryDirectory = Path.Combine(Path.GetTempPath(), "markdown-tests");
            Directory.CreateDirectory(TemporaryDirectory);
            Assert.True(Directory.Exists(TemporaryDirectory));
        }

        protected string TemporaryDirectory { get; }

        public override async Task InitializeAsync()
        {
            await base.InitializeAsync();
            await TestServices.Shell.AddTrustedFolderAsync(TemporaryDirectory, HangMitigatingCancellationToken);
        }
    }
}
