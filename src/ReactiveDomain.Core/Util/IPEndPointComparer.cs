using System.Net;

namespace ReactiveDomain.Util;

public class IPEndPointComparer : IComparer<IPEndPoint> {
	public int Compare(IPEndPoint? x, IPEndPoint? y) {
		if (x is null && y is null)
			return 0;
		if (x is null)
			return -1;
		if (y is null)
			return 1;
		var xx = x.Address.ToString();
		var yy = y.Address.ToString();
		var result = string.CompareOrdinal(xx, yy);
		return result == 0 ? x.Port.CompareTo(y.Port) : result;
	}
}
