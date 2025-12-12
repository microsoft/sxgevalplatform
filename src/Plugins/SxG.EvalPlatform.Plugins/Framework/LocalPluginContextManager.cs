namespace SxG.EvalPlatform.Plugins.Common.Framework
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;

    public class LocalPluginContextManager
    {
        private static readonly ConcurrentDictionary<int, Stack<LocalPluginContext>> ContextPerThreadId = new ConcurrentDictionary<int, Stack<LocalPluginContext>>();

        /// <summary>
        /// Indicates that an execution thread is starting, caching an LocalPluginContext for the thread.
        /// </summary>
        /// <param name="currentContext">The <c>LocalPluginContext</c> with the context for the thread that will start its execution.</param>
        /// <remarks>FinalizingExecution should be called at the end of the thread execution by the base class
        /// to deallocate the resources.</remarks>
        public static void InitiatingExecution(LocalPluginContext currentContext)
        {
            ContextPerThreadId.GetOrAdd(GetCurrentThreadId(), _ => new Stack<LocalPluginContext>()).Push(currentContext);
        }

        /// <summary>
        /// Indicates that a thread has done its job in the current context.
        /// </summary>
        public static void FinalizingExecution()
        {
            var currentThreadId = GetCurrentThreadId();
            if (ContextPerThreadId.ContainsKey(currentThreadId))
            {
                var localPluginContexts = ContextPerThreadId[currentThreadId];
                if (localPluginContexts != null && (localPluginContexts.Count == 0 || localPluginContexts.Count == 1))
                {
                    // Count of 0 or null are here for safety
                    ContextPerThreadId.TryRemove(currentThreadId, out localPluginContexts);
                }
                else
                {
                    ContextPerThreadId[currentThreadId].Pop();
                }
            }
        }

        /// <summary>
        /// Retrieves the <c>LocalPluginContext</c> for the running thread.
        /// </summary>
        /// <returns>The <c>LocalPluginContext</c> for the running thread.</returns>
        public static LocalPluginContext GetCurrentContext()
        {
            var currentThreadId = GetCurrentThreadId();

            LocalPluginContext context = null;
            if (ContextPerThreadId.ContainsKey(currentThreadId))
            {
                context = ContextPerThreadId[currentThreadId].Peek();
            }

            return context;
        }

        /// <summary>
        /// Retrieves the thread ID which is used internally as the key for the LocalPluginContext.
        /// </summary>
        /// <returns>The current thread's ID.</returns>
        private static int GetCurrentThreadId()
        {
            var currentThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            return currentThreadId;
        }
    }
}
