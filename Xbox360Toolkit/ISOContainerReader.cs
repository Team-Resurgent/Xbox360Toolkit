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

        public override bool TryMount()
        {
            try
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

                mSectorDecoder = new ISOSectorDecoder(mFilePath);
                if (mSectorDecoder.Init() == false)
                {
                    return false;
                }

                mMountCount++;
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
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
