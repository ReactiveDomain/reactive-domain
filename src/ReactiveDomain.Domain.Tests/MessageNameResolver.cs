using System;

namespace ReactiveDomain.Domain.Tests
{
    /// <summary>
    /// Resolves the canonical name of a message associated with a CLR type.
    /// </summary>
    /// <param name="type">The type to resolve the message name for.</param>
    /// <returns>The resolved message name.</returns>
    public delegate string MessageNameResolver(Type type);
}