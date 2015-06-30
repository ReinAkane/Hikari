using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    /// <summary>
    /// Manages work for Unity's thread.
    /// All methods not marked Unsafe are thread safe.
    /// 
    /// UnsafeUpdate must be called for UnityManager to do any work, but
    /// UnityManager will not call it.
    /// </summary>
    internal class UnityManager : ManagerBase
    {
        /// <summary>
        /// Creates a new UnityManager with the specified maximum number of Tasks
        /// to run in a single frame.
        /// </summary>
        /// <param name="max_tasks_in_a_frame">The maximum number of Tasks to run in a single frame. Default is infinite.</param>
        internal UnityManager ( int max_tasks_in_a_frame = -1 ) : base() 
        {
            maxTasksInAFrame = max_tasks_in_a_frame;
        }

        /// <summary>
        /// The maximum number of tasks to run in a single update.
        /// -1 = infinite.
        /// </summary>
        int maxTasksInAFrame;

        /// <summary>
        /// Checks for napping and awaked tasks, and assigns work.
        /// </summary>
        internal override void UnsafeUpdate ( )
        {
            UnsafeRequeueAwakenedTasks();

            // Run the tasks, up to our maximum per update.
            for ( int i = 0; i != maxTasksInAFrame; i++ )
            {
                ITask t;
                lock ( workLock )
                {
                    if ( waiting.Count <= 0 )
                        break;

                    t = waiting.Dequeue();
                }

                bool now_napping = t.Start();
                // If it's napping, push it to the napping group.
                if ( now_napping )
                    napping.Add(t);
            }
        }
    }
}
