using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using Xunit;


namespace ReactiveDomain.Testing
{
    public class AssertEx
    {
        /// <summary>
        /// Verifies that an action that sends a command throws a <see cref="CommandException"/> and the InnerException is of the correct type.
        /// </summary>
        /// <typeparam name="T">The expected type of the InnerException</typeparam>
        /// <param name="sendAction">An <see cref="Action"/> that sends a <see cref="Messaging.Command"/>.</param>
        public static void CommandThrows<T>(Action sendAction) where T : Exception
        {
            var exp = Assert.Throws<CommandException>(sendAction);
            Assert.IsType<T>(exp.InnerException);
        }

        /// <summary>
        /// Evaluates whether two array segments are equal.
        /// </summary>
        /// <typeparam name="T">The type of items in the sequence.</typeparam>
        /// <param name="expectedSequence">The expected values in the sequence.</param>
        /// <param name="buffer">The actual sequence whose values are to be checked.</param>
        /// <param name="offset">An index offset within <paramref name="buffer"/> where the sequence is expected to begin.</param>
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

        /// <summary>
        /// Asserts the given function will return false before the timeout expires.
        /// Repeatedly evaluates the function until false is returned or the timeout expires.
        /// Will return immediatly when the condition is false.
        /// Evaluates the timeout every 10 msec until expired.
        /// Will not yield the thread by default, if yielding is required to resolve deadlocks set yieldThread to true.
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">If true, the thread relinquishes the remainder of its time
        /// slice to any thread of equal priority that is ready to run.</param>
        public static void IsOrBecomesFalse(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            IsOrBecomesTrue(() => !func(), timeout, msg, yieldThread);
        }

        /// <summary>
        /// Asserts the given function will return true before the timeout expires.
        /// Repeatedly evaluates the function until true is returned or the timeout expires.
        /// Will return immediatly when the condition is true.
        /// Evaluates the timeout every 10 msec until expired.
        /// Will not yield the thread by default, if yielding is required to resolve deadlocks set yieldThread to true.
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">If true, the thread relinquishes the remainder of its time
        /// slice to any thread of equal priority that is ready to run.</param>
        public static void IsOrBecomesTrue(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            if (yieldThread) Thread.Sleep(0);
            if (!timeout.HasValue) timeout = 1000;
            var waitLoops = timeout / 10;
            var result = false;
            for (int i = 0; i < waitLoops; i++) {                
                if (SpinWait.SpinUntil(func, 10)){
                    result = true;
                    break;
                }
            }
            Assert.True(result, msg ?? "");
        }
    }
}
