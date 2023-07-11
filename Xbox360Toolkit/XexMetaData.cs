using System;

namespace Xbox360Toolkit
{
    public class XexMetaData
    {
        public XexRegion GameRegion;

        public uint TitleId;

        public uint MediaId;

        public uint Version;

        public uint BaseVersion;

        public uint DiscNum;

        public uint DiscTotal;

        public byte[] Thumbnail;

        public string TitleName;

        public string Description;

        public string Publisher;

        public string Developer;

        public string Genre;

        public XexMetaData()
        {
            Thumbnail = Array.Empty<byte>();
            TitleName = string.Empty;
            Description = string.Empty;
            Publisher = string.Empty;
            Developer = string.Empty;
            Genre = string.Empty;
        }
    }
}
