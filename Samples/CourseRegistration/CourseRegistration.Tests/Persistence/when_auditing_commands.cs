using CourseRegistration.Domain;
using CourseRegistration.ReadModels;
using Npgsql;
using ReactiveDomain.Messaging;

namespace CourseRegistration.Tests.Persistence;

// ReSharper disable once InconsistentNaming
public sealed class when_auditing_commands {
	/// <summary>The audit is in the read-side database; with it unreachable, commands still reach the write side.</summary>
	[Fact]
	public void a_command_is_sent_even_when_the_audit_cannot_be_written() {
		using var unreachable = NpgsqlDataSource.Create("Host=127.0.0.1;Port=1;Timeout=1;Pooling=false");
		using var h = new Harness();
		var audited = new AuditingCommandPublisher(h.Bus, new CommandAuditLog(unreachable));

		audited.Send(MessageBuilder.New(() => new OfferSection("2026FA", "CHEM 101", 1, "General Chemistry", 4, 30, Times.MonWed9)));

		Assert.Equal(30, h.Section(SectionAgg.IdFor("2026FA", "CHEM 101", 1)).Capacity);
	}
}
