using System.IO;

namespace Xbox360Toolkit.Internal.Models
{
    internal struct ISODetail
    {
        public Stream Stream;
        public long StartSector;
        public long EndSector;
    }
}
