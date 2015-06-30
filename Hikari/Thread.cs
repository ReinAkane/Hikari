using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    internal class Thread
    {
        internal class MultipleThreadClassesOnThread : Exception { }
        internal class MultipleTasksOnThread : Exception { }

        // This is the Thread object representing this particular thread.
        [ThreadStatic]
        Thread master;

        // This is the task that is running on this thread.
        ITask task = null;

        // Our lock for the thread.
        object _lock;

        // False if we need to stop this Thread.
        // Note that this is seperate from Task.Abort();
        volatile bool run;

        /// <summary>
        /// Returns true if this Thread is currently occupied.
        /// </summary>
        internal bool Running { get { lock ( _lock ) return task != null; } }

        /// <summary>
        /// Returns true if the Task in this Thread is currently napping.
        /// </summary>
        internal bool Napping { get { lock ( _lock ) return (task != null && task.IsNapping); } }

        /// <summary>
        /// Creates a Thread for the current thread (yo dawg).
        /// 
        /// Throws MultipleThreadClassesOnThread if there was already a Thread
        /// created for this thread.
        /// </summary>
        internal Thread ( )
        {
            if ( null != master )
                throw new MultipleThreadClassesOnThread();

            master = this;
            _lock = new object();
        }

        /// <summary>
        /// Pulls off the napper from this Thread and returns it.
        /// </summary>
        /// <returns>The Task on this thread.</returns>
        internal ITask PullNapper()
        {
            lock (_lock)
            {
                ITask result = task;
                task = null;
            }
            return task;
        }

        /// <summary>
        /// Starts the Thread to do some lifting around Tasks.
        /// 
        /// Throws MultipleThreadClassesOnThread if there was already a Thread
        /// created for this thread.
        /// </summary>
        internal void StartThread ( )
        {
            run = true;

            // Run thread manager
            Run();
        }

        /// <summary>
        /// Actually runs the manager.
        /// </summary>
        protected void Run ( )
        {
            while ( run )
            {
                // Run the task!
                if ( Running && !Napping )
                {
                    bool now_napping = task.Start();

                    // Hold on to it if its napping, the ThreadManager will pull it off.
                    if ( !now_napping )
                        lock(_lock) task = null;
                }

                // Let another thread go. We need to wait for a new task anyway.
                System.Threading.Thread.Sleep(0);
            }
        }

        /// <summary>
        /// Stops this Thread from checking for tasks.
        /// Does NOT call Task.Abort();
        /// </summary>
        internal void Stop()
        {
            run = false;
        }

        /// <summary>
        /// Starts a new task on this Thread.
        /// 
        /// Throws MultipleTasksOnThread if there is currently a task on this Thread.
        /// </summary>
        /// <param name="new_task">The new task to start.</param>
        internal void StartTask ( ITask new_task )
        {
            lock ( _lock )
            {
                if ( task != null )
                    throw new MultipleTasksOnThread();

                task = new_task;
            }
        }
    }
}
