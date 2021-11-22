using ReactiveUI;

namespace ReactiveDomain.UI
{
    /// <summary>
    /// A wrapper for ReactiveUI Interactions that re-implements the legacy recovery options.
    /// </summary>
    public static class Interactions
    {
        /// <summary>
        /// A wrapper for a <see cref="UserError"/> with custom recovery options to cancel, retry, or fail.
        /// </summary>
        public static readonly Interaction<UserError, RecoveryOptionResult> Errors = new Interaction<UserError, RecoveryOptionResult>();

        /// <summary>
        /// Describes to the code throwing the error what to do once the error is resolved.
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
