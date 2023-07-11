namespace Xbox360Toolkit.Interface
{
    public interface IContainerReader
    {
        public SectorDecoder? GetDecoder();
        public bool Mount();
        public void Dismount();
        public int GetMountCount();
        public bool TryGetDefault(out byte[] defaultData, out ContainerType containerType);
        public bool ReadSector(long sector, out byte[] sectorData);
    }
}
