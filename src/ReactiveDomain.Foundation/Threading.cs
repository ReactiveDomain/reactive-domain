using System;
using System.Reactive.Concurrency;
using System.Threading.Tasks;

namespace ReactiveDomain.Foundation
{
    public static class Threading
    {
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
        public static void RunOnUiThreadAsync(Action action)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false)
            {
                action(); // we're on the ui thread, just go for it
                return;
            }
            Task.Run(() => RunOnUiThread(_ => action()));
        }

        public static void RunOnUiThread(Action action)
        {
            RunOnUiThread(_ => action());
        }

        public static void RunOnUiThread(Action<object> action, object parm = null, bool fallback = true)
        {
            if (System.Windows.Application.Current?.Dispatcher.CheckAccess() ?? false)
            {
                action(parm); // we're on the ui thread, just go for it
                return;
            }
            // Execute the action on the UI thread.  Note that we sometimes call this
            // function from a unit-test, in which case there IS no UI thread.
            if (System.Windows.Application.Current != null)
                System.Windows.Application.Current.Dispatcher.Invoke(action, parm);
            else if (fallback)
                action(parm);
            else
                throw new InvalidOperationException("Unable to run on UI thread!");
        }
    }
}
