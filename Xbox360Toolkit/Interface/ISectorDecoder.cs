namespace Xbox360Toolkit.Interface
{
    public interface ISectorDecoder
    {
        public long TotalSectors();
        public long SectorSize();
        public byte[] ReadSector(long sector);
    }
}
