using ReactiveUI;

namespace ReactiveDomain.UI
{
    public static class Interactions
    {
        public static readonly Interaction<UserError, RecoveryOptionResult> Errors = new Interaction<UserError, RecoveryOptionResult>();

        /// <summary>
        /// RecoveryOptionResult describes to the code throwing the error what to do
        /// once the error is resolved. This is adapted from legacy UserError code.
        /// </summary>
        public enum RecoveryOptionResult
        {
            /// <summary>
            /// The operation should be canceled, but it is no longer an error.
            /// </summary>
            CancelOperation = 0,
            /// <summary>
            /// The operation should be retried with the same parameters.
            /// </summary>
            RetryOperation = 1,
            /// <summary>
            /// Recovery failed or not possible, you should rethrow as an Exception.
            /// </summary>
            FailOperation = 2
        }
    }
}
