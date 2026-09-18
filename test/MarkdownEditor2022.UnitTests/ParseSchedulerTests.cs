using System.Threading;
using Markdig;
using Markdig.Syntax;

namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class ParseSchedulerTests
    {
        [TestMethod]
        [Timeout(15000)]
        public async Task HeaderTyping_PublishesEachEditWithoutAQuietPeriod()
        {
            string text = "# ";
            List<string> parsed = [];
            using ParseScheduler scheduler = new(_ =>
            {
                MarkdownDocument markdown = Markdown.Parse(text, Document.Pipeline);
                Assert.IsInstanceOfType<HeadingBlock>(markdown[0]);
                Assert.AreEqual(text.Length - 1, markdown[0].Span.End);
                parsed.Add(text);
                return Task.CompletedTask;
            });

            foreach (char letter in "Heading")
            {
                text += letter;
                scheduler.Request();
                await scheduler.Completion;
                Assert.AreEqual(text, parsed[parsed.Count - 1]);
            }

            Assert.AreEqual(7, parsed.Count);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task EditsDuringParse_CoalesceWithoutOverlappingParses()
        {
            TaskCompletionSource<bool> started = NewCompletion();
            TaskCompletionSource<bool> release = NewCompletion();
            List<int> delivered = [];
            int latest = 0;
            int active = 0;
            using ParseScheduler scheduler = new(async _ =>
            {
                Assert.AreEqual(1, Interlocked.Increment(ref active));
                delivered.Add(latest);
                if (delivered.Count == 1)
                {
                    started.TrySetResult(true);
                    await release.Task;
                }
                Interlocked.Decrement(ref active);
            });

            scheduler.Request();
            Task worker = scheduler.Completion;
            await started.Task;
            for (int i = 1; i <= 50; i++)
            {
                latest = i;
                scheduler.Request();
            }
            release.TrySetResult(true);
            await worker;

            CollectionAssert.AreEqual(new[] { 0, 50 }, delivered);
        }

        [TestMethod]
        public async Task DisposeBeforeFirstRequest_DoesNotStartWorker()
        {
            int parses = 0;
            ParseScheduler scheduler = new(_ => { parses++; return Task.CompletedTask; });
            scheduler.Dispose();
            scheduler.Request();
            scheduler.Dispose();
            await scheduler.Completion;
            Assert.AreEqual(0, parses);
        }

        [TestMethod]
        [Timeout(15000)]
        public async Task DisposeDuringParse_CancelsWithoutStartingPendingParse()
        {
            TaskCompletionSource<bool> started = NewCompletion();
            TaskCompletionSource<bool> release = NewCompletion();
            int parses = 0;
            using ParseScheduler scheduler = new(async token =>
            {
                parses++;
                started.TrySetResult(true);
                await release.Task;
                token.ThrowIfCancellationRequested();
            });

            scheduler.Request();
            Task worker = scheduler.Completion;
            await started.Task;
            scheduler.Request();
            scheduler.Dispose();
            release.TrySetResult(true);
            await worker;
            scheduler.Dispose();
            scheduler.Request();

            Assert.AreEqual(1, parses);
            Assert.AreEqual(TaskStatus.RanToCompletion, worker.Status);
        }

        private static TaskCompletionSource<bool> NewCompletion() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
