using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveDomain.Util;


namespace ReactiveDomain {
    /// <summary>
  /// Represents a multi-request transaction with the Event Store
  /// </summary>
  public class EventStoreTransaction : IDisposable
  {
    /// <summary>
    /// The ID of the transaction. This can be used to recover
    /// a transaction later.
    /// </summary>
    public readonly long TransactionId;
    private readonly UserCredentials _userCredentials;
    private readonly IEventStoreTransactionConnection _connection;
    private bool _isRolledBack;
    private bool _isCommitted;

    /// <summary>
    /// Constructs a new <see cref="T:EventStore.ClientAPI.EventStoreTransaction" /></summary>
    /// <param name="transactionId">The transaction id of the transaction</param>
    /// <param name="userCredentials">User credentials under which transaction is committed.</param>
    /// <param name="connection">The connection the transaction is hooked to</param>
    internal EventStoreTransaction(
                long transactionId, 
                UserCredentials userCredentials, 
                IEventStoreTransactionConnection connection)
    {
      Ensure.Nonnegative(transactionId, nameof (transactionId));
      this.TransactionId = transactionId;
      this._userCredentials = userCredentials;
      this._connection = connection;
    }

    /// <summary>Asynchronously commits this transaction</summary>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> that returns expected version for following write requests</returns>
    public Task<WriteResult> CommitAsync()
    {
      if (this._isRolledBack)
        throw new InvalidOperationException("Cannot commit a rolledback transaction");
      if (this._isCommitted)
        throw new InvalidOperationException("Transaction is already committed");
      this._isCommitted = true;
      return this._connection.CommitTransactionAsync(this, this._userCredentials);
    }

    /// <summary>
    /// Writes to a transaction in the event store asynchronously
    /// </summary>
    /// <param name="events">The events to write</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> allowing the caller to control the async operation</returns>
    public Task WriteAsync(params EventData[] events)
    {
      return this.WriteAsync((IEnumerable<EventData>) events);
    }

    /// <summary>
    /// Writes to a transaction in the event store asynchronously
    /// </summary>
    /// <param name="events">The events to write</param>
    /// <returns>A <see cref="T:System.Threading.Tasks.Task" /> allowing the caller to control the async operation</returns>
    public Task WriteAsync(IEnumerable<EventData> events)
    {
      if (this._isRolledBack)
        throw new InvalidOperationException("Cannot write to a rolled-back transaction");
      if (this._isCommitted)
        throw new InvalidOperationException("Transaction is already committed");
      return this._connection.TransactionalWriteAsync(this, events, (UserCredentials) null);
    }

    /// <summary>Rollsback this transaction.</summary>
    public void Rollback()
    {
      if (this._isCommitted)
        throw new InvalidOperationException("Transaction is already committed");
      this._isRolledBack = true;
    }

    /// <summary>
    /// Disposes this transaction rolling it back if not already committed
    /// </summary>
    public void Dispose()
    {
      if (this._isCommitted)
        return;
      this._isRolledBack = true;
    }
  }
}