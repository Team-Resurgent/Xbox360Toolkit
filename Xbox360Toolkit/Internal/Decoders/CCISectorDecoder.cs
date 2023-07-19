using System;
using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal class CCISectorDecoder : SectorDecoder
    {
        private CCIDetails mCCIDetails;
        private FileStream mFileStream;
        private object mMutex;
        private bool mDisposed;

        public CCISectorDecoder(CCIDetails cciDetails)
        {
            mCCIDetails = cciDetails;
            mFileStream = new FileStream(mCCIDetails.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            mMutex = new object();
            mDisposed = false;
        }

        public override uint TotalSectors()
        {
            return (uint)(mCCIDetails.IndexInfo.Length - 1);
        }

        public override bool TryReadSector(long sector, out byte[] sectorData)
        {
            var position = mCCIDetails.IndexInfo[sector].Value;
            var LZ4Compressed = mCCIDetails.IndexInfo[sector].LZ4Compressed;
            var size = (int)(mCCIDetails.IndexInfo[sector + 1].Value - position);

            lock (mMutex)
            {
                mFileStream.Position = (long)position;
                if (size != Constants.XGD_SECTOR_SIZE || LZ4Compressed)
                {
                    var padding = mFileStream.ReadByte();
                    var decompressBuffer = new byte[size];
                    var decompressBytesRead = mFileStream.Read(decompressBuffer, 0, size);
                    if (decompressBytesRead != size)
                    {
                        sectorData = Array.Empty<byte>();
                        return false;
                    }
                    var decodeBuffer = new byte[Constants.XGD_SECTOR_SIZE];
                    var decompressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(decompressBuffer, 0, size - (padding + 1), decodeBuffer, 0, (int)Constants.XGD_SECTOR_SIZE);
                    if (decompressedSize < 0)
                    {
                        sectorData = Array.Empty<byte>();
                        return false;
                    }
                    sectorData = decodeBuffer;
                    return true;
                }
     
                sectorData = new byte[Constants.XGD_SECTOR_SIZE];
                var sectorBytesRead = mFileStream.Read(sectorData, 0, (int)Constants.XGD_SECTOR_SIZE);
                return sectorBytesRead == Constants.XGD_SECTOR_SIZE;
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
