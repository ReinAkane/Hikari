using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    /// <summary>
    /// Base class for Thread- and UnityManager. Contains the helpful stuff
    /// that's shared between them for dispatching tasks.
    /// </summary>
    internal abstract class ManagerBase
    {
        /// <summary>
        /// Lock for our waiting list.
        /// </summary>
        protected object workLock;

        /// <summary>
        /// Queue of Tasks waiting to be executed.
        /// This includes all recently woken Tasks.
        /// </summary>
        protected Queue<ITask> waiting;
        /// <summary>
        /// List of napping Tasks.
        /// 
        /// Should only be accessed in UnsafeUpdate.
        /// </summary>
        protected List<ITask> napping;

        internal ManagerBase()
        {
            waiting = new Queue<ITask>();
            napping = new List<ITask>();
            workLock = new object();
        }

        /// <summary>
        /// Enqueues a task to be run when the next thread is available.
        /// </summary>
        /// <param name="task">The task to run.</param>
        internal virtual void EnqueueTask ( ITask task )
        {
            lock ( workLock ) waiting.Enqueue(task);
        }

        /// <summary>
        /// Checks for napping and awaked tasks, and assigns work.
        /// </summary>
        internal abstract void UnsafeUpdate ( );

        /// <summary>
        /// Checks all napping tasks and Enqueues the awakened ones.
        /// This is not threadsafe in regard to napping list.
        /// </summary>
        protected void UnsafeRequeueAwakenedTasks()
        {
            // Inspect nappers and pull any awakened ones to waiting
            List<ITask> not_napping = new List<ITask>();
            foreach ( ITask t in napping )
            {
                if ( !t.IsNapping )
                {
                    not_napping.Add(t);
                    EnqueueTask(t);
                }
            }

            // Remove awakened from napping list
            foreach ( ITask t in not_napping )
                napping.Remove(t);
        }
    }
}
