using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Internal.Decoders
{
    internal abstract class SectorDecoder : ISectorDecoder
    {
        public abstract long TotalSectors();

        public abstract byte[] ReadSector(long sector);

        public bool TryGetXgdInfo(out XgdInfo? xgdInfo)
        {
            var found = false;
            var baseSector = 0U;

            XgdHeader? header = null;

            if (TotalSectors() >= Constants.XGD_MAGIC_SECTOR_XDKI)
            {
                var sector = ReadSector(Constants.XGD_MAGIC_SECTOR_XDKI);
                header = Helpers.GetXgdHeaer(sector);
                if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                {
                    baseSector = Constants.XGD_MAGIC_SECTOR_XDKI - Constants.XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && TotalSectors() >= Constants.XGD_MAGIC_SECTOR_XGD3)
            {
                var sector = ReadSector(Constants.XGD_MAGIC_SECTOR_XGD3);
                header = Helpers.GetXgdHeaer(sector);
                if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                {
                    baseSector = Constants.XGD_MAGIC_SECTOR_XGD3 - Constants.XGD_ISO_BASE_SECTOR;
                    found = true;
                }
            }

            if (found == false && TotalSectors() >= Constants.XGD_MAGIC_SECTOR_XGD2)
            {
                var sector = ReadSector(Constants.XGD_MAGIC_SECTOR_XGD2);
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

        public bool TryGetDefaultXex(out byte[] xbeData)
        {
            xbeData = Array.Empty<byte>();

            if (TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
            {
                return false;
            }

            var rootSector = xgdInfo.RootDirSector;
            var rootSize = xgdInfo.RootDirSize;
            var rootSectors = rootSize >> 11;

            var rootData = new byte[rootSize];
            for (var i = 0; i < rootSectors; i++)
            {
                var sectorData = ReadSector(xgdInfo.BaseSector + rootSector + (uint)i);
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

                    if (currentTreeNode.Offset * 4 >= rootData.Length)
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
                                var buffer = ReadSector(readSector);
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
