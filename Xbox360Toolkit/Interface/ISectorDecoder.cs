namespace Xbox360Toolkit.Interface
{
    public interface ISectorDecoder
    {
        public long TotalSectors();
        public byte[] ReadSector(long sector);
    }
}
