using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HikariThreading;
using System.Diagnostics;

namespace HikariTests
{
    /// <summary>
    /// Summary description for ThreadManagerTests
    /// </summary>
    [TestClass]
    public class ThreadManagerTests
    {
        ThreadManager tm;
        public ThreadManagerTests()
        {
            tm = new ThreadManager();
        }

        [TestMethod]
        public void CanRunSomething ( )
        {
            int i = 0;
            ITask task = new ActionTask(( _ ) => i = 5, false);
            tm.EnqueueTask(task);
            tm.UnsafeUpdate();

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
        public void CanNapTask ( )
        {
            int i = 0;
            ActionTask task = new ActionTask(( _ ) => _.IsNapping = true, false);
            task.Extend(( _ ) => i = 5);
            tm.EnqueueTask(task);
            tm.UnsafeUpdate();

            for ( int ms = 0; ms < 1000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.IsNapping )
                    break;
            }

            Assert.IsTrue(task.IsNapping, "Task not napping.");
            Assert.AreEqual(0, i, "Task extension ran even though it's napping.");

            task.IsNapping = false;
            tm.UnsafeUpdate();

            for ( int ms = 0; ms < 1000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.IsCompleted )
                    break;
            }

            Assert.IsTrue(task.IsCompleted, "Task never completed.");
            Assert.AreEqual(5, i, "Task thought it completed, but did not run");
        }

        [TestMethod]
        public void CanUpdateWithoutRuns()
        {
            tm.UnsafeUpdate();
        }

        [TestMethod]
        public void CreatesNewThreadsWhenMoreThanTwoWorkItems ( )
        {
            tm.DespawnThread();
            int i = -2;
            System.Threading.Thread.Sleep(600);
            int cur_threads = tm.NumThreads;
            for ( i = 0; i < 5; i++ )
                tm.EnqueueTask(new ActionTask(( _ ) => i++, false));
            bool result = tm.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm.NumThreads, "Did not actually spawn more threads.");
            tm.UnsafeUpdate();
        }

        [TestMethod]
        public void DestroysThreadsWhenOldEnough ( )
        {
            tm.SpawnThread();
            System.Threading.Thread.Sleep(12000);
            int cur_threads = tm.NumThreads;
            bool result = tm.DespawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it despawned a thread.");
            Assert.IsTrue(cur_threads > tm.NumThreads, "Did not actually despawn a thread.");
        }

        [TestMethod]
        public void DedicatedTasksContinueAfterNapping()
        {
            int i = 0;
            ActionTask task = new ActionTask(( _ ) => _.IsNapping = true, false);
            task.Extend(( _ ) => i = 5);
            tm.SpawnDedicatedThread(task);
            for ( int ms = 0; ms < 1000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.IsNapping )
                    break;
            }
            Assert.IsTrue(task.IsNapping, "Task didn't nap.");
            Assert.AreEqual(0, i, "Task ran while napping.");
            task.IsNapping = false;
            for ( int ms = 0; ms < 1000; ms += 1 )
            {
                System.Threading.Thread.Sleep(1);
                if ( task.IsCompleted )
                    break;
            }
            Assert.AreEqual(5, i, "Task didn't run after napping.");
        }
    }
}
