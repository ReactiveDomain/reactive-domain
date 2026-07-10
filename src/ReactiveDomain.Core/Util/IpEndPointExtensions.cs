using System.Net;

namespace ReactiveDomain.Util;

public static class IpEndPointExtensions {
	extension(IPEndPoint endPoint) {
		public string ToHttpUrl(string? rawUrl = null) =>
			$"http://{endPoint.Address}:{endPoint.Port}/{(rawUrl != null ? rawUrl.TrimStart('/') : string.Empty)}";

		public string ToHttpUrl(string formatString, params object[] args) =>
			$"http://{endPoint.Address}:{endPoint.Port}/{string.Format(formatString.TrimStart('/'), args)}";
	}
}
