using System;
using System.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;
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
        /// Will yield the thread after each evaluation, use the hint yieldThread if it is known the function should yield before evaluation.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">Execution hint to yield thread before evaluation</param>
        public static void IsOrBecomesFalse(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            IsOrBecomesTrue(() => !func(), timeout, msg, yieldThread);
        }

        /// <summary>
        /// Asserts the given function will return true before the timeout expires.  
        /// Repeatedly evaluates the function until true is returned or the timeout expires.  
        /// Will return immediately when the condition is true.  
        /// Evaluates the condition on an exponential back off up to 250 ms until timeout.  
        /// Will yield the thread after each evaluation, use the hint yieldThread if it is known the function should yield before evaluation.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">Execution hint to yield thread before evaluation</param>
        public static void IsOrBecomesTrue(Func<bool> func, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            if (!yieldThread && func() == true)
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
                using (var task = EvaluateAfterDelay(func, TimeSpan.FromMilliseconds(delay)))
                {
                    task.Wait();
                    if (task.Result == true)
                    {
                        result = true;
                        break;
                    }
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
        /// Asserts that the given read model will have exactly the expected version before the timeout expires.
        /// This can make tests fragile and should generally not be used unless the exact number of messages
        /// handled is critical to your tests. In most cases you should use <see cref="AtLeastModelVersion"/>
        /// instead.
        /// </summary>
        /// <param name="readModel">The read model.</param>
        /// <param name="expectedVersion">The read model's expected version.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">If true, the thread relinquishes the remainder of its time
        /// slice to any thread of equal priority that is ready to run.</param>
        public static void IsModelVersion(ReadModelBase readModel, int expectedVersion, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            IsOrBecomesTrue(() => readModel.Version == expectedVersion, timeout, msg, yieldThread);
        }

        /// <summary>
        /// Asserts that the given read model will have at least the expected version before the
        /// timeout expires. This is generally preferred to <see cref="IsModelVersion"/>.
        /// </summary>
        /// <param name="readModel">The read model.</param>
        /// <param name="expectedVersion">The read model's expected minimum version.</param>
        /// <param name="timeout">A timeout in milliseconds. If not specified, defaults to 1000.</param>
        /// <param name="msg">A message to display if the condition is not satisfied.</param>
        /// <param name="yieldThread">If true, the thread relinquishes the remainder of its time
        /// slice to any thread of equal priority that is ready to run.</param>
        public static void AtLeastModelVersion(ReadModelBase readModel, int expectedVersion, int? timeout = null, string msg = null, bool yieldThread = false)
        {
            IsOrBecomesTrue(() => readModel.Version >= expectedVersion, timeout, msg, yieldThread);
        }
        /// <summary>
        /// Evaluates the given function after the supplied delay.
        /// The current thread will yield at the start of the delay.  
        /// </summary>
        /// <param name="func">The function to evaluate.</param>
        /// <param name="delay">A delay timeSpan.</param>
        /// <param name="cancellationToken">A token to cancel evaluation, TaskCanceledException will be thrown.</param>
        public static async Task<bool> EvaluateAfterDelay(Func<bool> func, TimeSpan delay, CancellationToken cancellationToken = default)
        {
            await Task.Delay(delay, cancellationToken);
            return func();
        }
    }
}
