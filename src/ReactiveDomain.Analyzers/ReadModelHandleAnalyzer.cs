using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ReactiveDomain.Analyzers;

/// <summary>
/// Flags a public <c>Handle(T)</c> on a <c>ReadModelBase</c> subclass. A caller writing
/// <c>model.Handle(evt)</c> binds to that method, runs the handler on the calling thread, and
/// skips the model's queue.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ReadModelHandleAnalyzer : DiagnosticAnalyzer {
	public const string DiagnosticId = "RD0001";

	private static readonly DiagnosticDescriptor Rule = new(
		DiagnosticId,
		"Read model Handle bypasses the queue",
		"'{0}.Handle({1})' is public, so a caller writing `model.Handle(evt)` runs the handler on the calling thread and skips the queue. Implement IHandle<{1}> explicitly.",
		"Usage",
		DiagnosticSeverity.Warning,
		isEnabledByDefault: true,
		description:
			"ReadModelBase.Handle(IMessage) enqueues. A public Handle(T) for a type the model " +
			"implements IHandle<T> for is a better overload, so the call never reaches the queue.");

	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

	public override void Initialize(AnalysisContext context) {
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();
		context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);
	}

	private static void AnalyzeMethod(SymbolAnalysisContext context) {
		if (context.Symbol is not IMethodSymbol method)
			return;
		if (method.Name != "Handle" || method.DeclaredAccessibility != Accessibility.Public)
			return;
		if (method.IsStatic || method.Parameters.Length != 1)
			return;
		if (method.ContainingType is null || method.ContainingType.TypeKind != TypeKind.Class)
			return;
		if (!DerivesFromReadModelBase(method.ContainingType))
			return;

		var parameterType = method.Parameters[0].Type;
		if (parameterType.TypeKind == TypeKind.Error)
			return;
		if (parameterType.Name is "IMessage" or "Message"
			&& parameterType.ContainingNamespace.ToDisplayString() == "ReactiveDomain.Messaging")
			return;

		var parameterName = parameterType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

		context.ReportDiagnostic(Diagnostic.Create(
			Rule,
			method.Locations[0],
			method.ContainingType.Name,
			parameterName));
	}

	private static bool DerivesFromReadModelBase(INamedTypeSymbol type) {
		for (var current = type.BaseType; current is not null; current = current.BaseType) {
			if (current.Name == "ReadModelBase"
				&& current.ContainingNamespace.ToDisplayString() == "ReactiveDomain.Foundation")
				return true;
		}
		return false;
	}
}
