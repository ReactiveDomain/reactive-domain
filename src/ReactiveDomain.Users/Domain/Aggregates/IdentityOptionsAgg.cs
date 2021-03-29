using System;
using System.Collections.Generic;
using System.Text;

using ReactiveDomain.Messaging;
using ReactiveDomain.Users.Messages;
using ReactiveDomain.Util;

namespace ReactiveDomain.Users.Domain.Aggregates
{
    public class IdentityOptionsAgg : AggregateRoot
    {
        int _requiredLength;
        int _requiredUniqueCharacters;
        bool _requireNonAlphanumeric;
        bool _requireLowercase;
        bool _requireUppercase;
        bool _requireDigit;

        public IdentityOptionsAgg(Guid id, ICorrelatedMessage msg = null) : this(msg)
        {
            // raise the "started" event.
            Raise(new IdentityOptionsMsgs.RequiredLengthSet(Id, 6));
            Raise(new IdentityOptionsMsgs.RequiredUniqueCaractersSet(Id, 1));
            Raise(new IdentityOptionsMsgs.RequireNonAlphanumericSet(Id, true));
            Raise(new IdentityOptionsMsgs.RequireLowercaseSet(Id, true));
            Raise(new IdentityOptionsMsgs.RequireUppercaseSet(Id, true));
            Raise(new IdentityOptionsMsgs.RequireDigitSet(Id, true));

        }
        public IdentityOptionsAgg(ICorrelatedMessage msg = null) : base(msg)
        {
            RegisterHandlers();
        }

        private void RegisterHandlers()
        {
            Register<IdentityOptionsMsgs.RequiredLengthSet>((evt) => _requiredLength = evt.RequiredLength);
            Register<IdentityOptionsMsgs.RequiredUniqueCaractersSet>((evt) => _requiredUniqueCharacters = evt.RequiredUniqueCharacters);
            Register<IdentityOptionsMsgs.RequireNonAlphanumericSet>((evt) => _requireNonAlphanumeric = evt.OnOff);
            Register<IdentityOptionsMsgs.RequireLowercaseSet>((evt) => _requireLowercase = evt.OnOff);
            Register<IdentityOptionsMsgs.RequireUppercaseSet>((evt) => _requireUppercase = evt.OnOff);
            Register<IdentityOptionsMsgs.RequireDigitSet>((evt) => _requireDigit = evt.OnOff);
        }

        public void SetRequiredLength(int requiredLength)
        {
            if (Ensure.Equals(_requiredLength, requiredLength)) return;

            // raise new event.
            Raise(new IdentityOptionsMsgs.RequiredLengthSet(Id, requiredLength));
        }
        public void SetRequiredUniqueCharacters(int requiredUniqueCharacters)
        {
            if (Ensure.Equals(_requiredUniqueCharacters, requiredUniqueCharacters)) return;

            // raise new event.
            Raise(new IdentityOptionsMsgs.RequiredUniqueCaractersSet(Id, requiredUniqueCharacters));
        }
        public void ToggleRequireNonAlphanumeric(bool onOff)
        {
            if (Ensure.Equals(_requireNonAlphanumeric, onOff)) return;

            // raise new event.
            Raise(new IdentityOptionsMsgs.RequireNonAlphanumericSet(Id, onOff));
        }
        public void ToggleRequireLowercase(bool onOff)
        {
            if (Ensure.Equals(_requireLowercase, onOff)) return;

            // raise new event.
            Raise(new IdentityOptionsMsgs.RequireLowercaseSet(Id, onOff));
        }
        public void ToggleRequireUppercase(bool onOff)
        {
            if (Ensure.Equals(_requireUppercase, onOff)) return;

            // raise new event.
            Raise(new IdentityOptionsMsgs.RequireUppercaseSet(Id, onOff));
        }
        public void ToggleRequireDigit(bool onOff)
        {
            if (Ensure.Equals(_requireDigit, onOff)) return;

            // raise new event.
            Raise(new IdentityOptionsMsgs.RequireDigitSet(Id, onOff));
        }
    }
}
