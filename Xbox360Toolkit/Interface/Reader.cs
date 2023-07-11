using System;

namespace Xbox360Toolkit.Interface
{
    public abstract class Reader : IReader
    {
        public abstract SectorDecoder? GetDecoder();

        public abstract bool Mount();

        public abstract void Dismount();

        public abstract int GetMountCount();

        public bool TryGetDefault(out byte[] xbeData, out DefaultType defaultType)
        {
            xbeData = Array.Empty<byte>();
            defaultType = DefaultType.None;

            var decoder = GetDecoder();
            if (decoder == null)
            {
                return false;
            }
            return decoder.TryGetDefault(out xbeData, out defaultType);
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
