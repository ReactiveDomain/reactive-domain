// ReSharper disable TypeParameterCanBeVariant
namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// Used to apply events of type T to state.
    /// </summary>
    public interface IApply<T> where T : Event
    {
        void Apply(T @event);
    }
}
