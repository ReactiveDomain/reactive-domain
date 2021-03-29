using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.ReadModels
{
    public class IdentityOptionsRM : ReadModelBase, 
        IHandle<IdentityOptionsMsgs.RequiredLengthSet>,
        IHandle<IdentityOptionsMsgs.RequiredUniqueCaractersSet>,
        IHandle<IdentityOptionsMsgs.RequireNonAlphanumericSet>,
        IHandle<IdentityOptionsMsgs.RequireLowercaseSet>,
        IHandle<IdentityOptionsMsgs.RequireUppercaseSet>,
        IHandle<IdentityOptionsMsgs.RequireDigitSet>,
        IHandle<IdentityOptionsMsgs.MaxHistoricalPasswordsSet>,
        IHandle<IdentityOptionsMsgs.MaximumPasswordAgeSet>
    {
        public IdentityOptionsRM(string name, IConfiguredConnection conn) : base(name, () => conn.GetQueuedListener(nameof(IdentityOptionsRM)))
        {
            using (var reader = conn.GetReader(nameof(IdentityOptionsRM)))
            {
                reader.EventStream.Subscribe<IdentityOptionsMsgs.RequiredLengthSet>(this);
                reader.EventStream.Subscribe<IdentityOptionsMsgs.RequiredUniqueCaractersSet>(this);
                reader.EventStream.Subscribe<IdentityOptionsMsgs.RequireNonAlphanumericSet>(this);
                reader.EventStream.Subscribe<IdentityOptionsMsgs.RequireLowercaseSet>(this);
                reader.EventStream.Subscribe<IdentityOptionsMsgs.RequireUppercaseSet>(this);
                reader.EventStream.Subscribe<IdentityOptionsMsgs.RequireDigitSet>(this);
                reader.EventStream.Subscribe<IdentityOptionsMsgs.MaxHistoricalPasswordsSet>(this);
                reader.EventStream.Subscribe<IdentityOptionsMsgs.MaximumPasswordAgeSet>(this);

                reader.Read<IdentityOptionsAgg>();
            }

            EventStream.Subscribe<IdentityOptionsMsgs.RequiredLengthSet>(this);
            EventStream.Subscribe<IdentityOptionsMsgs.RequiredUniqueCaractersSet>(this);
            EventStream.Subscribe<IdentityOptionsMsgs.RequireNonAlphanumericSet>(this);
            EventStream.Subscribe<IdentityOptionsMsgs.RequireLowercaseSet>(this);
            EventStream.Subscribe<IdentityOptionsMsgs.RequireUppercaseSet>(this);
            EventStream.Subscribe<IdentityOptionsMsgs.RequireDigitSet>(this);
            EventStream.Subscribe<IdentityOptionsMsgs.MaxHistoricalPasswordsSet>(this);
            EventStream.Subscribe<IdentityOptionsMsgs.MaximumPasswordAgeSet>(this);

            Start<IdentityOptionsAgg>(blockUntilLive: true);
        }

        public PasswordOptions Password { get; set; } = new PasswordOptions();

        public void Handle(IdentityOptionsMsgs.RequiredLengthSet msg) => Password.RequiredLength = msg.RequiredLength;
        public void Handle(IdentityOptionsMsgs.RequiredUniqueCaractersSet msg) => Password.RequiredUniqueCharacters = msg.RequiredUniqueCharacters;
        public void Handle(IdentityOptionsMsgs.RequireNonAlphanumericSet msg) => Password.RequireNonAlphanumeric = msg.OnOff;
        public void Handle(IdentityOptionsMsgs.RequireLowercaseSet msg) => Password.RequireLowercase = msg.OnOff;
        public void Handle(IdentityOptionsMsgs.RequireUppercaseSet msg) => Password.RequireUppercase = msg.OnOff;
        public void Handle(IdentityOptionsMsgs.RequireDigitSet msg) => Password.RequireDigit = msg.OnOff;
        public void Handle(IdentityOptionsMsgs.MaxHistoricalPasswordsSet msg) => Password.MaxHistoricalPasswords = msg.MaximunNumberOfHistoricalPasswords;
        public void Handle(IdentityOptionsMsgs.MaximumPasswordAgeSet msg) => Password.MaxPasswordAge = msg.MaximumPasswordAge;
    }

    public class PasswordOptions
    {
        public int RequiredLength { get; set; } = 6;
        public int RequiredUniqueCharacters { get; set; } = 1;
        public int MaxHistoricalPasswords { get; set; } = 4;
        public int MaxPasswordAge { get; set; } = 90;
        public bool RequireNonAlphanumeric { get; set; } = true;
        public bool RequireLowercase { get; set; } = true;
        public bool RequireUppercase { get; set; } = true;
        public bool RequireDigit { get; set; } = true;
    }
}
