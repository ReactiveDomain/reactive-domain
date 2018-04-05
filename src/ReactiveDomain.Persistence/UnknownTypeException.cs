using System;

namespace ReactiveDomain {
    public class UnknownTypeException : Exception
    {
        public UnknownTypeException(string typeName):base($"TypeName'{typeName}' was not found in the currently loaded appdomains.")
        {}
    }
}