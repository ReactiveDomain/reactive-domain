using ReactiveDomain;

namespace ReactiveDomain.Example
{
    public class GroupModule : CommandHandlerModule
    {
        public GroupModule(IRepository<Group> groups)
        {
            For<StartGroup>()
                .Validate(new StartGroupValidator())
                .Handle(async (envelope, ct) =>
                {
                    var groupId = new GroupIdentifier(envelope.Command.GroupId);
                    var group = await groups.TryLoadAsync(groupId, ct);
                    if (group == null)
                    {
                        group = Group.Start(
                            groupId,
                            new GroupName(envelope.Command.Name),
                            new GroupAdministratorIdentifier(envelope.Command.AdministratorId));

                        await groups.SaveAsync(groupId, group, envelope.CommandId, envelope.CorrelationId, envelope.Metadata, ct);
                    }
                });

            For<StopGroup>()
                .Validate(new StopGroupValidator())
                .Handle(async (envelope, ct) =>
                {
                    var groupId = new GroupIdentifier(envelope.Command.GroupId);
                    var group = await groups.LoadAsync(groupId, ct);

                    group.Stop(new GroupAdministratorIdentifier(envelope.Command.AdministratorId));

                    await groups.SaveAsync(groupId, group, envelope.CommandId, envelope.CorrelationId, envelope.Metadata, ct);
                });
        }
    }
}