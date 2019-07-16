
namespace ReactiveDomain.Messaging.Bus
{
    public static class HandleExtensions
    {
        public static IHandle<TInput> WidenFrom<TInput, TOutput>(this IHandle<TOutput> handler)
            where TOutput : IMessage
            where TInput : TOutput
        {
            return new WideningHandler<TInput, TOutput>(handler);
        }

        public static IHandle<TInput> NarrowTo<TInput, TOutput>(this IHandle<TOutput> handler)
            where TInput : IMessage
            where TOutput : TInput
        {
            return new NarrowingHandler<TInput, TOutput>(handler);
        }
    }
}