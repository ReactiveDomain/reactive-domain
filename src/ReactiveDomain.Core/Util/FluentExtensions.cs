using System;

namespace ReactiveDomain.Util
{
    public static class FluentExtensions {

        public static T If<T>(this T t, Func<bool> cond, Func<T, T> builder) where T : class {
            return cond() ? builder(t) : t;
        }
    }
}
