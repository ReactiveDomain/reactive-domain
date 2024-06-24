using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using System;

namespace ReactiveDomain.Testing
{
    /// <summary>
    /// An empty repository. Implements <see cref="ICorrelatedRepository"/> and <see cref="IRepository"/>.
    /// </summary>
    public class NullRepository : ICorrelatedRepository, IRepository
    {
        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ICorrelatedRepository"/> and <see cref="IRepository"/>.
        /// </summary>
        /// <param name="aggregate">This parameter is ignored.</param>
        public void Delete(IEventSource aggregate) { }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ICorrelatedRepository"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="id">This parameter is ignored.</param>
        /// <param name="source">This parameter is ignored.</param>
        /// <returns><c>null</c></returns>
        public TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource
        {
            return null;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ICorrelatedRepository"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="id">This parameter is ignored.</param>
        /// <param name="version">This parameter is ignored.</param>
        /// <param name="source">This parameter is ignored.</param>
        /// <returns><c>null</c></returns>
        public TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource
        {
            return null;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ICorrelatedRepository"/> and <see cref="IRepository"/>.
        /// </summary>
        /// <param name="aggregate">This parameter is ignored.</param>
        public void HardDelete(IEventSource aggregate) { }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ICorrelatedRepository"/> and <see cref="IRepository"/>.
        /// </summary>
        /// <param name="aggregate">This parameter is ignored.</param>
        public void Save(IEventSource aggregate) { }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ICorrelatedRepository"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="id">This parameter is ignored.</param>
        /// <param name="aggregate">Output parameter for the retrieved aggregate.</param>
        /// <param name="source">This parameter is ignored.</param>
        /// <returns><c>false</c></returns>
        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource
        {
            aggregate = null;
            return false;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ICorrelatedRepository"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="id">This parameter is ignored.</param>
        /// <param name="version">This parameter is ignored.</param>
        /// <param name="aggregate">Output parameter for the retrieved aggregate.</param>
        /// <param name="source">This parameter is ignored.</param>
        /// <returns><c>false</c></returns>
        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource
        {
            aggregate = null;
            return false;

        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IRepository"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="id">This parameter is ignored.</param>
        /// <param name="version">This parameter is ignored.</param>
        /// <returns><c>null</c></returns>
        TAggregate IRepository.GetById<TAggregate>(Guid id, int version)
        {
            return null;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IRepository"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="id">This parameter is ignored.</param>
        /// <param name="aggregate">Output parameter for the retrieved aggregate.</param>
        /// <param name="version">This parameter is ignored.</param>
        /// <returns><c>false</c></returns>
        bool IRepository.TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version)
        {
            aggregate = null;
            return false;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IRepository"/>.
        /// </summary>
        /// <typeparam name="TAggregate">This type parameter is ignored.</typeparam>
        /// <param name="aggregate">This parameter is ignored.</param>
        /// <param name="version">This parameter is ignored.</param>
        void IRepository.Update<TAggregate>(ref TAggregate aggregate, int version)
        {
        }
    }
}
