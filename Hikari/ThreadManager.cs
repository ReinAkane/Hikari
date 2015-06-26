using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HikariThreading
{
    /// <summary>
    /// Manages assignment of work for a pool of threads.
    /// All methods not marked unsafe are threadsafe.
    /// 
    /// UnsafeUpdate must be called for ThreadManager to do any work,
    /// but ThreadManager will not call it.
    /// </summary>
    internal class ThreadManager : ManagerBase
    {
        /// <summary>
        /// The addition times of all Tasks in the waiting queue.
        /// </summary>
        Queue<DateTime> waitingBirths;

        /// <summary>
        /// Lock for the threads list.
        /// </summary>
        object threadLock;
        /// <summary>
        /// Our current threads.
        /// </summary>
        List<Thread> threads;
        /// <summary>
        /// This is a list of the threads that have been checked out as dedicated.
        /// Threads from here that have completed their task will merge into our
        /// larger pool of threads.
        /// </summary>
        List<Thread> dedicatedThreads;

        /// <summary>
        /// The maximum number of threads allowed in the ThreadManager.
        /// </summary>
        internal int MaxThreads { get { return maxThreads; } }
        int maxThreads;
        /// <summary>
        /// The minimum number of threads in the ThreadManager.
        /// </summary>
        internal int MinThreads { get { return minThreads; } }
        int minThreads;

        /// <summary>
        /// This is the current number of threads.
        /// May count some threads that haven't quite been spooled up.
        /// </summary>
        internal int NumThreads { get { return numThreads; } }
        int numThreads;

        /// <summary>
        /// Minimum amount of time to wait between spawning threads.
        /// Takes precendence over the maximums.
        /// </summary>
        TimeSpan minMsBetweenThreadSpawn = TimeSpan.FromMilliseconds(500);
        /// <summary>
        /// Maximum length of the waiting queue before spawning threads.
        /// </summary>
        uint maxQueueLengthBeforeNewThread = 4;
        /// <summary>
        /// Maximum age of the oldest item in the waiting queue before spawning
        /// new threads.
        /// </summary>
        TimeSpan maxQueueAgeInMsBeforeNewThread = TimeSpan.FromMilliseconds(1000);

        /// <summary>
        /// Maximum amount of time where a thread was doing nothing before
        /// despawning a thread.
        /// </summary>
        TimeSpan maxBoredomTimeBeforeThreadDespawn = TimeSpan.FromMilliseconds(10000);

        /// <summary>
        /// The last time a Thread was spawned.
        /// </summary>
        DateTime lastThreadBirth;

        /// <summary>
        /// The last time a Thread had nothing to do.
        /// </summary>
        DateTime lastNoBoredThreads;

        /// <summary>
        /// Creates a ThreadManager with the default number of max and min Threads.
        /// Also spawns minThreads threads.
        /// </summary>
        internal ThreadManager ( )
            : base()
        {
            waitingBirths = new Queue<DateTime>();
            threads = new List<Thread>();
            dedicatedThreads = new List<Thread>();
            threadLock = new object();
#if !NO_UNITY
            maxThreads = UnityEngine.SystemInfo.processorCount * 8;
            minThreads = UnityEngine.SystemInfo.processorCount - 1;
#else
            maxThreads = Environment.ProcessorCount * 8;
            minThreads = Environment.ProcessorCount - 1;
#endif
            numThreads = 0;
            lastThreadBirth = DateTime.Now;
            lastNoBoredThreads = DateTime.Now;

            for ( int i = 0; i < minThreads; i++ )
                SpawnThread();
        }

        /// <summary>
        /// Checks for new work in the Queue and sends it out,
        /// checks for spawning and despawning threads.
        /// </summary>
        internal override void UnsafeUpdate ( )
        {
            // Check out nappers and pull any awakened ones to waiting
            UnsafeRequeueAwakanedTasks();

            // Check if any dedicated threads are done. If so, add them to the pool.
            HandleDedicatedThreads();

            // Assign work, and remove napping threads from action.
            UnsafeHandleThreads();

            // Check for despawning and spawning
            // This only checks for a despawn if we didn't spawn.
            lock ( workLock )
            {
                if ( !SpawnThreadIfNeeded() )
                    DespawnThreadIfNeeded();
            }
        }

        /// <summary>
        /// Checks if any dedicated threads are finished, and if so adds them
        /// to the larger pool.
        /// </summary>
        private void HandleDedicatedThreads ( )
        {
            lock ( threadLock )
            {
                List<Thread> finishedThreads = new List<Thread>();
                // Find the finished ones
                foreach ( Thread t in dedicatedThreads )
                {
                    if ( !t.Running )
                        finishedThreads.Add(t);
                }

                // Move to finished ones
                foreach ( Thread t in finishedThreads )
                {
                    dedicatedThreads.Remove(t);
                    threads.Add(t);
                    numThreads++;
                    // Treat it as a thread birth since it is essentially a new
                    // thread for the manager.
                    lastThreadBirth = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Assigns work and removed napping Task from their threads.
        /// </summary>
        private void UnsafeHandleThreads ( )
        {
            List<Thread> boredThreads = new List<Thread>();

            // Get our bored and napping threads
            lock ( threadLock )
            {
                foreach ( Thread t in threads )
                {
                    if ( !t.Running )
                        boredThreads.Add(t);
                    else if ( t.Napping )
                    {
                        boredThreads.Add(t);
                        napping.Add(t.PullNapper());
                    }
                }
            }

            int bored = boredThreads.Count;
            // Give em work
            foreach ( Thread t in boredThreads )
            {
                ITask job;

                // Grab the Task inside the lock so we don't fuck up,
                // then start the task outside in case the... actually I dunno why.
                // Thought I might have a perma lock from that but its on a
                // different thread.
                lock ( workLock )
                {
                    // If we're outta work, stop
                    if ( waiting.Count <= 0 )
                        break;
                    job = waiting.Dequeue();
                    waitingBirths.Dequeue();
                }

                t.StartTask(job);
                bored--;
            }

            if ( bored == 0 )
                lastNoBoredThreads = DateTime.Now;
        }

        /// <summary>
        /// Enqueues a task to be run when the next thread is available.
        /// </summary>
        /// <param name="task">The task to run.</param>
        internal override void EnqueueTask ( ITask task )
        {
            // Rather than calling the base class we completely override to save
            // some time on the lock statement.
            lock ( workLock )
            {
                waiting.Enqueue(task);
                waitingBirths.Enqueue(DateTime.Now);
            }
        }

        /// <summary>
        /// Spawns a new thread and adds it to the thread pool.
        /// </summary>
        internal void SpawnThread ( )
        {
            lastThreadBirth = DateTime.Now;
            numThreads++;
            System.Threading.Thread sys_thread = new System.Threading.Thread(( ) =>
                {
                    Thread t = Thread.Spool();
                    lock ( threadLock ) threads.Add(t);
                    t.StartThread();
                });
            sys_thread.IsBackground = true;
            sys_thread.Start();
        }

        /// <summary>
        /// Spawns a dedicated thread to run the passed task.
        /// Once the task is completed, the Thread will be recycled.
        /// </summary>
        /// <param name="task">The task to run on the thread as it starts.</param>
        internal void SpawnDedicatedThread ( ITask task )
        {
            System.Threading.Thread sys_thread = new System.Threading.Thread(( ) =>
                {
                    Thread t = Thread.Spool();
                    lock ( threadLock ) dedicatedThreads.Add(t);
                    t.StartTask(task);
                    t.StartThread();
                });
            sys_thread.IsBackground = true;
            sys_thread.Start();
        }

        /// <summary>
        /// Checks if our situation calls for a new thread. If so, spawns one.
        /// </summary>
        /// <returns>True if a thread was spawned.</returns>
        internal bool SpawnThreadIfNeeded ( )
        {
            // No going over the maximum!
            if ( numThreads >= maxThreads )
                return false;

            // No spawning if it was recent!
            if ( DateTime.Now - lastThreadBirth < minMsBetweenThreadSpawn )
                return false;

            // Don't make one if there's no work.
            if ( waiting.Count == 0 )
                return false;

            // Alright now we can spawn a thread if the queue length is high enough
            // or if the next thing in the queue has been waiting long enough.
            if ( waiting.Count >= maxQueueLengthBeforeNewThread ||
                DateTime.Now - waitingBirths.Peek() > maxQueueAgeInMsBeforeNewThread )
            {
                SpawnThread();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checks if our situation calls for a thread to be despawned. If so,
        /// despawns one.
        /// </summary>
        /// <returns>True if a thread was despawned.</returns>
        internal bool DespawnThreadIfNeeded ( )
        {
            // No going under the minimum
            if ( numThreads <= minThreads )
                return false;

            // If the boredom time is long enough, despawn and reset it.
            if ( DateTime.Now - lastNoBoredThreads > maxBoredomTimeBeforeThreadDespawn )
            {
                DespawnThread();
                lastNoBoredThreads = DateTime.Now;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Despawns a thread.
        /// </summary>
        internal void DespawnThread ( )
        {
#if DEBUG
            bool despawned = false;
#endif

            // Look for a bored one to despawn.
            // Start at the back to despawn new ones first because...
            // I dunno I just feel like it.
            for ( int i = threads.Count - 1; i >= 0; i-- )
            {
                // Found one!
                if ( !threads[i].Running )
                {
                    threads[i].Stop();
                    threads.RemoveAt(i);
                    numThreads--;
#if DEBUG
                    despawned = true;
#endif
                    break;
                }
            }

            // The DEBUG statements here are to help catch a possible race
            // condition. It's not a game breaker, but should be fixed if it
            // exists so we'll only fail if we're in debug.
#if DEBUG
            if ( !despawned )
                throw new Exception("Tried to despawn a Thread but didn't find any bored ones. Is there a race condition?");
#endif
        }
    }
}
