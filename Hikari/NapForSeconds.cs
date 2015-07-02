using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    public class NapForSeconds : ICompletable
    {
        /// <summary>
        /// The time at which this NapForSeconds will be completed.
        /// </summary>
        private DateTime awakenTime;

        /// <summary>
        /// Creates a new NapForSeconds object that will nap for the passed
        /// number of seconds.
        /// </summary>
        /// <param name="seconds"></param>
        public NapForSeconds(float seconds)
        {
            awakenTime = DateTime.Now + TimeSpan.FromSeconds(seconds);
        }

        /// <summary>
        /// Returns true if we have passed or reached the awaken time.
        /// </summary>
        public bool IsCompleted { get { return DateTime.Now >= awakenTime; } }
    }
}
