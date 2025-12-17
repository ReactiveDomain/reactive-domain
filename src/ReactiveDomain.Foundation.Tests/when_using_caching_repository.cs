using System;
using ReactiveDomain.Foundation.StreamStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing.EventStore;
using ReactiveDomain.Util;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

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
        ICorrelatedMessage source = MessageBuilder.New(() => new CreateAccount(id));
        var cachedAccount = new Account(id, source);
        _cachingRepo.Save(cachedAccount);

        Assert.Equal(0, cachedAccount.Balance);

        var uncachedAccount = _repo.GetById<Account>(id);
        uncachedAccount.Credit(1);
        _repo.Save(uncachedAccount);

        Assert.Equal(1, uncachedAccount.Balance);

        var cachedAccount2 = _cachingRepo.GetById<Account>(id);
        Assert.Equal(1, cachedAccount.Balance);
        Assert.Equal(1, uncachedAccount.Balance);
        Assert.Equal(1, cachedAccount2.Balance);

        cachedAccount2.Credit(2);
        _cachingRepo.Save(cachedAccount2);

        Assert.Equal(3, cachedAccount.Balance); //cachedAccount is a pointer to same object as cachedAccount2 due to cache
        Assert.Equal(1, uncachedAccount.Balance); //uncachedAccount is an independent object
        Assert.Equal(3, cachedAccount2.Balance);

        uncachedAccount.Credit(5);
        Assert.Throws<WrongExpectedVersionException>(() => _repo.Save(uncachedAccount));

        Assert.Equal(3, cachedAccount.Balance); //cachedAccount is a pointer to same object as cachedAccount2 due to cache
        Assert.Equal(6, uncachedAccount.Balance); //uncachedAccount is an independent object
        Assert.Equal(3, cachedAccount2.Balance);

        var cachedAccount3 = _cachingRepo.GetById<Account>(id);
        Assert.Equal(3, cachedAccount.Balance);  //cachedAccount is a pointer to same object as acct2/3 due to cache
        Assert.Equal(6, uncachedAccount.Balance); //uncachedAccount is an independent object
        Assert.Equal(3, cachedAccount2.Balance); //cachedAccount is a pointer to same object as acct2/3 due to cache
        Assert.Equal(3, cachedAccount3.Balance); //cachedAccount is a pointer to same object as acct2/3 due to cache


        cachedAccount.Credit(5);
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
    public void can_cache_aggregates_with_different_types_and_same_id() {
        var accountId = Guid.NewGuid();
        ICorrelatedMessage source = MessageBuilder.New(() => new CreateAccount(accountId));
        var account = new Account(accountId, source);
        _cachingRepo.Save(account);

        ICorrelatedMessage source2 = MessageBuilder.New(() => new CreateAccount(accountId));
        var other = new OtherAggregateType(accountId, source2);
        _cachingRepo.Save(other);

        var cachedAccount = _cachingRepo.GetById<Account>(accountId);
        Assert.NotNull(cachedAccount);
        var cachedOther = _cachingRepo.GetById<OtherAggregateType>(accountId);
        Assert.NotNull(cachedOther);
    }

    [Fact]
    public void can_remove_by_id_from_the_cache() {
        var id = Guid.NewGuid();
        ICorrelatedMessage source = MessageBuilder.New(() => new CreateAccount(id));
        var cachedAccount = new Account(id, source);
        cachedAccount.Credit(5);
        _cachingRepo.Save(cachedAccount); //save at 5
        Assert.Equal(5, cachedAccount.Balance);

        cachedAccount.Credit(7);
        Assert.Equal(12, cachedAccount.Balance);

        var cacheCopy = _cachingRepo.GetById<Account>(id);
        Assert.Equal(12, cacheCopy.Balance);

        _cachingRepo.ClearCache<Account>(id);

        var persistedCopy = _cachingRepo.GetById<Account>(id);
        Assert.Equal(5, persistedCopy.Balance);

    }

    [Fact]
    public void can_delete_aggregate() {
        var newAccountId = Guid.NewGuid();
        ICorrelatedMessage source = MessageBuilder.New(() => new when_using_correlated_repository.CreateAccount(newAccountId));
        var newAccount = new when_using_correlated_repository.Account(newAccountId, source);
        _cachingRepo.Save(newAccount);

        var retrievedAccount = _cachingRepo.GetById<when_using_correlated_repository.Account>(newAccountId);
        _cachingRepo.Delete(retrievedAccount);

        Assert.Throws<AggregateNotFoundException>(() => _cachingRepo.GetById<when_using_correlated_repository.Account>(newAccountId));
    }

    public class Account : EventDrivenStateMachine {
        //n.b. for infrastructure testing only not for prod or business unit tests
        public long Balance { get; private set; }
        private Account() {
            Register<AccountCreated>(evt => Id = evt.AccountId);
            Register<AccountCredited>(evt => { Balance += evt.Amount; });
        }
        public Account(Guid id, ICorrelatedMessage source) : this() {
            Ensure.NotEmptyGuid(id, nameof(id));
            Raise(new AccountCreated(id));
        }
        public void Credit(uint amount) {
            Raise(new AccountCredited(Id, amount));
        }
    }

    public record CreateAccount(Guid AccountId) : Command;

    public record AccountCreated(Guid AccountId) : IMessage {
        public Guid MsgId { get; } = Guid.NewGuid();
    }

    public record CreditAccount(Guid AccountId, uint Amount) : Command;

    public record AccountCredited(Guid AccountId, uint Amount) : IMessage {
        public Guid MsgId { get; } = Guid.NewGuid();
    }

    public class OtherAggregateType : EventDrivenStateMachine {
        private OtherAggregateType() {
            Register<OtherAggregateCreated>(e => Id = e.AggregateId);
        }
        public OtherAggregateType(Guid id, ICorrelatedMessage source) : this() {
            Ensure.NotEmptyGuid(id, nameof(id));
            Raise(new OtherAggregateCreated(id));
        }
    }

    public record CreateOtherAggregate(Guid AggregateId) : Command;

    public record OtherAggregateCreated(Guid AggregateId) : IMessage {
        public Guid MsgId { get; } = Guid.NewGuid();
    }
}