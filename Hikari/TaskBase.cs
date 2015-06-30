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
        /// Whether or not this task is on a dedicated thread.
        /// </summary>
        protected volatile bool isDedicated;

        /// <summary>
        /// Whether or not this task is on Unity's thread.
        /// </summary>
        protected volatile bool onUnityThread;

        /// <summary>
        /// Whether or not this Task is napping (released thread for other use)
        /// </summary>
        protected bool napping;

        /// <summary>
        /// Whether or not this Task errored out.
        /// </summary>
        protected volatile bool failed;

        /// <summary>
        /// Whether or not extensions should be automatically cancelled on abort.
        /// </summary>
        protected volatile bool cancelExtensionsOnAbort;

        /// <summary>
        /// Lock used for interacting with onError.
        /// </summary>
        private object errorLock;

        /// <summary>
        /// Will be called when this errors, on its own thread.
        /// If this is not set, it will pass the error on to Unity's thread.
        /// </summary>
        private Action<Exception> onError;

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
        /// TryExtend (which returns false if the task had completed).
        /// </summary>
        public bool IsCompleted { get { return isCompleted; } }

        /// <summary>
        /// Whether or not the Task is a dedicated Task.
        /// </summary>
        public bool IsDedicated { get { return isDedicated; } }

        /// <summary>
        /// Whether or not to automatically cancel all extensions on Abort().
        /// 
        /// Default is false.
        /// </summary>
        public bool CancelExtensionsOnAbort { get { return cancelExtensionsOnAbort; } }

        /// <summary>
        /// Returns true if this Task threw an exception during execution.
        /// </summary>
        public bool Failed { get { return failed; } }

        /// <summary>
        /// Creates a new task with the passed action as the task to run.
        /// </summary>
        /// <param name="unity">Whether this Task will execute on Unity's thread.</param>
        internal TaskBase ( bool unity, bool cancel_extensions_on_abort, bool is_dedicated = false )
        {
            _lock = new object();
            isCompleted = false;
            aborted = false;
            napping = false;
            failed = false;
            errorLock = new Object();
            cancelExtensionsOnAbort = cancel_extensions_on_abort;
            onUnityThread = unity;
            isDedicated = is_dedicated;
        }

        /// <summary>
        /// Starts this task!
        /// </summary>
        bool ITask.Start ( )
        {
            // Not completed no more!
            isCompleted = false;

            // Run the task
            bool now_napping = false;
            try
            {
                now_napping = StartTask();
            }
            catch (Exception e)
            {
                failed = true;

                lock (errorLock)
                {
                    if ( onError != null ) onError(e);
                    // The ThreadManager will catch this and pass it to Unity.
                    else throw e;
                }
            }

            // Notify Hikari of completion.
            if ( !now_napping && !failed )
                isCompleted = true;

            return now_napping && !failed;
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
        /// If the Task has already completed, or was aborted, this will
        /// restart the Task (on its old Thread) with the new extension.
        /// </summary>
        /// <param name="next">The next action to do.</param>
        /// <returns>True if the Task was still running.</returns>
        public bool Extend ( T next )
        {
            lock ( _lock )
            {
                InternalExtend(next);
                if ( !isCompleted && !aborted )
                    return true;
            }
            Hikari.RequeueTask(this);
            return false;
        }

        /// <summary>
        /// Extends the Task to do the passed action. The new action will
        /// execute on the same Thread as the rest of the Task.
        /// 
        /// If the Task has already completed, or was aborted, this will simply
        /// return false.
        /// </summary>
        /// <param name="next">The next action to do.</param>
        /// <returns>Whether or not the task was successfully extended.</returns>
        public bool TryExtend ( T next )
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
        /// Adds a delegate to the onError event.
        /// This event is called when an error occurs in this Task, on this
        /// Task's thread.
        /// 
        /// If AddErrorHandler is never called, errors will be rethrown in
        /// Unity's thread.
        /// </summary>
        /// <param name="handler">The handler to add to the onError event.</param>
        public void AddErrorHandler ( Action<Exception> handler )
        {
            lock ( errorLock )
                onError += handler;
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
