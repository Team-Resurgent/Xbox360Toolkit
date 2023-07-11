namespace Xbox360Toolkit.Interface
{
    public interface IReader
    {
        public SectorDecoder? GetDecoder();
        public bool Mount();
        public void Dismount();
        public int GetMountCount();
        public bool TryGetDefault(out byte[] xbeData, out DefaultType defaultType);
        public bool ReadSector(long sector, out byte[] sectorData);
    }
}
