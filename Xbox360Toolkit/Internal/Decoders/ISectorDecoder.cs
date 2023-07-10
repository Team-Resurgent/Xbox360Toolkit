namespace Xbox360Toolkit.Internal.Decoders
{
    internal interface ISectorDecoder
    {
        public long TotalSectors();
        public byte[] ReadSector(long sector);
    }
}
