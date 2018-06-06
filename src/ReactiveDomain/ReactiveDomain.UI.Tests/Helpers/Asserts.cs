using System;
using System.Reactive.Linq;
using ReactiveUI;

namespace ReactiveDomain.UI.Tests.Helpers
{
    public class Assert
    {
        /// <summary>
        /// Verifies that a ReactiveCommand can be executed.
        /// </summary>
        /// <typeparam name="TIn">The command's input type</typeparam>
        /// <typeparam name="TOut">The command's output type</typeparam>
        /// <param name="cmd">The command whose CanExecute is to be compared</param>
        public static void CanExecute<TIn, TOut>(ReactiveCommand<TIn, TOut> cmd)
        {
            using (cmd.CanExecute.Replay(1).RefCount().ObserveOn(RxApp.MainThreadScheduler).Subscribe(Xunit.Assert.True)) { }
        }

        /// <summary>
        /// Verifies that a ReactiveCommand cannot be executed.
        /// </summary>
        /// <typeparam name="TIn">The command's input type</typeparam>
        /// <typeparam name="TOut">The command's output type</typeparam>
        /// <param name="cmd">The command whose CanExecute is to be compared</param>
        public static void CannotExecute<TIn, TOut>(ReactiveCommand<TIn, TOut> cmd)
        {
            using (cmd.CanExecute.Replay(1).RefCount().ObserveOn(RxApp.MainThreadScheduler).Subscribe(Xunit.Assert.False)) { }
        }
    }
}
