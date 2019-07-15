namespace Shovel
{
    class Program
    {
        private static Bootstrap _bootstrap;

        static void Main(string[] args)
        {
            _bootstrap = new Bootstrap();

            _bootstrap.Load();

            if (_bootstrap.Loaded)
            {
                _bootstrap.Run();
            }
        }
    }
}
