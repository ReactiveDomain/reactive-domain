namespace ReactiveDomain
{
    public static class StreamNameConversions
    {
        public static StreamNameConverter WithSuffix(string suffix) =>
            name => name.WithSuffix(suffix);

        public static StreamNameConverter WithPrefix(string prefix) => 
            name => name.WithPrefix(prefix);

        public static StreamNameConverter WithoutSuffix(string suffix) =>
            name => name.WithoutSuffix(suffix);

        public static StreamNameConverter WithoutPrefix(string prefix) =>
            name => name.WithoutPrefix(prefix);

        public static StreamNameConverter PassThru => 
            name => name;
    }
}