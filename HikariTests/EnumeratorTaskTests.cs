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
            i = 6;
            yield return waitfor;
            i++;
            yield return null;
            i++;
        }

        [TestMethod]
        public void CancelExtensionsOnAbortSets ( )
        {
            EnumeratorTask a = new EnumeratorTask(SampleTask(), false, true);
            Assert.IsTrue(a.CancelExtensionsOnAbort);
            a = new EnumeratorTask(SampleTask(), false, false);
            Assert.IsFalse(a.CancelExtensionsOnAbort);
        }

        [TestMethod]
        public void DoesTask ( )
        {
            EnumeratorTask a = new EnumeratorTask(SampleTask(), false);
            bool result = (a as ITask).Start();
            Assert.AreEqual(5, i, "Looks like the task didn't execute when instantiated with an Enumerator.");
            Assert.IsFalse(result, "Reported that it was napping when it completed.");
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
            bool result = (a as ITask).Start();
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.IsTrue(result, "Reported it was completed while napping.");
            Assert.AreEqual(6, i, "Ran extensions while napping or didn't run start before napping.");
            (t as ITask).Start();
            result = (a as ITask).Start();
            Assert.AreEqual(8, i, "After napping didn't continue correctly.");
            Assert.IsFalse(result, "Reported it was still napping once finished.");
            Assert.IsTrue(a.IsCompleted, "Doesn't think its done after napping.");
        }

        [TestMethod]
        public void DoesNotForceAwakeByDefault ( )
        {
            ActionTask t = new ActionTask(( _ ) => System.Threading.Thread.Sleep(0), false);
            EnumeratorTask a = new EnumeratorTask(SampleTaskWithYield(t), false);
            bool result = (a as ITask).Start();
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.AreEqual(6, i, "Ran extensions while napping or didn't run start before napping.");
            a.IsNapping = false;
            Assert.IsTrue(a.IsNapping, "Forced awake by default.");
            (t as ITask).Start();
            result = (a as ITask).Start();
            Assert.AreEqual(8, i, "After napping didn't continue correctly.");
            Assert.IsFalse(result, "Reported it was still napping once finished.");
            Assert.IsTrue(a.IsCompleted, "Doesn't think its done after napping.");
        }

        [TestMethod]
        public void CanSetToNapWhileWaiting ( )
        {
            ActionTask t = new ActionTask(( _ ) => System.Threading.Thread.Sleep(0), false);
            EnumeratorTask a = new EnumeratorTask(SampleTaskWithYield(t), false);
            bool result = (a as ITask).Start();
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.AreEqual(6, i, "Ran extensions while napping or didn't run start before napping.");
            a.IsNapping = true;
            (t as ITask).Start();
            result = (a as ITask).Start();
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.AreEqual(6, i, "Ran extensions while napping or didn't run start before napping.");
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            a.IsNapping = false;
            result = (a as ITask).Start();
            Assert.AreEqual(8, i, "After napping didn't continue correctly.");
            Assert.IsFalse(result, "Reported it was still napping once finished.");
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
            a.ForceAwaken();
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
