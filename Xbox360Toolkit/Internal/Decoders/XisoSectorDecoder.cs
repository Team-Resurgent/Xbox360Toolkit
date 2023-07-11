using System.IO;
using Xbox360Toolkit.Interface;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal class XisoSectorDecoder : SectorDecoder
    {
        private string mFilePath;

        public XisoSectorDecoder(string filePath)
        {
            mFilePath = filePath;
        }

        public override long TotalSectors()
        {
            return new FileInfo(mFilePath).Length / Constants.XGD_SECTOR_SIZE;
        }

        public override byte[] ReadSector(long sector)
        {
            using (var fileStream = new FileStream(mFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                binaryReader.BaseStream.Position = sector * Constants.XGD_SECTOR_SIZE;
                return binaryReader.ReadBytes((int)Constants.XGD_SECTOR_SIZE);
            }
        }
    }
}
