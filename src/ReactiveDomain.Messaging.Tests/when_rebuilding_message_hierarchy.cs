using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {

    // ReSharper disable InconsistentNaming
    public sealed class when_rebuilding_message_hierarchy:IDisposable {
        private string _filePath;
        sealed class TestMsg : IMessage {
            public Guid MsgId { get; private set; }
            public TestMsg()
            {
                MsgId = Guid.NewGuid();
            }
        }
        private void LoadDynamicAssembly() {
            var needed = new[] {
                "mscorlib",
                "netstandard",
                "Newtonsoft.Json",
                "System",
                "System.Core",
                "Microsoft.CSharp",
                "ReactiveDomain.Messaging",
                "ReactiveDomain.Core",
                "System.Numerics",
                "System.Xml"
            };
        
            var assemblies = (AppDomain.CurrentDomain
                .GetAssemblies()
                .Where(reference => needed.Contains(reference.GetName().Name,StringComparer.OrdinalIgnoreCase))
                .Select(reference => reference.Location)).Select(file => (MetadataReference)MetadataReference.CreateFromFile(file)).ToArray();

            var compilation = CSharpCompilation.Create("TestDynamicAssembly")
                .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
                .AddReferences(assemblies)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(
                    @"using ReactiveDomain.Messaging; namespace SimpleMsg { public class DynamicMessage : Message { } }"));

            _filePath =  AppDomain.CurrentDomain.BaseDirectory + $"\\TestDynamicAssembly{Guid.NewGuid()}.dll";

            var emitResult =compilation.Emit(_filePath);
            if(!emitResult.Success)
            {
                foreach(var diagnostic in emitResult.Diagnostics)
                {
                    Console.WriteLine(diagnostic.ToString());
                }
            }

            AppDomain.CurrentDomain.Load("TestDynamicAssembly");
        }
#if(NET48)
        [Fact]
        public void can_dynamically_add_types_without_clearing_handlers()
        {

            var bus = InMemoryBus.CreateTest();

            var fired = false;
            bus.Subscribe(new AdHocHandler<TestMsg>(m => fired = true));

            bus.Publish(new TestMsg());
            Assert.True(fired);

            LoadDynamicAssembly();
            var messageType = MessageHierarchy.GetTypeByName("DynamicMessage");
            Assert.Equal("DynamicMessage", messageType[0].Name);
            Assert.True(messageType.Count == 1);

            fired = false;
            bus.Publish(new TestMsg());
            AssertEx.IsOrBecomesTrue(() => fired);
        }
#endif
        public void Dispose() {
            if (!File.Exists(_filePath)) return;
            try {
                File.Delete(_filePath);
            }
            catch {
                // ignored
            }
        }
    }
    // ReSharper restore InconsistentNaming

}
