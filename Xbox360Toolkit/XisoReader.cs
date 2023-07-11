using System;
using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal.Decoders;

namespace Xbox360Toolkit
{
    public class XisoReader : IReader
    {
        private string mFilePath;
        private int mMountCount;
        private SectorDecoder? mSectorDecoder;

        public XisoReader(string filePath)
        {
            mFilePath = filePath;
            mMountCount = 0;
        }

        public SectorDecoder? GetDecoder()
        {
            return mSectorDecoder;
        }

        public bool Mount()
        {
            if (mMountCount > 0)
            {
                mMountCount++;
                return true;
            }

            if (File.Exists(mFilePath) == false)
            {
                return false;
            }

            var sectorDecoder = new XisoSectorDecoder(mFilePath);
            if (sectorDecoder.TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
            {
                return false;
            }

            mSectorDecoder = sectorDecoder;
            mMountCount++;
            return true;
        }

        public void Dismount()
        {
            if (mMountCount == 0)
            {
                return;
            }
            mMountCount--;
        }

        public int GetMountCount()
        {
            return mMountCount;
        }

        public bool TryGetDefault(out byte[] xbeData, out DefaultType defaultType)
        {
            xbeData = Array.Empty<byte>();
            defaultType = DefaultType.None;

            if (mMountCount == 0 || mSectorDecoder == null)
            {
                return false;
            }
            return mSectorDecoder.TryGetDefault(out xbeData, out defaultType);
        }

        public bool ReadSector(long sector, out byte[] sectorData)
        {
            if (mMountCount == 0 || mSectorDecoder == null)
            {
                sectorData = Array.Empty<byte>();
                return false;
            }
            sectorData = mSectorDecoder.ReadSector(sector);
            return true;
        }
    }
}
