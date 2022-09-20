﻿using System;

namespace ReactiveDomain.Foundation.StreamStore
{
    /// <summary>
    /// While it might seem more natural to save and restore the event set behind the aggregate,
    /// this cache stores only the collapsed state in the aggregate  
    /// </summary>
    public interface IAggregateCache : IRepository, IDisposable
    {
        bool Remove<TAggregate>(Guid id);
        void Clear();
    }
}