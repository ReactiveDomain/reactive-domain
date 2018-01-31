namespace ReactiveDomain.Domain.Tests.Example
{
    public class GroupModule : CommandHandlerModule
    {
        public GroupModule(IGroupRepository groups)
        {
            For<StartGroup>()
                .Validate(new StartGroupValidator())
                .Handle(async (envelope, ct) =>
                {
                    var groupId = new GroupIdentifier(envelope.Command.GroupId);
                    var group = await groups.TryLoadById(groupId, ct);
                    if (group == null)
                    {
                        group = Group.Start(
                            groupId,
                            new GroupName(envelope.Command.Name),
                            new GroupAdministratorIdentifier(envelope.Command.AdministratorId));

                        await groups.Save(group, envelope.CommandId, envelope.CorrelationId, envelope.Metadata, ct);
                    }
                });

            For<StopGroup>()
                .Validate(new StopGroupValidator())
                .Handle(async (envelope, ct) =>
                {
                    var group = await groups.LoadById(new GroupIdentifier(envelope.Command.GroupId), ct);

                    group.Stop(new GroupAdministratorIdentifier(envelope.Command.AdministratorId));

                    await groups.Save(group, envelope.CommandId, envelope.CorrelationId, envelope.Metadata, ct);
                });
        }
    }
}