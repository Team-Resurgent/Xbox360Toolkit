using System;
using System.IO;
using Xbox360Toolkit.Interface;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal class ISOSectorDecoder : SectorDecoder
    {
        private string mFilePath;
        private FileStream mFileStream;
        private object mMutex;
        private bool mDisposed;

        public ISOSectorDecoder(string filePath)
        {
            mFilePath = filePath;
            mFileStream = new FileStream(mFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            mMutex = new object();
            mDisposed = false;
        }

        public override uint TotalSectors()
        {
            return (uint)(new FileInfo(mFilePath).Length / Constants.XGD_SECTOR_SIZE);
        }

        public override bool TryReadSector(long sector, out byte[] sectorData)
        {
            sectorData = new byte[Constants.XGD_SECTOR_SIZE];
            lock (mMutex)
            {
                mFileStream.Position = sector * Constants.XGD_SECTOR_SIZE;
                var bytesRead = mFileStream.Read(sectorData, 0, (int)Constants.XGD_SECTOR_SIZE);
                return bytesRead == Constants.XGD_SECTOR_SIZE;
            }
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (mDisposed == false)
            {
                if (disposing)
                {
                    mFileStream.Dispose();
                }
                mDisposed = true;
            }
        }
    }
}
