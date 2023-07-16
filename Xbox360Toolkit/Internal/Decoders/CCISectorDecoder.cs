using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal class CCISectorDecoder : SectorDecoder
    {
        private CCIDetails mCCIDetails;

        public CCISectorDecoder(CCIDetails cciDetails)
        {
            mCCIDetails = cciDetails;
        }

        public override uint TotalSectors()
        {
            return (uint)(mCCIDetails.IndexInfo.Length - 1);
        }

        public override bool TryReadSector(long sector, out byte[] sectorData)
        {
            var decodeBuffer = new byte[Constants.XGD_SECTOR_SIZE];
            var position = mCCIDetails.IndexInfo[sector].Value;
            var LZ4Compressed = mCCIDetails.IndexInfo[sector].LZ4Compressed;
            var size = (int)(mCCIDetails.IndexInfo[sector + 1].Value - position);

            using (var fileStream = new FileStream(mCCIDetails.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                fileStream.Position = (long)position;

                if (size != Constants.XGD_SECTOR_SIZE || LZ4Compressed)
                {
                    var padding = binaryReader.ReadByte();
                    var buffer = binaryReader.ReadBytes(size);
                    var compressedSize = K4os.Compression.LZ4.LZ4Codec.Decode(buffer, 0, size - (padding + 1), decodeBuffer, 0, (int)Constants.XGD_SECTOR_SIZE);
                    if (compressedSize < 0)
                    {
                        throw new IOException("Decompression failed.");
                    }
                    sectorData = decodeBuffer;
                }
                else
                {
                    sectorData = binaryReader.ReadBytes((int)Constants.XGD_SECTOR_SIZE);
                }
            }
            return true;
        }
    }
}
