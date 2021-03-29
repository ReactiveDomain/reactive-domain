using System;
using System.Collections.Generic;

namespace ReactiveDomain.Users.Messages
{
    public class Describer
    {
        public static IdentityError PasswordTooShort(int requiredLength) => new IdentityError(nameof(PasswordTooShort), string.Format(Resources.PasswordTooShort, requiredLength));
        public static IdentityError PasswordRequiresNonAlphanumeric() => new IdentityError(nameof(PasswordRequiresNonAlphanumeric), Resources.PasswordRequiresNonAlphanumeric);
        public static IdentityError PasswordRequiresDigit() => new IdentityError(nameof(PasswordRequiresDigit), Resources.PasswordRequiresDigit);
        public static IdentityError PasswordRequiresLower() => new IdentityError(nameof(PasswordRequiresLower), Resources.PasswordRequiresLower);
        public static IdentityError PasswordRequiresUpper() => new IdentityError(nameof(PasswordRequiresUpper), Resources.PasswordRequiresUpper);
        public static IdentityError PasswordRequiresUniqueCharacters(int requiredUniqueCharacters) => new IdentityError(nameof(PasswordRequiresUniqueCharacters), string.Format(Resources.PasswordRequiresUniqueCharacters, requiredUniqueCharacters));
    }

    public class IdentityError
    {
        public readonly string Code;
        public readonly string Description;

        public IdentityError(string code, string description)
        {
            Code = code;
            Description = description;
        }
    }

    public class IdentityErrorException : Exception
    {
        public readonly IEnumerable<IdentityError> Errors;

        public IdentityErrorException(IEnumerable<IdentityError> errors)
        {
            Errors = errors;
        }
    }
}
