using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    /// <summary>
    /// Represents the direction of read operation (both from $all and usual streams)
    /// </summary>
    public enum ReadDirection
    {
        Forward,
        Backward,
    }
}
