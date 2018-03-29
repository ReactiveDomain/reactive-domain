using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    internal interface IResolvedEvent
    {
        string OriginalStreamId { get; }

        long OriginalEventNumber { get; }

        RecordedEvent OriginalEvent { get; }

        Position? OriginalPosition { get; }
    }
     /// <summary>
  /// A structure representing a single event or an resolved link event.
  /// </summary>
  public struct ResolvedEvent : IResolvedEvent
  {
    /// <summary>
    /// The event, or the resolved link event if this <see cref="T:EventStore.ClientAPI.ResolvedEvent" /> is
    /// a link event.
    /// </summary>
    public readonly RecordedEvent Event;
    /// <summary>
    /// The link event if this <see cref="T:EventStore.ClientAPI.ResolvedEvent" /> is a link event.
    /// </summary>
    public readonly RecordedEvent Link;
    /// <summary>
    /// The logical position of the <see cref="P:EventStore.ClientAPI.ResolvedEvent.OriginalEvent" />.
    /// </summary>
    public readonly Position? OriginalPosition;

    /// <summary>
    /// Returns the event that was read or which triggered the subscription.
    /// 
    /// If this <see cref="T:EventStore.ClientAPI.ResolvedEvent" /> represents a link event, the Link
    /// will be the <see cref="P:EventStore.ClientAPI.ResolvedEvent.OriginalEvent" />, otherwise it will be the
    /// event.
    /// </summary>
    public RecordedEvent OriginalEvent
    {
      get
      {
        return this.Link ?? this.Event;
      }
    }

    /// <summary>
    /// Indicates whether this <see cref="T:EventStore.ClientAPI.ResolvedEvent" /> is a resolved link
    /// event.
    /// </summary>
    public bool IsResolved
    {
      get
      {
        if (this.Link != null)
          return this.Event != null;
        return false;
      }
    }

    /// <summary>
    /// The stream name of the <see cref="P:EventStore.ClientAPI.ResolvedEvent.OriginalEvent" />.
    /// </summary>
    public string OriginalStreamId
    {
      get
      {
        return this.OriginalEvent.EventStreamId;
      }
    }

    /// <summary>
    /// The event number in the stream of the <see cref="P:EventStore.ClientAPI.ResolvedEvent.OriginalEvent" />.
    /// </summary>
    public long OriginalEventNumber
    {
      get
      {
        return this.OriginalEvent.EventNumber;
      }
    }

    //internal ResolvedEvent(ResolvedEvent evnt)
    //{
    //  this.Event = evnt.Event ?? new RecordedEvent(evnt.Event);
    //  this.Link = evnt.Link == null ? (RecordedEvent) null : new RecordedEvent(evnt.Link);
    //  this.OriginalPosition = new Position?(new Position(evnt.CommitPosition, evnt.PreparePosition));
    //}

    //internal ResolvedEvent(ClientMessage.ResolvedIndexedEvent evnt)
    //{
    //  this.Event = evnt.Event == null ? (RecordedEvent) null : new RecordedEvent(evnt.Event);
    //  this.Link = evnt.Link == null ? (RecordedEvent) null : new RecordedEvent(evnt.Link);
    //  this.OriginalPosition = new Position?();
    //}

    Position? IResolvedEvent.OriginalPosition
    {
      get
      {
        return this.OriginalPosition;
      }
    }

    RecordedEvent IResolvedEvent.OriginalEvent
    {
      get
      {
        return this.OriginalEvent;
      }
    }

    long IResolvedEvent.OriginalEventNumber
    {
      get
      {
        return this.OriginalEventNumber;
      }
    }

    string IResolvedEvent.OriginalStreamId
    {
      get
      {
        return this.OriginalStreamId;
      }
    }
  }
}
