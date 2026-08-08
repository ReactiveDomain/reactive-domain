using ReactiveDomain.Messaging;
using ReactiveDomain.Testing.EventStore;
using ReactiveDomain.Util;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_correlated_repository {
	private readonly CorrelatedStreamStoreRepository _correlatedRepo;
	private readonly Guid _accountId = Guid.NewGuid();
	private readonly MockStreamStoreConnection _mockStore;
	private readonly JsonMessageSerializer _serializer = new();
	private readonly string _streamName;

	public when_using_correlated_repository() {
		_mockStore = new MockStreamStoreConnection("testRepo");
		_mockStore.Connect();
		var namer = new PrefixedCamelCaseStreamNameBuilder();
		_streamName = namer.GenerateForAggregate(typeof(Account), _accountId);
		var repo = new StreamStoreRepository(namer, _mockStore, _serializer);
		_correlatedRepo = new CorrelatedStreamStoreRepository(repo);
		var source = MessageBuilder.New(() => new CreateAccount(_accountId));
		var account = new Account(_accountId, source);
		account.Credit(7);
		account.Credit(13);
		account.Credit(31);
		repo.Save(account);
	}

	[Fact]
	public void can_get_by_id() {
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		var retrievedAccount = _correlatedRepo.GetById<Account>(_accountId, source);
		Assert.NotNull(retrievedAccount);
		Assert.Equal(51, retrievedAccount.Balance);
		Assert.Equal(_accountId, retrievedAccount.Id);

	}

	[Fact]
	public void can_get_by_id_at_version() {
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
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
	public void can_try_get_by_id() {
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		Assert.True(_correlatedRepo.TryGetById<Account>(_accountId, out var retrievedAccount, source));
		Assert.NotNull(retrievedAccount);
		Assert.Equal(51, retrievedAccount.Balance);
		Assert.Equal(_accountId, retrievedAccount.Id);

	}

	[Fact]
	public void can_try_get_by_id_at_version() {
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		Assert.True(_correlatedRepo.TryGetById<Account>(_accountId, 1, out var retrievedAccount, source));
		Assert.NotNull(retrievedAccount);
		Assert.Equal(0, retrievedAccount.Balance);
		Assert.Equal(_accountId, retrievedAccount.Id);

		Assert.True(_correlatedRepo.TryGetById(_accountId, 3, out retrievedAccount, source));
		Assert.NotNull(retrievedAccount);
		Assert.Equal(20, retrievedAccount.Balance);
		Assert.Equal(_accountId, retrievedAccount.Id);

	}
	[Fact]
	public void try_get_does_not_throw() {
		var badId = Guid.NewGuid();
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		Assert.False(_correlatedRepo.TryGetById<Account>(badId, out var retrievedAccount, source));
		Assert.Null(retrievedAccount);

	}
	[Fact]
	public void invalid_get_rethrows() {
		var badId = Guid.NewGuid();
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		Assert.Throws<AggregateNotFoundException>(() => _correlatedRepo.GetById<Account>(badId, source));
	}

	[Fact]
	public void new_correlated_aggregates_inject_source_information() {
		var newAccountId = Guid.NewGuid();
		var source = MessageBuilder.New(() => new CreateAccount(newAccountId));
		var newAccount = new Account(newAccountId, source);
		newAccount.Credit(7);
		newAccount.Credit(13);
		newAccount.Credit(31);

		IEventSource eventSource = newAccount;
		var correlatedEvents = eventSource.TakeEvents().Select(evt => evt as ICorrelatedMessage).ToArray();
		foreach (var evt in correlatedEvents) {
			Assert.NotNull(evt);
			Assert.Equal(source.MsgId, evt.CausationId);
			Assert.Equal(source.CorrelationId, evt.CorrelationId);
		}
	}
	[Fact]
	public void updated_correlated_aggregates_inject_source_information() {

		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		var retrievedAccount = _correlatedRepo.GetById<Account>(_accountId, source);
		retrievedAccount.Credit(7);
		retrievedAccount.Credit(13);
		retrievedAccount.Credit(31);

		IEventSource eventSource = retrievedAccount;
		var correlatedEvents = eventSource.TakeEvents().Select(evt => evt as ICorrelatedMessage).ToArray();
		foreach (var evt in correlatedEvents) {
			Assert.NotNull(evt);
			Assert.Equal(source.MsgId, evt.CausationId);
			Assert.Equal(source.CorrelationId, evt.CorrelationId);
		}
	}


	[Fact]
	public void can_save_new_correlated_aggregates() {
		var newAccountId = Guid.NewGuid();
		var source = MessageBuilder.New(() => new CreateAccount(newAccountId));
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
	public void can_save_updated_correlated_aggregates() {
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));

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
	public void save_ends_the_unit_of_work() {
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		var account = _correlatedRepo.GetById<Account>(_accountId, source);
		account.Credit(50);
		_correlatedRepo.Save(account);

		Assert.Throws<InvalidOperationException>(() => account.Credit(1));
	}

	[Fact]
	public void save_and_continue_keeps_the_source_armed() {
		var source = MessageBuilder.New(() => new CreditAccount(_accountId, 50));
		var account = _correlatedRepo.GetById<Account>(_accountId, source);
		account.Credit(50);
		_correlatedRepo.SaveAndContinue(account);

		// The held instance keeps raising in the same unit of work, and every persisted event —
		// before and after the intermediate save — carries the one command's correlation and causation.
		account.Credit(49);
		_correlatedRepo.Save(account);

		Assert.Equal(150, _correlatedRepo.GetById<Account>(_accountId, source).Balance);
		var slice = _mockStore.ReadStreamForward(_streamName, 4, 2);
		foreach (var recorded in slice.Events) {
			var evt = Assert.IsAssignableFrom<ICorrelatedMessage>(_serializer.Deserialize(recorded));
			Assert.Equal(source.MsgId, evt.CausationId);
			Assert.Equal(source.CorrelationId, evt.CorrelationId);
		}

		// The final save cleared the source as usual, and a fresh retrieval re-arms it.
		Assert.Throws<InvalidOperationException>(() => account.Credit(1));
		var next = MessageBuilder.New(() => new CreditAccount(_accountId, 1));
		_correlatedRepo.GetById<Account>(_accountId, next).Credit(1);
	}

	[Fact]
	public void can_delete_aggregate() {
		var newAccountId = Guid.NewGuid();
		var source = MessageBuilder.New(() => new CreateAccount(newAccountId));
		var newAccount = new Account(newAccountId, source);
		_correlatedRepo.Save(newAccount);

		var retrievedAccount = _correlatedRepo.GetById<Account>(newAccountId, source);
		_correlatedRepo.Delete(retrievedAccount);

		Assert.Throws<AggregateNotFoundException>(() => _correlatedRepo.GetById<Account>(newAccountId, source));
	}

	public class Account : AggregateRoot {
		//n.b. for infrastructure testing only not for prod or business unit tests
		public long Balance { get; private set; }
		//reflection constructor
		// ReSharper disable once UnusedMember.Local
		private Account() {
			Register<AccountCreated>(evt => Id = evt.AccountId);
			Register<AccountCredited>(evt => { Balance += evt.Amount; });
		}
		public Account(Guid id, ICorrelatedMessage source) : this() {
			((ICorrelatedEventSource)this).Source = source;
			Ensure.NotEmptyGuid(id, "id");
			Raise(new AccountCreated(id));
		}

		public void Credit(uint amount) {
			Raise(new AccountCredited(Id, amount));
		}
	}
	public record CreateAccount(Guid AccountId) : Command;
	public record AccountCreated(Guid AccountId) : Event;
	public record CreditAccount(Guid AccountId, uint Amount) : Command;
	//use of base class Event is optional
	public record AccountCredited(Guid AccountId, uint Amount) : ICorrelatedMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
		public Guid CorrelationId { get; set; }
		public Guid CausationId { get; set; }
	}
}
