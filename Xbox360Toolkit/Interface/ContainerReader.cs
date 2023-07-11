using System;

namespace Xbox360Toolkit.Interface
{
    public abstract class ContainerReader : IContainerReader
    {
        public abstract SectorDecoder? GetDecoder();

        public abstract bool Mount();

        public abstract void Dismount();

        public abstract int GetMountCount();

        public bool TryGetDefault(out byte[] defaultData, out ContainerType containerType)
        {
            defaultData = Array.Empty<byte>();
            containerType = ContainerType.Unknown;

            var decoder = GetDecoder();
            if (decoder == null)
            {
                return false;
            }
            return decoder.TryGetDefault(out defaultData, out containerType);
        }

        public bool ReadSector(long sector, out byte[] sectorData)
        {
            var decoder = GetDecoder();
            if (decoder == null)
            {
                sectorData = Array.Empty<byte>();
                return false;
            }
            sectorData = decoder.ReadSector(sector);
            return true;
        }
    }
}
