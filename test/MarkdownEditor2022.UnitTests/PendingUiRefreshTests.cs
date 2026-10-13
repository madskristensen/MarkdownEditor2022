namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class PendingUiRefreshTests
    {
        [TestMethod]
        public void Burst_QueuesOnlyOneRefresh()
        {
            PendingUiRefresh refresh = new();
            Assert.IsTrue(refresh.TryQueue(out int generation));
            for (int i = 0; i < 100; i++)
            {
                Assert.IsFalse(refresh.TryQueue(out _));
            }

            Assert.IsTrue(refresh.TryStart(generation));
            Assert.IsFalse(refresh.TryStart(generation));
            Assert.IsTrue(refresh.TryQueue(out _));
        }

        [TestMethod]
        public void Cleanup_InvalidatesQueuedWork()
        {
            PendingUiRefresh refresh = new();
            refresh.TryQueue(out int generation);

            refresh.Reset();

            Assert.IsFalse(refresh.TryStart(generation));
            Assert.IsTrue(refresh.TryQueue(out int nextGeneration));
            Assert.IsTrue(refresh.TryStart(nextGeneration));
        }

        [TestMethod]
        public void OldCallback_CannotConsumeNewInitializationRefresh()
        {
            PendingUiRefresh refresh = new();
            refresh.TryQueue(out int oldGeneration);
            refresh.Reset();
            refresh.TryQueue(out int newGeneration);

            Assert.IsFalse(refresh.TryStart(oldGeneration));
            Assert.IsTrue(refresh.TryStart(newGeneration));
        }

        [TestMethod]
        public void ConcurrentRequests_QueueOnlyOneRefresh()
        {
            PendingUiRefresh refresh = new();
            int queued = 0;
            Parallel.For(0, 100, i =>
            {
                if (refresh.TryQueue(out _))
                {
                    Interlocked.Increment(ref queued);
                }
            });

            Assert.AreEqual(1, queued);
        }
    }
}
