﻿using System;
using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal;
using Xbox360Toolkit.Internal.Decoders;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit
{
    public class GODContainerReader : ContainerReader
    {
        private string mFilePath;
        private int mMountCount;
        private SectorDecoder? mSectorDecoder;

        public GODContainerReader(string filePath)
        {
            mFilePath = filePath;
            mMountCount = 0;
        }

        public override SectorDecoder GetDecoder()
        {
            if (mSectorDecoder == null)
            {
                throw new Exception("Container not mounted.");
            }
            return mSectorDecoder;
        }

        public override bool TryMount()
        {
            try
            {
                if (mMountCount > 0)
                {
                    mMountCount++;
                    return true;
                }

                if (File.Exists(mFilePath) == false)
                {
                    return false;
                }

                var dataPath = mFilePath + ".data";
                if (Directory.Exists(dataPath) == false)
                {
                    return false;
                }

                using (var fileStream = new FileStream(mFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
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

                    var startingBlock = (uint)(((contentMetaData.SvodVolumeDescriptor.StartingDataBlock2 << 16) & 0xFF0000) | ((contentMetaData.SvodVolumeDescriptor.StartingDataBlock1 << 8) & 0xFF00) | ((contentMetaData.SvodVolumeDescriptor.StartingDataBlock0) & 0xFF));
                    var numDataBlocks = (uint)(((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks2 << 16) & 0xFF0000) | ((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks1 << 8) & 0xFF00) | ((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks0) & 0xFF));


                    var godDetails = new GODDetails();
                    godDetails.DataPath = dataPath;
                    godDetails.DataFileCount = Helpers.ConvertEndian(contentMetaData.DataFiles);
                    godDetails.IsEnhancedGDF = (contentMetaData.SvodVolumeDescriptor.Features & (1 << 6)) != 0;
                    godDetails.BaseAddress = godDetails.IsEnhancedGDF ? 0x2000u : 0x12000u;
                    godDetails.StartingBlock = startingBlock;
                    godDetails.SectorCount = (numDataBlocks + startingBlock) * 2;

                    mSectorDecoder = new GODSectorDecoder(godDetails);
                    if (mSectorDecoder.Init() == false)
                    {
                        return false;
                    }

                    mMountCount++;
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
        }

        public override void Dismount()
        {
            if (mMountCount == 0)
            {
                return;
            }
            mMountCount--;
        }

        public override int GetMountCount()
        {
            return mMountCount;
        }
    }
}
