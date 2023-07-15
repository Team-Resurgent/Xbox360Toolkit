using System;
using System.Collections.Generic;

namespace Xbox360Toolkit.Interface
{
    public abstract class ContainerReader : IContainerReader
    {
        public abstract SectorDecoder GetDecoder();

        public abstract bool Mount();

        public abstract void Dismount();

        public abstract int GetMountCount();

        public bool TryGetDataSectors(out HashSet<uint> dataSectors)
        {
            try
            {
                return GetDecoder().TryGetDataSectors(out dataSectors);
            } 
            catch 
            {
                dataSectors = new HashSet<uint>();
                return false; 
            }
        }

        public bool TryGetDefault(out byte[] defaultData, out ContainerType containerType)
        {
            try
            {
                return GetDecoder().TryGetDefault(out defaultData, out containerType);
            }
            catch
            {
                defaultData = Array.Empty<byte>();
                containerType = ContainerType.Unknown;
                return false;
            }
        }

        public bool ReadSector(long sector, out byte[] sectorData)
        {
            try
            {
                return GetDecoder().TryReadSector(sector, out sectorData);
            }
            catch
            {
                sectorData = Array.Empty<byte>();
                return false;
            }
        }
    }
}
