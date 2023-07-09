using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Xbox360Toolkit
{
    internal class Helpers
    {
        public static string GetUtf8String(byte[] buffer)
        {
            var result = string.Empty;
            for (var i = 0; i < buffer.Length; i++)
            {
                var value = buffer[i];
                if (value == 0)
                {
                    break;
                }
                result += (char)value;
            }
            return result;
        }

        public static string GetUnicodeString(byte[] buffer)
        {
            var result = string.Empty;
            for (var i = 0; i < buffer.Length; i += 2)
            {
                var value = (short)Encoding.Unicode.GetString(buffer, i, 2)[0];
                if (value == 0)
                {
                    break;
                }
                result += (char)value;
            }
            return result;
        }

        public static T ByteToType<T>(BinaryReader reader)
        {
            byte[] bytes = reader.ReadBytes(Marshal.SizeOf(typeof(T)));
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            T theStructure = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return theStructure;
        }

        public static int SizeOf<T>()
        {
            return Marshal.SizeOf(typeof(T));
        }

    }
}
