using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Internal
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

        public static uint ConvertEndian(uint value)
        {
            return
                (value & 0x000000ff) << 24 |
                (value & 0x0000ff00) << 8 |
                (value & 0x00ff0000) >> 8 |
                (value & 0xff000000) >> 24;
        }

        public static ushort ConvertEndian(ushort value)
        {
            return (ushort)(
                (value & 0x000ff) << 8 |
                (value & 0xff00) >> 8
            );
        }

        public static XgdHeader GetXgdHeaer(byte[] sector)
        {
            using var sectorStream = new MemoryStream(sector);
            using var sectorReader = new BinaryReader(sectorStream);
            var header = ByteToType<XgdHeader>(sectorReader);
            return header;
        }
    }
}
