using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HikariThreading;

namespace HikariTests
{
    [TestClass]
    public class ActionTaskTests
    {
        [TestMethod]
        public void CancelExtensionsOnAbortSets ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => i = 1, false, true);
            Assert.IsTrue(a.CancelExtensionsOnAbort);
            a = new ActionTask(( task ) => i = 1, false, false);
            Assert.IsFalse(a.CancelExtensionsOnAbort);
        }

        [TestMethod]
        public void CanCancelExtensionsOnAbort ( )
        {
            int i = 0;
            ActionTask b = new ActionTask(( task ) =>
            {
                i = 2;
                task.Abort();
            }, false, true);
            b.Extend(( task ) => i = 4);
            (b as ITask).Start();
            Assert.AreEqual(2, i, "Task didn't correctly cancel extensions when aborted.");
        }

        [TestMethod]
        public void DoesTask ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => i = 1, false);
            bool result = (a as ITask).Start();
            Assert.AreEqual(1, i, "Looks like the task didn't execute when instantiated with an action.");
            Assert.IsFalse(result, "Task reported it was sleeping after completing.");
        }

        [TestMethod]
        public void ExtendsTask ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => i = 1, false);
            a.Extend(( task ) => i++);
            (a as ITask).Start();
            Assert.AreEqual(2, i, "Looks like a continued task doesn't actually continue.");
        }

        [TestMethod]
        public void IsCompletedFunctional ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => i = 1, false);
            Assert.IsFalse(a.IsCompleted, "Task thinks its completed before it ran.");
            (a as ITask).Start();
            Assert.IsTrue(a.IsCompleted, "Looks like the task doesn't register when it's completed.");
        }

        [TestMethod]
        public void CanExtendTwice ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => i = 1, false);
            a.Extend(( task ) => i++);
            a.Extend(( task ) => i++);
            (a as ITask).Start();
            Assert.AreEqual(3, i, "Looks like a continued task doesn't actually continue multiple times.");
        }

        [TestMethod]
        public void NappingFunctions ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => task.IsNapping = true, false);
            a.Extend(( task ) => i++);
            a.Extend(( task ) => i++);
            bool result = (a as ITask).Start();
            Assert.IsTrue(a.IsNapping, "Doesn't think it's napping...");
            Assert.IsFalse(a.IsCompleted, "Thinks its done while napping.");
            Assert.AreEqual(0, i, "Ran extensions while napping.");
            Assert.IsTrue(result, "Didn't report it was napping.");
            a.IsNapping = false;
            result = (a as ITask).Start();
            Assert.AreEqual(2, i, "After napping didn't continue correctly.");
            Assert.IsTrue(a.IsCompleted, "Doesn't think its done after napping.");
            Assert.IsFalse(result, "Reported it was still napping.");
        }

        [TestMethod]
        public void CanContinueInTask ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => i = 1, false);
            a.Extend(( task ) =>
                {
                    i++;
                    task.Extend(( task2 ) => i++);
                });
            (a as ITask).Start();
            Assert.AreEqual(3, i, "Looks like an extended task doesn't actually continue, when extended in the task.");
        }

        [TestMethod]
        public void CanAbort ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) => i = 1, false);
            a.Abort();
            Assert.IsTrue(a.Aborted, "Task doesn't think it was aborted.");

            ActionTask b = new ActionTask(( task ) =>
            {
                i = 2;
                task.Abort();
            }, false);
            b.Extend(( task ) =>
            {
                if ( !task.Aborted ) i = 3;
            });
            (b as ITask).Start();
            Assert.IsTrue(b.Aborted, "Task doesn't think it was aborted after continuation.");
            Assert.IsTrue(b.IsCompleted, "Task thinks it didn't complete after abortion.");
            Assert.AreEqual(2, i, "Task's aborted flag didn't set correctly.");
        }

        [TestMethod]
        public void CanCancelExtensions ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) =>
            {
                i = 1;
                task.CancelExtensions();
            }, false);
            a.Extend(( task ) => i = 3);
            (a as ITask).Start();
            Assert.AreEqual(1, i, "Task didn't correctly cancel extensions.");
        }

        [TestMethod]
        public void UnityFlagFunctional ( )
        {
            int i = 0;
            ActionTask a = new ActionTask(( task ) =>
            {
                if ( task.OnUnityThread ) i = 1;
            }, true);
            (a as ITask).Start();
            Assert.AreEqual(1, i, "OnUnityThread reported false, when should be true.");

            a = new ActionTask(( task ) =>
            {
                if ( task.OnUnityThread ) i = 2;
            }, false);
            (a as ITask).Start();
            Assert.AreEqual(1, i, "OnUnityThread reported true, when should be false.");
        }
    }
}
