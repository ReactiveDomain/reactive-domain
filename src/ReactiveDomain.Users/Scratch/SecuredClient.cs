using System;
using ReactiveDomain.Foundation.Domain;

namespace ReactiveDomain.Users.Scratch
{
    /// <summary>
    /// Given a client is *typically* assigned within an application, it seems natural that a secured client
    /// is a child of a <see cref="SecuredApplication" />.
    /// </summary>
    public class SecuredClient : ChildEntity
    {
        public SecuredClient(Guid id, AggregateRoot root) : base(id, root)
        {
        }
    }
}