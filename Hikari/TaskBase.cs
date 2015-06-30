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
    /// TaskBase is threadsafe.
    /// </summary>
    /// <typeparam name="T">The object that acts as an action in this Task.</typeparam>
    /// When modifying TaskBase keep in mind that every public method must be
    /// thread safe! I recommend using _lock for your lock to avoid deadlocks.
    /// Child classes also use _lock.
    public abstract class TaskBase<T> : ITask
    {
        /// <summary>
        /// This is our beautiful lock.
        /// </summary>
        /// Used by child classes as well as TaskBase.
        protected object _lock;

        /// <summary>
        /// Whether or not the current task has completed.
        /// </summary>
        protected volatile bool isCompleted;
        /// <summary>
        /// Whether or not the task has been aborted.
        /// </summary>
        protected volatile bool aborted;

        /// <summary>
        /// Whether or not this task is on Unity's thread.
        /// </summary>
        protected volatile bool onUnityThread;

        /// <summary>
        /// Whether or not this Task is napping (released thread for other use)
        /// </summary>
        protected bool napping;

        /// <summary>
        /// Whether or not extensions should be automatically cancelled on abort.
        /// </summary>
        protected volatile bool cancelExtensionsOnAbort;

        /// <summary>
        /// Returns true if the task is known to be on Unity's thread.
        /// If false, assume the task is not on Unity's thread.
        /// </summary>
        public bool OnUnityThread { get { return onUnityThread; } }

        /// <summary>
        /// Returns true if the task has been requested to abort.
        /// By default, your action in the Task but read this to quit on its own,
        /// but you can set the Task on creation to cancel all extensions when
        /// aborted.
        /// </summary>
        public bool Aborted { get { return aborted; } }

        /// <summary>
        /// Whether or not the current task is completed.
        /// This will only be true after ALL extensions are completed.
        /// Completed tasks cannot be modified or run.
        /// 
        /// Instead of using IsCompleted to decide whether to extend a Task, use
        /// TryExtend (which returns false if the task couldn't be extended).
        /// </summary>
        public bool IsCompleted { get { return isCompleted; } }

        /// <summary>
        /// Whether or not to automatically cancel all extensions on Abort().
        /// 
        /// Default is false.
        /// </summary>
        public bool CancelExtensionsOnAbort { get { return cancelExtensionsOnAbort; } }

        /// <summary>
        /// Creates a new task with the passed action as the task to run.
        /// </summary>
        /// <param name="unity">Whether this Task will execute on Unity's thread.</param>
        internal TaskBase ( bool unity, bool cancel_extensions_on_abort )
        {
            _lock = new object();
            isCompleted = false;
            aborted = false;
            napping = false;
            cancelExtensionsOnAbort = cancel_extensions_on_abort;
            onUnityThread = unity;
        }

        public class CannotStartException : Exception { internal CannotStartException ( string msg ) : base(msg) { } }
        /// <summary>
        /// Starts this task!
        /// </summary>
        bool ITask.Start ( )
        {
            // Run the task
            bool now_napping = StartTask();

            // Notify Hikari of completion.
            if ( !now_napping )
                isCompleted = true;

            return now_napping;
        }

        /// <summary>
        /// This needs to actually start the task at hand.
        /// This must manage any and all extensions.
        /// </summary>
        /// <returns>True if the Task is now napping.</returns>
        protected abstract bool StartTask ( );

        /// <summary>
        /// Aborts the task. Note that it cannot stop the current action,
        /// but this will stop it from continuing.
        /// </summary>
        public void Abort ( )
        {
            lock ( _lock )
            {
                aborted = true;
                if ( cancelExtensionsOnAbort )
                    CancelExtensions();
            }
        }

        /// <summary>
        /// Cancels all extensions. Does not abort.
        /// 
        /// This is already locked.
        /// </summary>
        public abstract void CancelExtensions ( );

        /// <summary>
        /// Extends the Task to do the passed action. The new action will
        /// execute on the same Thread as the rest of the Task.
        /// 
        /// If the Task has already completed, or was aborted, this will simply
        /// return false.
        /// </summary>
        /// <param name="next">The next action to do.</param>
        /// <returns>Whether or not the task was successfully extended.</returns>
        public bool Extend ( T next )
        {
            lock ( _lock )
            {
                if ( !isCompleted && !aborted )
                {
                    InternalExtend(next);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Actually does the work of extending. Do NOT use _lock in here, it
        /// is already locked.
        /// </summary>
        /// <param name="next">The next item to extend with.</param>
        protected abstract void InternalExtend ( T next );

        /// <summary>
        /// Gets or sets whether this Task is napping.
        /// Napping Tasks will not start their extensions until IsNapping is set
        /// to false. Napping tasks may be moved to a different thread.
        /// 
        /// Base classes of TaskBase must implement their own stop of continuation
        /// when IsNapping is true.
        /// </summary>
        public virtual bool IsNapping
        {
            get { lock ( _lock ) return napping; }
            set { lock ( _lock ) napping = value; }
        }
    }
}
