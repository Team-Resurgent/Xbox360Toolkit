namespace Xbox360Toolkit.Interface
{
    public interface ISectorDecoder
    {
        public uint TotalSectors();
        public uint SectorSize();
        public bool TryReadSector(long sector, out byte[] sectorData);
    }
}
