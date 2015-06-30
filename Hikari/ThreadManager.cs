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
    internal class ThreadManager : ManagerBase, IDisposable
    {
        /// <summary>
        /// The addition times of all Tasks in the waiting queue.
        /// </summary>
        Queue<DateTime> waitingSpawns;

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
        DateTime lastThreadSpawn;

        /// <summary>
        /// The last time no Threads had nothing to do.
        /// </summary>
        DateTime lastNoBoredThreads;

        /// <summary>
        /// Creates a very customized ThreadManager.
        /// </summary>
        /// <param name="min_threads">The minimum number of threads to have spawned at any time.</param>
        /// <param name="max_threads">The maximum number of threads to have spawned in the pool. Dedicated threads may be spawned over the maximum.</param>
        /// <param name="min_ms_between_thread_spawn">The minimum amount of time to wait before spawning a new thread.</param>
        /// <param name="max_ms_task_waiting_before_thread_spawn">The maximum time that a Task can be waiting in the queue before we spawn a new thread.</param>
        /// <param name="max_queue_length_before_thread_spawn">The maximum number of Tasks waiting in queue before we spawn a new thread.</param>
        /// <param name="max_boredom_time_before_thread_despawn">The maximum amount of time a thread can be idle before despawning the thread.</param>
        internal ThreadManager ( int min_threads, int max_threads, TimeSpan min_ms_between_thread_spawn,
            TimeSpan max_ms_task_waiting_before_thread_spawn, uint max_queue_length_before_thread_spawn,
            TimeSpan max_boredom_time_before_thread_despawn )
            : base()
        {
            Initialize();

            minThreads = min_threads;
            maxThreads = max_threads;

            minMsBetweenThreadSpawn = min_ms_between_thread_spawn;
            maxQueueAgeInMsBeforeNewThread = max_ms_task_waiting_before_thread_spawn;
            maxQueueLengthBeforeNewThread = max_queue_length_before_thread_spawn;
            maxBoredomTimeBeforeThreadDespawn = max_boredom_time_before_thread_despawn;

            for ( int i = 0; i < minThreads; i++ )
                SpawnThread();
        }

        /// <summary>
        /// Creates a ThreadManager with customized logic on when to spawn and despawn threads.
        /// </summary>
        /// <param name="min_ms_between_thread_spawn">The minimum amount of time to wait before spawning a new thread.</param>
        /// <param name="max_ms_task_waiting_before_thread_spawn">The maximum time that a Task can be waiting in the queue before we spawn a new thread.</param>
        /// <param name="max_queue_length_before_thread_spawn">The maximum number of Tasks waiting in queue before we spawn a new thread.</param>
        /// <param name="max_boredom_time_before_thread_despawn">The maximum amount of time a thread can be idle before despawning the thread.</param>
        internal ThreadManager ( TimeSpan min_ms_between_thread_spawn,
            TimeSpan max_ms_task_waiting_before_thread_spawn, uint max_queue_length_before_thread_spawn,
            TimeSpan max_boredom_time_before_thread_despawn )
            : base()
        {
            Initialize();

            minMsBetweenThreadSpawn = min_ms_between_thread_spawn;
            maxQueueAgeInMsBeforeNewThread = max_ms_task_waiting_before_thread_spawn;
            maxQueueLengthBeforeNewThread = max_queue_length_before_thread_spawn;
            maxBoredomTimeBeforeThreadDespawn = max_boredom_time_before_thread_despawn;

            for ( int i = 0; i < minThreads; i++ )
                SpawnThread();
        }

        /// <summary>
        /// Creates a very customized ThreadManager.
        /// </summary>
        /// <param name="min_threads">The minimum number of threads to have spawned at any time.</param>
        /// <param name="max_threads">The maximum number of threads to have spawned in the pool. Dedicated threads may be spawned over the maximum.</param>
        internal ThreadManager ( int min_threads, int max_threads )
            : base()
        {
            Initialize();

            minThreads = min_threads;
            maxThreads = max_threads;

            for ( int i = 0; i < minThreads; i++ )
                SpawnThread();
        }

        /// <summary>
        /// Creates a ThreadManager with all the defaults.
        /// </summary>
        internal ThreadManager ( )
            : base()
        {
            Initialize();

            for ( int i = 0; i < minThreads; i++ )
                SpawnThread();
        }

        /// <summary>
        /// Initializes the basic options for the ThreadManager.
        /// </summary>
        private void Initialize ( )
        {
            waitingSpawns = new Queue<DateTime>();
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
            lastThreadSpawn = DateTime.Now;
            lastNoBoredThreads = DateTime.Now;
        }

        /// <summary>
        /// Checks for new work in the Queue and sends it out,
        /// checks for spawning and despawning threads.
        /// </summary>
        internal override void UnsafeUpdate ( )
        {
            // Check out nappers and pull any awakened ones to waiting
            UnsafeRequeueAwakenedTasks();

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
                    // Treat it as a thread spawn since it is essentially a new
                    // thread for the manager.
                    lastThreadSpawn = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Assigns work and removed napping Task from their threads.
        /// </summary>
        private void UnsafeHandleThreads ( )
        {
            List<Thread> bored_threads = new List<Thread>();

            // Get our bored and napping threads
            lock ( threadLock )
            {
                foreach ( Thread thread in threads )
                {
                    if ( !thread.Running )
                        bored_threads.Add(thread);
                    else if ( thread.Napping )
                    {
                        bored_threads.Add(thread);
                        napping.Add(thread.PullNapper());
                    }
                }
            }

            int num_bored = bored_threads.Count;
            // Give em work
            foreach ( Thread thread in bored_threads )
            {
                ITask task;

                // Grab the Task inside the lock so we don't fuck up,
                // then start the task outside in case the... actually I dunno why.
                // Thought I might have a perma lock from that but its on a
                // different thread.
                lock ( workLock )
                {
                    // If we're outta work, stop
                    if ( waiting.Count <= 0 )
                        break;
                    task = waiting.Dequeue();
                    waitingSpawns.Dequeue();
                }

                thread.StartTask(task);
                num_bored--;
            }

            if ( num_bored == 0 )
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
                waitingSpawns.Enqueue(DateTime.Now);
            }
        }

        /// <summary>
        /// Spawns a new thread and adds it to the thread pool.
        /// </summary>
        internal void SpawnThread ( )
        {
            lastThreadSpawn = DateTime.Now;
            numThreads++;
            System.Threading.Thread sys_thread = new System.Threading.Thread(( ) =>
                {
                    try
                    {
                        Thread t = new Thread();
                        lock ( threadLock ) threads.Add(t);
                        t.StartThread();
                    }
                    catch ( Exception e )
                    {
                        Hikari.ScheduleUnity(( _ ) => { throw e; });
                    }
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
                    try
                    {
                        Thread t = new Thread();
                        lock ( threadLock ) dedicatedThreads.Add(t);
                        t.StartTask(task);
                        t.StartThread();
                    }
                    catch ( Exception e )
                    {
                        Hikari.ScheduleUnity(( _ ) => { throw e; });
                    }
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
            if ( DateTime.Now - lastThreadSpawn < minMsBetweenThreadSpawn )
                return false;

            // Don't make one if there's no work.
            if ( waiting.Count == 0 )
                return false;

            // Alright now we can spawn a thread if the queue length is high enough
            // or if the next thing in the queue has been waiting long enough.
            if ( waiting.Count >= maxQueueLengthBeforeNewThread ||
                DateTime.Now - waitingSpawns.Peek() > maxQueueAgeInMsBeforeNewThread )
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
            // Start at the back to despawn new ones first (might have very
            // slight performance benefit).
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

        /// <summary>
        /// Waits for all Threads to spawn.
        /// This is to make automated testing easier.
        /// </summary>
        internal void WaitForThreadSpawns ( )
        {
            while ( true )
            {
                lock ( threadLock )
                    if ( numThreads == threads.Count )
                        break;

                System.Threading.Thread.Sleep(1);
            }
        }

        /// <summary>
        /// Tells all threads managed by this manager to stop running once
        /// their Tasks finish up.
        /// </summary>
        public void Dispose ( )
        {
            lock ( threadLock )
            {
                foreach ( Thread thread in threads )
                    thread.Stop();
                foreach ( Thread thread in dedicatedThreads )
                    thread.Stop();
            }
        }
    }
}
