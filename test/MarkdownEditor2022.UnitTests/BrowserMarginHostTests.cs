namespace MarkdownEditor2022.UnitTests
{
    [TestClass]
    public class BrowserMarginHostTests
    {
        [TestMethod]
        [DataRow(0, 0, false)]
        [DataRow(0, 100, false)]
        [DataRow(100, 0, false)]
        [DataRow(100, 100, false)]
        [DataRow(100, 200, true)]
        [DataRow(200, 100, true)]
        public void RequiresBrowserRecreation_OnlyForDifferentNonzeroHosts(
            int previousHost, int currentHost, bool expected)
        {
            Assert.AreEqual(expected, BrowserMargin.RequiresBrowserRecreation(
                new IntPtr(previousHost), new IntPtr(currentHost)));
        }

        [TestMethod]
        public void TemporaryUnparenting_DoesNotHideSubsequentHostChange()
        {
            IntPtr mainWindow = new(100);
            IntPtr floatingWindow = new(200);

            Assert.IsFalse(BrowserMargin.RequiresBrowserRecreation(mainWindow, IntPtr.Zero),
                "A transient missing source during undocking must not recreate the browser.");
            Assert.IsTrue(BrowserMargin.RequiresBrowserRecreation(mainWindow, floatingWindow),
                "The old host must still be compared with the final floating-window HWND.");
        }

        [TestMethod]
        public void RepeatedDetachAndDock_RecreatesForEachMoveButNotRepeatedNotifications()
        {
            IntPtr mainWindow = new(100);
            IntPtr floatingWindow = new(200);

            for (int cycle = 0; cycle < 3; cycle++)
            {
                Assert.IsTrue(BrowserMargin.RequiresBrowserRecreation(mainWindow, floatingWindow),
                    "Undocking must replace the main-window browser.");
                Assert.IsFalse(BrowserMargin.RequiresBrowserRecreation(floatingWindow, floatingWindow),
                    "Repeated Loaded/source notifications must not replace the floating browser again.");
                Assert.IsTrue(BrowserMargin.RequiresBrowserRecreation(floatingWindow, mainWindow),
                    "Docking back must replace the floating-window browser.");
                Assert.IsFalse(BrowserMargin.RequiresBrowserRecreation(mainWindow, mainWindow),
                    "Repeated notifications must not replace the docked browser again.");
            }
        }
    }
}
