using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveDomain.Example
{
    public interface IGroupRepository
    {
        Task<Group> LoadById(GroupIdentifier identifier, CancellationToken ct = default(CancellationToken));
        Task<Group> TryLoadById(GroupIdentifier identifier, CancellationToken ct = default(CancellationToken));

        Task Save(Group @group, Guid causation, Guid correlation, Metadata metadata, CancellationToken ct = default(CancellationToken));
    }
}