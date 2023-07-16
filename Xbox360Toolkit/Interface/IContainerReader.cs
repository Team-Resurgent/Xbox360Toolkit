using System.Collections.Generic;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Interface
{
    public interface IContainerReader
    {
        public SectorDecoder GetDecoder();
        public bool Mount();
        public void Dismount();
        public int GetMountCount();
        public bool TryGetDataSectors(out HashSet<uint> dataSectors);
        public bool TryGetDefault(out byte[] defaultData, out ContainerType containerType);
    }
}
