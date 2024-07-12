

using System;
using System.Runtime.Serialization;

namespace ReactiveDomain.Transport.Framing
{
    public class PackageFramingException: Exception
    {
        public PackageFramingException()
        {
        }

        public PackageFramingException(string message) : base(message)
        {
        }

        public PackageFramingException(string message, Exception innerException) : base(message, innerException)
        {
        }

#if NET8_0_OR_GREATER
        [Obsolete(DiagnosticId = "SYSLIB0051")]
#endif
        protected PackageFramingException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
