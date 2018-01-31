using System;
using Newtonsoft.Json;

namespace ReactiveDomain.Domain.Tests.Example
{
    public class Show
    {
        public static void Usage()
        {
            var groupId = new GroupIdentifier(Guid.NewGuid());
            var groupName = new GroupName("Elvis Fanclub");
            var john = new GroupAdministratorIdentifier(Guid.NewGuid());
            var jane = new GroupAdministratorIdentifier(Guid.NewGuid());

            //Start a group (imagine a command)
            var group = Group.Start(groupId, groupName, john);
            PrintRecords(group);

            //Stop a group (imagine a command)
            group.Stop(jane);
            PrintRecords(group);

            //Take a snapshot of the group
            var source = (ISnapshotSource)group;
            var snapshot = source.TakeSnapshot();

            //Restore another instance from the snapshot
            var groupFromStore = Group.Factory();
            var destination = (ISnapshotSource)groupFromStore;
            destination.RestoreFromSnapshot(snapshot);

            //Here's an example of a composition root
            var settings = new JsonSerializerSettings();
            var invoker = new CommandHandlerInvoker(
                new CommandHandlerModule[]
                {
                    new GroupModule(
                        new GroupRepository(
                            null, /* todo - connection to embedded eventstore */
                            new EventSourceReaderConfiguration(
                                StreamNameConversions.PassThru, 
                                () => 
                                    new StreamEventsSliceTranslator(
                                        typeName => Type.GetType(typeName, true),
                                        settings),
                                new SliceSize(100)),
                            new EventSourceWriterConfiguration(
                                StreamNameConversions.PassThru, 
                                new EventSourceChangesetTranslator(type => type.FullName, settings))))
                });

            // Somewhere in the infrastructure, probably as part of an IHandleCommand<> (see below)
            var command =
                /* typically comes from the wire, here in deserialized shape. */
                new StartGroup(Guid.NewGuid(), "Fans of Elvis", Guid.NewGuid());
            invoker.Invoke(new CommandEnvelope().SetCommand(command)).Wait(); //Don't block obviously, go full async.
        }

        private static void PrintRecords(IEventSource group)
        {
            foreach (var @event in group.TakeEvents())
            {
                Console.WriteLine(@event.GetType().Name);
            }
        }
    }
}