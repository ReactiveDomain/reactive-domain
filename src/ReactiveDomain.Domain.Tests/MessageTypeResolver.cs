using System;

namespace ReactiveDomain
{
    /// <summary>
    /// Resolves the CLR type that is associated with the canonical name of a message.
    /// </summary>
    /// <param name="name">The message name to resolve a type for.</param>
    /// <returns>The resolved CLR type.</returns>
    public delegate Type MessageTypeResolver(string name);
}