using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HikariThreading;

namespace HikariTests
{
    [TestClass]
    public class EnumeratorTaskTests
    {
        int i = 0;

        System.Collections.IEnumerator SampleTask ( )
        {
            yield return null;
            i = 5;
        }

        System.Collections.IEnumerator SampleExtension ( )
        {
            yield return null;
            i--;
        }

        System.Collections.IEnumerator SampleTaskWithYield(ITask waitfor)
        {
            yield return null;
            i = 6;
            yield return waitfor;
            i++;
            yield return null;
            i++;
        }

        [TestMethod]
        public void DoesTask ( )
        {
            EnumeratorTask a = new EnumeratorTask(SampleTask(), false);
            (a as ITask).Start();
            Assert.AreEqual(5, i, "Looks like the task didn't execute when instantiated with an Enumerator.");
        }

        [TestMethod]
        public void ExtendsTask ( )
        {
            EnumeratorTask a = new EnumeratorTask(SampleTask(), false);
            a.Extend(SampleExtension());
            (a as ITask).Start();
            Assert.AreEqual(4, i, "Looks like a continued task doesn't actually continue.");
        }

        [TestMethod]
        public void IsCompletedFunctional ( )
        {
            EnumeratorTask a = new EnumeratorTask(SampleTask(), false);
            Assert.IsFalse(a.IsCompleted, "Task thinks its completed before it ran.");
            (a as ITask).Start();
            Assert.IsTrue(a.IsCompleted, "Looks like the task doesn't register when it's completed.");
        }

        [TestMethod]
        public void CanExtendTwice ( )
        {
            EnumeratorTask a = new EnumeratorTask(SampleTask(), false);
            a.Extend(SampleExtension());
            a.Extend(SampleExtension());
            (a as ITask).Start();
            Assert.AreEqual(3, i, "Looks like a continued task doesn't actually continue multiple times.");
        }

        [TestMethod]
        public void NappingFunctions ( )
        {
            ActionTask t = new ActionTask(( _ ) => System.Threading.Thread.Sleep(0), false);
            EnumeratorTask a = new EnumeratorTask(SampleTaskWithYield(t), false);
            (a as ITask).Start();
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.AreEqual(6, i, "Ran extensions while napping or didn't run start before napping.");
            (t as ITask).Start();
            (a as ITask).Start();
            Assert.AreEqual(8, i, "After napping didn't continue correctly.");
            Assert.IsTrue(a.IsCompleted, "Doesn't think its done after napping.");
        }

        [TestMethod]
        public void ForceAwakeningFunctions ( )
        {
            ActionTask t = new ActionTask(( _ ) => System.Threading.Thread.Sleep(0), false);
            EnumeratorTask a = new EnumeratorTask(SampleTaskWithYield(t), false);
            (a as ITask).Start();
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.AreEqual(6, i, "Ran extensions while napping or didn't run start before napping.");
            a.IsNapping = false;
            (a as ITask).Start();
            Assert.AreEqual(8, i, "After napping didn't continue correctly.");
            Assert.IsTrue(a.IsCompleted, "Doesn't think its done after napping.");
        }

        [TestMethod]
        public void CanCancelExtensions ( )
        {
            ActionTask t = new ActionTask(( _ ) => System.Threading.Thread.Sleep(0), false);
            EnumeratorTask a = new EnumeratorTask(SampleTaskWithYield(t), false);
            (a as ITask).Start();
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.AreEqual(6, i, "Ran extensions while napping or didn't run start before napping.");
            a.CancelExtensions();
            a.IsNapping = false;
            (a as ITask).Start();

            Assert.AreEqual(6, i, "Task didn't correctly cancel extensions when aborted.");
        }
    }
}
