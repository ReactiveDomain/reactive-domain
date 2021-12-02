using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;

namespace ReactiveDomain.UI
{
    /// <summary>
    /// Static methods for running actions on different threads.
    /// </summary>
    public static class Threading
    {
        /// <summary>
        /// The current <see cref="DispatcherScheduler"/> if available, otherwise the <see cref="ThreadPoolScheduler"/>
        /// </summary>
        public static IScheduler MainThreadScheduler
        {
            get
            {
                try
                {
                    return DispatcherScheduler.Current;
                }
                catch
                {
                    return ThreadPoolScheduler.Instance;
                }
            }
        }

        /// <summary>
        /// Runs the specified action on the Dispatcher once the Dispatcher is not busy.
        /// </summary>
        /// <param name="action">The action to run.</param>
        public static void RunOnUiThreadAsync(Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false)
            {
                action(); // we're on the ui thread, just go for it
                return;
            }
            Task.Run(() => RunOnUiThread(_ => action()));
        }

        /// <summary>
        /// Runs the specified action on the Dispatcher immediately.
        /// </summary>
        /// <param name="action">The action to run.</param>
        public static void RunOnUiThread(Action action)
        {
            RunOnUiThread(_ => action());
        }

        /// <summary>
        /// Runs the specified action on the Dispatcher immediately, using the specified object as input to the action.
        /// </summary>
        /// <param name="action">The action to run.</param>
        /// <param name="param">The input to the action.</param>
        /// <param name="fallback">If <c>true</c> and the Dispatcher does not exist, e.g., when calling from a unit test,
        /// run the action on the calling thread.</param>
        /// <exception cref="InvalidOperationException">The Dispatcher does not exist and <paramref name="fallback"/> is <c>false</c>.</exception>
        public static void RunOnUiThread(Action<object> action, object param = null, bool fallback = true)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false)
            {
                action(param); // we're on the ui thread, just go for it
                return;
            }
            // Execute the action on the UI thread.  Note that we sometimes call this
            // function from a unit-test, in which case there IS no UI thread.
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.Dispatcher.Invoke(action, param);
            else if (fallback)
                action(param);
            else
                throw new InvalidOperationException("Unable to run on UI thread!");
        }
    }
}
