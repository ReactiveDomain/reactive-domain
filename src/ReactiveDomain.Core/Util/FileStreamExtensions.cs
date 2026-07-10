// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable AssignNullToNotNullAttribute

using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Util;

public static class FileStreamExtensions {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");
	private static readonly Action<FileStream> _flushSafe;

	[DllImport("kernel32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	public static extern bool FlushFileBuffers(SafeFileHandle hFile);

	//[DllImport("kernel32.dll", SetLastError = true)]
	//[return: MarshalAs(UnmanagedType.Bool)]
	//static extern bool FlushViewOfFile(IntPtr lpBaseAddress, UIntPtr dwNumberOfBytesToFlush);

	static FileStreamExtensions() {
		try {
			var arg = Expression.Parameter(typeof(FileStream), "f");
			Expression expr = Expression.Field(arg, typeof(FileStream).GetField("_handle", BindingFlags.Instance | BindingFlags.NonPublic)!);
			var getFileHandle = Expression.Lambda<Func<FileStream, SafeFileHandle>>(expr, arg).Compile();
			_flushSafe = f => {
				f.Flush(flushToDisk: false);
				if (!FlushFileBuffers(getFileHandle(f)))
					throw new Exception($"FlushFileBuffers failed with err: {Marshal.GetLastWin32Error()}");
			};
		} catch (Exception exc) {
			_log.ErrorException(exc, "Error while compiling sneaky SafeFileHandle getter.");
			_flushSafe = f => f.Flush(flushToDisk: true);
		}
	}

	public static void FlushToDisk(this FileStream fs) {
		_flushSafe(fs);
	}
}
