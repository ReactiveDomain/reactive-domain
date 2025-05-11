# ReactiveDomain.IdentityStorage Component

[← Back to Components](README.md)

The ReactiveDomain.IdentityStorage component provides infrastructure for storing and managing identities in a Reactive Domain application. It helps with user authentication, authorization, and identity management using event sourcing principles.

## Key Features

- Identity storage and retrieval
- User authentication and authorization
- Role-based access control
- Claims-based identity
- Identity versioning and history
- Integration with external identity providers

## Core Types

### Identity Management

- **Identity**: Represents a user identity
- **IdentityAggregate**: Aggregate root for identity entities
- **IdentityRepository**: Repository for identity aggregates
- **IdentityService**: Service for identity operations
- **IdentityManager**: Manager for identity lifecycle

### Authentication

- **Authenticator**: Handles authentication requests
- **PasswordHasher**: Hashes and verifies passwords
- **AuthenticationResult**: Result of authentication attempts
- **AuthenticationToken**: Token for authenticated sessions
- **TokenValidator**: Validates authentication tokens

### Authorization

- **Role**: Represents a security role
- **Permission**: Represents a security permission
- **RoleRepository**: Repository for role entities
- **PermissionRepository**: Repository for permission entities
- **AuthorizationService**: Service for authorization operations

## Usage Examples

### Creating a New Identity

```csharp
public class CreateIdentityHandler : ICommandHandler<CreateIdentity>
{
    private readonly ICorrelatedRepository _repository;
    private readonly PasswordHasher _passwordHasher;
    
    public CreateIdentityHandler(
        ICorrelatedRepository repository,
        PasswordHasher passwordHasher)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
    }
    
    public void Handle(CreateIdentity command)
    {
        // Check if identity already exists
        if (_repository.TryGetById<IdentityAggregate>(command.IdentityId, out _, command))
        {
            throw new IdentityAlreadyExistsException($"Identity with ID {command.IdentityId} already exists");
        }
        
        // Create a new identity
        var identity = new IdentityAggregate(command.IdentityId);
        
        // Hash the password
        var hashedPassword = _passwordHasher.HashPassword(command.Password);
        
        // Initialize the identity
        identity.Initialize(
            command.Username,
            command.Email,
            hashedPassword,
            command.DisplayName,
            command);
        
        // Assign initial roles
        foreach (var role in command.Roles)
        {
            identity.AssignRole(role, command);
        }
        
        // Save the identity
        _repository.Save(identity);
    }
}
```

### Authenticating a User

```csharp
public class AuthenticateUserHandler : ICommandHandler<AuthenticateUser>
{
    private readonly ICorrelatedRepository _repository;
    private readonly PasswordHasher _passwordHasher;
    private readonly TokenGenerator _tokenGenerator;
    private readonly IEventBus _eventBus;
    
    public AuthenticateUserHandler(
        ICorrelatedRepository repository,
        PasswordHasher passwordHasher,
        TokenGenerator tokenGenerator,
        IEventBus eventBus)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
        _eventBus = eventBus;
    }
    
    public void Handle(AuthenticateUser command)
    {
        // Find the identity by username
        var identity = _repository.FindByUsername<IdentityAggregate>(command.Username, command);
        
        if (identity == null)
        {
            // Publish authentication failed event
            _eventBus.Publish(MessageBuilder.From(command, () => new AuthenticationFailed(
                command.Username,
                "Identity not found",
                DateTime.UtcNow)));
                
            throw new AuthenticationException("Invalid username or password");
        }
        
        // Verify the password
        if (!_passwordHasher.VerifyPassword(command.Password, identity.HashedPassword))
        {
            // Record failed authentication attempt
            identity.RecordFailedAuthenticationAttempt(command);
            _repository.Save(identity);
            
            // Publish authentication failed event
            _eventBus.Publish(MessageBuilder.From(command, () => new AuthenticationFailed(
                command.Username,
                "Invalid password",
                DateTime.UtcNow)));
                
            throw new AuthenticationException("Invalid username or password");
        }
        
        // Generate authentication token
        var token = _tokenGenerator.GenerateToken(
            identity.Id,
            identity.Username,
            identity.Roles,
            identity.Claims);
            
        // Record successful authentication
        identity.RecordSuccessfulAuthentication(command);
        _repository.Save(identity);
        
        // Publish authentication succeeded event
        _eventBus.Publish(MessageBuilder.From(command, () => new AuthenticationSucceeded(
            identity.Id,
            identity.Username,
            token,
            DateTime.UtcNow)));
    }
}
```

### Checking Authorization

```csharp
public class AuthorizationService : IAuthorizationService
{
    private readonly ICorrelatedRepository _repository;
    private readonly IRoleRepository _roleRepository;
    
    public AuthorizationService(
        ICorrelatedRepository repository,
        IRoleRepository roleRepository)
    {
        _repository = repository;
        _roleRepository = roleRepository;
    }
    
    public bool IsAuthorized(Guid identityId, string permission, ICorrelatedMessage source)
    {
        // Get the identity
        if (!_repository.TryGetById<IdentityAggregate>(identityId, out var identity, source))
        {
            return false;
        }
        
        // Check if identity has the permission directly
        if (identity.HasPermission(permission))
        {
            return true;
        }
        
        // Check if any of the identity's roles has the permission
        foreach (var roleName in identity.Roles)
        {
            var role = _roleRepository.GetByName(roleName);
            
            if (role != null && role.HasPermission(permission))
            {
                return true;
            }
        }
        
        return false;
    }
}
```

## Integration with Other Components

The IdentityStorage component integrates with:

- **ReactiveDomain.Core**: Uses core interfaces and types
- **ReactiveDomain.Foundation**: Provides identity infrastructure for domain components
- **ReactiveDomain.Messaging**: Integrates with command and event handling
- **ReactiveDomain.Persistence**: Uses event sourcing for identity storage

## Configuration Options

### Identity Options

- **PasswordRequirements**: Requirements for password complexity
- **LockoutOptions**: Options for account lockout
- **TokenOptions**: Options for authentication tokens
- **UserValidationOptions**: Options for user validation

### Authentication Options

- **TokenExpirationTime**: Expiration time for authentication tokens
- **RefreshTokenExpirationTime**: Expiration time for refresh tokens
- **AllowMultipleSessions**: Whether to allow multiple sessions for a user
- **RequireTwoFactorAuth**: Whether to require two-factor authentication

### Authorization Options

- **DefaultPolicy**: Default authorization policy
- **FallbackPolicy**: Fallback authorization policy
- **RequireAuthenticatedUser**: Whether to require authenticated users by default
- **AddDefaultRoles**: Whether to add default roles

## Best Practices

1. **Secure Password Storage**: Always hash passwords using a strong algorithm
2. **Implement Account Lockout**: Lock accounts after multiple failed authentication attempts
3. **Use Short-Lived Tokens**: Use short-lived authentication tokens with refresh tokens
4. **Implement Role-Based Access Control**: Use roles for coarse-grained access control
5. **Implement Claims-Based Authorization**: Use claims for fine-grained access control
6. **Audit Identity Operations**: Keep an audit trail of identity operations
7. **Validate User Input**: Validate all user input to prevent injection attacks
8. **Use HTTPS**: Always use HTTPS for identity operations
9. **Implement Two-Factor Authentication**: Add an extra layer of security with two-factor authentication
10. **Follow Security Best Practices**: Stay up-to-date with security best practices

## Common Identity Patterns

### User Registration and Confirmation

```csharp
public class RegisterUserHandler : ICommandHandler<RegisterUser>
{
    private readonly ICorrelatedRepository _repository;
    private readonly PasswordHasher _passwordHasher;
    private readonly EmailService _emailService;
    
    public RegisterUserHandler(
        ICorrelatedRepository repository,
        PasswordHasher passwordHasher,
        EmailService emailService)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }
    
    public void Handle(RegisterUser command)
    {
        // Create a new identity
        var identity = new IdentityAggregate(Guid.NewGuid());
        
        // Hash the password
        var hashedPassword = _passwordHasher.HashPassword(command.Password);
        
        // Generate confirmation token
        var confirmationToken = Guid.NewGuid().ToString("N");
        
        // Initialize the identity
        identity.Initialize(
            command.Username,
            command.Email,
            hashedPassword,
            command.DisplayName,
            command);
            
        // Set confirmation token
        identity.SetConfirmationToken(confirmationToken, command);
        
        // Save the identity
        _repository.Save(identity);
        
        // Send confirmation email
        _emailService.SendConfirmationEmail(
            command.Email,
            command.Username,
            confirmationToken);
    }
}
```

### Password Reset

```csharp
public class RequestPasswordResetHandler : ICommandHandler<RequestPasswordReset>
{
    private readonly ICorrelatedRepository _repository;
    private readonly EmailService _emailService;
    
    public RequestPasswordResetHandler(
        ICorrelatedRepository repository,
        EmailService emailService)
    {
        _repository = repository;
        _emailService = emailService;
    }
    
    public void Handle(RequestPasswordReset command)
    {
        // Find the identity by email
        var identity = _repository.FindByEmail<IdentityAggregate>(command.Email, command);
        
        if (identity == null)
        {
            // Don't reveal that the email doesn't exist
            return;
        }
        
        // Generate reset token
        var resetToken = Guid.NewGuid().ToString("N");
        var resetTokenExpiration = DateTime.UtcNow.AddHours(24);
        
        // Set reset token
        identity.SetPasswordResetToken(resetToken, resetTokenExpiration, command);
        
        // Save the identity
        _repository.Save(identity);
        
        // Send password reset email
        _emailService.SendPasswordResetEmail(
            identity.Email,
            identity.Username,
            resetToken);
    }
}
```

## Related Documentation

- [Command API Reference](../api-reference/types/command.md)
- [ICommandHandler API Reference](../api-reference/types/icommand-handler.md)
- [AggregateRoot API Reference](../api-reference/types/aggregate-root.md)
- [IRepository API Reference](../api-reference/types/irepository.md)
- [ICorrelatedRepository API Reference](../api-reference/types/icorrelated-repository.md)

## Navigation

**Section Navigation**:
- [← Previous: ReactiveDomain.Policy](policy.md)
- [↑ Parent: Component Documentation](README.md)
- [→ Next: ReactiveDomain.Tools](tools.md)

**Quick Links**:
- [Home](../README.md)
- [Core Concepts](../core-concepts.md)
- [API Reference](../api-reference/README.md)
- [Code Examples](../code-examples/README.md)
- [Troubleshooting](../troubleshooting.md)

---

*This documentation is part of the [Reactive Domain](https://github.com/ReactiveDomain/reactive-domain) project.*
