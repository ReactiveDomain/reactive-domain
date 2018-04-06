using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Testing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace MessageHierarchyTests
{
    public class MessageHierarchyTest
    {
        [Fact]
        public void TestNameLookup()
        {
            var messageType = MessageHierarchy.GetTypeByName("Message");
            Assert.Equal(typeof(Message), messageType);
            var childMessageType = MessageHierarchy.GetTypeByName("Event");
            Assert.Equal(typeof(Event), childMessageType);
        }
        [Fact]
        public void TestFullNameLookup()
        {
            var messageType = MessageHierarchy.GetTypeByFullName("ReactiveDomain.Messaging.Message");
            Assert.Equal(typeof(Message), messageType);
            var childMessageType = MessageHierarchy.GetTypeByFullName("ReactiveDomain.Messaging.Event");
            Assert.Equal(typeof(Event), childMessageType);
        }
        [Fact]
        public void TestMessageAncestorse()
        {
            var sut = typeof(Message);
            var ancestors = MessageHierarchy.AncestorsAndSelf(sut);
            Assert.Single(ancestors);
            Assert.Contains(typeof(Message), ancestors);

            sut = typeof(ParentTestEvent);
            ancestors = MessageHierarchy.AncestorsAndSelf(sut);
            Assert.Equal(4, ancestors.Count());
            Assert.Contains(typeof(ParentTestEvent), ancestors);
            Assert.Contains(typeof(DomainEvent), ancestors);
            Assert.Contains(typeof(Event), ancestors);
            Assert.Contains(typeof(Message), ancestors);
        }
        [Fact]
        public void TestMessageDescendents()
        {
            var sut = typeof(ParentTestEvent);
            var descendants = MessageHierarchy.DescendantsAndSelf(sut);
            Assert.Equal(3, descendants.Count());
            Assert.Contains(typeof(ParentTestEvent), descendants);
            Assert.Contains(typeof(ChildTestEvent), descendants);
            Assert.Contains(typeof(GrandChildTestEvent), descendants);

            sut = typeof(GrandChildTestEvent);
            descendants = MessageHierarchy.DescendantsAndSelf(sut);
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
