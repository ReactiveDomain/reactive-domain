using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {

#if !(NETCOREAPP3_1 || NETSTANDARD2_1 || NETSTANDARD2_1)    

    // ReSharper disable InconsistentNaming
    public class when_rebuilding_message_hierarchy {
        sealed class TestMsg : IMessage {
            public Guid MsgId { get; private set; }
            public TestMsg()
            {
                MsgId = Guid.NewGuid();
            }
        }
        private Assembly GetDynamicAssembly() {
            var needed = new[] {
                "mscorlib",
                "Newtonsoft.Json",
                "System",
                "System.Core",
                "Microsoft.CSharp",
                "ReactiveDomain.Messaging",
                "ReactiveDomain.Core",
                "System.Numerics",
                "System.Xml"};
        
            var assemblies = (AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(a => needed.Contains(a.GetName().Name,StringComparer.OrdinalIgnoreCase))
                .Select(a => a.Location)).ToArray();
            CompilerParameters parameters = new CompilerParameters {
                OutputAssembly = $"TestDynamicAssembly.dll"};

            parameters.ReferencedAssemblies.AddRange(assemblies);

            var code = @"using ReactiveDomain.Messaging; namespace SimpleMsg { public class DynamicMessage : Message { } }";
            CompilerResults r = CodeDomProvider.CreateProvider("CSharp").CompileAssemblyFromSource(parameters, code);
            return r.CompiledAssembly;
        }
        [Fact]
        public void can_dynamicly_add_types_without_clearing_handlers() {
            
            var bus = InMemoryBus.CreateTest();

            var fired = false;
            bus.Subscribe(new AdHocHandler<TestMsg>(m => fired = true));

            bus.Publish(new TestMsg());
            Assert.True(fired);
            
            AppDomain.CurrentDomain.Load(GetDynamicAssembly().GetName());
            var messageType = MessageHierarchy.GetTypeByName("DynamicMessage");
            Assert.Equal("DynamicMessage", messageType[0].Name);
            Assert.True(messageType.Count == 1);
           
            fired = false;
            bus.Publish(new TestMsg());
            AssertEx.IsOrBecomesTrue(()=> fired);


        }
    }
    // ReSharper restore InconsistentNaming
#endif
}
