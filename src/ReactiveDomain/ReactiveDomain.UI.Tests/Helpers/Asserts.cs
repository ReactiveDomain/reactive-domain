using System;
using ReactiveUI;

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
