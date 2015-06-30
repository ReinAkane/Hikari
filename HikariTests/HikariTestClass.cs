using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HikariThreading;

namespace HikariTests
{
    [TestClass]
    public class HikariTestClass
    {
        class ExpectedException : Exception { }

        System.Collections.IEnumerator EnumTask ( )
        {
            yield return null;
        }

        [TestMethod]
        public void CanRunSomething ( )
        {
            int i = 0;
            ActionTask task = Hikari.Schedule(( _ ) => i = 5);

            Hikari.Instance.Update();

            for ( int ms = 0; ms < 10000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.IsCompleted )
                    break;
            }

            Assert.IsTrue(task.IsCompleted, "Task never completed.");
            Assert.AreEqual(5, i, "Task thought it completed, but did not run");
        }

        [TestMethod]
        public void CanRunSomethingOnUnity ( )
        {
            int i = 0;
            ActionTask task = Hikari.ScheduleUnity(( _ ) => i = 5);

            Hikari.Instance.Update();

            Assert.IsTrue(task.IsCompleted, "Task never completed.");
            Assert.AreEqual(5, i, "Task thought it completed, but did not run");
        }

        [TestMethod]
        public void CanRunSomethingOnDedicated ( )
        {
            int i = 0;
            ActionTask task = Hikari.SpawnDedicatedTask(( _ ) => i = 5);

            Hikari.Instance.Update();

            for ( int ms = 0; ms < 10000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.IsCompleted )
                    break;
            }

            Assert.IsTrue(task.IsCompleted, "Task never completed.");
            Assert.AreEqual(5, i, "Task thought it completed, but did not run");
        }

        [TestMethod]
        public void FlagsWork ( )
        {
            int i = 0;
            ActionTask task = Hikari.ScheduleUnity(( _ ) => i = 5);
            Assert.IsTrue(task.OnUnityThread, "Unity flag not set.");
            Assert.IsFalse(task.IsDedicated, "Dedicated flag set wrongly.");
            task = Hikari.Schedule(( _ ) => i = 5);
            Assert.IsFalse(task.OnUnityThread, "Unity flag set wrongly.");
            Assert.IsFalse(task.IsDedicated, "Dedicated flag set wrongly.");
            task = Hikari.SpawnDedicatedTask(( _ ) => i = 5);
            Assert.IsFalse(task.OnUnityThread, "Unity flag set wrongly.");
            Assert.IsTrue(task.IsDedicated, "Dedicated flag not set.");

            Hikari.Instance.Update();
        }

        [TestMethod]
        public void FlagsWorkForEnumerators ( )
        {
            EnumeratorTask task = Hikari.ScheduleUnity(EnumTask());
            Assert.IsTrue(task.OnUnityThread, "Unity flag not set.");
            Assert.IsFalse(task.IsDedicated, "Dedicated flag set wrongly.");
            task = Hikari.Schedule(EnumTask());
            Assert.IsFalse(task.OnUnityThread, "Unity flag set wrongly.");
            Assert.IsFalse(task.IsDedicated, "Dedicated flag set wrongly.");
            task = Hikari.SpawnDedicatedTask(EnumTask());
            Assert.IsFalse(task.OnUnityThread, "Unity flag set wrongly.");
            Assert.IsTrue(task.IsDedicated, "Dedicated flag not set.");

            Hikari.Instance.Update();
        }

        [TestMethod]
        public void TasksRequeueOnExtensionCorrectly ( )
        {
            int i = 0;
            ActionTask task = Hikari.ScheduleUnity(( _ ) =>
                {
                    i++;
                    System.Threading.Thread.Sleep(100);
                    i++;
                });
            Hikari.Instance.Update();
            task.Extend(( _ ) => i++);
            Hikari.Instance.Update();

            for ( int h = 0; h < 200; h++ )
                if ( task.IsCompleted )
                    break;
                else
                    System.Threading.Thread.Sleep(1);

            Assert.AreEqual(3, i, "Task didn't run correctly when extended during runtime.");
            Assert.IsTrue(task.IsCompleted, "Task did not report completion.");

            task.Extend(( _ ) => i++);
            Hikari.Instance.Update();

            for ( int h = 0; h < 200; h++ )
                if ( task.IsCompleted )
                    break;
                else
                    System.Threading.Thread.Sleep(1);

            Assert.AreEqual(4, i, "Task didn't run correctly when extended after runtime.");
            Assert.IsTrue(task.IsCompleted, "Task did not report completion.");
        }

        [TestMethod]
        public void ExceptionsAreSentToUnity ( )
        {
            ActionTask task = Hikari.Schedule(( _ ) =>
            {
                throw new ExpectedException();
            });

            Hikari.Instance.Update();

            for ( int ms = 0; ms < 10000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.Failed )
                    break;
            }

            Assert.IsFalse(task.IsCompleted, "Task thinks it completed.");
            Assert.IsTrue(task.Failed, "Task doesn't think it failed.");

            bool exception_thrown = false;
            try
            {
                Hikari.Instance.Update();
            }
            catch ( ExpectedException )
            {
                exception_thrown = true;
            }
            Assert.IsTrue(exception_thrown, "Exception not thrown on Unity's thread.");
        }

        [TestMethod]
        public void HandledExceptionsAreNotSentToUnity ( )
        {
            bool exception_thrown = false;
            ActionTask task = Hikari.Schedule(( _ ) =>
            {
                throw new ExpectedException();
            });

            task.AddErrorHandler(( _ ) => exception_thrown = true);

            Hikari.Instance.Update();

            for ( int ms = 0; ms < 10000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.Failed )
                    break;
            }

            Assert.IsFalse(task.IsCompleted, "Task thinks it completed.");
            Assert.IsTrue(task.Failed, "Task doesn't think it failed.");

            Hikari.Instance.Update();

            Assert.IsTrue(exception_thrown, "Exception not thrown on Unity's thread.");
        }
    }
}
