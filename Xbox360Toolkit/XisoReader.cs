using System;
using System.IO;
using Xbox360Toolkit.Internal.Decoders;

namespace Xbox360Toolkit
{
    public class XisoReader : IReader
    {
        private SectorDecoder? mSectorDecoder;

        public bool Mount(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                return false;
            }

            var sectorDecoder = new XisoSectorDecoder(filePath);
            if (sectorDecoder.TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
            {
                return false;
            }

            mSectorDecoder = sectorDecoder;
            return true;
        }

        public bool TryGetDefaultXex(out byte[] xbeData)
        {
            xbeData = Array.Empty<byte>();

            if (mSectorDecoder == null)
            {
                return false;
            }
            return mSectorDecoder.TryGetDefaultXex(out xbeData);
        }

        public bool ReadSector(long sector, out byte[] sectorData)
        {
            if (mSectorDecoder == null)
            {
                sectorData = Array.Empty<byte>();
                return false;
            }
            sectorData = mSectorDecoder.ReadSector(sector);
            return true;
        }
    }
}
