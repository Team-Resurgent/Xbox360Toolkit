using System;
using System.IO;
using Xbox360Toolkit.Internal;
using Xbox360Toolkit.Internal.Decoders;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit
{
    public class GodReader : IReader
    {
        private SectorDecoder? mSectorDecoder;

        public bool Mount(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                return false;
            }

            var dataPath = filePath + ".data";
            if (Directory.Exists(dataPath) == false)
            {
                return false;
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                var header = Helpers.ByteToType<XCONTENT_HEADER>(binaryReader);

                var signatureType = Helpers.ConvertEndian(header.SignatureType);
                if (signatureType != (uint)XCONTENT_SIGNATURE_TYPE.LIVE_SIGNED && signatureType != (uint)XCONTENT_SIGNATURE_TYPE.CONSOLE_SIGNED && signatureType != (uint)XCONTENT_SIGNATURE_TYPE.PIRS_SIGNED)
                {
                    return false;
                }

                var contentMetaData = Helpers.ByteToType<XCONTENT_METADATA>(binaryReader);
                var contentType = Helpers.ConvertEndian(contentMetaData.ContentType);
                if (contentType != Constants.NXE_CONTAINER_TYPE && contentType != Constants.GOD_CONTAINER_TYPE)
                {
                    return false;
                }

                var godDetails = new GodDetails();
                godDetails.DataPath = dataPath;
                godDetails.DataFileCount = Helpers.ConvertEndian(contentMetaData.DataFiles);
                godDetails.IsEnhancedGDF = (contentMetaData.SvodVolumeDescriptor.Features & (1 << 6)) != 0;
                godDetails.BaseAddress = godDetails.IsEnhancedGDF ? 0x2000u : 0x12000u;
                godDetails.StartingBlock = (uint)(((contentMetaData.SvodVolumeDescriptor.StartingDataBlock2 << 16) & 0xFF0000) | ((contentMetaData.SvodVolumeDescriptor.StartingDataBlock1 << 8) & 0xFF00) | ((contentMetaData.SvodVolumeDescriptor.StartingDataBlock0) & 0xFF));
                godDetails.SectorCount = (uint)(((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks2 << 16) & 0xFF0000) | ((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks1 << 8) & 0xFF00) | ((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks0) & 0xFF));

                var sectorDecoder = new GodSectorDecoder(godDetails);
                if (sectorDecoder.TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
                {
                    return false;
                }

                mSectorDecoder = sectorDecoder;
                return true;
            }
        }

        public bool TryGetDefaultXex(out byte[] xbeData)
        {
            xbeData = Array.Empty<byte>();

            if (mSectorDecoder == null)
            {
                return false;
            }
            return  mSectorDecoder.TryGetDefaultXex(out xbeData);
        }

        public bool ReadSector(long sector, out byte[] sectorData)
        {
            if (mSectorDecoder == null)
            {
                sectorData = Array.Empty<byte>();
                return false;
            }
            sectorData = mSectorDecoder.ReadSector(sector);
            return true;
        }
    }
}
