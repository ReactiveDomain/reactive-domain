using System;
using System.Threading;
using Newtonsoft.Json;
using ReactiveDomain.Foundation.StreamStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing.EventStore;
using ReactiveDomain.Util;
using Xunit;

namespace ReactiveDomain.Foundation.Tests {
    // ReSharper disable once InconsistentNaming
    public class when_using_caching_repository {
        private readonly CachingRepository _cachingRepo;
        private readonly StreamStoreRepository _repo;


        public when_using_caching_repository() {

            var mockStore = new MockStreamStoreConnection("testRepo");
            mockStore.Connect();
            _repo = new StreamStoreRepository(new PrefixedCamelCaseStreamNameBuilder(), mockStore, new JsonMessageSerializer());
            _cachingRepo = new CachingRepository(_repo);
        }

        [Fact]
        public void can_handle_repeated_and_cross_updates() {

            var id = Guid.NewGuid();
            var cachedAccount = new Account(id, CorrelatedMessage.NewRoot());
            _cachingRepo.Save(cachedAccount);

            Assert.Equal(0, cachedAccount.Balance);

            var uncachedAccount = _repo.GetById<Account>(id);
            uncachedAccount.Credit(1, CorrelatedMessage.NewRoot());
            _repo.Save(uncachedAccount);

            Assert.Equal(1, uncachedAccount.Balance);

            var cachedAccount2 = _cachingRepo.GetById<Account>(id);
            Assert.Equal(1, cachedAccount.Balance);
            Assert.Equal(1, uncachedAccount.Balance);
            Assert.Equal(1, cachedAccount2.Balance);

            cachedAccount2.Credit(2, CorrelatedMessage.NewRoot());
            _cachingRepo.Save(cachedAccount2);

            Assert.Equal(3, cachedAccount.Balance); //cachedAccount is a pointer to same object as cachedAccount2 due to cache
            Assert.Equal(1, uncachedAccount.Balance); //uncachedAccount is an independent object
            Assert.Equal(3, cachedAccount2.Balance);

            uncachedAccount.Credit(5, CorrelatedMessage.NewRoot());
            Assert.Throws<WrongExpectedVersionException>(() => _repo.Save(uncachedAccount));

            Assert.Equal(3, cachedAccount.Balance); //cachedAccount is a pointer to same object as cachedAccount2 due to cache
            Assert.Equal(6, uncachedAccount.Balance); //uncachedAccount is an independent object
            Assert.Equal(3, cachedAccount2.Balance);

            var cachedAccount3 = _cachingRepo.GetById<Account>(id);
            Assert.Equal(3, cachedAccount.Balance);  //cachedAccount is a pointer to same object as acct2/3 due to cache
            Assert.Equal(6, uncachedAccount.Balance); //uncachedAccount is an independent object
            Assert.Equal(3, cachedAccount2.Balance); //cachedAccount is a pointer to same object as acct2/3 due to cache
            Assert.Equal(3, cachedAccount3.Balance); //cachedAccount is a pointer to same object as acct2/3 due to cache


            cachedAccount.Credit(5, CorrelatedMessage.NewRoot());
            Assert.Equal(8, cachedAccount.Balance);

            _cachingRepo.Save(cachedAccount);
            Assert.Equal(8, cachedAccount.Balance);

            var cachedAccount4 = _cachingRepo.GetById<Account>(id);

            Assert.Equal(8, cachedAccount.Balance);  //cachedAccount is a pointer to same object as acct2/3/4 due to cache
            Assert.Equal(6, uncachedAccount.Balance); //uncachedAccount is an independent object
            Assert.Equal(8, cachedAccount2.Balance); //cachedAccount is a pointer to same object as acct2/3/4 due to cache
            Assert.Equal(8, cachedAccount3.Balance); //cachedAccount is a pointer to same object as acct2/3/4 due to cache
            Assert.Equal(8, cachedAccount4.Balance); //cachedAccount is a pointer to same object as acct2/3/4 due to cache
        }
        [Fact]
        public void can_remove_by_id_from_the_cache() {
            var id = Guid.NewGuid();
            var cachedAccount = new Account(id, CorrelatedMessage.NewRoot());
            cachedAccount.Credit(5, CorrelatedMessage.NewRoot());
            _cachingRepo.Save(cachedAccount); //save at 5
            Assert.Equal(5, cachedAccount.Balance);

            cachedAccount.Credit(7, CorrelatedMessage.NewRoot());
            Assert.Equal(12, cachedAccount.Balance);

            var cacheCopy = _cachingRepo.GetById<Account>(id);
            Assert.Equal(12, cacheCopy.Balance);

            _cachingRepo.ClearCache(id);

            var persistedCopy = _cachingRepo.GetById<Account>(id);
            Assert.Equal(5, persistedCopy.Balance);

        }


        public class Account : EventDrivenStateMachine {
            //n.b. for infrastructure testing only not for prod or business unit tests
            public long Balance { get; private set; }
            private Account() {
                Register<AccountCreated>(evt => Id = evt.AccountId);
                Register<AccountCredited>(evt => { Balance += evt.Amount; });
            }
            public Account(Guid id, CorrelatedMessage source) : this() {
                Ensure.NotEmptyGuid(id, "id");
                Raise(new AccountCreated(id, source ?? CorrelatedMessage.NewRoot()));
            }
            public void Credit(uint amount, CorrelatedMessage source) {
                Raise(new AccountCredited(Id, amount, source));
            }
        }
        public class CreateAccount : Command {
            public readonly Guid AccountId;
            public CreateAccount(Guid accountId, Command source) : this(accountId, source, null) { }
            public CreateAccount(Guid accountId, CorrelatedMessage source, CancellationToken? token = null) : this(accountId, source.CorrelationId, source.SourceId, token) { }
            public CreateAccount(Guid accountId, CorrelationId correlationId, SourceId sourceId, CancellationToken? token = null) : base(correlationId, sourceId, token) {
                AccountId = accountId;
            }
        }
        public class AccountCreated : Event {
            public readonly Guid AccountId;
            public AccountCreated(Guid accountId, CorrelatedMessage source) : this(accountId, source.CorrelationId, source.SourceId) { }
            [JsonConstructor]
            public AccountCreated(Guid accountId, CorrelationId correlationId, SourceId sourceId) : base(correlationId, sourceId) {
                AccountId = accountId;
            }
        }
        public class CreditAccount : Command {
            public readonly Guid AccountId;
            public readonly uint Amount;
            public CreditAccount(Guid accountId, uint amount, Command source) : this(accountId, amount, source, null) { }
            public CreditAccount(Guid accountId, uint amount, CorrelatedMessage source, CancellationToken? token = null) : this(accountId, amount, source.CorrelationId, source.SourceId, token) { }
            public CreditAccount(Guid accountId, uint amount, CorrelationId correlationId, SourceId sourceId, CancellationToken? token = null) : base(correlationId, sourceId, token) {
                AccountId = accountId;
                Amount = amount;
            }
        }
        public class AccountCredited : Event {
            public readonly Guid AccountId;
            public readonly uint Amount;
            public AccountCredited(Guid accountId, uint amount, CorrelatedMessage source) : this(accountId, amount, source.CorrelationId, source.SourceId) { }
            [JsonConstructor]
            public AccountCredited(Guid accountId, uint amount, CorrelationId correlationId, SourceId sourceId) : base(correlationId, sourceId) {
                AccountId = accountId;
                Amount = amount;
            }
        }
    }
}