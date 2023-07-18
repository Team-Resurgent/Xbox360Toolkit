using System.IO;
using Xbox360Toolkit.Interface;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal class ISOSectorDecoder : SectorDecoder
    {
        private string mFilePath;

        public ISOSectorDecoder(string filePath)
        {
            mFilePath = filePath;
        }

        public override uint TotalSectors()
        {
            return (uint)(new FileInfo(mFilePath).Length / Constants.XGD_SECTOR_SIZE);
        }

        public override bool TryReadSector(long sector, out byte[] sectorData)
        {
            using (var fileStream = new FileStream(mFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                fileStream.Position = sector * Constants.XGD_SECTOR_SIZE;
                var sectorBuffer = new byte[Constants.XGD_SECTOR_SIZE];
                var bytesRead = fileStream.Read(sectorBuffer, 0, (int)Constants.XGD_SECTOR_SIZE);
                sectorData = sectorBuffer;
                return bytesRead == Constants.XGD_SECTOR_SIZE;
            }
        }
    }
}
