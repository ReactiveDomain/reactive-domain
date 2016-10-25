using System.Security.Permissions;
using System.Windows.Threading;

namespace ReactiveDomain.Tests.Helpers
{
    public static class DispatcherUtil
    {
        /// <summary>
        /// This is needed to get unit-tests for ViewModels to work.  After your unit-test causes
        /// various ReadModel properties to update, call this.  It will cause of the Subscribe()
        /// calls in all of the ViewModels to fire.  Then you can check the ViewModel properties.
        /// If you have sequences to test (update some RM properties, check the VM's, update 
        /// some more RM properties, check the VM's again) then you need to call DoEvents() after
        /// EACH SET of RM property updates.
        /// </summary>
        [SecurityPermission(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        public static void DoEvents()
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,
                new DispatcherOperationCallback(ExitFrame), frame);
            Dispatcher.PushFrame(frame);
        }

        private static object ExitFrame(object frame)
        {
            ((DispatcherFrame)frame).Continue = false;
            return null;
        }


    }
}