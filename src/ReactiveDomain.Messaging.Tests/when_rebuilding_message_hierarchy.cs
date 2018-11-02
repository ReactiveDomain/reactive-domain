using System;
using System.CodeDom.Compiler;
using System.Linq;
using System.Reflection;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {

#if !(NETCOREAPP2_0 || NETSTANDARD2_0)    

    // ReSharper disable InconsistentNaming
    public class when_rebuilding_message_hierarchy {
        sealed class TestMsg : Message { }
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
                GenerateInMemory = true,
                OutputAssembly = $"{Guid.NewGuid()}.dll"};

            parameters.ReferencedAssemblies.AddRange(assemblies);

            var code = @"using ReactiveDomain.Messaging; namespace SimpleMsg { public class DynamicMessage : Message { } }";
            CompilerResults r = CodeDomProvider.CreateProvider("CSharp").CompileAssemblyFromSource(parameters, code);
            return r.CompiledAssembly;
        }
        [Fact]
        public void in_memory_bus_remembers_handlers() {
            var method = typeof(MessageHierarchy).GetMethod("LoadAssembly", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(method);

            var bus = InMemoryBus.CreateTest();

            var fired = false;
            bus.Subscribe(new AdHocHandler<TestMsg>(m => fired = true));

            bus.Publish(new TestMsg());
            Assert.True(fired);

            // ReSharper disable once PossibleNullReferenceException
            method.Invoke(null, new object[] { GetDynamicAssembly() });

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
