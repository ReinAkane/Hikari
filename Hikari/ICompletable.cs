using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    /// <summary>
    /// This interface is used to Nap until something completes, like a Task
    /// or a timer.
    /// </summary>
    public interface ICompletable
    {
        /// <summary>
        /// Returns true if this Completable is completed.
        /// Must be threadsafe.
        /// </summary>
        bool IsCompleted { get; }
    }
}
