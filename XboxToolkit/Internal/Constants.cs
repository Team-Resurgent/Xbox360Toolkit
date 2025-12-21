namespace XboxToolkit.Internal
{
    internal static class Constants
    {
        public const string XEX_FILE_NAME = "default.xex";
        public const string XBE_FILE_NAME = "default.xbe";
        public const string XGD_IMAGE_MAGIC = "MICROSOFT*XBOX*MEDIA";
        public const uint XGD_SECTOR_SIZE = 0x800;
        public const uint XGD_ISO_BASE_SECTOR = 0x20;
        public const uint XGD_MAGIC_SECTOR_XDKI = XGD_ISO_BASE_SECTOR;

        public const uint XGD_MAGIC_SECTOR_XGD1 = 0x30620;

        public const uint XGD_MAGIC_SECTOR_XGD2 = 0x1FB40;
        public const uint XGD2_PFI_OFFSET = 0xFD8E800;
        public const uint XGD2_DMI_OFFSET = 0xFD8F000;
        public const uint XGD2_SS_OFFSET = 0xFD8F800;

        public const uint XGD_MAGIC_SECTOR_XGD3 = 0x4120;
        public const uint XGD3_PFI_OFFSET = 0x2076800;
        public const uint XGD3_DMI_OFFSET = 0x2077000;
        public const uint XGD3_SS_OFFSET = 0x2077800;

        public const uint SVOD_START_SECTOR = XGD_ISO_BASE_SECTOR;

        // Xbox Original root directory sector (matches extract-xiso)
        public const uint XISO_ROOT_DIRECTORY_SECTOR = 0x108;

        public const uint NXE_CONTAINER_TYPE = 0x4000;
        public const uint GOD_CONTAINER_TYPE = 0x7000;
        
        // Padding byte used in XISO format (matches extract-xiso)
        public const byte XISO_PAD_BYTE = 0xFF;
        
        // XISO file modulus for padding (matches extract-xiso)
        public const uint XISO_FILE_MODULUS = 0x10000; // 65536 bytes
        
        // ECMA-119 volume descriptor offsets (matches extract-xiso)
        public const uint ECMA_119_DATA_AREA_START = 0x8000; // Sector 0x10
        public const uint ECMA_119_VOLUME_SPACE_SIZE = ECMA_119_DATA_AREA_START + 80;
        public const uint ECMA_119_VOLUME_SET_SIZE = ECMA_119_DATA_AREA_START + 120;
        public const uint ECMA_119_VOLUME_SET_IDENTIFIER = ECMA_119_DATA_AREA_START + 190;
        public const uint ECMA_119_VOLUME_CREATION_DATE = ECMA_119_DATA_AREA_START + 813;
    }
}
