using System;

namespace ReactiveDomain.UI.ViewObjects
{
    /// <summary>
    /// An immutable class for packaging exceptions with a user-facing message.
    /// </summary>
    public class UserError
    {
        public readonly string ErrorMessage;
        public readonly Exception Ex;

        /// <summary>
        /// Create a new immutable UserError.
        /// </summary>
        /// <param name="errorMessage"></param>
        /// <param name="ex"></param>
        public UserError(
            string errorMessage,
            Exception ex)
        {
            ErrorMessage = errorMessage;
            Ex = ex;
        }
    }
}
