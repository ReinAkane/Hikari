using System;
using System.Collections;
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
    /// EnumeratorTasks are Tasks that wrap Enumerators similar to coroutines.
    /// yield statements are treated as extensions, and as such napping will
    /// pause between yields.
    /// 
    /// You can yield certain statements to sleep for periods of time, or until
    /// other Tasks finish.
    /// </summary>
    public class EnumeratorTask : TaskBase<IEnumerator>
    {
        /// <summary>
        /// Continue the task with this in whatever thread this Task was run.
        /// </summary>
        System.Collections.Generic.Queue<IEnumerator> extensions;

        /// <summary>
        /// The current enumerator.
        /// </summary>
        IEnumerator current = null;

        /// <summary>
        /// If this is set to something, the EnumeratorTask will nap until
        /// the Task is completed.
        /// 
        /// Forcefully waking this task will override this.
        /// </summary>
        ITask napUntilComplete = null;

        internal EnumeratorTask ( IEnumerator action, bool unity, bool cancel_extensions_on_abort = true, bool is_dedicated = false )
            : base(unity, cancel_extensions_on_abort, is_dedicated)
        {
            extensions = new System.Collections.Generic.Queue<IEnumerator>();
            extensions.Enqueue(action);
        }

        /// <summary>
        /// Actually runs the enumerator.
        /// Pauses the enumerator and does not extend while napping.
        /// </summary>
        protected override bool StartTask ( )
        {
            while ( !IsNapping )
            {
                IEnumerator current;

                // Move to next Enumerator if needed
                lock ( _lock )
                {
                    if ( this.current == null )
                    {
                        // Nothing to dooo~
                        if ( extensions.Count <= 0 )
                            return false;
                        this.current = extensions.Dequeue();
                    }

                    // Store it! Gonna need to use it at least once w/o the lock.
                    current = this.current;
                }

                bool more_in_current = current.MoveNext();
                // Didn't see anything. Move along!
                if ( !more_in_current )
                {
                    lock ( _lock ) this.current = null;
                    continue;
                }
                object current_result = current.Current;

                // Looks like they're asking for a nap.
                if ( current_result != null ) HandleYield(current_result);
            }

            return true;
        }

        /// <summary>
        /// Thrown when an EnumeratorTask got a yielded object that it couldn't
        /// handle.
        /// </summary>
        public class CouldNotHandleYieldException : Exception
        {
            internal CouldNotHandleYieldException ( string msg ) : base(msg) { }
        }
        public class CannotWaitForSelfException : Exception
        {
            internal CannotWaitForSelfException ( string msg ) : base(msg) { }
        }
        /// <summary>
        /// Handles the return statement in a yield, putting the task to sleep
        /// for some amount of time.
        /// </summary>
        /// <param name="result">The object returned.</param>
        private void HandleYield ( object result )
        {
            ITask task = result as ITask;
            if ( task != null )
            {
                // Make sure they don't wait for themselves
                if ( task == this ) throw new CannotWaitForSelfException("A Task cannot wait for itself to finish.");

                lock ( _lock )
                    napUntilComplete = task;
                return;
            }

            // Add expected classes
            throw new CouldNotHandleYieldException("Could not handle yielded object " + result.ToString() + " of type " + result.GetType().Name +
                ".\nYou can yield null, or a TaskBase object.");
        }

        /// <summary>
        /// Cancels the processing of the current enumerator, and all future
        /// ones.
        /// </summary>
        public override void CancelExtensions ( )
        {
            lock ( _lock )
            {
                current = null;
                extensions.Clear();
            }
        }

        /// <summary>
        /// Actually does the work of extending. Do NOT use _lock in here, it
        /// is already locked.
        /// </summary>
        /// <param name="next">The next item to extend with.</param>
        protected override void InternalExtend ( IEnumerator next )
        {
            extensions.Enqueue(next);
        }

        /// <summary>
        /// Override to allow waiting for tasks.
        /// 
        /// Setting this to false while the task is waiting for another task
        /// will not awaken the task until the other task has completed. use
        /// ForceAwaken() for those situations.
        /// </summary>
        public override bool IsNapping
        {
            get
            {
                bool waiting = false;
                lock ( _lock ) waiting = (napUntilComplete != null && !napUntilComplete.IsCompleted);
                return waiting || base.IsNapping;
            }
            set
            {
                base.IsNapping = value;

            }
        }

        /// <summary>
        /// Forces the Task awake, even if it was waiting for another Task to
        /// finish.
        /// </summary>
        public void ForceAwaken ( )
        {
            lock ( _lock ) napUntilComplete = null;
            IsNapping = false;
        }
    }
}
