using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal class GODSectorDecoder : SectorDecoder
    {
        private GODDetails mGODDetails;

        public GODSectorDecoder(GODDetails godDetails)
        {
            mGODDetails = godDetails;
        }

        private long SectorToAddress(long sector, out uint dataFileIndex)
        {
            if (sector == Constants.SVOD_START_SECTOR || sector == Constants.SVOD_START_SECTOR + 1)
            {
                dataFileIndex = 0;
                return mGODDetails.BaseAddress + (sector - Constants.SVOD_START_SECTOR) * Constants.XGD_SECTOR_SIZE;
            }

            var adjustedSector = sector - mGODDetails.StartingBlock * 2 + (mGODDetails.IsEnhancedGDF ? 2 : 0);
            dataFileIndex = (uint)(adjustedSector / 0x14388);
            if (dataFileIndex > mGODDetails.DataFileCount)
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
            return mGODDetails.SectorCount;
        }

        public override bool TryReadSector(long sector, out byte[] sectorData)
        {
            var dataOffset = SectorToAddress(sector, out var dataFileIndex);
            var filePath = Path.Combine(mGODDetails.DataPath, string.Format("Data{0:D4}", dataFileIndex));
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                binaryReader.BaseStream.Position = dataOffset;
                sectorData = binaryReader.ReadBytes((int)Constants.XGD_SECTOR_SIZE);
            }
            return true;
        }
    }
}
