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
        internal UnityManager ( ) : base() { }

        /// <summary>
        /// The maximum number of tasks to run in a single update.
        /// -1 = infinite.
        /// </summary>
        int maxTasksInAFrame = -1;

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
