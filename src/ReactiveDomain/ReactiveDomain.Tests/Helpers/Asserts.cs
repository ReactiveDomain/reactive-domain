using System;
using System.Collections.Generic;
using System.Threading;
using ReactiveDomain.Bus;
using ReactiveDomain.Tests.Helpers;
using ReactiveUI;
using Xunit.Sdk;

// ReSharper disable once CheckNamespace - this is where it is supposed to be
namespace Xunit
{
#if XUNIT_VISIBILITY_INTERNAL
    internal
#else
    public
#endif
    partial class Assert
    {
        /// <summary>
        /// Verifies that the returned value of a function becomes false within the specified timeout.
        /// </summary>
        /// <param name="func">The function whose return value is expected to be false</param>
        /// <param name="timeout">The timeout, which defaults to 750 msec</param>
        /// <param name="msg">The user message to be shown</param>
        public static void IsOrBecomesFalse(Func<bool> func, int? timeout = null, string msg = null)
        {
            IsOrBecomesTrue(() => !func(), timeout, msg);
        }

        /// <summary>
        /// Verifies that the returned value of a function becomes true within the specified timeout.
        /// </summary>
        /// <param name="func">The function whose return value is expected to be false</param>
        /// <param name="timeout">The timeout, which defaults to 750 msec</param>
        /// <param name="msg">The user message to be shown</param>
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

        /// <summary>
        /// Verifies that an action to fire a command on a bus throws an exception.
        /// </summary>
        /// <typeparam name="T">The type of the inner exception to be expected</typeparam>
        /// <param name="fireAction">The fire command action that should throw</param>
        public static void CommandThrows<T>(Action fireAction) where T : Exception
        {
            var exp = Throws<CommandException>(fireAction);
            IsType<T>(exp.InnerException);
        }

        /// <summary>
        /// Verifies that a segment of an array is equal to the expected sequence
        /// </summary>
        /// <typeparam name="T">The type of the objects in the arrays</typeparam>
        /// <param name="expectedSequence">The expected sequence</param>
        /// <param name="buffer">The array that contains the segment to be compared against</param>
        /// <param name="offset">The offset within the array where the sequence to be compared starts</param>
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

        /// <summary>
        /// Verifies that two objects are equal, using a default comparer.
        /// </summary>
        /// <typeparam name="T">The type of the objects to be compared</typeparam>
        /// <param name="expected">The expected value</param>
        /// <param name="actual">The value to be compared against</param>
        /// <param name="userMessage">The user message to be shown</param>
        /// <exception cref="EqualException">Thrown when the objects are not equal</exception>
        public static void Equal<T>(T expected, T actual, string userMessage)
        {
            Equal(expected, actual, GetEqualityComparer<T>(), userMessage);
        }

        /// <summary>
        /// Verifies that two objects are equal, using a custom equatable comparer.
        /// </summary>
        /// <typeparam name="T">The type of the objects to be compared</typeparam>
        /// <param name="expected">The expected value</param>
        /// <param name="actual">The value to be compared against</param>
        /// <param name="comparer">The comparer used to compare the two objects</param>
        /// <param name="userMessage">The user message to be shown</param>
        /// <exception cref="EqualException">Thrown when the objects are not equal</exception>
        public static void Equal<T>(T expected, T actual, IEqualityComparer<T> comparer, string userMessage)
        {
            GuardArgumentNotNull("comparer", comparer);

            if (!comparer.Equals(expected, actual))
                throw new EqualExceptionEx(expected, actual, userMessage);
        }

        /// <summary>
        /// Verifies that two objects are not equal, using a default comparer.
        /// </summary>
        /// <typeparam name="T">The type of the objects to be compared</typeparam>
        /// <param name="expected">The expected object</param>
        /// <param name="actual">The actual object</param>
        /// <param name="userMessage">The user message to be shown</param>
        /// <exception cref="NotEqualException">Thrown when the objects are equal</exception>
        public static void NotEqual<T>(T expected, T actual, string userMessage)
        {
            NotEqual(expected, actual, GetEqualityComparer<T>());
        }

        /// <summary>
        /// Verifies that two objects are not equal, using a custom equality comparer.
        /// </summary>
        /// <typeparam name="T">The type of the objects to be compared</typeparam>
        /// <param name="expected">The expected object</param>
        /// <param name="actual">The actual object</param>
        /// <param name="comparer">The comparer used to examine the objects</param>
        /// <param name="userMessage">The user message to be shown</param>
        /// <exception cref="NotEqualException">Thrown when the objects are equal</exception>
        public static void NotEqual<T>(T expected, T actual, IEqualityComparer<T> comparer, string userMessage)
        {
            Assert.GuardArgumentNotNull("comparer", comparer);

            if (comparer.Equals(expected, actual))
                throw new NotEqualExceptionEx(ArgumentFormatter.Format(expected), ArgumentFormatter.Format(actual), userMessage);
        }

        /// <summary>
        /// Verifies that a ReactiveCommand can be executed.
        /// </summary>
        /// <typeparam name="TIn">The command's input type</typeparam>
        /// <typeparam name="TOut">The command's output type</typeparam>
        /// <param name="cmd">The command whose CanExecute is to be compared</param>
        public static void CanExecute<TIn, TOut>(ReactiveCommand<TIn, TOut> cmd)
        {
            using (cmd.CanExecute.Subscribe(True)) { }
        }

        /// <summary>
        /// Verifies that a ReactiveCommand cannot be executed.
        /// </summary>
        /// <typeparam name="TIn">The command's input type</typeparam>
        /// <typeparam name="TOut">The command's output type</typeparam>
        /// <param name="cmd">The command whose CanExecute is to be compared</param>
        public static void CannotExecute<TIn, TOut>(ReactiveCommand<TIn, TOut> cmd)
        {
            using (cmd.CanExecute.Subscribe(False)) { }
        }
    }
}
