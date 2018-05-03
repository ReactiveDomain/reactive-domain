using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using ReactiveDomain.Buffers.Examples;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Buffers.FileIO
{
    public static unsafe class UnbufferedFileTools
    {
        #region Unbuffered write
        /*
        if we need to check for the file alignment requirements for completely unbuffered writes
        implement this
        BOOL WINAPI GetFileInformationByHandleEx(
                                          _In_  HANDLE                    hFile,
                                          _In_  FILE_INFO_BY_HANDLE_CLASS FileInformationClass,
                                          _Out_ LPVOID                    lpFileInformation,
                                          _In_  DWORD                     dwBufferSize
                                        );
        GetFileInformationByHandleEx(FileAlignmentInfo)
        //in param
        typedef enum _FILE_INFO_BY_HANDLE_CLASS { 
                                          FileBasicInfo                   = 0,
                                          FileStandardInfo                = 1,
                                          FileNameInfo                    = 2,
                                          FileRenameInfo                  = 3,
                                          FileDispositionInfo             = 4,
                                          FileAllocationInfo              = 5,
                                          FileEndOfFileInfo               = 6,
                                          FileStreamInfo                  = 7,
                                          FileCompressionInfo             = 8,
                                          FileAttributeTagInfo            = 9,
                                          FileIdBothDirectoryInfo         = 10, // 0xA
                                          FileIdBothDirectoryRestartInfo  = 11, // 0xB
                                          FileIoPriorityHintInfo          = 12, // 0xC
                                          FileRemoteProtocolInfo          = 13, // 0xD
                                          FileFullDirectoryInfo           = 14, // 0xE
                                          FileFullDirectoryRestartInfo    = 15, // 0xF
                                          FileStorageInfo                 = 16, // 0x10
                              this one => FileAlignmentInfo               = 17, // 0x11
                                          FileIdInfo                      = 18, // 0x12
                                          FileIdExtdDirectoryInfo         = 19, // 0x13
                                          FileIdExtdDirectoryRestartInfo  = 20, // 0x14
                                          MaximumFileInfoByHandlesClass
                                        } FILE_INFO_BY_HANDLE_CLASS, *PFILE_INFO_BY_HANDLE_CLASS;
            //out param
            uint32 lpFileInformation
            definition must contain _one_ of the following values
                FILE_BYTE_ALIGNMENT 0x00000000 If this value is specified, there are no alignment requirements for the device.
                FILE_WORD_ALIGNMENT 0x00000001 If this value is specified, data MUST be aligned on a 2-byte boundary.
                FILE_LONG_ALIGNMENT 0x00000003 If this value is specified, data MUST be aligned on a 4-byte boundary.
                FILE_QUAD_ALIGNMENT 0x00000007 If this value is specified, data MUST be aligned on an 8-byte boundary.
                FILE_OCTA_ALIGNMENT 0x0000000f If this value is specified, data MUST be aligned on a 16-byte boundary.
                FILE_32_BYTE_ALIGNMENT 0x0000001f If this value is specified, data MUST be aligned on a 32-byte boundary.
                FILE_64_BYTE_ALIGNMENT 0x0000003f If this value is specified, data MUST be aligned on a 64-byte boundary.
                FILE_128_BYTE_ALIGNMENT 0x0000007f If this value is specified, data MUST be aligned on a 128-byte boundary.
                FILE_256_BYTE_ALIGNMENT 0x000000ff If this value is specified, data MUST be aligned on a 256-byte boundary.
                FILE_512_BYTE_ALIGNMENT 0x000001ff If this value is specified, data MUST be aligned on a 512-byte boundary.
            https://msdn.microsoft.com/en-us/library/cc232065.aspx

            use the above to set BLOCK_SIZE
            then change call to  CreateFile

            
            fileHandle = CreateFile(fileName, GENERIC_WRITE, 0, 0, CREATE_ALWAYS, NO_BUFFERING, 0);

        */
        #endregion

        #region Cancellation
        /*
        CancelIoEx function
        https://msdn.microsoft.com/en-us/library/windows/desktop/aa363792(v=vs.85).aspx
        */
        #endregion
        private static readonly ILogger Log = LogManager.GetLogger("Storage");

        // ReSharper disable InconsistentNaming
        private const uint NO_BUFFERING = 0x20000000;
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const uint CREATE_ALWAYS = 2;
        // ReSharper restore InconsistentNaming
        private const int BlockSize = 65536;

        // Define the Windows system functions that are called by this class via COM Interop:
        [DllImport("kernel32", SetLastError = true)]
        static extern IntPtr CreateFile
        (
             string fileName,          // file name
             uint desiredAccess,       // access mode
             uint shareMode,           // share mode
             uint securityAttributes,  // Security Attributes
             uint creationDisposition, // how to create
             uint flagsAndAttributes,  // file attributes
             int hTemplateFile         // handle to template file
        );

        [DllImport("kernel32", SetLastError = true)]
        static extern bool ReadFile
        (
             IntPtr hFile,      // handle to file
             void* pBuffer,            // data buffer
             int numberOfBytesToRead,  // number of bytes to read
             int* pNumberOfBytesRead,  // number of bytes read
             int overlapped            // overlapped buffer which is used for async I/O.  Not used here.
        );

        [DllImport("kernel32", SetLastError = true)]
        static extern bool WriteFile
        (
            IntPtr handle,			    // handle to file
            void* pBuffer,              // data buffer
            int numberOfBytesToWrite,	// Number of bytes to write.
            int* pNumberOfBytesWritten, // Number of bytes that were written..
            int overlapped				// Overlapped buffer which is used for async I/O.  Not used here.
        );

        [DllImport("kernel32", SetLastError = true)]
        static extern bool CloseHandle
        (
             IntPtr hObject     // handle to object
        );

        public static ApplicationException LastException { get; private set; }

        private static bool TryOpenForWriting(string fileName, out IntPtr fileHandle)
        {
            //this is a buffered write
            // This function uses the Windows API CreateFile function to open an existing file.
            // If the file exists, it will be overwritten.
            fileHandle = CreateFile(fileName, GENERIC_WRITE, 0, 0, CREATE_ALWAYS, 0, 0);
            if (fileHandle != IntPtr.Zero) return true;
            var wExp = new Win32Exception();
            LastException = new ApplicationException("UnbufferedFile:OpenForWriting - Could not open file " + fileName + " - " + wExp.Message);
            Log.Error(LastException.Message);
            return false;
        }
        internal static bool TryWrite(FileInfo file, VideoSlice buffer)
        {
            return TryWrite(file, (int)buffer.BufferSize, buffer.AsBytePtr);
        }
        public static bool TryWrite(FileInfo file, int numBytesToWrite, byte* pBuf)
        {
            return TryWrite(file.FullName, numBytesToWrite, pBuf);
        }

        public static bool TryWrite(string fileName, int numBytesToWrite, byte* pBuf)
        {
            var fileHandle = IntPtr.Zero;
            try
            {
                Log.Trace("About to write " + numBytesToWrite + "bytes to file '" + fileName + "'");
                var bytesOutput = 0;
                if (!TryOpenForWriting(fileName, out fileHandle))
                {
                    return false; //File open failed
                }

                // This function writes out chunks at a time instead of the entire file.  This is the fastest write function,
                // perhaps because the block size is an even multiple of the sector size.
                var remainingBytes = numBytesToWrite;
                // Do until there are no more bytes to write.
                do
                {
                    var bytesToWrite = Math.Min(remainingBytes, BlockSize);
                    int bytesWritten;
                    if (!WriteFile(fileHandle, pBuf, bytesToWrite, &bytesWritten, 0))
                    {
                        var wExp = new Win32Exception();
                        LastException =
                            new ApplicationException("UnbufferedFile:WriteBlocks - Error occurred writing a file. - " + wExp.Message);
                        Log.Error(LastException.Message);
                        return false;
                    }
                    pBuf += bytesToWrite;
                    bytesOutput += bytesToWrite;
                    remainingBytes -= bytesToWrite;
                } while (remainingBytes > 0);
                Log.Trace("Successfully wrote " + bytesOutput + "bytes to file '" + fileName + "'");
                return bytesOutput == numBytesToWrite;
            }
            finally
            {
                Close(ref fileHandle);
            }

        }
        private static bool TryOpenForReading(string fileName, out IntPtr fileHandle)
        {
            // This function uses the Windows API CreateFile function to open an existing file.
            // A return value of true indicates success.
            fileHandle = CreateFile(fileName, GENERIC_READ, 0, 0, OPEN_EXISTING, 0, 0);
            if (fileHandle != IntPtr.Zero) return true;
            //Got an error
            var wExp = new Win32Exception();
            LastException = new ApplicationException("UnbufferedFile:OpenForReading - Could not open file " + fileName + " - " + wExp.Message);
            return false;

        }

        internal static bool TryRead(FileInfo file, VideoSlice buffer)
        {
            return TryRead(file, (int)buffer.BufferSize, buffer.AsBytePtr);
        }

        public static bool TryRead(FileInfo file, int bytesToRead, byte* pBuf)
        {
            return TryRead(file.FullName, bytesToRead, pBuf);

        }

        public static bool TryRead(string filename, int bytesToRead, byte* pBuf)
        {
            // This function reads a total of BytesToRead at a time.  There is a limit of 2gb per call.
            var fileHandle = IntPtr.Zero;
            try
            {
                Log.Trace("About to read " + bytesToRead + "bytes from file '" + filename + "'");
                if (!TryOpenForReading(filename, out fileHandle)) return false;
                var bytesReadInBlock = 0;
                var bytesRead = 0;
                // Do until there are no more bytes to read or the buffer is full.
                do
                {
                    var blockByteSize = Math.Min(BlockSize, bytesToRead - bytesRead);
                    if (!ReadFile(fileHandle, pBuf, blockByteSize, &bytesReadInBlock, 0))
                    {
                        var wExp = new Win32Exception();
                        LastException =
                            new ApplicationException("UnbufferedFile:ReadBytes - Error occurred reading a file. - " +
                                                     wExp.Message);
                        return false;
                    }
                    if (bytesReadInBlock == 0)
                        break;
                    bytesRead += bytesReadInBlock;
                    pBuf += bytesReadInBlock;
                } while (bytesRead < bytesToRead);
                Log.Trace("Successfully read " + bytesRead + "bytes from file '" + filename + "'");
                return true;
            }
            finally
            {
                Close(ref fileHandle);
            }

        }

        public static void Close(ref IntPtr fileHandle)
        {
            if (fileHandle == IntPtr.Zero) return;
            CloseHandle(fileHandle);
            fileHandle = IntPtr.Zero;
        }

    }
}
