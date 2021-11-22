using System;

namespace ReactiveDomain.UI
{
    /// <summary>
    /// An immutable class for packaging exceptions with a user-facing message.
    /// </summary>
    public class UserError
    {
        /// <summary>
        /// The user-facing message to present.
        /// </summary>
        public readonly string ErrorMessage;
        /// <summary>
        /// The <see cref="Exception"/> associated with this error.
        /// </summary>
        public readonly Exception Ex;

        /// <summary>
        /// Create a new immutable UserError.
        /// </summary>
        /// <param name="errorMessage">The user-facing message to present.</param>
        /// <param name="ex">The <see cref="Exception"/> associated with this error.</param>
        public UserError(
            string errorMessage,
            Exception ex)
        {
            ErrorMessage = errorMessage;
            Ex = ex;
        }
    }
}
