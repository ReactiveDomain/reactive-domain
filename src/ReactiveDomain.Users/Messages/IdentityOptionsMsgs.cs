using System;
using System.Collections.Generic;
using System.Text;

using ReactiveDomain.Messaging;

namespace ReactiveDomain.Users.Messages
{
    public static class IdentityOptionsMsgs
    {
        public class RequiredLengthSet : Event
        {
            public readonly Guid IdentityOptionsId;
            public readonly int RequiredLength;

            public RequiredLengthSet(Guid identityOptionsId, int requiredLength)
            {
                IdentityOptionsId = identityOptionsId;
                RequiredLength = requiredLength;
            }
        }
        public class RequiredUniqueCaractersSet : Event
        {
            public readonly Guid IdentityOptionsId;
            public readonly int RequiredUniqueCharacters;

            public RequiredUniqueCaractersSet(Guid identityOptionsId, int requiredUniqueCharacters)
            {
                IdentityOptionsId = identityOptionsId;
                RequiredUniqueCharacters = requiredUniqueCharacters;
            }
        }
        public class RequireNonAlphanumericSet : Event
        {
            public readonly Guid IdentityOptionsId;
            public readonly bool OnOff;

            public RequireNonAlphanumericSet(Guid identityOptionsId, bool onOff)
            {
                IdentityOptionsId = identityOptionsId;
                OnOff = onOff;
            }
        }
        public class RequireLowercaseSet : Event
        {
            public readonly Guid IdentityOptionsId;
            public readonly bool OnOff;

            public RequireLowercaseSet(Guid identityOptionsId, bool onOff)
            {
                IdentityOptionsId = identityOptionsId;
                OnOff = onOff;
            }
        }
        public class RequireUppercaseSet : Event
        {
            public readonly Guid IdentityOptionsId;
            public readonly bool OnOff;

            public RequireUppercaseSet(Guid identityOptionsId, bool onOff)
            {
                IdentityOptionsId = identityOptionsId;
                OnOff = onOff;
            }
        }
        public class RequireDigitSet : Event
        {
            public readonly Guid IdentityOptionsId;
            public readonly bool OnOff;

            public RequireDigitSet(Guid identityOptionsId, bool onOff)
            {
                IdentityOptionsId = identityOptionsId;
                OnOff = onOff;
            }
        }
    }
}
