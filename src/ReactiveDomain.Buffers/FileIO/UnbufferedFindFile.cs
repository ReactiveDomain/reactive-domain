using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ReactiveDomain.Util;

namespace ReactiveDomain.Buffers.FileIO
{
    /// <summary>
    /// Provides an efficient file/directory enumeration 
    /// </summary>
    public class UnbufferedFindFile
    {
        #region Static data and helpers
        private static readonly char[] InvalidFilePathChars;
        private static readonly char[] InvalidFilePatternChars;
        static UnbufferedFindFile()
        {
            InvalidFilePathChars = Path.GetInvalidPathChars();
            var set = new List<char>(Path.GetInvalidFileNameChars());
            set.Remove('*');
            set.Remove('?');
            InvalidFilePatternChars = set.ToArray();
        }

        public static List<FileInfo> GetFilesIn(DirectoryInfo directory, string searchPattern = Star)
        {
            var fileList = new List<FileInfo>();
            FilesIn(directory.FullName,
                (fArg) =>{
                            fileList.Add( new FileInfo(Path.Combine(directory.FullName, fArg.Name)));
                         },
                searchPattern);
            return fileList;
        }

        public static void FilesIn(string directory, Action<FileFoundEventArgs> fileFoundAction, string searchPattern = Star)
        {
            Ensure.NotNull(fileFoundAction, "fileFoundAction");
            if (string.IsNullOrWhiteSpace(searchPattern))
                searchPattern = Star;
            var ff = new UnbufferedFindFile(directory, searchPattern)
            {
                _fileFound = fileFoundAction
            };

            ff.Find();
        }
       
        #endregion
        #region Kernel32
        internal static class Kernel32
        {
            internal const int MAX_PATH = 260;
            internal const int MAX_ALTERNATE = 14;
            internal const int ERROR_FILE_NOT_FOUND = 2;
            internal const int ERROR_PATH_NOT_FOUND = 3;
            internal const int ERROR_ACCESS_DENIED = 5;
            [StructLayout(LayoutKind.Sequential)]
            public struct FileTime
            {
                public uint dwLowDateTime;
                public uint dwHighDateTime;
                public DateTime ToDateTimeUtc()
                {
                    return DateTime.FromFileTimeUtc(dwLowDateTime | ((long)dwHighDateTime << 32));
                }
            };
            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            public struct WIN32_FIND_DATA
            {
                public FileAttributes dwFileAttributes;
                public FileTime ftCreationTime;
                public FileTime ftLastAccessTime;
                public FileTime ftLastWriteTime;
                public uint nFileSizeHigh; //changed all to uint from int, otherwise you run into unexpected overflow
                public uint nFileSizeLow; //| http://www.pinvoke.net/default.aspx/Structures/WIN32_FIND_DATA.html
                private readonly uint dwReserved0; //|
                private readonly uint dwReserved1; //v
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_PATH)]
                public char[] cFileName;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = MAX_ALTERNATE)]
                private readonly char[] cAlternateFileName;
                public bool IgnoredByName => (cFileName[0] == Zero) ||
                                             (cFileName[0] == '.' && cFileName[1] == Zero) ||
                                             (cFileName[0] == '.' && cFileName[1] == '.' && cFileName[2] == Zero);
            }
            public enum FindexInfoLevels
            {
                FindExInfoStandard = 0,
                FindExInfoBasic = 1
            }
            public enum FindexSearchOps
            {
                FindExSearchNameMatch = 0,
                FindExSearchLimitToDirectories = 1,
                FindExSearchLimitToDevices = 2
            }
            [Flags]
            public enum FindexAdditionalFlags
            {
                FindFirstExCaseSensitive = 1,
                FindFirstExLargeFetch = 2,
            }
            [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
            public static extern IntPtr FindFirstFileEx(
                IntPtr lpFileName,
                FindexInfoLevels fInfoLevelId,
                out WIN32_FIND_DATA lpFindFileData,
                FindexSearchOps fSearchOp,
                IntPtr lpSearchFilter,
                FindexAdditionalFlags dwAdditionalFlags);
            [DllImport("kernel32", CharSet = CharSet.Unicode)]
            public static extern bool FindNextFile(IntPtr hFindFile, out WIN32_FIND_DATA lpFindFileData);
            [DllImport("kernel32.dll")]
            public static extern bool FindClose(IntPtr hFindFile);
        }
        #endregion
        #region FileFoundEventArgs
        /// <summary> Provides a simple struct to capture file info, given by <see cref="FileFoundEventArgs.GetInfo()"/> method </summary>
        public struct Info
        {
            /// <summary> Returns the parent folder's full path </summary>
            public string ParentPath => Path.GetDirectoryName(FullPath);
            /// <summary> Gets or sets the full path of the file or folder </summary>
            public string FullPath { get; set; }
            /// <summary> Returns the file or folder name (with extension) </summary>
            public string Name => Path.GetFileName(FullPath);
            /// <summary> Returns the extension or String.Empty </summary>
            public string Extension => Path.GetExtension(FullPath);
            /// <summary> Returns the UNC path to the parent folder </summary>
            public string ParentPathUnc => (FullPath.StartsWith(@"\\")) ? ParentPath : (UncPrefix + ParentPath);
            /// <summary> Returns the UNC path to the file or folder </summary>
            public string FullPathUnc => (FullPath.StartsWith(@"\\")) ? FullPath : (UncPrefix + FullPath);
            /// <summary> Gets or sets the length in bytes </summary>
            public long Length { get; set; }
            /// <summary> Gets or sets the file or folder attributes </summary>
            public FileAttributes Attributes { get; set; }
            /// <summary> Gets or sets the file or folder CreationTime in Utc </summary>
            public DateTime CreationTimeUtc { get; set; }
            /// <summary> Gets or sets the file or folder LastAccessTime in Utc </summary>
            public DateTime LastAccessTimeUtc { get; set; }
            /// <summary> Gets or sets the file or folder LastWriteTime in Utc </summary>
            public DateTime LastWriteTimeUtc { get; set; }
        }
        internal class Win32FindData
        {
            public char[] Buffer;
            public IntPtr BufferAddress;
            public Kernel32.WIN32_FIND_DATA Value;
        }
        /// <summary>
        /// Provides access to the file or folder information during enumeration, DO NOT keep a reference to this
        /// class as it's meaning will change during enumeration.
        /// </summary>
        public sealed class FileFoundEventArgs : EventArgs
        {
            private readonly Win32FindData _ff;
            private int _uncPrefixLength;
            private int _parentNameLength;
            private int _itemNameLength;

            internal FileFoundEventArgs(Win32FindData ff)
            {
                _ff = ff;
            }
            internal void SetNameOffsets(int uncPrefixLength, int parentIx, int itemIx)
            {
                _parentNameLength = parentIx;
                _itemNameLength = itemIx;
                _uncPrefixLength = uncPrefixLength;
            }
            /// <summary> Returns the parent folder's full path </summary>
            public string ParentPath => new string(_ff.Buffer, _uncPrefixLength, _parentNameLength - _uncPrefixLength);
            /// <summary> Returns the UNC path to the parent folder </summary>
            public string ParentPathUnc => new string(_ff.Buffer, 0, _parentNameLength);
            /// <summary> Gets the full path of the file or folder </summary>
            public string FullPath => new string(_ff.Buffer, _uncPrefixLength, _itemNameLength - _uncPrefixLength);
            /// <summary> Returns the UNC path to the file or folder </summary>
            public string FullPathUnc => new string(_ff.Buffer, 0, _itemNameLength);
            /// <summary> Returns the file or folder name (with extension) </summary>
            public string Name => new string(_ff.Buffer, _parentNameLength, _itemNameLength - _parentNameLength);
            /// <summary> Returns the extension or String.Empty </summary>
            public string Extension
            {
                get
                {
                    for (int ix = _itemNameLength; ix > _parentNameLength; ix--)
                        if (_ff.Buffer[ix] == '.')
                            return new String(_ff.Buffer, ix, _itemNameLength - ix);
                    return String.Empty;
                }
            }
            /// <summary> Gets the length in bytes </summary>
            public long Length => _ff.Value.nFileSizeLow | ((long)_ff.Value.nFileSizeHigh << 32);
            /// <summary> Gets the file or folder attributes </summary>
            public FileAttributes Attributes => _ff.Value.dwFileAttributes;
            /// <summary> Gets the file or folder CreationTime in Utc </summary>
            public DateTime CreationTimeUtc => _ff.Value.ftCreationTime.ToDateTimeUtc();
            /// <summary> Gets the file or folder LastAccessTime in Utc </summary>
            public DateTime LastAccessTimeUtc => _ff.Value.ftLastAccessTime.ToDateTimeUtc();
            /// <summary> Gets the file or folder LastWriteTime in Utc </summary>
            public DateTime LastWriteTimeUtc => _ff.Value.ftLastWriteTime.ToDateTimeUtc();
            /// <summary> Returns true if the file or folder is ReadOnly </summary>
            public bool IsReadOnly => (Attributes & FileAttributes.ReadOnly) != 0;
            /// <summary> Returns true if the file or folder is Hidden </summary>
            public bool IsHidden => (Attributes & FileAttributes.Hidden) != 0;
            /// <summary> Returns true if the file or folder is System </summary>
            public bool IsSystem => (Attributes & FileAttributes.System) != 0;
            /// <summary> Returns true if the file or folder is Directory </summary>
            public bool IsDirectory => (Attributes & FileAttributes.Directory) != 0;
            /// <summary> Returns true if the file or folder is ReparsePoint </summary>
            public bool IsReparsePoint => (Attributes & FileAttributes.ReparsePoint) != 0;
            /// <summary> Returns true if the file or folder is Compressed </summary>
            public bool IsCompressed => (Attributes & FileAttributes.Compressed) != 0;
            /// <summary> Returns true if the file or folder is Off-line </summary>
            public bool IsOffline => (Attributes & FileAttributes.Offline) != 0;
            /// <summary> Returns true if the file or folder is Encrypted </summary>
            public bool IsEncrypted => (Attributes & FileAttributes.Encrypted) != 0;
            /// <summary>
            /// Captures the current state as a <see cref="UnbufferedFindFile.Info"/> structure.
            /// </summary>
            public Info GetInfo()
            {
                return new Info
                {
                    FullPath = FullPath,
                    Length = Length,
                    Attributes = Attributes,
                    CreationTimeUtc = CreationTimeUtc,
                    LastAccessTimeUtc = LastAccessTimeUtc,
                    LastWriteTimeUtc = LastWriteTimeUtc,
                };
            }
            /// <summary> Gets or sets the Cancel flag to abort the current enumeration </summary>
            public bool CancelEnumeration { get; set; }
        }
        #endregion
        private const string Star = "*";
        private const char Slash = '\\';
        private const char Zero = '\0';
        /// <summary> Returns the Unc path prefix used </summary>
        public const string UncPrefix = @"\\?\";
        private readonly Win32FindData _ff;
        private char[] _fpattern;
        private int _baseOffset;
        private bool _isUncPath;

        /// <summary> Creates a FindFile instance. </summary>
        private UnbufferedFindFile(string rootDirectory, string filePattern = Star)
        {
            if (string.IsNullOrEmpty(rootDirectory) || string.IsNullOrEmpty(filePattern))
                throw new ArgumentException();
            _ff = new Win32FindData
            {
                BufferAddress = IntPtr.Zero,
                Buffer = new char[0x100000],
                Value = new Kernel32.WIN32_FIND_DATA()
            };
            BaseDirectory = rootDirectory;

            if (filePattern.IndexOfAny(InvalidFilePatternChars) >= 0)
                throw new InvalidOperationException("Invalid characters in pattern.");
            _fpattern = filePattern.TrimStart(Slash).ToCharArray();
        }

        private Action<FileFoundEventArgs> _fileFound;
        private int UncPrefixLength => _isUncPath ? 4 : 0;
        private string BaseDirectory
        {
            get { return new string(_ff.Buffer, UncPrefixLength, _baseOffset - UncPrefixLength); }
            set
            {
                if (value.IndexOfAny(InvalidFilePathChars) > 0)
                    throw new InvalidOperationException("Invalid characters in path.");
                if (!value.StartsWith(@"\\"))
                    value = UncPrefix + value;
                if (!value.EndsWith(@"\"))
                    value += @"\";
                _isUncPath = value.StartsWith(UncPrefix);
                value.CopyTo(0, _ff.Buffer, 0, _baseOffset = value.Length);
            }
        }
       
        private void Find()
        {
            Ensure.NotNull(_fileFound, "FileFoundEventHandler");
            var hdl = GCHandle.Alloc(_ff.Buffer, GCHandleType.Pinned);
            try
            {
                var args = new FileFoundEventArgs(_ff);
                _ff.BufferAddress = hdl.AddrOfPinnedObject();
                FindFileEx(args, _baseOffset);
            }
            finally
            {
                _ff.BufferAddress = IntPtr.Zero;
                hdl.Free();
            }
        }
        private void FindFileEx(FileFoundEventArgs args, int slength)
        {
            _fpattern.CopyTo(_ff.Buffer, slength);
            _ff.Buffer[slength + _fpattern.Length] = Zero;
            var hFile = Kernel32.FindFirstFileEx(
                _ff.BufferAddress,
                Kernel32.FindexInfoLevels.FindExInfoBasic,
                out _ff.Value,
                Kernel32.FindexSearchOps.FindExSearchNameMatch,
                IntPtr.Zero,
                Kernel32.FindexAdditionalFlags.FindFirstExLargeFetch);
            if ((IntPtr.Size == 4 && hFile.ToInt32() == -1) ||
                (IntPtr.Size == 8 && hFile.ToInt64() == -1L))
            {
                Win32Error(Marshal.GetLastWin32Error());
                return;
            }

            try
            {
                do
                {
                    var sposition = slength;
                    for (int ix = 0; ix < Kernel32.MAX_PATH && sposition < _ff.Buffer.Length && _ff.Value.cFileName[ix] != 0; ix++)
                        _ff.Buffer[sposition++] = _ff.Value.cFileName[ix];
                    if (sposition == _ff.Buffer.Length)
                        throw new PathTooLongException();
                    if (_ff.Value.IgnoredByName) continue;
                    if ((_ff.Value.dwFileAttributes & FileAttributes.Directory) == FileAttributes.Directory) continue;
                    //got one
                    args.SetNameOffsets(UncPrefixLength, slength, sposition);
                    _fileFound(args);
                } while (!args.CancelEnumeration && Kernel32.FindNextFile(hFile, out _ff.Value));
            }
            finally
            {
                Kernel32.FindClose(hFile);
            }
        }
        private void Win32Error(int errorCode)
        {
            switch (errorCode)
            {
                case Kernel32.ERROR_FILE_NOT_FOUND:
                case Kernel32.ERROR_PATH_NOT_FOUND:
                    return;
                case Kernel32.ERROR_ACCESS_DENIED:
                default:
                    throw new Win32Exception(errorCode);
            }
        }
    }
}
