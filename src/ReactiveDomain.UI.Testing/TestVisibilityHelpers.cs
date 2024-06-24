using System;
using System.Reflection;

namespace ReactiveDomain.UI.Testing
{
    public static class TestVisibilityExtensions
    {
        /// <summary>
        /// Allows test to access the current value of a the ReadModelProperty.
        /// This leverages the fact that the current value is stored to be published to new subscribers.
        /// </summary>
        /// <typeparam name="T">the ReadModelProperty type</typeparam>
        /// <param name="readModelProperty">the ReadModelProperty</param>
        /// <returns></returns>
        public static T CurrentValue<T>(this IObservable<T> readModelProperty)
        {
            if (!(readModelProperty is ReadModelProperty<T>)) { new InvalidOperationException($"Observable is not a ReadModelProperty."); }
            return GetInstanceField<T>(readModelProperty, "_lastValue");
        }
        private static T GetInstanceField<T>(object instance, string fieldName)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            FieldInfo field = instance.GetType().GetField(fieldName, bindFlags);
            T value = (T)field?.GetValue(instance);
            if (value == null) { throw new InvalidOperationException($"{fieldName} not found on {typeof(T).Name}."); }
            return value;
        }
    }
}
