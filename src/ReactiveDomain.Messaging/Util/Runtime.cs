using System;

namespace ReactiveDomain.Messaging.Util
{
    public static class Runtime
    {
        public static readonly bool IsMono = Type.GetType("Mono.Runtime") != null;
    }
}