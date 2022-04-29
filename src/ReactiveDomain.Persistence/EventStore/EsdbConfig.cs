namespace ReactiveDomain.EventStore
{
    public class EsdbConfig
    {
        public string Path { get; set; }
        public string WorkingDir { get; set; }
        public string Args { get; set; }
        public string ConnectionString { get; set; }
        public string Schema { get; set; }
    }
}
