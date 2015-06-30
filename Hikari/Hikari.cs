using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace HikariThreading
{
    /// <summary>
    /// Never put Hikari on a GameObject! Hikari will spawn one for you.
    /// 
    /// Hikari is the entry point to the threading system. It is fully threadsafe
    /// and designed to be accessed statically.
    /// 
    /// To schedule a task in Hikari use the following code:
    /// <example>Hikari.Schedule( ( ActionTask task ) => YourWorkHere(); )</example>
    /// 
    /// To schedule a task in Unity use the following code:
    /// <example>Hikari.ScheduleUnity( ( ActionTask task ) => YourWorkHere(); )</example>
    /// 
    /// You may also schedule tasks using enumerators, similar to coroutines in Unity.
    /// </summary>
    /// If you are planning on modifying Hikari here are some things to know:
    /// The Hikari class delegates all work to our Managers (ManagerBase,
    /// ThreadManager, and UnityManager).
    /// 
    /// ThreadManager assigns our Thread objects ITasks to run.
    /// 
    /// UnityManager runs ITasks directly.
    /// 
    /// Users can use the various Task classes to control tasks in flight. TaskBase
    /// is the lowest level they have access to and contains method declarations for
    /// all the methods they will use, but many of those methods are defined in
    /// ActionTask and EnumeratorTask.
    public class Hikari : UnityEngine.MonoBehaviour
    {
        /// <summary>
        /// Starts up Hikari with all the default options. Not
        /// necessary to call, but if you don't a GameObject will be
        /// spawned the first time you call Hikari. The spawned
        /// GameObject will be set to not destroy on load.
        /// 
        /// Spawn is not threadsafe.
        /// </summary>
        /// <param name="max_tasks_per_frame">The maximum number of Tasks for Hikari to run in Unity per frame.</param>
        /// <param name="min_threads">The minimum number of Threads for Hikari to have spawned at once.</param>
        /// <param name="max_threads">The maximum number of Threads for Hikari to have spawned at once. Dedicated Tasks may cause Hikari to go over this maximum.</param>
        /// <param name="min_ms_between_thread_spawn">The minimum amount of time to wait before spawning a new thread.</param>
        /// <param name="max_ms_task_waiting_before_thread_spawn">The maximum time that a Task can be waiting in the queue before we spawn a new thread.</param>
        /// <param name="max_queue_length_before_thread_spawn">The maximum number of Tasks waiting in queue before we spawn a new thread.</param>
        /// <param name="max_boredom_time_before_thread_despawn">The maximum amount of time a thread can be idle before despawning the thread.</param>
        /// <returns>The spawned game object for Hikari.</returns>
        public static UnityEngine.GameObject Spawn ( int max_tasks_per_frame, int min_threads, int max_threads,
            TimeSpan min_ms_between_thread_spawn, TimeSpan max_ms_task_waiting_before_thread_spawn,
            uint max_queue_length_before_thread_spawn, TimeSpan max_boredom_time_before_thread_despawn )
        {
            UnityEngine.GameObject obj = StartSpawn();

            // Instantiate hikari
            hikari.threadManager = new ThreadManager(min_threads, max_threads, min_ms_between_thread_spawn,
                max_ms_task_waiting_before_thread_spawn, max_queue_length_before_thread_spawn,
                max_boredom_time_before_thread_despawn);
            hikari.unityManager = new UnityManager(max_tasks_per_frame);

            return obj;
        }

        /// <summary>
        /// Starts up Hikari with all the default options. Not
        /// necessary to call, but if you don't a GameObject will be
        /// spawned the first time you call Hikari. The spawned
        /// GameObject will be set to not destroy on load.
        /// 
        /// Spawn is not threadsafe.
        /// </summary>
        /// <param name="max_tasks_per_frame">The maximum number of Tasks for Hikari to run in Unity per frame.</param>
        /// <param name="min_threads">The minimum number of Threads for Hikari to have spawned at once.</param>
        /// <param name="max_threads">The maximum number of Threads for Hikari to have spawned at once. Dedicated Tasks may cause Hikari to go over this maximum.</param>
        /// <returns>The spawned game object for Hikari.</returns>
        public static UnityEngine.GameObject Spawn ( int max_tasks_per_frame, int min_threads, int max_threads )
        {
            UnityEngine.GameObject obj = StartSpawn();

            // Instantiate hikari
            hikari.threadManager = new ThreadManager(min_threads, max_threads);
            hikari.unityManager = new UnityManager(max_tasks_per_frame);

            return obj;
        }

        /// <summary>
        /// Starts up Hikari with all the default options. Not
        /// necessary to call, but if you don't a GameObject will be
        /// spawned the first time you call Hikari. The spawned
        /// GameObject will be set to not destroy on load.
        /// 
        /// Spawn is not threadsafe.
        /// </summary>
        /// <param name="max_tasks_per_frame">The maximum number of Tasks for Hikari to run in Unity per frame.</param>
        /// <returns>The spawned game object for Hikari.</returns>
        public static UnityEngine.GameObject Spawn ( int max_tasks_per_frame )
        {
            UnityEngine.GameObject obj = StartSpawn();

            // Instantiate hikari
            hikari.threadManager = new ThreadManager();
            hikari.unityManager = new UnityManager(max_tasks_per_frame);

            return obj;
        }

        /// <summary>
        /// Starts up Hikari with all the default options. Not
        /// necessary to call, but if you don't a GameObject will be
        /// spawned the first time you call Hikari. The spawned
        /// GameObject will be set to not destroy on load.
        /// 
        /// Spawn is not threadsafe.
        /// </summary>
        /// <param name="min_threads">The minimum number of Threads for Hikari to have spawned at once.</param>
        /// <param name="max_threads">The maximum number of Threads for Hikari to have spawned at once. Dedicated Tasks may cause Hikari to go over this maximum.</param>
        /// <returns>The spawned game object for Hikari.</returns>
        public static UnityEngine.GameObject Spawn ( int min_threads, int max_threads )
        {
            UnityEngine.GameObject obj = StartSpawn();

            // Instantiate hikari
            hikari.threadManager = new ThreadManager(min_threads, max_threads);
            hikari.unityManager = new UnityManager();

            return obj;
        }

        /// <summary>
        /// Starts up Hikari with all the default options. Not
        /// necessary to call, but if you don't a GameObject will be
        /// spawned the first time you call Hikari. The spawned
        /// GameObject will be set to not destroy on load.
        /// 
        /// Spawn is not threadsafe.
        /// </summary>
        /// <returns>The spawned game object for Hikari.</returns>
        public static UnityEngine.GameObject Spawn ( )
        {
            UnityEngine.GameObject obj = StartSpawn();

            // Instantiate hikari
            hikari.threadManager = new ThreadManager();
            hikari.unityManager = new UnityManager();

            return obj;
        }

        /// <summary>
        /// This does the reusable parts of the overloaded Spawn() method.
        /// </summary>
        /// <returns>The spawned game object for Hikari.</returns>
        private static UnityEngine.GameObject StartSpawn()
        {
            // Error if hikari exists
            if ( hikari != null )
                throw new Exception("Cannot spawn a second Hikari instance.");

            // Generate new Hikari and game object.
            UnityEngine.GameObject obj = new UnityEngine.GameObject("Autogenerated_HikariObject");
            DontDestroyOnLoad(obj);
            hikari = obj.AddComponent<Hikari>();

            return obj;
        }

        /// <summary>
        /// Failsafe so that no one accidentally creates a Hikari object.
        /// </summary>
        void Start ( )
        {
            if ( hikari != this )
                throw new Exception("Hikari must be started through Hikari.Spawn! Do NOT add it to any game objects!");
        }

        /// <summary>
        /// The lock for checking if we need to spawn Hikari.
        /// </summary>
        private static object _lock = new object();
        /// <summary>
        /// The single instance of Hikari in existence.
        /// Only reference this to set it in Spawn. Use Instance from everywhere
        /// else.
        /// </summary>
        private static Hikari hikari = null;
        /// <summary>
        /// The single instance of Hikari in existence.
        /// Will spawn one if there isn't one yet.
        /// </summary>
        private static Hikari Instance
        {
            get
            {
                lock ( _lock )
                    if ( hikari == null ) Spawn();
                return hikari;
            }
        }

        /// <summary>
        /// The manager and dispatcher for Hikari's spawned threads.
        /// </summary>
        private ThreadManager threadManager;
        /// <summary>
        /// The manager and dispatcher for Hikari's Unity work.
        /// </summary>
        private UnityManager unityManager;

        /// <summary>
        /// The maximum number of threads Hikari can spawn.
        /// </summary>
        public static int MaxThreads { get { return Instance.threadManager.MaxThreads; } }
        /// <summary>
        /// The minimum number of threads Hikari will have at the ready.
        /// </summary>
        public static int MinThreads { get { return Instance.threadManager.MinThreads; } }

        /// <summary>
        /// Schedules a task to be run in Hikari.
        /// </summary>
        /// <param name="to_schedule">The method to run in the task.</param>
        /// <param name="cancel_extensions_on_abort">Whether or not to cancel extensions automatically when the Task is aborted. Defaults to true.</param>
        /// <returns>The task that was added.</returns>
        public static ActionTask Schedule ( Action<ActionTask> to_schedule, bool cancel_extensions_on_abort = true )
        {
            ActionTask t = new ActionTask(to_schedule, false, cancel_extensions_on_abort);
            Instance.threadManager.EnqueueTask(t);
            return t;
        }

        /// <summary>
        /// Schedules a task to be run in Hikari.
        /// 
        /// Enumerator tasks may yield null to allow napping, or yield another
        /// Task to nap until that task finishes, then start up again.
        /// </summary>
        /// <param name="to_schedule">The enumerator to run in the task.</param>
        /// <param name="cancel_extensions_on_abort">Whether or not to cancel extensions automatically when the Task is aborted. Defaults to true.</param>
        /// <returns>The task that was added.</returns>
        public static EnumeratorTask Schedule ( System.Collections.IEnumerator to_schedule, bool cancel_extensions_on_abort = true )
        {
            EnumeratorTask t = new EnumeratorTask(to_schedule, false, cancel_extensions_on_abort);
            Instance.threadManager.EnqueueTask(t);
            return t;
        }

        /// <summary>
        /// Schedules a task to be run on Unity's thread.
        /// </summary>
        /// <param name="to_schedule">The method to run in the task.</param>
        /// <param name="cancel_extensions_on_abort">Whether or not to cancel extensions automatically when the Task is aborted. Defaults to true.</param>
        /// <returns>The task that was added.</returns>
        public static ActionTask ScheduleUnity ( Action<ActionTask> to_schedule, bool cancel_extensions_on_abort = true )
        {
            ActionTask t = new ActionTask(to_schedule, true, cancel_extensions_on_abort);
            Instance.unityManager.EnqueueTask(t);
            return t;
        }

        /// <summary>
        /// Schedules a task to be run on Unity's thread.
        /// 
        /// Enumerator tasks may yield null to allow napping, or yield another
        /// Task to nap until that task finishes, then start up again.
        /// </summary>
        /// <param name="to_schedule">The enumerator to run in the task.</param>
        /// <param name="cancel_extensions_on_abort">Whether or not to cancel extensions automatically when the Task is aborted. Defaults to true.</param>
        /// <returns>The task that was added.</returns>
        public static EnumeratorTask ScheduleUnity ( System.Collections.IEnumerator to_schedule, bool cancel_extensions_on_abort = true )
        {
            EnumeratorTask t = new EnumeratorTask(to_schedule, false, cancel_extensions_on_abort);
            Instance.unityManager.EnqueueTask(t);
            return t;
        }

        /// <summary>
        /// Creates a dedicated task and a thread for it.
        /// 
        /// Dedicated tasks will not relinquish control of their thread while
        /// napping, allowing them to restart immediately after napping.
        /// 
        /// Once the task has completed, its thread will be recycled in Hikari
        /// and may be used for other tasks.
        /// </summary>
        /// <param name="task">The action to run on the new dedicated task.</param>
        /// <param name="cancel_extensions_on_abort">Whether or not to cancel extensions automatically when the Task is aborted. Defaults to true.</param>
        /// <returns>The new dedicated task.</returns>
        public static ActionTask SpawnDedicatedTask ( Action<ActionTask> task, bool cancel_extensions_on_abort = true )
        {
            ActionTask t = new ActionTask(task, true, cancel_extensions_on_abort);
            Instance.threadManager.SpawnDedicatedThread(t);
            return t;
        }

        /// <summary>
        /// Creates a dedicated task and a thread for it.
        /// 
        /// Dedicated tasks will not relinquish control of their thread while
        /// napping, allowing them to restart immediately after napping.
        /// 
        /// Once the task has completed, its thread will be recycled in Hikari
        /// and may be used for other tasks.
        /// 
        /// Enumerator tasks may yield null to allow napping, or yield another
        /// Task to nap until that task finishes, then start up again.
        /// </summary>
        /// <param name="task">The enumerator to run on the new dedicated task.</param>
        /// <param name="cancel_extensions_on_abort">Whether or not to cancel extensions automatically when the Task is aborted. Defaults to true.</param>
        /// <returns>The new dedicated task.</returns>
        public static EnumeratorTask SpawnDedicatedTask ( System.Collections.IEnumerator task, bool cancel_extensions_on_abort = true )
        {
            EnumeratorTask t = new EnumeratorTask(task, true, cancel_extensions_on_abort);
            Instance.threadManager.SpawnDedicatedThread(t);
            return t;
        }

        /// <summary>
        /// Unity's update loop. All we need to do is update the managers.
        /// </summary>
        void Update ( )
        {
            // Maybe the threadManager should be moved to its own thread.
            threadManager.UnsafeUpdate();
            unityManager.UnsafeUpdate();
        }
    }
}
