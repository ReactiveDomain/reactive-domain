using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Users.Domain.Aggregates;
using ReactiveDomain.Users.Messages;

namespace ReactiveDomain.Users.ReadModels
{
    public class RegisteredApplicationsRM :
        ReadModelBase,
        IHandle<ApplicationMsgs.ApplicationCreated>
    {
        public RegisteredApplicationsRM(Func<IListener> getListener)
            : base(nameof(RegisteredApplicationsRM), getListener)
        {
            EventStream.Subscribe<ApplicationMsgs.ApplicationCreated>(this);
            Start<ApplicationRoot>();
        }

        public IReadOnlyList<Application> Applications => _applications.AsReadOnly();
        private readonly List<Application> _applications = new List<Application>();

        public void Handle(ApplicationMsgs.ApplicationCreated message)
        {
            _applications.Add(new Application(message.ApplicationId, message.Name, message.ApplicationVersion));
        }
    }
}
