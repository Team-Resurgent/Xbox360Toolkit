using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Xbox360Toolkit
{
    public static class XisoUtility
    {
        public const string XEX_FILE_NAME = "default.xex";
        public const string XGD_IMAGE_MAGIC = "MICROSOFT*XBOX*MEDIA";
        public const uint XGD_SECTOR_SIZE = 0x800;
        public const uint XGD_ISO_BASE_SECTOR = 0x20;
        public const uint XGD_MAGIC_SECTOR_XDKI = XGD_ISO_BASE_SECTOR;

        public const uint XGD_MAGIC_SECTOR_XGD2 = 0x1FB40;
        public const uint XGD2_PFI_OFFSET = 0xFD8E800;
        public const uint XGD2_DMI_OFFSET = 0xFD8F000;
        public const uint XGD2_SS_OFFSET = 0xFD8F800;

        public const uint XGD_MAGIC_SECTOR_XGD3 = 0x4120;
        public const uint XGD3_PFI_OFFSET = 0x2076800;
        public const uint XGD3_DMI_OFFSET = 0x2077000;
        public const uint XGD3_SS_OFFSET = 0x2077800;

        public const uint SVOD_START_SECTOR = XGD_ISO_BASE_SECTOR;

        private struct TreeNodeInfo
        {
            public uint DirectorySize { get; set; }
            public long DirectoryPos { get; set; }
            public uint Offset { get; set; }
            public string Path { get; set; }
        };

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public class XgdHeader
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] Magic = Array.Empty<byte>();

            public string MagicString => Helpers.GetUtf8String(Magic);

            public uint RootDirSector;

            public uint RootDirSize;

            public long CreationFileTime;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0x7c8)]
            public byte[] Padding = Array.Empty<byte>();

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] MagicTail = Array.Empty<byte>();

            public string MagicTailString => Helpers.GetUtf8String(MagicTail);
        }

        private static XgdHeader GetXgdHeaer(byte[] sector)
        {
            using var sectorStream = new MemoryStream(sector);
            using var sectorReader = new BinaryReader(sectorStream);
            var header = Helpers.ByteToType<XgdHeader>(sectorReader);
            return header;
        }

        public struct XgdInfo
        {
            public uint BaseSector;
            public uint RootDirSector;
            public uint RootDirSize;
            public DateTime CreationDateTime;
        }

        private static byte[] ReadSector(BinaryReader binaryReader, uint sector)
        {
            binaryReader.BaseStream.Position = sector * XGD_SECTOR_SIZE;
            return binaryReader.ReadBytes((int)XGD_SECTOR_SIZE);
        }

        private static bool GetXgdInfo(BinaryReader binaryReader, ref XgdInfo xgdInfo)
        {
            var found = false;
            var maxSize = binaryReader.BaseStream.Length;
            var baseSector = 0U;

            XgdHeader? header = null;

            if (maxSize > ((XGD_MAGIC_SECTOR_XDKI + 1) * XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(binaryReader, XGD_MAGIC_SECTOR_XDKI);
                header = GetXgdHeaer(sector);
                if (header != null && header.MagicString.Equals(XGD_IMAGE_MAGIC) && header.MagicTailString.Equals(XGD_IMAGE_MAGIC))
                {
                    baseSector = XGD_MAGIC_SECTOR_XDKI - XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && maxSize > ((XGD_MAGIC_SECTOR_XGD3 + 1) * XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(binaryReader, XGD_MAGIC_SECTOR_XGD3);
                header = GetXgdHeaer(sector);
                if (header != null && header.MagicString.Equals(XGD_IMAGE_MAGIC) && header.MagicTailString.Equals(XGD_IMAGE_MAGIC))
                {
                    baseSector = XGD_MAGIC_SECTOR_XGD3 - XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && maxSize > ((XGD_MAGIC_SECTOR_XGD2 + 1) * XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(binaryReader, XGD_MAGIC_SECTOR_XGD2);
                header = GetXgdHeaer(sector);
                if (header != null && header.MagicString.Equals(XGD_IMAGE_MAGIC) && header.MagicTailString.Equals(XGD_IMAGE_MAGIC))
                {
                    baseSector = XGD_MAGIC_SECTOR_XGD2 - XGD_ISO_BASE_SECTOR;
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

            return false;
        }

        public static bool TryGetDefaultXexFromIso(string filePath, out byte[] xbeData)
        {
            xbeData = Array.Empty<byte>();

            if (File.Exists(filePath) == false) 
            {
                return false;
            }

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                var xgdInfo = new XgdInfo();
                if (GetXgdInfo(binaryReader, ref xgdInfo) == false)
                {
                    return false;
                }

                var rootSector = xgdInfo.RootDirSector;
                var rootSize = xgdInfo.RootDirSize;
                var rootOffset = (long)rootSector << 11;

                var treeNodes = new List<TreeNodeInfo>
                {
                    new TreeNodeInfo
                    {
                        DirectorySize = rootSize,
                        DirectoryPos = rootOffset,
                        Offset = 0,
                        Path = string.Empty
                    }
                };

                while (treeNodes.Count > 0)
                {
                    var currentTreeNode = treeNodes[0];
                    treeNodes.RemoveAt(0);

                    if ((currentTreeNode.Offset * 4) >= currentTreeNode.DirectorySize)
                    {
                        continue;
                    }

                    var currentPosition = (xgdInfo.BaseSector << 11) + currentTreeNode.DirectoryPos + currentTreeNode.Offset * 4;
                    fileStream.Position = currentPosition;

                    var left = binaryReader.ReadUInt16();
                    var right = binaryReader.ReadUInt16();
                    var sector = binaryReader.ReadUInt32();
                    var size = binaryReader.ReadUInt32();
                    var attribute = binaryReader.ReadByte();
                    var nameLength = binaryReader.ReadByte();
                    var filenameBytes = binaryReader.ReadBytes(nameLength);

                    var filename = Encoding.ASCII.GetString(filenameBytes);
                    if (filename.Equals(XEX_FILE_NAME, StringComparison.CurrentCultureIgnoreCase))
                    {
                        var readSector = sector + xgdInfo.BaseSector;
                        var result = new byte[size];
                        var processed = 0U;
                        if (size > 0)
                        {
                            while (processed < size)
                            {
                                var buffer = ReadSector(binaryReader, readSector);
                                var bytesToCopy = (uint)Math.Min(size - processed, 2048);
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
                            DirectorySize = currentTreeNode.DirectorySize,
                            DirectoryPos = currentTreeNode.DirectoryPos,
                            Offset = left,
                            Path = currentTreeNode.Path
                        });
                    }
                  
                    if (right != 0)
                    {
                        treeNodes.Add(new TreeNodeInfo
                        {
                            DirectorySize = currentTreeNode.DirectorySize,
                            DirectoryPos = currentTreeNode.DirectoryPos,
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
