using System;
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
    internal interface ITask
    {
        /// <summary>
        /// Starts the Task.
        /// </summary>
        void Start ( );

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
    }
}
