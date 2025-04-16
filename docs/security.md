# Security Guide

[← Back to Table of Contents](README.md)

This guide provides best practices and considerations for securing applications built with Reactive Domain.

## Table of Contents

- [Authentication and Authorization](#authentication-and-authorization)
- [Data Protection and Privacy](#data-protection-and-privacy)
- [Audit Logging and Compliance](#audit-logging-and-compliance)
- [Secure Deployment Practices](#secure-deployment-practices)
- [Threat Modeling](#threat-modeling)
- [Security Testing Strategies](#security-testing-strategies)

## Authentication and Authorization

### Command Authorization

Implement authorization for commands:

```csharp
public class AuthorizedCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserContext _userContext;
    
    public AuthorizedCommandBus(
        ICommandBus innerBus,
        IAuthorizationService authorizationService,
        IUserContext userContext)
    {
        _innerBus = innerBus;
        _authorizationService = authorizationService;
        _userContext = userContext;
    }
    
    public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        // Authorize the command
        if (!_authorizationService.IsAuthorized(_userContext.CurrentUser, command))
        {
            throw new UnauthorizedAccessException($"User is not authorized to execute {typeof(TCommand).Name}");
        }
        
        // Forward to inner bus
        _innerBus.Send(command);
    }
}
```

### Query Authorization

Implement authorization for queries:

```csharp
public class AuthorizedQueryBus : IQueryBus
{
    private readonly IQueryBus _innerBus;
    private readonly IAuthorizationService _authorizationService;
    private readonly IUserContext _userContext;
    
    public AuthorizedQueryBus(
        IQueryBus innerBus,
        IAuthorizationService authorizationService,
        IUserContext userContext)
    {
        _innerBus = innerBus;
        _authorizationService = authorizationService;
        _userContext = userContext;
    }
    
    public TResult Query<TQuery, TResult>(TQuery query) where TQuery : class, IQuery<TResult>
    {
        // Authorize the query
        if (!_authorizationService.IsAuthorized(_userContext.CurrentUser, query))
        {
            throw new UnauthorizedAccessException($"User is not authorized to execute {typeof(TQuery).Name}");
        }
        
        // Forward to inner bus
        return _innerBus.Query<TQuery, TResult>(query);
    }
}
```

### Role-Based Access Control

Implement role-based access control:

```csharp
public class RoleBasedAuthorizationService : IAuthorizationService
{
    private readonly Dictionary<Type, string[]> _commandRoles = new Dictionary<Type, string[]>();
    private readonly Dictionary<Type, string[]> _queryRoles = new Dictionary<Type, string[]>();
    
    public void RegisterCommandRoles<TCommand>(params string[] roles) where TCommand : ICommand
    {
        _commandRoles[typeof(TCommand)] = roles;
    }
    
    public void RegisterQueryRoles<TQuery>(params string[] roles) where TQuery : IQuery
    {
        _queryRoles[typeof(TQuery)] = roles;
    }
    
    public bool IsAuthorized(IUser user, object message)
    {
        if (message is ICommand command)
        {
            return IsAuthorizedForCommand(user, command);
        }
        else if (message is IQuery query)
        {
            return IsAuthorizedForQuery(user, query);
        }
        
        return false;
    }
    
    private bool IsAuthorizedForCommand(IUser user, ICommand command)
    {
        if (_commandRoles.TryGetValue(command.GetType(), out var roles))
        {
            return roles.Any(role => user.IsInRole(role));
        }
        
        // By default, deny access if no roles are specified
        return false;
    }
    
    private bool IsAuthorizedForQuery(IUser user, IQuery query)
    {
        if (_queryRoles.TryGetValue(query.GetType(), out var roles))
        {
            return roles.Any(role => user.IsInRole(role));
        }
        
        // By default, deny access if no roles are specified
        return false;
    }
}
```

### Integration with ASP.NET Core Identity

Integrate with ASP.NET Core Identity:

```csharp
public class AspNetCoreUserContext : IUserContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public AspNetCoreUserContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    public IUser CurrentUser => new AspNetCoreUser(_httpContextAccessor.HttpContext?.User);
}

public class AspNetCoreUser : IUser
{
    private readonly ClaimsPrincipal _claimsPrincipal;
    
    public AspNetCoreUser(ClaimsPrincipal claimsPrincipal)
    {
        _claimsPrincipal = claimsPrincipal;
    }
    
    public string Id => _claimsPrincipal?.FindFirstValue(ClaimTypes.NameIdentifier);
    
    public string Name => _claimsPrincipal?.FindFirstValue(ClaimTypes.Name);
    
    public bool IsAuthenticated => _claimsPrincipal?.Identity?.IsAuthenticated ?? false;
    
    public bool IsInRole(string role)
    {
        return _claimsPrincipal?.IsInRole(role) ?? false;
    }
    
    public IEnumerable<string> Roles
    {
        get
        {
            return _claimsPrincipal?.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList() ?? new List<string>();
        }
    }
}
```

## Data Protection and Privacy

### Event Data Encryption

Encrypt sensitive event data:

```csharp
public class EncryptingEventSerializer : IEventSerializer
{
    private readonly IEventSerializer _innerSerializer;
    private readonly IEncryptionService _encryptionService;
    private readonly HashSet<string> _sensitiveEventTypes;
    
    public EncryptingEventSerializer(
        IEventSerializer innerSerializer,
        IEncryptionService encryptionService,
        IEnumerable<string> sensitiveEventTypes)
    {
        _innerSerializer = innerSerializer;
        _encryptionService = encryptionService;
        _sensitiveEventTypes = new HashSet<string>(sensitiveEventTypes);
    }
    
    public object Deserialize(RecordedEvent recordedEvent)
    {
        // Decrypt event data if necessary
        if (IsSensitiveEventType(recordedEvent.EventType))
        {
            var decryptedData = _encryptionService.Decrypt(recordedEvent.Data);
            var decryptedEvent = new RecordedEvent(
                recordedEvent.EventStreamId,
                recordedEvent.EventNumber,
                recordedEvent.EventId,
                recordedEvent.EventType,
                decryptedData,
                recordedEvent.Metadata,
                recordedEvent.IsJson,
                recordedEvent.Created);
                
            return _innerSerializer.Deserialize(decryptedEvent);
        }
        
        return _innerSerializer.Deserialize(recordedEvent);
    }
    
    public IEventData Serialize(object @event, Guid eventId)
    {
        var eventData = _innerSerializer.Serialize(@event, eventId);
        
        // Encrypt event data if necessary
        if (IsSensitiveEventType(@event.GetType().Name))
        {
            var encryptedData = _encryptionService.Encrypt(eventData.Data);
            return new EventData(
                eventData.EventId,
                eventData.Type,
                eventData.IsJson,
                encryptedData,
                eventData.Metadata);
        }
        
        return eventData;
    }
    
    private bool IsSensitiveEventType(string eventType)
    {
        return _sensitiveEventTypes.Contains(eventType);
    }
}
```

### Encryption Service

Implement an encryption service:

```csharp
public interface IEncryptionService
{
    byte[] Encrypt(byte[] data);
    byte[] Decrypt(byte[] encryptedData);
}

public class AesEncryptionService : IEncryptionService
{
    private readonly byte[] _key;
    private readonly byte[] _iv;
    
    public AesEncryptionService(string keyBase64, string ivBase64)
    {
        _key = Convert.FromBase64String(keyBase64);
        _iv = Convert.FromBase64String(ivBase64);
    }
    
    public byte[] Encrypt(byte[] data)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = _key;
            aes.IV = _iv;
            
            using (var encryptor = aes.CreateEncryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream())
            {
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                }
                
                return ms.ToArray();
            }
        }
    }
    
    public byte[] Decrypt(byte[] encryptedData)
    {
        using (var aes = Aes.Create())
        {
            aes.Key = _key;
            aes.IV = _iv;
            
            using (var decryptor = aes.CreateDecryptor(aes.Key, aes.IV))
            using (var ms = new MemoryStream(encryptedData))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var output = new MemoryStream())
            {
                cs.CopyTo(output);
                return output.ToArray();
            }
        }
    }
}
```

### Personally Identifiable Information (PII) Handling

Handle PII securely:

```csharp
public class PiiEvent
{
    public Guid SubjectId { get; }
    public PiiData PiiData { get; }
    
    public PiiEvent(Guid subjectId, PiiData piiData)
    {
        SubjectId = subjectId;
        PiiData = piiData;
    }
}

[Serializable]
public class PiiData
{
    public string Name { get; set; }
    public string Email { get; set; }
    public string Address { get; set; }
    public string PhoneNumber { get; set; }
    
    // Add other PII fields as needed
}

public class PiiRepository
{
    private readonly IEncryptionService _encryptionService;
    private readonly IDatabase _database;
    
    public PiiRepository(IEncryptionService encryptionService, IDatabase database)
    {
        _encryptionService = encryptionService;
        _database = database;
    }
    
    public void Store(Guid subjectId, PiiData piiData)
    {
        // Serialize PII data
        var serialized = JsonConvert.SerializeObject(piiData);
        var bytes = Encoding.UTF8.GetBytes(serialized);
        
        // Encrypt PII data
        var encrypted = _encryptionService.Encrypt(bytes);
        
        // Store encrypted data with subject ID
        _database.Store(subjectId.ToString(), Convert.ToBase64String(encrypted));
    }
    
    public PiiData Retrieve(Guid subjectId)
    {
        // Retrieve encrypted data
        var encryptedBase64 = _database.Retrieve(subjectId.ToString());
        
        if (string.IsNullOrEmpty(encryptedBase64))
            return null;
            
        // Decrypt PII data
        var encrypted = Convert.FromBase64String(encryptedBase64);
        var decrypted = _encryptionService.Decrypt(encrypted);
        var serialized = Encoding.UTF8.GetString(decrypted);
        
        // Deserialize PII data
        return JsonConvert.DeserializeObject<PiiData>(serialized);
    }
    
    public void Delete(Guid subjectId)
    {
        // Delete PII data
        _database.Delete(subjectId.ToString());
    }
}
```

### Data Masking

Implement data masking for logs:

```csharp
public class DataMaskingLogger : ILogger
{
    private readonly ILogger _innerLogger;
    private readonly IEnumerable<IDataMasker> _maskers;
    
    public DataMaskingLogger(ILogger innerLogger, IEnumerable<IDataMasker> maskers)
    {
        _innerLogger = innerLogger;
        _maskers = maskers;
    }
    
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        var message = formatter(state, exception);
        var maskedMessage = ApplyMasking(message);
        
        _innerLogger.Log(logLevel, eventId, state, exception, (s, e) => maskedMessage);
    }
    
    private string ApplyMasking(string message)
    {
        var result = message;
        
        foreach (var masker in _maskers)
        {
            result = masker.Mask(result);
        }
        
        return result;
    }
    
    public bool IsEnabled(LogLevel logLevel)
    {
        return _innerLogger.IsEnabled(logLevel);
    }
    
    public IDisposable BeginScope<TState>(TState state)
    {
        return _innerLogger.BeginScope(state);
    }
}

public interface IDataMasker
{
    string Mask(string input);
}

public class CreditCardMasker : IDataMasker
{
    private static readonly Regex CreditCardRegex = new Regex(@"\b(?:\d{4}[ -]?){3}\d{4}\b");
    
    public string Mask(string input)
    {
        return CreditCardRegex.Replace(input, match =>
        {
            var card = match.Value.Replace(" ", "").Replace("-", "");
            return $"{card.Substring(0, 4)}********{card.Substring(12)}";
        });
    }
}

public class EmailMasker : IDataMasker
{
    private static readonly Regex EmailRegex = new Regex(@"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b");
    
    public string Mask(string input)
    {
        return EmailRegex.Replace(input, match =>
        {
            var parts = match.Value.Split('@');
            var username = parts[0];
            var domain = parts[1];
            
            var maskedUsername = username.Length <= 2
                ? username
                : $"{username[0]}***{username[username.Length - 1]}";
                
            return $"{maskedUsername}@{domain}";
        });
    }
}
```

## Audit Logging and Compliance

### Command Audit Logging

Log all commands for audit purposes:

```csharp
public class AuditingCommandBus : ICommandBus
{
    private readonly ICommandBus _innerBus;
    private readonly IAuditLogger _auditLogger;
    private readonly IUserContext _userContext;
    
    public AuditingCommandBus(
        ICommandBus innerBus,
        IAuditLogger auditLogger,
        IUserContext userContext)
    {
        _innerBus = innerBus;
        _auditLogger = auditLogger;
        _userContext = userContext;
    }
    
    public void Send<TCommand>(TCommand command) where TCommand : class, ICommand
    {
        // Log the command for audit purposes
        _auditLogger.LogCommand(
            command.GetType().Name,
            JsonConvert.SerializeObject(command),
            _userContext.CurrentUser?.Id,
            DateTime.UtcNow);
        
        // Forward to inner bus
        _innerBus.Send(command);
    }
}
```

### Event Audit Logging

Log all events for audit purposes:

```csharp
public class AuditingEventStore : IEventStore
{
    private readonly IEventStore _innerEventStore;
    private readonly IAuditLogger _auditLogger;
    
    public AuditingEventStore(IEventStore innerEventStore, IAuditLogger auditLogger)
    {
        _innerEventStore = innerEventStore;
        _auditLogger = auditLogger;
    }
    
    public void AppendToStream(string streamName, long expectedVersion, IEnumerable<IEventData> events)
    {
        // Log events for audit purposes
        foreach (var @event in events)
        {
            _auditLogger.LogEvent(
                streamName,
                @event.Type,
                Convert.ToBase64String(@event.Data),
                DateTime.UtcNow);
        }
        
        // Forward to inner event store
        _innerEventStore.AppendToStream(streamName, expectedVersion, events);
    }
    
    // ... other methods ...
}
```

### Audit Logger Implementation

Implement an audit logger:

```csharp
public interface IAuditLogger
{
    void LogCommand(string commandType, string commandData, string userId, DateTime timestamp);
    void LogEvent(string streamName, string eventType, string eventData, DateTime timestamp);
    void LogQuery(string queryType, string queryData, string userId, DateTime timestamp);
}

public class DatabaseAuditLogger : IAuditLogger
{
    private readonly IDbConnection _connection;
    
    public DatabaseAuditLogger(IDbConnection connection)
    {
        _connection = connection;
    }
    
    public void LogCommand(string commandType, string commandData, string userId, DateTime timestamp)
    {
        const string sql = @"
            INSERT INTO AuditLog (Type, Action, Data, UserId, Timestamp)
            VALUES (@Type, @Action, @Data, @UserId, @Timestamp)";
            
        _connection.Execute(sql, new
        {
            Type = "Command",
            Action = commandType,
            Data = commandData,
            UserId = userId,
            Timestamp = timestamp
        });
    }
    
    public void LogEvent(string streamName, string eventType, string eventData, DateTime timestamp)
    {
        const string sql = @"
            INSERT INTO AuditLog (Type, Action, StreamName, Data, Timestamp)
            VALUES (@Type, @Action, @StreamName, @Data, @Timestamp)";
            
        _connection.Execute(sql, new
        {
            Type = "Event",
            Action = eventType,
            StreamName = streamName,
            Data = eventData,
            Timestamp = timestamp
        });
    }
    
    public void LogQuery(string queryType, string queryData, string userId, DateTime timestamp)
    {
        const string sql = @"
            INSERT INTO AuditLog (Type, Action, Data, UserId, Timestamp)
            VALUES (@Type, @Action, @Data, @UserId, @Timestamp)";
            
        _connection.Execute(sql, new
        {
            Type = "Query",
            Action = queryType,
            Data = queryData,
            UserId = userId,
            Timestamp = timestamp
        });
    }
}
```

### GDPR Compliance

Implement GDPR compliance features:

```csharp
public class GdprService
{
    private readonly IEventStore _eventStore;
    private readonly PiiRepository _piiRepository;
    
    public GdprService(IEventStore eventStore, PiiRepository piiRepository)
    {
        _eventStore = eventStore;
        _piiRepository = piiRepository;
    }
    
    public void DeletePersonalData(Guid subjectId)
    {
        // Delete PII data
        _piiRepository.Delete(subjectId);
        
        // Anonymize events
        AnonymizeEvents(subjectId);
    }
    
    private void AnonymizeEvents(Guid subjectId)
    {
        // Find all streams related to the subject
        var streamName = $"subject-{subjectId}";
        
        // Read all events from the stream
        var events = _eventStore.ReadStreamForward(streamName, 0, int.MaxValue);
        
        // Create anonymized events
        var anonymizedEvents = events.Select(e => AnonymizeEvent(e)).ToList();
        
        // Delete the original stream
        _eventStore.DeleteStream(streamName, events.Last().EventNumber);
        
        // Create a new anonymized stream
        var anonymizedStreamName = $"anonymized-{Guid.NewGuid()}";
        _eventStore.AppendToStream(anonymizedStreamName, ExpectedVersion.NoStream, anonymizedEvents);
    }
    
    private IEventData AnonymizeEvent(RecordedEvent @event)
    {
        // Deserialize the event
        var eventData = JsonConvert.DeserializeObject<dynamic>(Encoding.UTF8.GetString(@event.Data));
        
        // Anonymize personal data
        if (eventData.Name != null) eventData.Name = "REDACTED";
        if (eventData.Email != null) eventData.Email = "REDACTED";
        if (eventData.Address != null) eventData.Address = "REDACTED";
        if (eventData.PhoneNumber != null) eventData.PhoneNumber = "REDACTED";
        
        // Serialize the anonymized event
        var anonymizedData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventData));
        
        // Create a new event data
        return new EventData(
            @event.EventId,
            @event.EventType,
            @event.IsJson,
            anonymizedData,
            @event.Metadata);
    }
    
    public PiiData ExportPersonalData(Guid subjectId)
    {
        // Retrieve PII data
        return _piiRepository.Retrieve(subjectId);
    }
}
```

## Secure Deployment Practices

### Secure Configuration

Implement secure configuration management:

```csharp
public class SecureConfigurationProvider : IConfigurationProvider
{
    private readonly IConfiguration _configuration;
    private readonly IEncryptionService _encryptionService;
    
    public SecureConfigurationProvider(IConfiguration configuration, IEncryptionService encryptionService)
    {
        _configuration = configuration;
        _encryptionService = encryptionService;
    }
    
    public string GetConnectionString(string name)
    {
        var encryptedConnectionString = _configuration.GetConnectionString(name);
        
        if (string.IsNullOrEmpty(encryptedConnectionString))
            return null;
            
        // Decrypt connection string
        var encrypted = Convert.FromBase64String(encryptedConnectionString);
        var decrypted = _encryptionService.Decrypt(encrypted);
        
        return Encoding.UTF8.GetString(decrypted);
    }
    
    public T GetSection<T>(string sectionName) where T : class, new()
    {
        var section = _configuration.GetSection(sectionName);
        
        if (!section.Exists())
            return new T();
            
        // Bind section to object
        var result = new T();
        section.Bind(result);
        
        return result;
    }
}
```

### Secret Management

Implement secure secret management:

```csharp
public interface ISecretManager
{
    string GetSecret(string secretName);
    void SetSecret(string secretName, string secretValue);
}

public class AzureKeyVaultSecretManager : ISecretManager
{
    private readonly SecretClient _secretClient;
    
    public AzureKeyVaultSecretManager(string keyVaultUrl, TokenCredential credential)
    {
        _secretClient = new SecretClient(new Uri(keyVaultUrl), credential);
    }
    
    public string GetSecret(string secretName)
    {
        var response = _secretClient.GetSecret(secretName);
        return response.Value.Value;
    }
    
    public void SetSecret(string secretName, string secretValue)
    {
        _secretClient.SetSecret(secretName, secretValue);
    }
}
```

### Secure Communication

Implement secure communication:

```csharp
public class SecureStreamStoreConnection : IStreamStoreConnection
{
    private readonly IStreamStoreConnection _innerConnection;
    private readonly X509Certificate2 _clientCertificate;
    
    public SecureStreamStoreConnection(
        IStreamStoreConnection innerConnection,
        X509Certificate2 clientCertificate)
    {
        _innerConnection = innerConnection;
        _clientCertificate = clientCertificate;
    }
    
    public void Connect()
    {
        // Configure secure connection
        var settings = new ConnectionSettings();
        settings.UseSslConnection(_clientCertificate);
        
        // Connect with secure settings
        _innerConnection.Connect();
    }
    
    // ... other methods ...
}
```

## Threat Modeling

### Identify Assets

Identify valuable assets in your system:

1. **Event Data**: The events stored in the event store
2. **User Data**: Personal information of users
3. **Authentication Credentials**: User credentials and access tokens
4. **Business Logic**: The business rules implemented in aggregates
5. **Configuration**: Sensitive configuration settings

### Identify Threats

Identify potential threats to those assets:

1. **Unauthorized Access**: Attackers gaining access to sensitive data
2. **Data Tampering**: Modification of events or other data
3. **Information Disclosure**: Leakage of sensitive information
4. **Denial of Service**: Attacks that make the system unavailable
5. **Elevation of Privilege**: Gaining higher privileges than authorized

### Assess Risks

Assess the risks of each threat:

1. **Risk = Likelihood × Impact**
2. **Likelihood**: Probability of the threat occurring
3. **Impact**: Potential damage if the threat occurs
4. **Risk Level**: High, Medium, or Low

### Mitigate Risks

Implement controls to mitigate risks:

1. **Preventive Controls**: Prevent threats from occurring
2. **Detective Controls**: Detect when threats occur
3. **Corrective Controls**: Recover from threats that have occurred

## Security Testing Strategies

### Static Analysis

Use static analysis tools to identify security issues:

1. **Code Scanning**: Use tools like SonarQube or Microsoft Security Code Analysis
2. **Dependency Scanning**: Check for vulnerabilities in dependencies
3. **Secret Scanning**: Detect hardcoded secrets in code

### Dynamic Analysis

Use dynamic analysis to identify runtime security issues:

1. **Penetration Testing**: Simulate attacks to identify vulnerabilities
2. **Fuzzing**: Test with unexpected inputs to find weaknesses
3. **Runtime Monitoring**: Monitor for security events during execution

### Security Unit Tests

Write security-focused unit tests:

```csharp
[Fact]
public void CannotExecuteCommandWithoutAuthorization()
{
    // Arrange
    var user = new TestUser { Id = "user1", Roles = new[] { "User" } };
    var userContext = new TestUserContext(user);
    
    var authorizationService = new RoleBasedAuthorizationService();
    authorizationService.RegisterCommandRoles<CreateAccount>("Admin");
    
    var innerBus = new InMemoryCommandBus();
    var authorizedBus = new AuthorizedCommandBus(innerBus, authorizationService, userContext);
    
    var command = new CreateAccount(Guid.NewGuid(), "John Doe", 100);
    
    // Act & Assert
    Assert.Throws<UnauthorizedAccessException>(() => authorizedBus.Send(command));
}

[Fact]
public void CanExecuteCommandWithAuthorization()
{
    // Arrange
    var user = new TestUser { Id = "user1", Roles = new[] { "Admin" } };
    var userContext = new TestUserContext(user);
    
    var authorizationService = new RoleBasedAuthorizationService();
    authorizationService.RegisterCommandRoles<CreateAccount>("Admin");
    
    var innerBus = new InMemoryCommandBus();
    var authorizedBus = new AuthorizedCommandBus(innerBus, authorizationService, userContext);
    
    var command = new CreateAccount(Guid.NewGuid(), "John Doe", 100);
    
    // Act
    authorizedBus.Send(command);
    
    // Assert
    // No exception thrown
}
```

### Security Integration Tests

Write security-focused integration tests:

```csharp
[Fact]
public async Task CannotAccessProtectedApiWithoutAuthentication()
{
    // Arrange
    var client = _factory.CreateClient();
    
    // Act
    var response = await client.GetAsync("/api/accounts");
    
    // Assert
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task CannotAccessAdminApiWithUserRole()
{
    // Arrange
    var client = _factory.CreateClient();
    
    // Authenticate as user
    var token = await GetUserToken(client, "user@example.com", "password");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    
    // Act
    var response = await client.GetAsync("/api/admin/accounts");
    
    // Assert
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}

[Fact]
public async Task CanAccessAdminApiWithAdminRole()
{
    // Arrange
    var client = _factory.CreateClient();
    
    // Authenticate as admin
    var token = await GetAdminToken(client, "admin@example.com", "password");
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    
    // Act
    var response = await client.GetAsync("/api/admin/accounts");
    
    // Assert
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}
```

[↑ Back to Top](#security-guide) | [← Back to Table of Contents](README.md)
