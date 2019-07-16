using System;

namespace Shovel
{
    class Program
    {
        private static Bootstrap _bootstrap;

        static void Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "help")
            {
                Console.WriteLine("Use: Shovel {stream=[stream name]} {eventtype=[event type]}");
                Console.WriteLine("if stream name or event type ends with '*' symbol it will be used as wildcard");
                return;
            }

            _bootstrap = new Bootstrap();

            _bootstrap.Load();

            if (_bootstrap.Loaded)
            {
                if (args.Length > 0)
                {
                    _bootstrap.PrepareFilters(args);
                }

                _bootstrap.Run();
            }
        }
    }
}
