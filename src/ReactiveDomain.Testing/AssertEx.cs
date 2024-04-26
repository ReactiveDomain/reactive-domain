using System;
using System.Threading;
using ReactiveDomain.Foundation;
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
        /// Will return immediately when the condition is false.  
        /// Evaluates the condition on an exponential back off up to 250 ms until timeout.  
        /// Will yield the thread after each evaluation.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        public static void IsOrBecomesFalse(Func<bool> func, TimeSpan timeout, string msg = null)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentException("Timeout must be greater than zero", nameof(timeout));
            IsOrBecomesFalse(func, (int)timeout.TotalMilliseconds, msg);
        }

        /// <summary>
        /// Asserts the given function will return false before the timeout expires.  
        /// Repeatedly evaluates the function until false is returned or the timeout expires.  
        /// Will return immediately when the condition is false.  
        /// Evaluates the condition on an exponential back off up to 250 ms until timeout.  
        /// Will yield the thread after each evaluation.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">Ignored, will be removed in a future release.</param>
        public static void IsOrBecomesFalse(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            IsOrBecomesTrue(() => !func(), timeout, msg);
        }

        /// <summary>
        /// Asserts the given function will return true before the timeout expires.  
        /// Repeatedly evaluates the function until true is returned or the timeout expires.  
        /// Will return immediately when the condition is true.  
        /// Evaluates the condition on an exponential back off up to 250 ms until timeout.  
        /// Will yield the thread after each evaluation.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        public static void IsOrBecomesTrue(Func<bool> func, TimeSpan timeout, string msg = null)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentException("Timeout must be greater than zero", nameof(timeout));
            IsOrBecomesTrue(func, (int)timeout.TotalMilliseconds, msg);
        }

        /// <summary>
        /// Asserts the given function will return true before the timeout expires.  
        /// Repeatedly evaluates the function until true is returned or the timeout expires.  
        /// Will return immediately when the condition is true.  
        /// Evaluates the condition on an exponential back off up to 250 ms until timeout.  
        /// Will yield the thread after each evaluation.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">Ignored, will be removed in a future release.</param>
        public static void IsOrBecomesTrue(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            if (func())
            {
                Assert.True(true, msg ?? "");
                return;
            }

            var result = false;
            var startTime = Environment.TickCount; //returns MS since machine start
            var endTime = startTime + (timeout ?? 1000);


            var delay = 1;
            while (true)
            {
                if (EvaluateAfterDelay(func, TimeSpan.FromMilliseconds(delay)))
                {
                    result = true;
                    break;
                }

                var now = Environment.TickCount;
                if ((endTime - now) <= 0) { break; }
                if (delay < 250)
                {
                    delay = delay << 1;
                }
                delay = Math.Min(delay, endTime - now);
            }
            Assert.True(result, msg ?? "");
        }

        /// <summary>
        /// Asserts that the given read model will have at least the expected version before the
        /// timeout expires.  If a test needs to check an exact model version use
        /// 'IsOrBecomesTrue(()=> ReadModel.Version == [expectedVersion], [timeout])'. 
        /// </summary>
        /// <param name="readModel">The read model.</param>
        /// <param name="expectedVersion">The read model's expected minimum version.</param>
        /// <param name="timeout">The timeout.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        public static void AtLeastModelVersion(ReadModelBase readModel, int expectedVersion, TimeSpan timeout, string msg = null)
        {
            if (timeout <= TimeSpan.Zero)
                throw new ArgumentException("Timeout must be greater than zero", nameof(timeout));
            AtLeastModelVersion(readModel, expectedVersion, (int)timeout.TotalMilliseconds, msg);
        }

        /// <summary>
        /// Asserts that the given read model will have at least the expected version before the
        /// timeout expires.  If a test needs to check an exact model version use
        /// 'IsOrBecomesTrue(()=> ReadModel.Version == [expectedVersion], [timeout])'. 
        /// </summary>
        /// <param name="readModel">The read model.</param>
        /// <param name="expectedVersion">The read model's expected minimum version.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        public static void AtLeastModelVersion(ReadModelBase readModel, int expectedVersion, int? timeout = null, string msg = null)
        {
            IsOrBecomesTrue(() => readModel.Version >= expectedVersion, timeout, msg);
        }

        /// <summary>
        /// Evaluates the given function after the supplied delay.
        /// The current thread will yield at the start of the delay.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="delay">A delay <see cref="TimeSpan"/>.</param>
        public static bool EvaluateAfterDelay(Func<bool> func, TimeSpan delay)
        {
            Thread.Sleep(delay);
            return func();
        }
    }
}
