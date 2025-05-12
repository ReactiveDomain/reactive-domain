# ReactiveDomain.Core Assembly

[← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)

The ReactiveDomain.Core assembly contains the fundamental interfaces and abstractions that form the foundation of the Reactive Domain library. These core interfaces define the contract for event sourcing and are used throughout the library.

## Table of Contents

- [Interfaces](#interfaces)
  - [IEventSource](#ieventsource)
  - [IMetadataSource](#imetadatasource)
- [Classes](#classes)
  - [Metadata](#metadata)
- [Exceptions](#exceptions)
  - [AggregateNotFoundException](#aggregatenotfoundexception)
  - [AggregateDeletedException](#aggregatedeletedexception)
  - [AggregateVersionException](#aggregateversionexception)

## Interfaces

### IEventSource

**Namespace**: `ReactiveDomain`

**Purpose**: Represents a source of events from the perspective of restoring from and taking events.

**Declaration**:
```csharp
public interface IEventSource
{
    Guid Id { get; }
    long ExpectedVersion { get; set; }
    void RestoreFromEvents(IEnumerable<object> events);
    void UpdateWithEvents(IEnumerable<object> events, long expectedVersion);
    object[] TakeEvents();
}
```

#### Properties

##### Id

**Type**: `Guid`  
**Accessibility**: `get`  
**Description**: Gets the unique identifier for this EventSource. This must be provided by the implementing class.

##### ExpectedVersion

**Type**: `long`  
**Accessibility**: `get`, `set`  
**Description**: Gets or sets the expected version this instance is at.

#### Methods

##### RestoreFromEvents

**Signature**: `void RestoreFromEvents(IEnumerable<object> events)`  
**Parameters**:
- `events` (`IEnumerable<object>`): The events to restore from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.

**Description**: Restores this instance from the history of events.

##### UpdateWithEvents

**Signature**: `void UpdateWithEvents(IEnumerable<object> events, long expectedVersion)`  
**Parameters**:
- `events` (`IEnumerable<object>`): The events to update with.
- `expectedVersion` (`long`): The expected version to start from.

**Exceptions**:
- `System.ArgumentNullException`: Thrown when `events` is `null`.
- `System.InvalidOperationException`: Thrown when this instance does not have historical events or expected version mismatch.

**Description**: Updates this instance with the provided events, starting from the expected version.

##### TakeEvents

**Signature**: `object[] TakeEvents()`  
**Returns**: The recorded events.

**Description**: Takes the recorded history of events from this instance (CQS violation, beware).

### IMetadataSource

**Namespace**: `ReactiveDomain`

**Purpose**: Defines the contract for entities that have associated metadata.

**Declaration**:
```csharp
public interface IMetadataSource
{
    Metadata ReadMetadata();
    Metadata Initialize();
    void Initialize(Metadata md);
}
```

#### Methods

##### ReadMetadata

**Signature**: `Metadata ReadMetadata()`  
**Returns**: The object's metadata.

**Description**: Gets the object's metadata.

##### Initialize

**Signature**: `Metadata Initialize()`  
**Returns**: The initialized metadata.

**Description**: Initializes an object's metadata using default values.

##### Initialize

**Signature**: `void Initialize(Metadata md)`  
**Parameters**:
- `md` (`Metadata`): The metadata to use for initialization.

**Description**: Initializes an object using the provided metadata.

## Classes

### Metadata

**Namespace**: `ReactiveDomain`

**Purpose**: Represents metadata associated with an object.

**Declaration**:
```csharp
public class Metadata : Dictionary<string, object>
{
    public Metadata();
    public Metadata(IDictionary<string, object> dictionary);
    public Metadata(Metadata metadata);
    public T GetValue<T>(string key);
    public bool TryGetValue<T>(string key, out T value);
}
```

#### Constructors

##### Metadata()

**Signature**: `public Metadata()`  
**Description**: Initializes a new instance of the Metadata class.

##### Metadata(IDictionary<string, object>)

**Signature**: `public Metadata(IDictionary<string, object> dictionary)`  
**Parameters**:
- `dictionary` (`IDictionary<string, object>`): The dictionary to initialize from.

**Description**: Initializes a new instance of the Metadata class with the specified dictionary.

##### Metadata(Metadata)

**Signature**: `public Metadata(Metadata metadata)`  
**Parameters**:
- `metadata` (`Metadata`): The metadata to copy from.

**Description**: Initializes a new instance of the Metadata class with the specified metadata.

#### Methods

##### GetValue<T>

**Signature**: `public T GetValue<T>(string key)`  
**Type Parameters**:
- `T`: The type of the value to get.

**Parameters**:
- `key` (`string`): The key of the value to get.

**Returns**: The value associated with the specified key.

**Exceptions**:
- `KeyNotFoundException`: Thrown when the key is not found.
- `InvalidCastException`: Thrown when the value cannot be cast to the specified type.

**Description**: Gets the value associated with the specified key and casts it to the specified type.

##### TryGetValue<T>

**Signature**: `public bool TryGetValue<T>(string key, out T value)`  
**Type Parameters**:
- `T`: The type of the value to get.

**Parameters**:
- `key` (`string`): The key of the value to get.
- `value` (`T`): When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter.

**Returns**: `true` if the key was found; otherwise, `false`.

**Description**: Tries to get the value associated with the specified key and cast it to the specified type.

## Exceptions

### AggregateNotFoundException

**Namespace**: `ReactiveDomain`

**Purpose**: Thrown when an aggregate is not found.

**Declaration**:
```csharp
public class AggregateNotFoundException : Exception
{
    public AggregateNotFoundException(Guid id, Type type);
    public AggregateNotFoundException(Guid id, Type type, Exception innerException);
    public Guid Id { get; }
    public Type Type { get; }
}
```

#### Constructors

##### AggregateNotFoundException(Guid, Type)

**Signature**: `public AggregateNotFoundException(Guid id, Type type)`  
**Parameters**:
- `id` (`Guid`): The ID of the aggregate that was not found.
- `type` (`Type`): The type of the aggregate that was not found.

**Description**: Initializes a new instance of the AggregateNotFoundException class.

##### AggregateNotFoundException(Guid, Type, Exception)

**Signature**: `public AggregateNotFoundException(Guid id, Type type, Exception innerException)`  
**Parameters**:
- `id` (`Guid`): The ID of the aggregate that was not found.
- `type` (`Type`): The type of the aggregate that was not found.
- `innerException` (`Exception`): The exception that is the cause of the current exception.

**Description**: Initializes a new instance of the AggregateNotFoundException class with a specified error message and a reference to the inner exception that is the cause of this exception.

#### Properties

##### Id

**Type**: `Guid`  
**Accessibility**: `get`  
**Description**: Gets the ID of the aggregate that was not found.

##### Type

**Type**: `Type`  
**Accessibility**: `get`  
**Description**: Gets the type of the aggregate that was not found.

### AggregateDeletedException

**Namespace**: `ReactiveDomain`

**Purpose**: Thrown when an attempt is made to access a deleted aggregate.

**Declaration**:
```csharp
public class AggregateDeletedException : Exception
{
    public AggregateDeletedException(Guid id, Type type);
    public AggregateDeletedException(Guid id, Type type, Exception innerException);
    public Guid Id { get; }
    public Type Type { get; }
}
```

#### Constructors

##### AggregateDeletedException(Guid, Type)

**Signature**: `public AggregateDeletedException(Guid id, Type type)`  
**Parameters**:
- `id` (`Guid`): The ID of the deleted aggregate.
- `type` (`Type`): The type of the deleted aggregate.

**Description**: Initializes a new instance of the AggregateDeletedException class.

##### AggregateDeletedException(Guid, Type, Exception)

**Signature**: `public AggregateDeletedException(Guid id, Type type, Exception innerException)`  
**Parameters**:
- `id` (`Guid`): The ID of the deleted aggregate.
- `type` (`Type`): The type of the deleted aggregate.
- `innerException` (`Exception`): The exception that is the cause of the current exception.

**Description**: Initializes a new instance of the AggregateDeletedException class with a specified error message and a reference to the inner exception that is the cause of this exception.

#### Properties

##### Id

**Type**: `Guid`  
**Accessibility**: `get`  
**Description**: Gets the ID of the deleted aggregate.

##### Type

**Type**: `Type`  
**Accessibility**: `get`  
**Description**: Gets the type of the deleted aggregate.

### AggregateVersionException

**Namespace**: `ReactiveDomain`

**Purpose**: Thrown when there is a version mismatch for an aggregate.

**Declaration**:
```csharp
public class AggregateVersionException : Exception
{
    public AggregateVersionException(Guid id, Type type, long expectedVersion, long actualVersion);
    public AggregateVersionException(Guid id, Type type, long expectedVersion, long actualVersion, Exception innerException);
    public Guid Id { get; }
    public Type Type { get; }
    public long ExpectedVersion { get; }
    public long ActualVersion { get; }
}
```

#### Constructors

##### AggregateVersionException(Guid, Type, long, long)

**Signature**: `public AggregateVersionException(Guid id, Type type, long expectedVersion, long actualVersion)`  
**Parameters**:
- `id` (`Guid`): The ID of the aggregate with the version mismatch.
- `type` (`Type`): The type of the aggregate with the version mismatch.
- `expectedVersion` (`long`): The expected version.
- `actualVersion` (`long`): The actual version.

**Description**: Initializes a new instance of the AggregateVersionException class.

##### AggregateVersionException(Guid, Type, long, long, Exception)

**Signature**: `public AggregateVersionException(Guid id, Type type, long expectedVersion, long actualVersion, Exception innerException)`  
**Parameters**:
- `id` (`Guid`): The ID of the aggregate with the version mismatch.
- `type` (`Type`): The type of the aggregate with the version mismatch.
- `expectedVersion` (`long`): The expected version.
- `actualVersion` (`long`): The actual version.
- `innerException` (`Exception`): The exception that is the cause of the current exception.

**Description**: Initializes a new instance of the AggregateVersionException class with a specified error message and a reference to the inner exception that is the cause of this exception.

#### Properties

##### Id

**Type**: `Guid`  
**Accessibility**: `get`  
**Description**: Gets the ID of the aggregate with the version mismatch.

##### Type

**Type**: `Type`  
**Accessibility**: `get`  
**Description**: Gets the type of the aggregate with the version mismatch.

##### ExpectedVersion

**Type**: `long`  
**Accessibility**: `get`  
**Description**: Gets the expected version.

##### ActualVersion

**Type**: `long`  
**Accessibility**: `get`  
**Description**: Gets the actual version.

[↑ Back to Top](#reactivedomaincore-assembly) | [← Back to API Reference](../README.md) | [← Back to Table of Contents](../../README.md)
