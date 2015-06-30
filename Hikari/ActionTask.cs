using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    /// <summary>
    /// Multithreading in Hikari is completely made up of managing Tasks.
    /// A Task may have its own thread, be run on a shared thread, or be run on
    /// Unity's thread. Tasks will act the same regardless of their thread
    /// situation.
    /// 
    /// ActionTasks are Tasks that wrap simple delegates.
    /// </summary>
    public class ActionTask : TaskBase<Action<ActionTask>>
    {
        /// <summary>
        /// Continue the task with these in whatever thread this Task was run.
        /// </summary>
        Queue<Action<ActionTask>> extensions;

        /// <summary>
        /// Creates a new Task with the passed action as the task to run.
        /// </summary>
        /// <param name="task">The task to run.</param>
        /// <param name="unity">Whether this Task will execute on Unity's thread.</param>
        /// <param name="cancel_extensions_on_abort">Whether or not it will automatically cancel all extensions when the Task is aborted.</param>
        public ActionTask ( Action<ActionTask> task, bool unity, bool cancel_extensions_on_abort = true )
            : base(unity, cancel_extensions_on_abort)
        {
            extensions = new Queue<Action<ActionTask>>();
            extensions.Enqueue(task);
        }

        /// <summary>
        /// Runs the task and all extensions.
        /// Stops running extensions while Napping.
        /// </summary>
        protected override bool StartTask ( )
        {
            while ( !IsNapping )
            {
                Action<ActionTask> current;
                // Grab the action.
                lock ( _lock )
                {
                    // Guess we're done!
                    if ( extensions.Count <= 0 )
                        return false;
                    current = extensions.Dequeue();
                }
                // Make sure we aren't holding the lock while running the action,
                // as the action may change states in this Task.
                current(this);
            }

            return true;
        }

        /// <summary>
        /// Cancels all extensions. Does not abort.
        /// </summary>
        public override void CancelExtensions ( )
        {
            lock ( _lock ) extensions.Clear();
        }

        /// <summary>
        /// Actually does the work of extending. Do NOT use _lock in here, it
        /// is already locked.
        /// </summary>
        /// <param name="next">The next item to extend with.</param>
        protected override void InternalExtend ( Action<ActionTask> next )
        {
            extensions.Enqueue(next);
        }
    }
}
