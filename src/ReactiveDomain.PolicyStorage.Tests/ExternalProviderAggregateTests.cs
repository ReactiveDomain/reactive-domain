using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing;
using ReactiveDomain.Users.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Users.Tests
{
    public sealed class ExternalProviderAggregateTests
    {
        private readonly ICorrelatedMessage _command = MessageBuilder.New(() => new TestMessages.RootCommand());

        [Fact]
        public void can_add_external_provider()
        {
            var providerId = Guid.NewGuid();
            const string providerName = "TestProvider";
            var provider = new ExternalProvider(
                                providerId,
                                providerName,
                                _command);
            var events = provider.TakeEvents();
            Assert.Collection(
                events,
                e =>
                {
                    if (e is ExternalProviderMsgs.ProviderCreated created)
                    {
                        Assert.Equal(providerId, created.ProviderId);
                        Assert.Equal(providerName, created.ProviderName);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                });
        }

        [Fact]
        public void cannot_add_external_provider_with_invalid_id()
        {
            var providerId = Guid.Empty;
            const string providerName = "TestProvider";
            Assert.Throws<ArgumentException>(() => new ExternalProvider(
                                                        providerId,
                                                        providerName,
                                                        _command));
        }

        [Fact]
        public void cannot_add_external_provider_with_invalid_name()
        {
            var providerId = Guid.NewGuid();
            var providerName = string.Empty;
            Assert.Throws<ArgumentNullException>(() => new ExternalProvider(
                                                            providerId,
                                                            providerName,
                                                            _command));
        }

        [Fact]
        public void cannot_add_external_provider_without_correct_correlation_source()
        {
            var providerId = Guid.NewGuid();
            const string providerName = "TestProvider";
            Assert.Throws<ArgumentNullException>(() => new ExternalProvider(
                                                            providerId,
                                                            providerName,
                                                            null));
            Assert.Throws<ArgumentException>(() => new ExternalProvider(
                                                            providerId,
                                                            providerName,
                                                            new TestCommands.Command1()));
        }

        [Fact]
        public void can_log_failed_authentication()
        {
            var providerId = Guid.NewGuid();
            const string providerName = "TestProvider";
            const string ipAddress = "127.0.0.1";
            var provider = new ExternalProvider(
                                providerId,
                                providerName,
                                _command);
            provider.NotAuthenticatedInvalidCredentials(ipAddress);
            var events = provider.TakeEvents();
            Assert.Collection(
                events,
                e => Assert.IsType<ExternalProviderMsgs.ProviderCreated>(e),
                e =>
                {
                    if (e is ExternalProviderMsgs.AuthenticationFailedInvalidCredentials failed)
                    {
                        Assert.Equal(providerId, failed.ProviderId);
                        Assert.Equal(ipAddress, failed.HostIPAddress);
                    }
                    else
                    {
                        throw new Exception("wrong event.");
                    }
                });
        }
    }
}
