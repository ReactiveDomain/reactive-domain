using System;
using System.Threading;
using ReactiveDomain.Bus;
using ReactiveDomain.Tests.Helpers;

// ReSharper disable once CheckNamespace - this is where it is supposed to be
namespace Xunit
{
    public partial class Assert
    {
        public static void IsOrBecomesFalse(Func<bool> func, int? timeout = null, string msg = null)
        {
            IsOrBecomesTrue(() => !func(), timeout, msg);
        }

        public static void IsOrBecomesTrue(Func<bool> func, int? timeout = null, string msg = null)
        {
            if (!timeout.HasValue) timeout = 750;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // ReSharper disable once LoopVariableIsNeverChangedInsideLoop - Yes it does
            while (!func())
            {
                Thread.Sleep(10);
                DispatcherUtil.DoEvents();
                if (sw.ElapsedMilliseconds > timeout) break;
            }
            True(func(), msg ?? "");
        }

        public static void CommandThrows<T>(Action fireAction) where T : Exception
        {
            var exp = Throws<CommandException>(fireAction);
            IsType<T>(exp.InnerException);
        }


        public static void ArraySegmentEqual<T>(
            T[] expectedSequence, T[] buffer, int offset = 0)
        {
            for (int i = 0; i < expectedSequence.Length; i++)
            {
                int b = i + offset;

                True(buffer[b].Equals(expectedSequence[i]),
                    $"Byte #{b} differs: {buffer[b]} != {expectedSequence[i]}");
            }
        }
    }  
}
