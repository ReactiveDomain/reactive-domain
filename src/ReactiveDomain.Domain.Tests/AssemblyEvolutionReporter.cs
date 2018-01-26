using Xunit;
using PublicApiGenerator;
using System.IO;
using System.Diagnostics;

namespace ReactiveDomain
{
    public class AssemblyEvolutionReporter
    {
        [Fact]
        public void WriteLatestVersion()
        {
            var assembly = typeof(AggregateRootEntity).Assembly;
            var report = ApiGenerator.GeneratePublicApi(assembly);
            var path = 
                ".." + Path.DirectorySeparatorChar + 
                ".." + Path.DirectorySeparatorChar + 
                ".." + Path.DirectorySeparatorChar + 
                ".." + Path.DirectorySeparatorChar + 
                "Versions" + Path.DirectorySeparatorChar +
                "latest.txt";
            File.WriteAllText(path, report);
        }
    }
}