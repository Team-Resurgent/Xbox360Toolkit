using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mime;
using System.Runtime.InteropServices;
using System.Text;
using Xbox360Toolkit.Internal;
using static Xbox360Toolkit.XisoUtility;

namespace Xbox360Toolkit
{
    public static class GodUtility
    {
        private enum XCONTENT_SIGNATURE_TYPE
        {
            CONSOLE_SIGNED = 0x434F4E20,    // CON
            LIVE_SIGNED = 0x4C495645,       // LIVE
            PIRS_SIGNED = 0x50495253        // PIRS
        }

        private enum XCONTENT_VOLUME_TYPE
        {
            STFS_VOLUME = 0x0,
            SVOD_VOLUME = 0x1
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct XCONTENT_SIGNATURE
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x100)]
            public byte[] Signature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x128)]
            public byte[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct XCONTENT_LICENSE
        {
            public ulong LicenseeId;
            public uint LicenseBits;
            public uint LicenseFlags;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct XCONTENT_HEADER
        {
            public uint SignatureType;
            public XCONTENT_SIGNATURE Signature;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
            public XCONTENT_LICENSE[] LicenseDescriptors;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x14)]
            public byte[] ContentId;
            public uint SizeOfHeaders;

            //public bool IsLiveSigned()
            //{
            //    return Utility.SwapUInt32((UInt32)SignatureType) == (UInt32)XCONTENT_SIGNATURE_TYPE.LIVE_SIGNED;
            //}
            //public bool IsConsoleSigned()
            //{
            //    return Utility.SwapUInt32((UInt32)SignatureType) == (UInt32)XCONTENT_SIGNATURE_TYPE.CONSOLE_SIGNED;
            //}
            //public bool IsPirsSigned()
            //{
            //    return Utility.SwapUInt32((UInt32)SignatureType) == (UInt32)XCONTENT_SIGNATURE_TYPE.PIRS_SIGNED;
            //}
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct XEX_EXECUTION_ID
        {
            public uint MediaId;
            public uint Version;
            public uint BaseVersion;
            public uint TitleId;
            public byte Platform;
            public byte ExecutableType;
            public byte DiscNum;
            public byte DiscsInSet;
            public uint SaveGameID;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SVOD_DEVICE_DESCRIPTOR
        {
            public byte DescriptorLength;
            public byte BlockCacheElementCount;
            public byte WorkerThreadProcessor;
            public byte WorkerThreadPriority;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x14)]
            public byte[] FirstFragmentHashEntry;
            public byte Features;
            public byte NumberOfDataBlocks2;
            public byte NumberOfDataBlocks1;
            public byte NumberOfDataBlocks0;
            public byte StartingDataBlock0;
            public byte StartingDataBlock1;
            public byte StartingDataBlock2;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x5)]
            public byte[] Reserved;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct XCONTENT_METADATA_MEDIA_DATA
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
            public byte[] SeriesId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x10)]
            public byte[] SeasonId;
            public ushort SeasonNumber;
            public ushort EpisodeNumber;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct StringType
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x100)]
            public byte[] Value;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct XCONTENT_METADATA
        {
            public uint ContentType;
            public uint ContentMetadataVersion;
            public long ContentSize;
            public XEX_EXECUTION_ID ExecutionId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x5)]
            public byte[] ConsoleId;
            public long Creator;
            public SVOD_DEVICE_DESCRIPTOR SvodVolumeDescriptor;
            public uint DataFiles;
            public long DataFilesSize;
            public uint VolumeType;
            public long OnlineCreator;
            public uint Category;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x20)]
            public byte[] Reserved2;
            public XCONTENT_METADATA_MEDIA_DATA Data;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x14)]
            public byte[] DeviceId;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x9)]
            public StringType[] DisplayName;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x9)]
            public StringType[] Description;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x80)]
            public byte[] Publisher;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x80)]
            public byte[] TitleName;
            public byte Flags;
            public uint ThumbnailSize;
            public uint TitleThumbnailSize;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3D00)]
            public byte[] Thumbnail;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3)]
            public StringType[] DisplayNameEx;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3D00)]
            public byte[] TitleThumbnail;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x3)]
            public StringType[] DescriptionEx;
        }

        private struct GodDetails
        {
            public string DataPath;
            public uint DataFileCount;
            public uint BaseAddress;
            public uint StartingBlock;
            public uint SectorCount;
            public bool IsEnhancedGDF;
        }

        private static uint SectorToAddress(GodDetails godDetails, uint sector, out uint dataFileIndex)
        {
            if (sector == Constants.SVOD_START_SECTOR || sector == Constants.SVOD_START_SECTOR + 1)
            {
                dataFileIndex = 0;
                return godDetails.BaseAddress + ((sector - Constants.SVOD_START_SECTOR) * Constants.XGD_SECTOR_SIZE);
            }

            var adjustedSector = sector - (godDetails.StartingBlock * 2) + (godDetails.IsEnhancedGDF ? 2U : 0U);
            dataFileIndex = adjustedSector / 0x14388;
            if (dataFileIndex > godDetails.DataFileCount)
            {
                dataFileIndex = 0;
            }
            var dataSector = adjustedSector % 0x14388;
            var dataBlock = dataSector / 0x198;           
            dataSector %= 0x198;    

            var dataFileOffset = (dataSector + (dataBlock * 0x198)) * 0x800;
            dataFileOffset += 0x1000;  
            dataFileOffset += (dataBlock * 0x1000) + 0x1000;  
            return dataFileOffset;
        }

        private static byte[] ReadSector(GodDetails godDetails, uint sector)
        {
            var dataOffset = SectorToAddress(godDetails, sector, out var dataFileIndex);
            var filePath = Path.Combine(godDetails.DataPath, string.Format("Data{0:D4}", dataFileIndex));
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                binaryReader.BaseStream.Position = dataOffset;
                return binaryReader.ReadBytes((int)Constants.XGD_SECTOR_SIZE);
            }
        }

        private static bool TryGetXgdInfo(GodDetails godDetails, out XgdInfo? xgdInfo)
        {
            var found = false;
            var maxSize = godDetails.SectorCount * Constants.XGD_SECTOR_SIZE;
            var baseSector = 0U;

            XgdHeader? header = null;

            if (maxSize > ((Constants.XGD_MAGIC_SECTOR_XDKI + 1) * Constants.XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(godDetails, Constants.XGD_MAGIC_SECTOR_XDKI);
                header = Helpers.GetXgdHeaer(sector);
                if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                {
                    baseSector = Constants.XGD_MAGIC_SECTOR_XDKI - Constants.XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && maxSize > ((Constants.XGD_MAGIC_SECTOR_XGD3 + 1) * Constants.XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(godDetails, Constants.XGD_MAGIC_SECTOR_XGD3);
                header = Helpers.GetXgdHeaer(sector);
                if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                {
                    baseSector = Constants.XGD_MAGIC_SECTOR_XGD3 - Constants.XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && maxSize > ((Constants.XGD_MAGIC_SECTOR_XGD2 + 1) * Constants.XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(godDetails, Constants.XGD_MAGIC_SECTOR_XGD2);
                header = Helpers.GetXgdHeaer(sector);
                if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                {
                    baseSector = Constants.XGD_MAGIC_SECTOR_XGD2 - Constants.XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == true && header != null)
            {
                xgdInfo = new XgdInfo
                {
                    BaseSector = baseSector,
                    RootDirSector = header.RootDirSector,
                    RootDirSize = header.RootDirSize,
                    CreationDateTime = DateTime.FromFileTime(header.CreationFileTime)
                };
                return true;
            }

            xgdInfo = null;
            return false;
        }

        public static bool IsGod(string filePath)
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

                return true;
            }
        }

        public static bool TryGetDefaultXexFromGod(string filePath, out byte[] xbeData)
        {
            xbeData = Array.Empty<byte>();

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
                godDetails.BaseAddress = godDetails.IsEnhancedGDF ? (uint)0x2000 : (uint)0x12000;
                godDetails.StartingBlock = (uint)(((contentMetaData.SvodVolumeDescriptor.StartingDataBlock2 << 16) & 0xFF0000) | ((contentMetaData.SvodVolumeDescriptor.StartingDataBlock1 << 8) & 0xFF00) | ((contentMetaData.SvodVolumeDescriptor.StartingDataBlock0) & 0xFF));
                godDetails.SectorCount = (uint)(((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks2 << 16) & 0xFF0000) | ((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks1 << 8) & 0xFF00) | ((contentMetaData.SvodVolumeDescriptor.NumberOfDataBlocks0) & 0xFF));

                if (TryGetXgdInfo(godDetails, out var xgdInfo) == false || xgdInfo == null)
                {
                    return false;
                }

                var rootSector = xgdInfo.RootDirSector;
                var rootSize = xgdInfo.RootDirSize;
                var rootSectors = rootSize >> 11;

                var rootData = new byte[rootSize];
                for (var i = 0; i < rootSectors; i++)
                {
                    var sectorData = ReadSector(godDetails, xgdInfo.BaseSector + rootSector + (uint)i);
                    Array.Copy(sectorData, 0, rootData, i * Constants.XGD_SECTOR_SIZE, Constants.XGD_SECTOR_SIZE);
                }

                var treeNodes = new List<TreeNodeInfo>
                {
                    new TreeNodeInfo
                    {
                        Offset = 0,
                        Path = string.Empty
                    }
                };

                using (var rootDataStream = new MemoryStream(rootData))
                using (var rootDataReader = new BinaryReader(rootDataStream))
                {
                    while (treeNodes.Count > 0)
                    {
                        var currentTreeNode = treeNodes[0];
                        treeNodes.RemoveAt(0);

                        if ((currentTreeNode.Offset * 4) >= rootData.Length)
                        {
                            continue;
                        }

                        rootDataStream.Position = currentTreeNode.Offset * 4;

                        var left = rootDataReader.ReadUInt16();
                        var right = rootDataReader.ReadUInt16();
                        var sector = rootDataReader.ReadUInt32();
                        var size = rootDataReader.ReadUInt32();
                        var attribute = rootDataReader.ReadByte();
                        var nameLength = rootDataReader.ReadByte();
                        var filenameBytes = rootDataReader.ReadBytes(nameLength);

                        var filename = Encoding.ASCII.GetString(filenameBytes);
                        if (filename.Equals(Constants.XEX_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                        {
                            var readSector = sector + xgdInfo.BaseSector;
                            var result = new byte[size];
                            var processed = 0U;
                            if (size > 0)
                            {
                                while (processed < size)
                                {
                                    var buffer = ReadSector(godDetails, readSector);
                                    var bytesToCopy = Math.Min(size - processed, 2048);
                                    Array.Copy(buffer, 0, result, processed, bytesToCopy);
                                    readSector++;
                                    processed += bytesToCopy;
                                }
                            }
                            xbeData = result;
                            return true;
                        }

                        if (left == 0xFFFF)
                        {
                            continue;
                        }

                        if (left != 0)
                        {
                            treeNodes.Add(new TreeNodeInfo
                            {
                                Offset = left,
                                Path = currentTreeNode.Path
                            });
                        }

                        if (right != 0)
                        {
                            treeNodes.Add(new TreeNodeInfo
                            {
                                Offset = right,
                                Path = currentTreeNode.Path
                            });
                        }
                    }

                    return false;
                }
            }
        }

    }
}
