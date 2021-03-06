﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    /// <summary>
    /// The internal representation for tasks. This contains all of the methods
    /// on tasks that Hikari will use natively. Methods for the user's use are
    /// in TaskBase.
    /// </summary>
    internal interface ITask : ICompletable
    {
        /// <summary>
        /// Starts the Task.
        /// 
        /// Returns whether or not its napping.
        /// </summary>
        bool Start ( );

        /// <summary>
        /// Returns true if the Task is completed in its entirety.
        /// While Napping, returns false.
        /// </summary>
        bool IsCompleted { get; }

        /// <summary>
        /// Returns true if the Task is napping at the moment.
        /// Note that napping is when a Task can release control of its Thread
        /// until something awakens it.
        /// </summary>
        bool IsNapping { get; }

        /// <summary>
        /// Returns true if the task is known to be on Unity's thread.
        /// If false, assume the task is not on Unity's thread.
        /// </summary>
        bool OnUnityThread { get; }

        /// <summary>
        /// Returns true if the task was created as a dedicated task.
        /// </summary>
        bool IsDedicated { get; }
    }
}
