# Hikari
Threading solution for Unity.

HikariThreading.Hikari is the entry point to the threading system. It is fully threadsafe
and designed to be accessed statically.

To schedule a task in Hikari use the following code:
Hikari.Schedule( ( ActionTask task ) => YourWorkHere(); )

To schedule a task to be run in Unity use the following code:
Hikari.ScheduleUnity( ( ActionTask task ) => YourWorkHere(); )
 
You may also schedule tasks using enumerators, similar to coroutines in Unity.
