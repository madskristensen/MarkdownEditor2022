using Microsoft.VisualStudio.Extensibility.Testing;
using Xunit;

namespace MarkdownEditor2022.IntegrationTests
{
    [IdeSettings(MinVersion = VisualStudioVersion.VS2022, MaxVersion = VisualStudioVersion.VS2022)]
    public abstract class AbstractIntegrationTest : AbstractIdeIntegrationTest
    {
    }
}
