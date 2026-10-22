using CourseRegistration.Domain;
using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;

namespace CourseRegistration.Tests.Persistence;

/// <summary>
/// The search filter is written twice, as SQL and as a predicate. These run each case against both
/// stores and expect the same answer from each, so the two halves cannot drift apart.
/// </summary>
// ReSharper disable once InconsistentNaming
public abstract class when_searching_sections {
	protected abstract IProjectionDb NewDb();

	private readonly Dictionary<string, Guid> _ids = [];

	private SectionQueries Seed() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		db.InTransaction(scope => {
			Add("chem-am", "CHEM 101", 1, Times.MonWed9);
			Add("chem-pm", "CHEM 101", 2, Times.TueThu13);
			Add("chem-full", "CHEM 101", 3, Times.Fri15, held: 30);
			Add("chemistry-lab", "CHEMX 150", 1, Times.Fri15);
			Add("bio-pm", "Bio 110", 1, Times.TueThu13);
			Add("hist-cancelled", "HIST 200", 1, Times.TueThu13, cancelled: true);
			Add("chem-next-term", "CHEM 101", 1, Times.TueThu13, term: "2027SP");

			void Add(string name, string course, int number, Meeting[] meetings, int held = 0, bool cancelled = false, string term = "2026FA") {
				var row = new SectionRow {
					Id = Guid.NewGuid(), Term = term, CourseCode = course, SectionNumber = number, Title = name,
					Credits = 4, Capacity = 30, SeatsHeld = held, IsCancelled = cancelled, Meetings = meetings
				};
				_ids[name] = row.Id;
				store.Upsert(scope, row);
			}
		});
		return new SectionQueries(db.ReadOnlyRows<Guid, SectionRow>());
	}

	private string[] Names(Page<SectionRow> page) => page.Items.Select(r => _ids.Single(i => i.Value == r.Id).Key).ToArray();

	[Fact]
	public void a_term_search_lists_open_sections_by_course_then_number() {
		var queries = Seed();

		var page = queries.Search(new SectionSearch("2026FA"), 0, 10);

		Assert.Equal(["bio-pm", "chem-am", "chem-pm", "chemistry-lab"], Names(page));
		Assert.Equal(4, page.Total);
	}

	[Fact]
	public void full_sections_appear_only_when_asked_for_and_cancelled_ones_never() {
		var queries = Seed();

		var page = queries.Search(new SectionSearch("2026FA", OpenOnly: false), 0, 10);

		Assert.Equal(["bio-pm", "chem-am", "chem-pm", "chem-full", "chemistry-lab"], Names(page));
	}

	[Fact]
	public void a_department_matches_the_code_before_the_space_ignoring_case() {
		var queries = Seed();

		Assert.Equal(["chem-am", "chem-pm"], Names(queries.Search(new SectionSearch("2026FA", Department: "chem"), 0, 10)));
		Assert.Equal(["bio-pm"], Names(queries.Search(new SectionSearch("2026FA", Department: "BIO"), 0, 10)));
	}

	[Fact]
	public void like_wildcards_in_a_department_are_literal_characters() {
		var queries = Seed();

		Assert.Empty(queries.Search(new SectionSearch("2026FA", Department: "C_EM"), 0, 10).Items);
		Assert.Empty(queries.Search(new SectionSearch("2026FA", Department: "%"), 0, 10).Items);
		Assert.Empty(queries.Search(new SectionSearch("2026FA", Department: @"CHEM\"), 0, 10).Items);
	}

	/// <summary>The SQL half reads the meetings out of the jsonb column; the predicate half reads the deserialized array.</summary>
	[Fact]
	public void a_start_time_keeps_sections_whose_every_meeting_starts_at_or_after_it() {
		var queries = Seed();

		var page = queries.Search(new SectionSearch("2026FA", StartsAtOrAfterMinute: 12 * 60), 0, 10);

		Assert.Equal(["bio-pm", "chem-pm", "chemistry-lab"], Names(page));
	}

	[Fact]
	public void pages_split_the_same_ordering_and_each_reports_the_full_count() {
		var queries = Seed();

		var first = queries.Search(new SectionSearch("2026FA"), 0, 3);
		var second = queries.Search(new SectionSearch("2026FA"), 3, 3);

		Assert.Equal(["bio-pm", "chem-am", "chem-pm"], Names(first));
		Assert.Equal(["chemistry-lab"], Names(second));
		Assert.Equal((4, 4), (first.Total, second.Total));
	}

	[Fact]
	public void a_section_is_found_by_its_catalog_name() {
		var queries = Seed();

		Assert.Equal(_ids["chem-pm"], queries.Find("2026FA", "chem 101", 2)?.Id);
		Assert.Equal(_ids["bio-pm"], queries.Find("2026FA", "BIO 110", 1)?.Id);
		Assert.Null(queries.Find("2026FA", "CHEM 101", 9));
		Assert.Equal(2, queries.GetMany([_ids["chem-am"], _ids["bio-pm"]]).Count);
	}
}

// ReSharper disable once InconsistentNaming
public sealed class when_searching_sections_in_memory : when_searching_sections {
	protected override IProjectionDb NewDb() => new InMemoryProjectionDb();
}

[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_searching_sections_in_postgres(PostgresFixture postgres) : when_searching_sections {
	protected override IProjectionDb NewDb() => new PostgresProjectionDb(postgres.NewDatabase());
}
