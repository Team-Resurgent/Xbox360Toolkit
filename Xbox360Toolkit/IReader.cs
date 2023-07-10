namespace Xbox360Toolkit
{
    public interface IReader
    {
        public bool Mount(string filePath);
        public bool TryGetDefaultXex(out byte[] xbeData);
        public bool ReadSector(long sector, out byte[] sectorData);
    }
}
