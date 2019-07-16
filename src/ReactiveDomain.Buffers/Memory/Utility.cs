using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;

namespace ReactiveDomain.Buffers.Memory
{
    public static class Utility
    {
        public static bool IsEqualTo(this DirectoryInfo self, DirectoryInfo other)
        {
            return String.Compare(
                self.FullName.TrimEnd(Path.DirectorySeparatorChar),
                other.FullName.TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.InvariantCultureIgnoreCase) == 0;
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public static bool NearlyEqual(float a, float b, float epsilon)
        {
            float absA = Math.Abs(a);
            float absB = Math.Abs(b);
            float diff = Math.Abs(a - b);

            if (a == b)
            {
                // shortcut, handles infinities
                return true;
            }
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (a == 0 || b == 0 || diff < float.MinValue)
            {
                // a or b is zero or both are extremely close to it
                // relative error is less meaningful here
                return diff < (epsilon * float.MaxValue);
            } // use relative error
            return diff / (absA + absB) < epsilon;
        }



        //public static JsonSerializerSettings SerializerSettings = new JsonSerializerSettings
        //{
        //    TypeNameHandling = TypeNameHandling.None,
        //    DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        //    DateFormatHandling = DateFormatHandling.IsoDateFormat,
        //    DateParseHandling = DateParseHandling.DateTime,
        //    DefaultValueHandling = DefaultValueHandling.Populate,
        //    Formatting = Formatting.Indented
        //};









        /// <summary>
        /// Constants for use in video buffer management 
        /// Tightly coupled to the exact field layout and size of the BufferStructure
        /// </summary>



        public const int FramesPerBuffer = 30;
        
        public static unsafe Guid ParseGuidBuffer(byte* buffer)
        {
            return new Guid(
                        *(int*)buffer,
                        *(short*)(buffer + 4),
                        *(short*)(buffer + 6),
                        buffer[8],
                        buffer[9],
                        buffer[10],
                        buffer[11],
                        buffer[12],
                        buffer[13],
                        buffer[14],
                        buffer[15]
                        );
        }

        public static unsafe void CopyToBuffer(this Guid guid, byte* buffer)
        {
            var bytes = guid.ToByteArray();
            for (int i = 0; i < 16; i++)
            {
                buffer[i] = bytes[i];
            }
        }
        public static int PowerOf2RoundUp(int x)
        {
            if (x < 0)
                return 0;
            --x;
            x |= x >> 1;
            x |= x >> 2;
            x |= x >> 4;
            x |= x >> 8;
            x |= x >> 16;
            return x + 1;


        }
        #region External
       
#if NET472 || NET452
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern void CopyMemory(IntPtr destination, IntPtr source, uint length);

        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        public static extern unsafe void CopyMemory(byte* destination, byte* source, uint length);


        /// <summary>
        /// P-Invoked Memset
        /// </summary>
        /// <param name="destination">Pointer to target byte array</param>
        /// <param name="c">Character to Set</param>
        /// <param name="count">Number of characters (max = Length of array)</param>
        /// <returns></returns>
        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        public static extern IntPtr MemSet(IntPtr destination, int c, int count);
#else
        public static unsafe void CopyMemory(IntPtr destination, IntPtr source, uint length)
        {
            Buffer.MemoryCopy(destination.ToPointer(), source.ToPointer(), length, length);
        }
        public static unsafe void CopyMemory(byte* destination, byte* source, uint length)
        {
            Buffer.MemoryCopy(destination, source, length, length);
        }

        public static unsafe void MemSet(IntPtr destination, int c, int count)
        {
            System.Runtime.CompilerServices.Unsafe.InitBlock(destination.ToPointer(), (byte)c, (uint) count);
        }
#endif

        public static void Clear(IntPtr target, int sizeInBytes)
        {
            MemSet(target, 0x0, sizeInBytes);
        }

        #endregion


        /// <summary>
        /// Aligns a pointer in an over allocated buffer to a 16 byte boundary
        /// N.B. this will reduce the availible buffer size by up to 8 bytes as all 
        /// byte arrays are already allocated on an 8 byte boundary in .net
        /// </summary>
        /// <param name="f">the pointer to align</param>
        /// <returns></returns>
        public static unsafe byte* AlignTo16(byte* f)
        {
            return (byte*)(16 * (((long)f + 15) / 16));
        }
    }
}
