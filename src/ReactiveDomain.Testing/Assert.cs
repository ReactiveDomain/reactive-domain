using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using Xunit;


namespace ReactiveDomain.Testing
{
    public class AssertEx
    {
        public static void CommandThrows<T>(Action fireAction) where T : Exception
        {
            var exp = Assert.Throws<CommandException>(fireAction);
            Assert.IsType<T>(exp.InnerException);
        }

        public static void ArraySegmentEqual<T>(
            T[] expectedSequence, T[] buffer, int offset = 0)
        {
            for (int i = 0; i < expectedSequence.Length; i++)
            {
                int b = i + offset;

                Assert.True(buffer[b].Equals(expectedSequence[i]),
                    $"Byte #{b} differs: {buffer[b]} != {expectedSequence[i]}");
            }
        }

        public static void IsOrBecomesFalse(Func<bool> func, int? timeout = null, string msg = null)
        {
            IsOrBecomesTrue(() => !func(), timeout, msg);
        }

        public static void IsOrBecomesTrue(Func<bool> func, int? timeout = null, string msg = null)
        {
            if (!timeout.HasValue) timeout = 1000;
            var waitLoops = timeout/10;
            for (int i = 0; i < waitLoops; i++) {
                SpinWait.SpinUntil(func, 10);
                if(func()) break;
                //DispatchOtherThings();
            }
            Assert.True(func(), msg ?? "");
        }
    }
}
