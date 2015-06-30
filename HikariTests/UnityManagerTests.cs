using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HikariThreading;
using System.Diagnostics;

namespace HikariTests
{
    [TestClass]
    public class UnityManagerTests
    {
        UnityManager um;

        [TestInitialize]
        public void Init ( )
        {
            um = new UnityManager();
        }

        [TestMethod]
        public void CanRunSomething ( )
        {
            int i = 0;
            ITask task = new ActionTask(( _ ) => i = 5, false);
            um.EnqueueTask(task);
            um.UnsafeUpdate();

            Assert.IsTrue(task.IsCompleted, "Task never completed.");
            Assert.AreEqual(5, i, "Task thought it completed, but did not run");
        }

        [TestMethod]
        public void CanNapTask ( )
        {
            int i = 0;
            ActionTask task = new ActionTask(( _ ) => _.IsNapping = true, false);
            task.Extend(( _ ) => i = 5);
            um.EnqueueTask(task);
            um.UnsafeUpdate();

            Assert.IsTrue(task.IsNapping, "Task not napping.");
            Assert.AreEqual(0, i, "Task extension ran even though it's napping.");

            task.IsNapping = false;
            um.UnsafeUpdate();

            Assert.IsTrue(task.IsCompleted, "Task never completed.");
            Assert.AreEqual(5, i, "Task thought it completed, but did not run");
        }

        [TestMethod]
        public void CanUpdateWithoutRuns ( )
        {
            um.UnsafeUpdate();
        }

        [TestMethod]
        public void CanLimitTasksPerUpdate ( )
        {
            int i = 0;
            um = new UnityManager(2);
            for ( int j = 0; j < 5; j++ )
            {
                ITask task = new ActionTask(( _ ) => i++, false);
                um.EnqueueTask(task);
            }
            um.UnsafeUpdate();
            Assert.AreEqual(2, i, "Didn't just run 2.");
            um.UnsafeUpdate();
            Assert.AreEqual(4, i, "Didn't just run 2.");
            um.UnsafeUpdate();
            Assert.AreEqual(5, i, "Didn't run last one correctly.");

            i = 0;
            um = new UnityManager(1);
            for ( int j = 0; j < 5; j++ )
            {
                ITask task = new ActionTask(( _ ) => i++, false);
                um.EnqueueTask(task);
            }
            for ( int j = 0; j < 5; j++ )
            {
                um.UnsafeUpdate();
                Assert.AreEqual(j + 1, i, "Didn't just run 1.");
            }
        }

        [TestMethod]
        public void NappingTestsDontCountTowardsLimit ( )
        {
            int i = 0;
            um = new UnityManager(1);
            ActionTask task = new ActionTask(( _ ) => _.IsNapping = true, false);
            task.Extend(( _ ) => i = 100);
            um.EnqueueTask(task);
            for ( int j = 0; j < 5; j++ )
            {
                ITask t = new ActionTask(( _ ) => i++, false);
                um.EnqueueTask(t);
            }
            um.UnsafeUpdate();
            Assert.IsTrue(task.IsNapping, "Task didn't start napping.");
            for ( int j = 0; j < 5; j++ )
            {
                um.UnsafeUpdate();
                Assert.AreEqual(j + 1, i, "Didn't just run 1.");
            }
            task.IsNapping = false;
            um.UnsafeUpdate();
            Assert.AreEqual(100, i, "Didn't run awakened task.");
        }
    }
}
