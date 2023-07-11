namespace Xbox360Toolkit.Interface
{
    public interface ISectorDecoder
    {
        public uint TotalSectors();
        public uint SectorSize();
        public byte[] ReadSector(long sector);
    }
}
