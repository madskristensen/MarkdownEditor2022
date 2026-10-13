namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class BoundedCacheTests
    {
        [TestMethod]
        public void Set_EvictsLeastRecentlyReadEntry()
        {
            BoundedCache<string, string> cache = new(2);
            cache.Set("a", "first");
            cache.Set("b", "second");
            Assert.IsTrue(cache.TryGetValue("a", out _));
            cache.Set("c", "third");

            Assert.IsFalse(cache.TryGetValue("b", out _));
            Assert.IsTrue(cache.TryGetValue("a", out _));
            Assert.IsTrue(cache.TryGetValue("c", out _));
        }

        [TestMethod]
        public void Set_ReplacesVersionWithoutRetainingOldEntry()
        {
            BoundedCache<string, string> cache = new(2);
            cache.Set("template", "old");
            cache.Set("stylesheet", "css");
            cache.Set("template", "new");

            Assert.IsTrue(cache.TryGetValue("template", out string value));
            Assert.AreEqual("new", value);
            Assert.IsTrue(cache.TryGetValue("stylesheet", out _));
        }

        [TestMethod]
        public void Clear_ReleasesAllEntries()
        {
            BoundedCache<string, object> cache = new(2);
            cache.Set("a", new object());
            cache.Set("b", new object());
            cache.Clear();

            Assert.IsFalse(cache.TryGetValue("a", out _));
            Assert.IsFalse(cache.TryGetValue("b", out _));
            cache.Set("c", new object());
            Assert.IsTrue(cache.TryGetValue("c", out _));
        }

        [TestMethod]
        public void Set_ManyDistinctKeys_RetainsOnlyCapacity()
        {
            BoundedCache<int, int> cache = new(8);
            for (int i = 0; i < 1000; i++)
            {
                cache.Set(i, i);
            }

            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(i >= 992, cache.TryGetValue(i, out _));
            }
        }
    }
}
