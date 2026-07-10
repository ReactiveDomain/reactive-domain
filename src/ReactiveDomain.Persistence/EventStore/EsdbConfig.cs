namespace ReactiveDomain.EventStore;

public class EsdbConfig(string path, string workingDir, string args, string connectionString, string schema) {
	public string Path { get; set; } = path;
	public string WorkingDir { get; set; } = workingDir;
	public string Args { get; set; } = args;
	public string ConnectionString { get; set; } = connectionString;
	public string Schema { get; set; } = schema;
}
