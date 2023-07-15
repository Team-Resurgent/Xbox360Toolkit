using System;
using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal.Decoders;

namespace Xbox360Toolkit
{
    public class ISOContainerReader : ContainerReader
    {
        private string mFilePath;
        private int mMountCount;
        private SectorDecoder? mSectorDecoder;

        public ISOContainerReader(string filePath)
        {
            mFilePath = filePath;
            mMountCount = 0;
        }

        public override SectorDecoder GetDecoder()
        {
            if (mSectorDecoder == null)
            {
                throw new Exception("Container not mounted.");
            }
            return mSectorDecoder;
        }

        public override bool Mount()
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

            var sectorDecoder = new ISOSectorDecoder(mFilePath);
            if (sectorDecoder.TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
            {
                return false;
            }

            mSectorDecoder = sectorDecoder;
            mMountCount++;
            return true;
        }

        public override void Dismount()
        {
            if (mMountCount == 0)
            {
                return;
            }
            mMountCount--;
        }

        public override int GetMountCount()
        {
            return mMountCount;
        }
    }
}
