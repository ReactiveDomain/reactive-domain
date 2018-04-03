using System.IO;
using PublicApiGenerator;
using Xunit;

namespace ReactiveDomain.Domain.Tests
{
    public class AssemblyEvolutionReporter
    {
        //todo fix this
        //near as I can tell setting the package path on nuget to local broke this
        //the relative path will always got to the local  folder now, not respecting the 
        //actual path entered
        [Fact(Skip = "Microsoft Sucks" )]
        public void WriteLatestVersion()
        {
            var assembly = typeof(EventDrivenStateMachine).Assembly;
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