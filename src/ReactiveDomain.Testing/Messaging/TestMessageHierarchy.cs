using System;
using System.Linq;
using ReactiveDomain.Messaging;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing {
    public class MessageHierarchyTest {
        [Fact]
        public void TestNameLookup() {
            var messageType = MessageHierarchy.GetTypeByName("Message");
            Assert.Equal(typeof(Message), messageType[0]);
            Assert.True(messageType.Count == 1);
            var childMessageType = MessageHierarchy.GetTypeByName("Event");
            Assert.Equal(typeof(Event), childMessageType[0]);
            Assert.True(childMessageType.Count == 1);
        }
        [Fact]
        public void TestFullNameLookup() {
            var messageType = MessageHierarchy.GetTypeByFullName("ReactiveDomain.Messaging.Message");
            Assert.Equal(typeof(Message), messageType);
            var childMessageType = MessageHierarchy.GetTypeByFullName("ReactiveDomain.Messaging.Event");
            Assert.Equal(typeof(Event), childMessageType);
        }
        [Fact]
        public void TestMessageAncestors() {
            var sut = typeof(Message);
            var ancestors = MessageHierarchy.AncestorsAndSelf(sut).ToList();
            Assert.Collection(ancestors,
                x => Assert.Equal( typeof(Message),x),
                x => Assert.Equal( typeof(object),x)
                );
                
            sut = typeof(ParentTestEvent);
            ancestors = MessageHierarchy.AncestorsAndSelf(sut).ToList();
            Assert.Equal(3, ancestors.Count());
            Assert.Contains(typeof(ParentTestEvent), ancestors);
            Assert.Contains(typeof(Message), ancestors);
            Assert.Contains(typeof(object), ancestors);
        }
        [Fact]
        public void TestMessageDescendents() {
            var sut = typeof(ParentTestEvent);
            var descendants = MessageHierarchy.DescendantsAndSelf(sut).ToList();
            Assert.Equal(3, descendants.Count());
            Assert.Contains(typeof(ParentTestEvent), descendants);
            Assert.Contains(typeof(ChildTestEvent), descendants);
            Assert.Contains(typeof(GrandChildTestEvent), descendants);

            sut = typeof(GrandChildTestEvent);
            descendants = MessageHierarchy.DescendantsAndSelf(sut).ToList();
            Assert.Single(descendants);
            Assert.Contains(typeof(GrandChildTestEvent), descendants);
        }
        // TODO: Unit Test for loaded assembly scenario.
        //[Fact]
        //public void TestAssemblyLoad()
        //{
        //    // Load a new assembly holding types derived from Message
        //    Assembly assembly = Assembly.Load("TestDynamicLoadMessageTypes");
        //    // Assure already loaded types are not affected ...
        //    var sut = typeof(MessageChild);
        //    var messageChildType = MessageHierarchy.GetTypeByName("MessageChild");
        //    Assert.Equal(sut, messageChildType);

        //    var ancestors = MessageHierarchy.AncestorsAndSelf(sut);
        //    Assert.Equal(3, ancestors.Count());
        //    Assert.Contains(typeof(Message), ancestors);
        //    Assert.Contains(typeof(MessageParent), ancestors);
        //    Assert.Contains(typeof(MessageChild), ancestors);

        //    var descendants = MessageHierarchy.DescendantsAndSelf(sut);
        //    Assert.Single(descendants);
        //    Assert.Contains(typeof(MessageChild), descendants);

        //    // Assure new types have been loaded
        //    Type loadedType = assembly.GetType("TestDynamicLoadMessageTypes.DynamicallyLoadedMessage");
        //    Type loadedChildType = assembly.GetType("TestDynamicLoadMessageTypes.DynamicallyLoadedChildMessage");
        //    Assert.NotNull(loadedType);

        //    var dynamicallyLoadedMessageType = MessageHierarchy.GetTypeByName("DynamicallyLoadedMessage");
        //    Assert.Equal(loadedType, dynamicallyLoadedMessageType);

        //    dynamicallyLoadedMessageType = MessageHierarchy.GetTypeByFullName("TestDynamicLoadMessageTypes.DynamicallyLoadedMessage");
        //    Assert.Equal(loadedType, dynamicallyLoadedMessageType);

        //    descendants = MessageHierarchy.DescendantsAndSelf(loadedType);
        //    Assert.Equal(2, descendants.Count());
        //    Assert.Contains(loadedType, descendants);
        //    Assert.Contains(loadedChildType, descendants);
        //}
    }
}
