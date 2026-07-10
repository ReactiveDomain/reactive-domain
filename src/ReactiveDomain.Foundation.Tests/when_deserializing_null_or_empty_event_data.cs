using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public class when_deserializing_null_or_empty_event_data {
	private readonly JsonMessageSerializer _serializer = new();

	[Fact]
	public void deserialize_returns_null_for_null_metadata() {
		var evt = new RecordedEvent(
			"test-stream", Guid.NewGuid(), 0, "TestEvent",
			data: [0x7B, 0x7D],
			metadata: null!,
			isJson: true, DateTime.UtcNow, 0);

		var result = _serializer.Deserialize(evt);

		Assert.Null(result);
	}

	[Fact]
	public void deserialize_returns_null_for_empty_metadata() {
		var evt = new RecordedEvent(
			"test-stream", Guid.NewGuid(), 0, "TestEvent",
			data: [0x7B, 0x7D],
			metadata: [],
			isJson: true, DateTime.UtcNow, 0);

		var result = _serializer.Deserialize(evt);

		Assert.Null(result);
	}

	[Fact]
	public void deserialize_returns_null_for_null_data() {
		var evt = new RecordedEvent(
			"test-stream", Guid.NewGuid(), 0, "TestEvent",
			data: null!,
			metadata: [0x7B, 0x7D],
			isJson: true, DateTime.UtcNow, 0);

		var result = _serializer.Deserialize(evt);

		Assert.Null(result);
	}

	[Fact]
	public void deserialize_returns_null_for_empty_data() {
		var evt = new RecordedEvent(
			"test-stream", Guid.NewGuid(), 0, "TestEvent",
			data: [],
			metadata: [0x7B, 0x7D],
			isJson: true, DateTime.UtcNow, 0);

		var result = _serializer.Deserialize(evt);

		Assert.Null(result);
	}

	[Fact]
	public void deserialize_returns_null_when_both_null() {
		var evt = new RecordedEvent(
			"test-stream", Guid.NewGuid(), 0, "TestEvent",
			data: null!,
			metadata: null!,
			isJson: true, DateTime.UtcNow, 0);

		var result = _serializer.Deserialize(evt);

		Assert.Null(result);
	}
}
