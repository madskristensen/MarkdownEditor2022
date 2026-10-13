using System.Threading;
using System.Threading.Tasks;
using MarkdownEditor2022.Services;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class PdfExportServiceTests
    {
        [TestMethod]
        public async Task WaitForCompletionAsync_CompletedTaskSucceeds()
        {
            await PdfExportService.WaitForCompletionAsync(Task.CompletedTask, TimeSpan.FromSeconds(5), "Timed out.");
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task WaitForCompletionAsync_PendingTaskCanCompleteBeforeDeadline()
        {
            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task wait = PdfExportService.WaitForCompletionAsync(completion.Task, TimeSpan.FromSeconds(30), "Timed out.");

            Assert.IsFalse(wait.IsCompleted);
            completion.SetResult(true);

            await wait;
        }

        [TestMethod]
        [Timeout(5000)]
        public async Task WaitForCompletionAsync_MissingCompletionSurfacesPhaseSpecificTimeout()
        {
            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            const string message = "Timed out loading the HTML content for PDF export.";

            TimeoutException exception = await Assert.ThrowsExactlyAsync<TimeoutException>(() =>
                PdfExportService.WaitForCompletionAsync(completion.Task, TimeSpan.FromMilliseconds(20), message));

            Assert.AreEqual(message, exception.Message);
            Assert.IsInstanceOfType<OperationCanceledException>(exception.InnerException);
            Assert.IsFalse(completion.Task.IsCompleted);
        }

        [TestMethod]
        public async Task WaitForCompletionAsync_OperationFailureIsNotReplaced()
        {
            InvalidOperationException failure = new("Browser failed.");

            InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
                PdfExportService.WaitForCompletionAsync(Task.FromException(failure), TimeSpan.FromSeconds(5), "Timed out."));

            Assert.AreSame(failure, exception);
        }

        [TestMethod]
        public async Task WaitForCompletionAsync_OperationCancellationIsNotReportedAsTimeout()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            await Assert.ThrowsExactlyAsync<TaskCanceledException>(() =>
                PdfExportService.WaitForCompletionAsync(Task.FromCanceled(cancellation.Token), TimeSpan.FromSeconds(5), "Timed out."));
        }
    }
}
