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
        [TestInitialize]
        public void Init ( )
        {
            tm = new ThreadManager(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100),
                2, TimeSpan.FromMilliseconds(200));
        }

        [TestMethod]
        public void MaxAndMinDefaultsSet ( )
        {
            ThreadManager tm = new ThreadManager();
            Assert.AreEqual(Environment.ProcessorCount * 8, tm.MaxThreads);
            Assert.AreEqual(Environment.ProcessorCount - 1, tm.MinThreads);
        }

        [TestMethod]
        public void MinThreadsFunctions ( )
        {
            ThreadManager tm = new ThreadManager(1, 100);
            Assert.AreEqual(1, tm.NumThreads);
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
        public void CanUpdateWithoutRuns ( )
        {
            tm.UnsafeUpdate();
        }

        [TestMethod]
        public void MinTimeActuallyStopsCreationOfThreads ( )
        {
            ThreadManager tm = new ThreadManager(TimeSpan.FromMilliseconds(100), TimeSpan.MinValue,
                uint.MaxValue, TimeSpan.MaxValue);
            int i = 0;
            int cur_threads = tm.NumThreads;
            tm.EnqueueTask(new ActionTask(( _ ) => i++, false));
            bool result = tm.SpawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it needed new threads before reaching the timeout.");
            Assert.AreEqual(cur_threads, tm.NumThreads, "Spawned another thread before reaching the timeout");
            System.Threading.Thread.Sleep(100);
            result = tm.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm.NumThreads, "Did not actually spawn more threads.");
            // Second thread...
            cur_threads = tm.NumThreads;
            result = tm.SpawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it needed new threads before reaching the timeout.");
            Assert.AreEqual(cur_threads, tm.NumThreads, "Spawned another thread before reaching the timeout");
            System.Threading.Thread.Sleep(100);
            result = tm.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm.NumThreads, "Did not actually spawn more threads.");

            ThreadManager tm2 = new ThreadManager(TimeSpan.FromMilliseconds(300), TimeSpan.MinValue,
                uint.MaxValue, TimeSpan.MaxValue);
            cur_threads = tm2.NumThreads;
            tm2.EnqueueTask(new ActionTask(( _ ) => i++, false));
            result = tm2.SpawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it needed new threads before reaching the timeout.");
            Assert.AreEqual(cur_threads, tm2.NumThreads, "Spawned another thread before reaching the timeout");
            System.Threading.Thread.Sleep(100);
            result = tm2.SpawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it needed new threads before reaching the timeout.");
            Assert.AreEqual(cur_threads, tm2.NumThreads, "Spawned another thread before reaching the timeout");
            System.Threading.Thread.Sleep(200);
            result = tm2.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm2.NumThreads, "Did not actually spawn more threads.");
        }

        [TestMethod]
        public void CreatesNewThreadsWhenMoreThanAskedForWorkItems ( )
        {
            ThreadManager tm = new ThreadManager(TimeSpan.MinValue, TimeSpan.MaxValue,
                2, TimeSpan.MaxValue);
            int i = -2;
            int cur_threads = tm.NumThreads;
            for ( int j = 0; j < 3; j++ )
                tm.EnqueueTask(new ActionTask(( _ ) => i++, false));
            bool result = tm.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm.NumThreads, "Did not actually spawn more threads.");


            ThreadManager tm2 = new ThreadManager(TimeSpan.MinValue, TimeSpan.MaxValue,
                6, TimeSpan.MaxValue);
            cur_threads = tm2.NumThreads;
            for ( i = 0; i < 3; i++ )
                tm2.EnqueueTask(new ActionTask(( _ ) => i++, false));
            result = tm2.SpawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it spawned more threads before reaching the max queue length.");
            Assert.IsTrue(cur_threads == tm2.NumThreads, "Spawned a new thread before reaching the max queue length.");
            for ( i = 0; i < 3; i++ )
                tm2.EnqueueTask(new ActionTask(( _ ) => i++, false));
            result = tm2.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm2.NumThreads, "Did not actually spawn more threads.");
        }

        [TestMethod]
        public void CreatesNewThreadsWhenStuffWaitsTooLong ( )
        {
            ThreadManager tm = new ThreadManager(TimeSpan.MinValue, TimeSpan.MinValue,
                uint.MaxValue, TimeSpan.MaxValue);
            int i = 0;
            int cur_threads = tm.NumThreads;
            tm.EnqueueTask(new ActionTask(( _ ) => i++, false));
            bool result = tm.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm.NumThreads, "Did not actually spawn more threads.");

            ThreadManager tm2 = new ThreadManager(TimeSpan.MinValue, TimeSpan.FromMilliseconds(300),
                uint.MaxValue, TimeSpan.MaxValue);
            cur_threads = tm2.NumThreads;
            tm2.EnqueueTask(new ActionTask(( _ ) => i++, false));
            result = tm2.SpawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it needed new threads before reaching the timeout.");
            Assert.AreEqual(cur_threads, tm2.NumThreads, "Spawned another thread before reaching the timeout");
            System.Threading.Thread.Sleep(300);
            result = tm2.SpawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it spawned more threads.");
            Assert.IsTrue(cur_threads < tm2.NumThreads, "Did not actually spawn more threads.");
        }

        [TestMethod]
        public void DestroysThreadsWhenOldEnough ( )
        {
            tm.SpawnThread();
            System.Threading.Thread.Sleep(300);
            int cur_threads = tm.NumThreads;
            bool result = tm.DespawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it despawned a thread.");
            Assert.IsTrue(cur_threads > tm.NumThreads, "Did not actually despawn a thread.");

            ThreadManager tm2 = new ThreadManager(TimeSpan.MinValue, TimeSpan.MaxValue, 100, TimeSpan.FromMilliseconds(1));
            tm2.SpawnThread();
            cur_threads = tm2.NumThreads;
            System.Threading.Thread.Sleep(10);
            result = tm2.DespawnThreadIfNeeded();
            Assert.IsTrue(result, "Did not report that it despawned a thread.");
            Assert.IsTrue(cur_threads > tm2.NumThreads, "Did not actually despawn a thread.");
        }

        [TestMethod]
        public void DoesNotGoBelowMinimumThreads ( )
        {
            ThreadManager tm = new ThreadManager(1, 100, TimeSpan.MaxValue, TimeSpan.MaxValue, 100, TimeSpan.FromMilliseconds(100));
            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(1, tm.NumThreads, "Spawned more than the minimum threads?");
            bool result = tm.DespawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it despawned a thread when at minimum.");
            Assert.AreEqual(1, tm.NumThreads, "Despawned a thread to get below minimum.");

            tm = new ThreadManager(5, 100, TimeSpan.MaxValue, TimeSpan.MaxValue, 100, TimeSpan.FromMilliseconds(100));
            System.Threading.Thread.Sleep(200);
            Assert.AreEqual(5, tm.NumThreads, "Spawned more than the minimum threads?");
            result = tm.DespawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it despawned a thread when at minimum.");
            Assert.AreEqual(5, tm.NumThreads, "Despawned a thread to get below minimum.");
        }

        [TestMethod]
        public void DoesNotDespawnThreadsTooFast ( )
        {
            tm.SpawnThread();
            int cur_threads = tm.NumThreads;
            bool result = tm.DespawnThreadIfNeeded();
            Assert.IsFalse(result, "Reported that it despawned a thread very quickly.");
            Assert.IsFalse(cur_threads > tm.NumThreads, "Despawned a thread very quickly.");
        }

        [TestMethod]
        public void DoesNotDespawnWorkingThread ( )
        {
            ThreadManager tm = new ThreadManager(0, 1, TimeSpan.MinValue, TimeSpan.MaxValue, 1, TimeSpan.FromMilliseconds(10));
            tm.SpawnThread();
            ActionTask task = new ActionTask(( _ ) => System.Threading.Thread.Sleep(500), false);
            tm.EnqueueTask(task);
            tm.UnsafeUpdate();
            System.Threading.Thread.Sleep(20);
            tm.UnsafeUpdate();
            Assert.AreEqual(1, tm.NumThreads, "Despawned a thread while working.");
        }

        [TestMethod]
        public void DedicatedTasksContinueAfterNapping ( )
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

        [TestMethod]
        public void RunsWaitingTasksCorrectly ( )
        {
            int i = 0;
            tm = new ThreadManager(2, 2);
            for ( int j = 0; j < 10; j++ )
                tm.EnqueueTask(new ActionTask(( _ ) => i++, false));

            for ( int j = 2; j <= 10; j += 2 )
            {
                tm.UnsafeUpdate();
                System.Threading.Thread.Sleep(10);
                Assert.AreEqual(j, i, "Ran some number other than 2 of the Tasks at once on update #" + j / 2);
            }
        }

        [TestMethod]
        public void LetsOthersUseNappingThreadsCorrectly ( )
        {
            int i = 0;
            object _lock = new object();
            ThreadManager tman = new ThreadManager(1, 1);
            // Using this to test interaction very specifically.
            tman.WaitForThreadSpawns();
            ActionTask task = new ActionTask(( _ ) =>
            {
                _.IsNapping = true;
            }, false);
            task.Extend(( _ ) => i = 100);
            tman.EnqueueTask(task);
            for ( int j = 0; j < 10; j++ )
                tman.EnqueueTask(new ActionTask(( _ ) =>
                {
                    lock ( _lock )
                        i++;
                }, false));

            tman.UnsafeUpdate();
            for ( int j = 0; j < 100; j++ )
                if ( task.IsNapping )
                    break;
                else
                    System.Threading.Thread.Sleep(1);

            for ( int j = 0; j < 10; j++ )
            {
                tman.UnsafeUpdate();
                System.Threading.Thread.Sleep(1);
                lock ( _lock ) Assert.AreEqual(j + 1, i, "Ran some number other than 1 of the Tasks at once on update #" + j);
            }

            task.IsNapping = false;
            tman.UnsafeUpdate();
            System.Threading.Thread.Sleep(1);
            Assert.AreEqual(100, i, "Did not run awakened Task.");

            tman.Dispose();
        }
    }
}
