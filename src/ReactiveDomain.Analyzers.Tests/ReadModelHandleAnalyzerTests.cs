using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using ReactiveDomain.Analyzers;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Analyzers.Tests;

public sealed class ReadModelHandleAnalyzerTests {
	[Fact]
	public async Task public_handle_on_a_read_model_is_warned() {
		const string source = """
			using ReactiveDomain.Foundation;
			using ReactiveDomain.Messaging;
			using ReactiveDomain.Messaging.Bus;
			public sealed class Sample : ReadModelBase, IHandle<Sample.E> {
			    public Sample(IConfiguredConnection c) : base(nameof(Sample), c) { }
			    public void Handle(E e) { }
			    public record E : Event;
			}
			""";
		var diagnostics = await Diagnose(source);
		var warning = Assert.Single(diagnostics, d => d.Id == ReadModelHandleAnalyzer.DiagnosticId);
		Assert.Contains("Handle(E)", warning.GetMessage());
	}

	[Fact]
	public async Task explicit_handle_is_silent() {
		const string source = """
			using ReactiveDomain.Foundation;
			using ReactiveDomain.Messaging;
			using ReactiveDomain.Messaging.Bus;
			public sealed class Sample : ReadModelBase, IHandle<Sample.E> {
			    public Sample(IConfiguredConnection c) : base(nameof(Sample), c) { }
			    void IHandle<E>.Handle(E e) { }
			    public record E : Event;
			}
			""";
		var diagnostics = await Diagnose(source);
		Assert.DoesNotContain(diagnostics, d => d.Id == ReadModelHandleAnalyzer.DiagnosticId);
	}

	[Fact]
	public async Task handle_of_imessage_on_the_base_is_silent() {
		const string source = """
			using ReactiveDomain.Foundation;
			using ReactiveDomain.Messaging;
			public sealed class Sample : ReadModelBase {
			    public Sample(IConfiguredConnection c) : base(nameof(Sample), c) { }
			    public void Inject(IMessage m) => Handle(m);
			}
			""";
		var diagnostics = await Diagnose(source);
		Assert.DoesNotContain(diagnostics, d => d.Id == ReadModelHandleAnalyzer.DiagnosticId);
	}

	private static async Task<ImmutableArray<Diagnostic>> Diagnose(string source) {
		var tree = CSharpSyntaxTree.ParseText(source);
		var refs = new List<MetadataReference>();
		if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted) {
			foreach (var path in trusted.Split(Path.PathSeparator)) {
				if (path.Length > 0)
					refs.Add(MetadataReference.CreateFromFile(path));
			}
		}
		refs.Add(MetadataReference.CreateFromFile(typeof(ReadModelBase).Assembly.Location));
		refs.Add(MetadataReference.CreateFromFile(typeof(IMessage).Assembly.Location));
		refs.Add(MetadataReference.CreateFromFile(typeof(IEventSource).Assembly.Location));
		var compilation = CSharpCompilation.Create(
			"analyzer-test",
			[tree],
			refs,
			new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
		var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(new ReadModelHandleAnalyzer());
		return await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync();
	}
}
