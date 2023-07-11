using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal class GodSectorDecoder : SectorDecoder
    {
        private GodDetails mGodDetails;

        public GodSectorDecoder(GodDetails godDetails)
        {
            mGodDetails = godDetails;
        }

        private long SectorToAddress(long sector, out uint dataFileIndex)
        {
            if (sector == Constants.SVOD_START_SECTOR || sector == Constants.SVOD_START_SECTOR + 1)
            {
                dataFileIndex = 0;
                return mGodDetails.BaseAddress + (sector - Constants.SVOD_START_SECTOR) * Constants.XGD_SECTOR_SIZE;
            }

            var adjustedSector = sector - mGodDetails.StartingBlock * 2 + (mGodDetails.IsEnhancedGDF ? 2 : 0);
            dataFileIndex = (uint)(adjustedSector / 0x14388);
            if (dataFileIndex > mGodDetails.DataFileCount)
            {
                dataFileIndex = 0;
            }
            var dataSector = adjustedSector % 0x14388;
            var dataBlock = dataSector / 0x198;
            dataSector %= 0x198;

            var dataFileOffset = (dataSector + dataBlock * 0x198) * 0x800;
            dataFileOffset += 0x1000;
            dataFileOffset += dataBlock * 0x1000 + 0x1000;
            return dataFileOffset;
        }

        public override uint TotalSectors()
        {
            return mGodDetails.SectorCount;
        }

        public override byte[] ReadSector(long sector)
        {
            var dataOffset = SectorToAddress(sector, out var dataFileIndex);
            var filePath = Path.Combine(mGodDetails.DataPath, string.Format("Data{0:D4}", dataFileIndex));
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                binaryReader.BaseStream.Position = dataOffset;
                return binaryReader.ReadBytes((int)Constants.XGD_SECTOR_SIZE);
            }
        }
    }
}
