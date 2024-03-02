using System.IO;

namespace Xbox360Toolkit.Internal.Models
{
    internal struct CCIDetail
    {
        public Stream Stream;
        public CCIIndex[] IndexInfo;
        public long StartSector;
        public long EndSector;
    }
}
