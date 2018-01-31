using System.Collections.Generic;

namespace ReactiveDomain.Domain.Tests
{
    public class CapturingManyRoute
    {
        private readonly List<object> _capturing;

        public CapturingManyRoute()
        {
            _capturing = new List<object>();
        }

        public void Capture<TEvent>(TEvent result)
        {
            _capturing.Add(result);
        }

        public object[] Captured => _capturing.ToArray();
    }
}