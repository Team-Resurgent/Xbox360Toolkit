using System;
using System.Collections.Generic;
using System.Text;

namespace Xbox360Toolkit.Internal.Models
{
    internal struct GodDetails
    {
        public string DataPath;
        public uint DataFileCount;
        public uint BaseAddress;
        public uint StartingBlock;
        public uint SectorCount;
        public bool IsEnhancedGDF;
    }
}
