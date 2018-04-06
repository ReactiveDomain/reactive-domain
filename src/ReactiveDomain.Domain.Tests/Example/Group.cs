using System;

namespace ReactiveDomain.Domain.Tests.Example
{
    public class Group : AggregateRootEntity, ISnapshotSource
    {
        public static readonly Func<Group> Factory = () => new Group();

        private GroupIdentifier _groupId;
        private GroupName _name;
        private bool _active;

        private Group()
        {
            Register<GroupStarted>(_ =>
            {
                _groupId = new GroupIdentifier(_.GroupId);
                _name = new GroupName(_.Name);
                _active = true;
            });

            Register<GroupStopped>(_ =>
            {
                _active = false;
            });
        }

        public new GroupIdentifier Id => _groupId;

        public static Group Start(GroupIdentifier groupId, GroupName name, GroupAdministratorIdentifier startedBy)
        {
            var group = new Group();
            group.Raise(new GroupStarted(
                groupId.ToGuid(),
                name.ToString(),
                startedBy.ToGuid(),
                0 // DateTimeOffset.UtcNow.Ticks
            ));
            return group;
        }

        public void Stop(GroupAdministratorIdentifier stoppedBy)
        {
            if (_active)
            {
                Raise(new GroupStopped(
                    _groupId.ToGuid(),
                    _name.ToString(),
                    stoppedBy.ToGuid(),
                    0 // DateTimeOffset.UtcNow.Ticks
                ));
            }
        }

        void ISnapshotSource.RestoreFromSnapshot(object snapshot)
        {
            if(snapshot is GroupSnapshot data)
            {
                _groupId = new GroupIdentifier(data.GroupId);
                _name = new GroupName(data.Name);
                _active = data.Active;
            }
        }

        object ISnapshotSource.TakeSnapshot()
        {
            return new GroupSnapshot
            {
                GroupId = _groupId.ToGuid(),
                Name = _name.ToString(),
                Active = _active
            };
        }
    }
}