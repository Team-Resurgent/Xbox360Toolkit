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
            using (var binaryReader = new BinaryReader(fileStream))
            {
                binaryReader.BaseStream.Position = sector * Constants.XGD_SECTOR_SIZE;
                sectorData = binaryReader.ReadBytes((int)Constants.XGD_SECTOR_SIZE);
            }
            return sectorData.Length == Constants.XGD_SECTOR_SIZE;
        }
    }
}
