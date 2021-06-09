using System;
using System.Linq;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing.EventStore;
using ReactiveDomain.Util;
using Xunit;

namespace ReactiveDomain.Foundation.Tests
{
	// ReSharper disable once InconsistentNaming
	public class when_using_correlated_repository
	{
		private readonly CorrelatedStreamStoreRepository _correlatedRepo;
		private readonly Guid _accountId = Guid.NewGuid();

		public when_using_correlated_repository()
		{
			var mockStore = new MockStreamStoreConnection("testRepo");
			mockStore.Connect();
			var repo = new StreamStoreRepository(new PrefixedCamelCaseStreamNameBuilder(), mockStore, new JsonMessageSerializer());
			_correlatedRepo = new CorrelatedStreamStoreRepository(repo);
			ICorrelatedMessage source = MessageBuilder.New(() => new CreateAccount(_accountId));
			var account = new Account(_accountId, source);
			account.Credit(7);
			account.Credit(13);
			account.Credit(31);
			repo.Save(account);
		}

		[Fact]
		public void can_get_by_id()
		{
			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
			var retrievedAccount = _correlatedRepo.GetById<Account>(_accountId, source);
			Assert.NotNull(retrievedAccount);
			Assert.Equal(51, retrievedAccount.Balance);
			Assert.Equal(_accountId, retrievedAccount.Id);

		}

		[Fact]
		public void can_get_by_id_at_version()
		{
			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
			var retrievedAccount = _correlatedRepo.GetById<Account>(_accountId, 1, source);
			Assert.NotNull(retrievedAccount);
			Assert.Equal(0, retrievedAccount.Balance);
			Assert.Equal(_accountId, retrievedAccount.Id);

			retrievedAccount = _correlatedRepo.GetById<Account>(_accountId, 2, source);
			Assert.NotNull(retrievedAccount);
			Assert.Equal(7, retrievedAccount.Balance);
			Assert.Equal(_accountId, retrievedAccount.Id);

		}
		[Fact]
		public void can_try_get_by_id()
		{
			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
			Assert.True(_correlatedRepo.TryGetById<Account>(_accountId, out var retrievedAccount, source));
			Assert.NotNull(retrievedAccount);
			Assert.Equal(51, retrievedAccount.Balance);
			Assert.Equal(_accountId, retrievedAccount.Id);

		}

		[Fact]
		public void can_try_get_by_id_at_version()
		{
			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
			Assert.True(_correlatedRepo.TryGetById<Account>(_accountId, 1, out var retrievedAccount, source));
			Assert.NotNull(retrievedAccount);
			Assert.Equal(0, retrievedAccount.Balance);
			Assert.Equal(_accountId, retrievedAccount.Id);

			Assert.True(_correlatedRepo.TryGetById<Account>(_accountId, 3, out retrievedAccount, source));
			Assert.NotNull(retrievedAccount);
			Assert.Equal(20, retrievedAccount.Balance);
			Assert.Equal(_accountId, retrievedAccount.Id);

		}
		[Fact]
		public void try_get_does_not_throw()
		{
			var badId = Guid.NewGuid();
			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
			Assert.False(_correlatedRepo.TryGetById<Account>(badId, out var retrievedAccount, source));
			Assert.Null(retrievedAccount);

		}
		[Fact]
		public void invalid_get_rethrows()
		{
			var badId = Guid.NewGuid();
			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
			Assert.Throws<AggregateNotFoundException>(() => _correlatedRepo.GetById<Account>(badId, source));
		}

		[Fact]
		public void new_correlated_aggregates_inject_source_information()
		{
			var newAccountId = Guid.NewGuid();
			ICorrelatedMessage source = MessageBuilder.New(() => new CreateAccount(newAccountId));
			var newAccount = new Account(newAccountId, source);
			newAccount.Credit(7);
			newAccount.Credit(13);
			newAccount.Credit(31);

			var eventSource = (IEventSource)newAccount;
			var correlatedEvents = eventSource.TakeEvents().Select(evt => evt as ICorrelatedMessage).ToArray();
			foreach (var evt in correlatedEvents) {
				Assert.Equal(source.MsgId, evt.CausationId);
				Assert.Equal(source.CorrelationId, evt.CorrelationId);
			}
		}
		[Fact]
		public void updated_correlated_aggregates_inject_source_information()
		{

			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
			var retrievedAccount = _correlatedRepo.GetById<Account>(_accountId, source);
			retrievedAccount.Credit(7);
			retrievedAccount.Credit(13);
			retrievedAccount.Credit(31);

			var eventSource = (IEventSource)retrievedAccount;
			var correlatedEvents = eventSource.TakeEvents().Select(evt => evt as ICorrelatedMessage).ToArray();
			foreach (var evt in correlatedEvents) {
				Assert.Equal(source.MsgId, evt.CausationId);
				Assert.Equal(source.CorrelationId, evt.CorrelationId);
			}
		}


		[Fact]
		public void can_save_new_correlated_aggregates()
		{
			var newAccountId = Guid.NewGuid();
			ICorrelatedMessage source = MessageBuilder.New(() => new CreateAccount(newAccountId));
			var newAccount = new Account(newAccountId, source);
			newAccount.Credit(7);
			newAccount.Credit(13);
			newAccount.Credit(31);
			_correlatedRepo.Save(newAccount);

			var retrievedAccount = _correlatedRepo.GetById<Account>(newAccountId, source);
			Assert.NotNull(retrievedAccount);
			Assert.Equal(51, retrievedAccount.Balance);
			Assert.Equal(newAccountId, retrievedAccount.Id);
		}

		[Fact]
		public void can_save_updated_correlated_aggregates()
		{
			ICorrelatedMessage source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));

			var retrievedAccount = _correlatedRepo.GetById<Account>(_accountId, source);
			Assert.NotNull(retrievedAccount);
			Assert.Equal(_accountId, retrievedAccount.Id);

			retrievedAccount.Credit(50);
			_correlatedRepo.Save(retrievedAccount);

			var retrievedAccount2 = _correlatedRepo.GetById<Account>(_accountId, source);
			Assert.NotNull(retrievedAccount2);
			Assert.Equal(_accountId, retrievedAccount2.Id);
			Assert.Equal(101, retrievedAccount.Balance);
        }

        [Fact]
        public void can_delete_aggregate()
        {
            var newAccountId = Guid.NewGuid();
            ICorrelatedMessage source = MessageBuilder.New(() => new CreateAccount(newAccountId));
            var newAccount = new Account(newAccountId, source);
			_correlatedRepo.Save(newAccount);

            var retrievedAccount = _correlatedRepo.GetById<Account>(newAccountId, source);
            _correlatedRepo.Delete(retrievedAccount);

            Assert.Throws<AggregateNotFoundException>(() => _correlatedRepo.GetById<Account>(newAccountId, source));
        }

		public class Account : AggregateRoot
		{
			//n.b. for infrastructure testing only not for prod or business unit tests
			public long Balance { get; private set; }
			//reflection constructor
			// ReSharper disable once UnusedMember.Local
			private Account()
			{
				Register<AccountCreated>(evt => Id = evt.AccountId);
				Register<AccountCredited>(evt => { Balance += evt.Amount; });
			}
			public Account(Guid id, ICorrelatedMessage source) : this()
			{
				((ICorrelatedEventSource)this).Source = source;
				Ensure.NotEmptyGuid(id, "id");
				Raise(new AccountCreated(id));
			}

			public void Credit(uint amount)
			{
				Raise(new AccountCredited(Id, amount));
			}
		}
		public class CreateAccount : Command
		{
			public readonly Guid AccountId;
			public CreateAccount(Guid accountId)
			{
				AccountId = accountId;
			}
		}
		public class AccountCreated : Event
		{
			public readonly Guid AccountId;
			public AccountCreated(Guid accountId)
			{
				AccountId = accountId;
			}
		}
		public class CreditAccount : Command
		{
			public readonly Guid AccountId;
			public readonly uint Amount;
			public CreditAccount(Guid accountId, uint amount)
			{
				AccountId = accountId;
				Amount = amount;
			}
		}
		//use of base class Event is optional
		public class AccountCredited : ICorrelatedMessage
		{
			public Guid MsgId { get; }
			public Guid CorrelationId { get; set; }
			public Guid CausationId { get; set; }
			public readonly Guid AccountId;
			public readonly uint Amount;

			public AccountCredited(Guid accountId, uint amount)
			{
				MsgId = Guid.NewGuid();
				AccountId = accountId;
				Amount = amount;
			}
		}
	}
}