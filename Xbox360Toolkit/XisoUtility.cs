using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xbox360Toolkit.Internal;

namespace Xbox360Toolkit
{
    public static class XisoUtility
    {
        private static byte[] ReadSector(BinaryReader binaryReader, uint sector)
        {
            binaryReader.BaseStream.Position = sector * Constants.XGD_SECTOR_SIZE;
            return binaryReader.ReadBytes((int)Constants.XGD_SECTOR_SIZE);
        }

        private static bool TryGetXgdInfo(BinaryReader binaryReader, out XgdInfo? xgdInfo)
        {
            var found = false;
            var maxSize = binaryReader.BaseStream.Length;
            var baseSector = 0U;

            XgdHeader? header = null;

            if (maxSize > ((Constants.XGD_MAGIC_SECTOR_XDKI + 1) * Constants.XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(binaryReader, Constants.XGD_MAGIC_SECTOR_XDKI);
                header = Helpers.GetXgdHeaer(sector);
                if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                {
                    baseSector = Constants.XGD_MAGIC_SECTOR_XDKI - Constants.XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && maxSize > ((Constants.XGD_MAGIC_SECTOR_XGD3 + 1) * Constants.XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(binaryReader, Constants.XGD_MAGIC_SECTOR_XGD3);
                header = Helpers.GetXgdHeaer(sector);
                if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                {
                    baseSector = Constants.XGD_MAGIC_SECTOR_XGD3 - Constants.XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && maxSize > ((Constants.XGD_MAGIC_SECTOR_XGD2 + 1) * Constants.XGD_SECTOR_SIZE))
            {
                var sector = ReadSector(binaryReader, Constants.XGD_MAGIC_SECTOR_XGD2);
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

        public static bool IsIso(string filePath)
        {
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var binaryReader = new BinaryReader(fileStream))
            {
                return TryGetXgdInfo(binaryReader, out var xgdInfo);
            }
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
                if (TryGetXgdInfo(binaryReader, out var xgdInfo) == false || xgdInfo == null)
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
                        Offset = 0,
                        Path = string.Empty
                    }
                };

                while (treeNodes.Count > 0)
                {
                    var currentTreeNode = treeNodes[0];
                    treeNodes.RemoveAt(0);

                    if ((currentTreeNode.Offset * 4) >= rootSize)
                    {
                        continue;
                    }

                    fileStream.Position = (xgdInfo.BaseSector << 11) + rootOffset + currentTreeNode.Offset * 4;

                    var left = binaryReader.ReadUInt16();
                    var right = binaryReader.ReadUInt16();
                    var sector = binaryReader.ReadUInt32();
                    var size = binaryReader.ReadUInt32();
                    var attribute = binaryReader.ReadByte();
                    var nameLength = binaryReader.ReadByte();
                    var filenameBytes = binaryReader.ReadBytes(nameLength);

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
