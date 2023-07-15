using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xbox360Toolkit.Internal;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit.Interface
{
    public abstract class SectorDecoder : ISectorDecoder
    {
        public abstract uint TotalSectors();

        public uint SectorSize()
        {
            return Constants.XGD_SECTOR_SIZE;
        }

        public abstract bool TryReadSector(long sector, out byte[] sectorData);

        internal bool TryGetXgdInfo(out XgdInfo xgdInfo)
        {
            var found = false;
            var baseSector = 0U;

            var header = new XgdHeader();

            if (TotalSectors() >= Constants.XGD_MAGIC_SECTOR_XDKI)
            {
                if (TryReadSector(Constants.XGD_MAGIC_SECTOR_XDKI, out var sector) == true)
                {
                    header = Helpers.GetXgdHeaer(sector);
                    if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                    {
                        baseSector = Constants.XGD_MAGIC_SECTOR_XDKI - Constants.XGD_ISO_BASE_SECTOR;
                        found = true;
                    }
                }
            }

            if (found == false && TotalSectors() >= Constants.XGD_MAGIC_SECTOR_XGD1)
            {
                if (TryReadSector(Constants.XGD_MAGIC_SECTOR_XGD1, out var sector) == true)
                {
                    header = Helpers.GetXgdHeaer(sector);
                    if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                    {
                        baseSector = Constants.XGD_MAGIC_SECTOR_XGD1 - Constants.XGD_ISO_BASE_SECTOR;
                        found = true;
                    }
                }
            }

            if (found == false && TotalSectors() >= Constants.XGD_MAGIC_SECTOR_XGD3)
            {
                if (TryReadSector(Constants.XGD_MAGIC_SECTOR_XGD3, out var sector) == true)
                {
                    header = Helpers.GetXgdHeaer(sector);
                    if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                    {
                        baseSector = Constants.XGD_MAGIC_SECTOR_XGD3 - Constants.XGD_ISO_BASE_SECTOR;
                        found = true;
                    }
                }
            }

            if (found == false && TotalSectors() >= Constants.XGD_MAGIC_SECTOR_XGD2)
            {
                if (TryReadSector(Constants.XGD_MAGIC_SECTOR_XGD2, out var sector) == true)
                {
                    header = Helpers.GetXgdHeaer(sector);
                    if (header != null && Helpers.GetUtf8String(header.Magic).Equals(Constants.XGD_IMAGE_MAGIC) && Helpers.GetUtf8String(header.MagicTail).Equals(Constants.XGD_IMAGE_MAGIC))
                    {
                        baseSector = Constants.XGD_MAGIC_SECTOR_XGD2 - Constants.XGD_ISO_BASE_SECTOR;
                        found = true;
                    }
                }
            }

            xgdInfo = new XgdInfo();

            if (found == true && header != null)
            {
                xgdInfo.BaseSector = baseSector;
                xgdInfo.RootDirSector = header.RootDirSector;
                xgdInfo.RootDirSize = header.RootDirSize;
                xgdInfo.CreationDateTime = DateTime.FromFileTime(header.CreationFileTime);
                return true;
            }

            return false;
        }



        public bool TryGetDataSectors(out HashSet<uint> dataSectors)
        {
            dataSectors = new HashSet<uint>();

            if (TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
            {
                return false;
            }

            dataSectors.Add(xgdInfo.BaseSector + Constants.XGD_ISO_BASE_SECTOR);
            dataSectors.Add(xgdInfo.BaseSector + Constants.XGD_ISO_BASE_SECTOR + 1);

            var rootSectors = xgdInfo.RootDirSize / Constants.XGD_SECTOR_SIZE;
            var rootData = new byte[xgdInfo.RootDirSize];
            for (var i = 0; i < rootSectors; i++)
            {
                var currentRootSector = xgdInfo.BaseSector + xgdInfo.RootDirSector + (uint)i;
                dataSectors.Add(currentRootSector);
                if (TryReadSector(currentRootSector, out var sectorData) == false)
                {
                    return false;
                }
                Array.Copy(sectorData, 0, rootData, i * Constants.XGD_SECTOR_SIZE, Constants.XGD_SECTOR_SIZE);
            }

            var treeNodes = new List<TreeNodeInfo>
            {
                new TreeNodeInfo
                {
                    DirectoryData = rootData,
                    Offset = 0,
                    Path = string.Empty
                }
            };

            while (treeNodes.Count > 0)
            {
                var currentTreeNode = treeNodes[0];
                treeNodes.RemoveAt(0);

                using (var directoryDataStream = new MemoryStream(currentTreeNode.DirectoryData))
                using (var directoryDataDataReader = new BinaryReader(directoryDataStream))
                {

                    if (currentTreeNode.Offset * 4 >= directoryDataStream.Length)
                    {
                        continue;
                    }

                    directoryDataStream.Position = currentTreeNode.Offset * 4;

                    var left = directoryDataDataReader.ReadUInt16();
                    var right = directoryDataDataReader.ReadUInt16();
                    var sector = directoryDataDataReader.ReadUInt32();
                    var size = directoryDataDataReader.ReadUInt32();
                    var attribute = directoryDataDataReader.ReadByte();
                    var nameLength = directoryDataDataReader.ReadByte();
                    var filenameBytes = directoryDataDataReader.ReadBytes(nameLength);
                    var filename = Encoding.ASCII.GetString(filenameBytes);

                    if (left == 0xFFFF)
                    {
                        continue;
                    }

                    if (left != 0)
                    {
                        treeNodes.Add(new TreeNodeInfo
                        {
                            DirectoryData = currentTreeNode.DirectoryData,
                            Offset = left,
                            Path = currentTreeNode.Path
                        });
                    }

                    if (size > 0)
                    {
                       
                        if ((attribute & 0x10) != 0)
                        {
                            var directorySectors = size / Constants.XGD_SECTOR_SIZE;
                            var directoryData = new byte[size];
                            for (var i = 0; i < directorySectors; i++)
                            {
                                var currentDirectorySector = xgdInfo.BaseSector + sector + (uint)i;
                                dataSectors.Add(currentDirectorySector);
                                if (TryReadSector(currentDirectorySector, out var sectorData) == false)
                                {
                                    return false;
                                }
                                Array.Copy(sectorData, 0, directoryData, i * Constants.XGD_SECTOR_SIZE, Constants.XGD_SECTOR_SIZE);
                            }

                            treeNodes.Add(new TreeNodeInfo
                            {
                                DirectoryData = directoryData,
                                Offset = 0,
                                Path = Path.Combine(currentTreeNode.Path, filename)
                            });
                        }
                        else
                        {
                            var fileSectors = Helpers.RoundToMultiple(size, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                            for (var i = 0; i < fileSectors; i++)
                            {
                                var currentFileSector = xgdInfo.BaseSector + sector + (uint)i;
                                dataSectors.Add(currentFileSector);
                            }
                        }
                    }

                    if (right != 0)
                    {
                        treeNodes.Add(new TreeNodeInfo
                        {
                            DirectoryData = currentTreeNode.DirectoryData,
                            Offset = right,
                            Path = currentTreeNode.Path
                        });
                    }
                }
            }
            return true;
        }



        //public bool TryGetDataSectors(out HashSet<uint> dataSectors)
        //{
        //    dataSectors = new HashSet<uint>();

        //    if (TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
        //    {
        //        return false;
        //    }

        //    dataSectors.Add(xgdInfo.BaseSector + Constants.XGD_ISO_BASE_SECTOR);
        //    dataSectors.Add(xgdInfo.BaseSector + Constants.XGD_ISO_BASE_SECTOR + 1);

        //    var rootSector = xgdInfo.RootDirSector;
        //    var rootSize = xgdInfo.RootDirSize;
        //    var rootSectors = rootSize >> 11;

        //    var rootData = new byte[rootSize];
        //    for (var i = 0; i < rootSectors; i++)
        //    {
        //        var currentRootSector = xgdInfo.BaseSector + rootSector + (uint)i;
        //        dataSectors.Add(currentRootSector);
        //        var sectorData = ReadSector(currentRootSector);
        //        Array.Copy(sectorData, 0, rootData, i * Constants.XGD_SECTOR_SIZE, Constants.XGD_SECTOR_SIZE);
        //    }

        //    var treeNodes = new List<TreeNodeInfo>
        //    {
        //        new TreeNodeInfo
        //        {
        //            DirectorySector = rootSector,
        //            DirectorySectors = rootSectors,
        //            Offset = 0,
        //            Path = string.Empty
        //        }
        //    };

        //        while (treeNodes.Count > 0)
        //        {
        //            var currentTreeNode = treeNodes[0];
        //            treeNodes.RemoveAt(0);

        //            if (currentTreeNode.Offset * 4 >= rootData.Length)
        //            {
        //                continue;
        //            }

        //            rootDataStream.Position = currentTreeNode.Offset * 4;

        //            var left = rootDataReader.ReadUInt16();
        //            var right = rootDataReader.ReadUInt16();
        //            var sector = rootDataReader.ReadUInt32();
        //            var size = rootDataReader.ReadUInt32();
        //            var attribute = rootDataReader.ReadByte();
        //            var nameLength = rootDataReader.ReadByte();
        //            var filenameBytes = rootDataReader.ReadBytes(nameLength);

        //            var filename = Encoding.ASCII.GetString(filenameBytes);
        //            var isXbe = filename.Equals(Constants.XBE_FILE_NAME, StringComparison.CurrentCultureIgnoreCase);
        //            var isXex = filename.Equals(Constants.XEX_FILE_NAME, StringComparison.CurrentCultureIgnoreCase);
        //            //if (isXbe || isXex)
        //            //{
        //            //    containerType = isXbe ? ContainerType.XboxOriginal : ContainerType.Xbox360;

        //            //    var readSector = sector + xgdInfo.BaseSector;
        //            //    var result = new byte[size];
        //            //    var processed = 0U;
        //            //    if (size > 0)
        //            //    {
        //            //        while (processed < size)
        //            //        {
        //            //            var buffer = ReadSector(readSector);
        //            //            var bytesToCopy = Math.Min(size - processed, 2048);
        //            //            Array.Copy(buffer, 0, result, processed, bytesToCopy);
        //            //            readSector++;
        //            //            processed += bytesToCopy;
        //            //        }
        //            //    }
        //            //    defaultData = result;
        //            //    return true;
        //            //}

        //            if (left == 0xFFFF)
        //            {
        //                continue;
        //            }

        //            if (left != 0)
        //            {
        //                treeNodes.Add(new TreeNodeInfo
        //                {
        //                    Offset = left,
        //                    Path = currentTreeNode.Path
        //                });
        //            }

        //            if ((attribute & 0x10) != 0)
        //            {
        //                if (size > 0)
        //                {
        //                    treeNodes.Add(new TreeNodeInfo
        //                    {
        //                        DirectorySize = size,
        //                        DirectoryPos = sector << 11,
        //                        Offset = 0,
        //                        Path = Path.Combine(currentTreeNode.Path, filename)
        //                    });
        //                }
        //            }
        //            else
        //            {
        //                if (size > 0)
        //                {
        //                    //for (var i = (sectorOffset + sector); i < (sectorOffset + sector) + ((size + 2047) >> 11); i++)
        //                    //{
        //                    //    dataSectors.Add((uint)i);
        //                    //}
        //                }
        //            }

        //            if (right != 0)
        //            {
        //                treeNodes.Add(new TreeNodeInfo
        //                {
        //                    Offset = right,
        //                    Path = currentTreeNode.Path
        //                });
        //            }
        //        }

        //        return false;
        //    }
        //}

        //public static HashSet<uint> GetDataSectorsFromXiso(IImageInput input, Action<float>? progress, CancellationToken cancellationToken)
        //{
        //    if (progress != null)
        //    {
        //        progress(0);
        //    }

        //    var sectorOffset = input.TotalSectors == Constants.RedumpSectors ? Constants.VideoSectors : 0U;

        //    var dataSectors = new HashSet<uint>();

        //    var position = 20U;
        //    var headerSector = (uint)sectorOffset + 0x20U;
        //    dataSectors.Add(headerSector);
        //    dataSectors.Add(headerSector + 1);
        //    position += headerSector << 11;

        //    var rootSector = input.ReadUint32(position);
        //    var rootSize = input.ReadUint32(position + 4);
        //    var rootOffset = (long)rootSector << 11;

        //    var treeNodes = new List<TreeNodeInfo>
        //    {
        //        new TreeNodeInfo
        //        {
        //            DirectorySize = rootSize,
        //            DirectoryPos = rootOffset,
        //            Offset = 0,
        //            Path = string.Empty
        //        }
        //    };

        //    var totalNodes = 1;
        //    var processedNodes = 0;

        //    while (treeNodes.Count > 0)
        //    {
        //        var currentTreeNode = treeNodes[0];
        //        treeNodes.RemoveAt(0);
        //        processedNodes++;

        //        var currentPosition = (sectorOffset << 11) + currentTreeNode.DirectoryPos + currentTreeNode.Offset * 4;

        //        for (var i = currentPosition >> 11; i < (currentPosition >> 11) + ((currentTreeNode.DirectorySize - (currentTreeNode.Offset * 4) + 2047) >> 11); i++)
        //        {
        //            dataSectors.Add((uint)i);
        //        }

        //        if ((currentTreeNode.Offset * 4) >= currentTreeNode.DirectorySize)
        //        {
        //            continue;
        //        }

        //        var left = input.ReadUint16(currentPosition);
        //        var right = input.ReadUint16(currentPosition + 2);
        //        var sector = (long)input.ReadUint32(currentPosition + 4);
        //        var size = input.ReadUint32(currentPosition + 8);
        //        var attribute = input.ReadByte(currentPosition + 12);

        //        var nameLength = input.ReadByte(currentPosition + 13);
        //        var filenameBytes = input.ReadBytes(currentPosition + 14, nameLength);

        //        var filename = Encoding.ASCII.GetString(filenameBytes);
        //        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(1252);
        //        if (encoding != null)
        //        {
        //            filename = encoding.GetString(filenameBytes);
        //        }

        //        if (left == 0xFFFF)
        //        {
        //            continue;
        //        }

        //        if (left != 0)
        //        {
        //            treeNodes.Add(new TreeNodeInfo
        //            {
        //                DirectorySize = currentTreeNode.DirectorySize,
        //                DirectoryPos = currentTreeNode.DirectoryPos,
        //                Offset = left,
        //                Path = currentTreeNode.Path
        //            });
        //            totalNodes++;
        //        }

        //        if ((attribute & 0x10) != 0)
        //        {
        //            if (size > 0)
        //            {
        //                treeNodes.Add(new TreeNodeInfo
        //                {
        //                    DirectorySize = size,
        //                    DirectoryPos = sector << 11,
        //                    Offset = 0,
        //                    Path = Path.Combine(currentTreeNode.Path, filename)
        //                });
        //                totalNodes++;
        //            }
        //        }
        //        else
        //        {
        //            if (size > 0)
        //            {
        //                for (var i = (sectorOffset + sector); i < (sectorOffset + sector) + ((size + 2047) >> 11); i++)
        //                {
        //                    dataSectors.Add((uint)i);
        //                }
        //            }
        //        }

        //        if (right != 0)
        //        {
        //            treeNodes.Add(new TreeNodeInfo
        //            {
        //                DirectorySize = currentTreeNode.DirectorySize,
        //                DirectoryPos = currentTreeNode.DirectoryPos,
        //                Offset = right,
        //                Path = currentTreeNode.Path
        //            });
        //            totalNodes++;
        //        }

        //        if (progress != null)
        //        {
        //            progress(processedNodes / (float)totalNodes);
        //        }

        //        if (cancellationToken.IsCancellationRequested)
        //        {
        //            break;
        //        }
        //    }

        //    return dataSectors;
        //}

        //public static bool CreateCCI(IImageInput input, string outputPath, string name, string extension, bool scrub, bool trimmedScrub, Action<int, float>? progress, CancellationToken cancellationToken)
        //{
        //    if (progress != null)
        //    {
        //        progress(0, 0);
        //    }

        //    Action<float> progress1 = (percent) => {
        //        if (progress != null)
        //        {
        //            progress(0, percent);
        //        }
        //    };

        //    Action<float> progress2 = (percent) => {
        //        if (progress != null)
        //        {
        //            progress(1, percent);
        //        }
        //    };

        //    var endSector = input.TotalSectors;
        //    var dataSectors = new HashSet<uint>();
        //    if (scrub)
        //    {
        //        dataSectors = GetDataSectorsFromXiso(input, progress1, cancellationToken);

        //        if (trimmedScrub)
        //        {
        //            endSector = Math.Min(dataSectors.Max() + 1, input.TotalSectors);
        //        }

        //        var securitySectors = GetSecuritySectorsFromXiso(input, dataSectors, false, progress2, cancellationToken).ToArray();
        //        for (var i = 0; i < securitySectors.Length; i++)
        //        {
        //            dataSectors.Add(securitySectors[i]);
        //        }
        //    }

        //    var sectorOffset = input.TotalSectors == Constants.RedumpSectors ? Constants.VideoSectors : 0U;

        //    var splitMargin = 0xFF000000L;
        //    var emptySector = new byte[2048];
        //    var compressedData = new byte[2048];
        //    var sectorsWritten = (uint)sectorOffset;
        //    var iteration = 0;

        //    while (sectorsWritten < endSector)
        //    {
        //        var indexInfos = new List<IndexInfo>();

        //        var outputFile = Path.Combine(outputPath, iteration > 0 ? $"{name}.{iteration + 1}{extension}" : $"{name}{extension}");
        //        var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write);
        //        var outputWriter = new BinaryWriter(outputStream);

        //        uint header = 0x4D494343U;
        //        outputWriter.Write(header);

        //        uint headerSize = 32;
        //        outputWriter.Write(headerSize);

        //        ulong uncompressedSize = (ulong)0;
        //        outputWriter.Write(uncompressedSize);

        //        ulong indexOffset = (ulong)0;
        //        outputWriter.Write(indexOffset);

        //        uint blockSize = 2048;
        //        outputWriter.Write(blockSize);

        //        byte version = 1;
        //        outputWriter.Write(version);

        //        byte indexAlignment = 2;
        //        outputWriter.Write(indexAlignment);

        //        ushort unused = 0;
        //        outputWriter.Write(unused);

        //        var splitting = false;
        //        var sectorCount = 0U;
        //        while (sectorsWritten < endSector)
        //        {
        //            var writeSector = true;
        //            if (scrub)
        //            {
        //                writeSector = dataSectors.Contains(sectorsWritten);
        //            }

        //            var sectorToWrite = writeSector == true ? input.ReadSectors(sectorsWritten, 1) : emptySector;

        //            var compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(sectorToWrite, compressedData, K4os.Compression.LZ4.LZ4Level.L12_MAX);
        //            if (compressedSize > 0 && compressedSize < (2048 - (4 + (1 << indexAlignment))))
        //            {
        //                var multiple = (1 << indexAlignment);
        //                var padding = ((compressedSize + 1 + multiple - 1) / multiple * multiple) - (compressedSize + 1);
        //                outputWriter.Write((byte)padding);
        //                outputWriter.Write(compressedData, 0, compressedSize);
        //                if (padding != 0)
        //                {
        //                    outputWriter.Write(new byte[padding]);
        //                }
        //                indexInfos.Add(new IndexInfo { Value = (ushort)(compressedSize + 1 + padding), Compressed = true });
        //            }
        //            else
        //            {
        //                outputWriter.Write(sectorToWrite);
        //                indexInfos.Add(new IndexInfo { Value = 2048, Compressed = false });
        //            }

        //            uncompressedSize += 2048;
        //            sectorsWritten++;
        //            sectorCount++;

        //            if (outputStream.Position > splitMargin)
        //            {
        //                splitting = true;
        //                break;
        //            }

        //            if (progress != null)
        //            {
        //                progress(2, sectorsWritten / (float)(endSector - sectorOffset));
        //            }

        //            if (cancellationToken.IsCancellationRequested)
        //            {
        //                break;
        //            }
        //        }

        //        if (cancellationToken.IsCancellationRequested)
        //        {
        //            outputStream.Dispose();
        //            outputWriter.Dispose();
        //            return true;
        //        }

        //        indexOffset = (ulong)outputStream.Position;

        //        var position = (ulong)headerSize;
        //        for (var i = 0; i < indexInfos.Count; i++)
        //        {
        //            var index = (uint)(position >> indexAlignment) | (indexInfos[i].Compressed ? 0x80000000U : 0U);
        //            outputWriter.Write(index);
        //            position += indexInfos[i].Value;
        //        }
        //        var indexEnd = (uint)(position >> indexAlignment);
        //        outputWriter.Write(indexEnd);

        //        outputStream.Position = 8;
        //        outputWriter.Write(uncompressedSize);
        //        outputWriter.Write(indexOffset);

        //        outputStream.Dispose();
        //        outputWriter.Dispose();

        //        if (splitting)
        //        {
        //            File.Move(outputFile, Path.Combine(outputPath, $"{name}.{iteration + 1}{extension}"));
        //        }

        //        iteration++;
        //    }

        //    return true;
        //}



        public bool TryGetDefault(out byte[] defaultData, out ContainerType containerType)
        {
            defaultData = Array.Empty<byte>();
            containerType = ContainerType.Unknown;

            if (TryGetXgdInfo(out var xgdInfo) == false || xgdInfo == null)
            {
                return false;
            }

            var rootSectors = xgdInfo.RootDirSize / Constants.XGD_SECTOR_SIZE;
            var rootData = new byte[xgdInfo.RootDirSize];
            for (var i = 0; i < rootSectors; i++)
            {
                if (TryReadSector(xgdInfo.BaseSector + xgdInfo.RootDirSector + (uint)i, out var sectorData) == false)
                {
                    return false;
                }
                Array.Copy(sectorData, 0, rootData, i * Constants.XGD_SECTOR_SIZE, Constants.XGD_SECTOR_SIZE);
            }

            var treeNodes = new List<TreeNodeInfo>
            {
                new TreeNodeInfo
                {
                    DirectoryData = rootData,
                    Offset = 0,
                    Path = string.Empty
                }
            };

            while (treeNodes.Count > 0)
            {
                var currentTreeNode = treeNodes[0];
                treeNodes.RemoveAt(0);

                using (var directoryDataStream = new MemoryStream(currentTreeNode.DirectoryData))
                using (var directoryDataDataReader = new BinaryReader(directoryDataStream))
                {

                    if (currentTreeNode.Offset * 4 >= directoryDataStream.Length)
                    {
                        continue;
                    }

                    directoryDataStream.Position = currentTreeNode.Offset * 4;

                    var left = directoryDataDataReader.ReadUInt16();
                    var right = directoryDataDataReader.ReadUInt16();
                    var sector = directoryDataDataReader.ReadUInt32();
                    var size = directoryDataDataReader.ReadUInt32();
                    var attribute = directoryDataDataReader.ReadByte();
                    var nameLength = directoryDataDataReader.ReadByte();
                    var filenameBytes = directoryDataDataReader.ReadBytes(nameLength);

                    var filename = Encoding.ASCII.GetString(filenameBytes);
                    var isXbe = filename.Equals(Constants.XBE_FILE_NAME, StringComparison.CurrentCultureIgnoreCase);
                    var isXex = filename.Equals(Constants.XEX_FILE_NAME, StringComparison.CurrentCultureIgnoreCase);
                    if (isXbe || isXex)
                    {
                        containerType = isXbe ? ContainerType.XboxOriginal : ContainerType.Xbox360;

                        var readSector = sector + xgdInfo.BaseSector;
                        var result = new byte[size];
                        var processed = 0U;
                        if (size > 0)
                        {
                            while (processed < size)
                            {
                                if (TryReadSector(readSector, out var buffer) == false)
                                {
                                    return false;
                                }
                                var bytesToCopy = Math.Min(size - processed, 2048);
                                Array.Copy(buffer, 0, result, processed, bytesToCopy);
                                readSector++;
                                processed += bytesToCopy;
                            }
                        }
                        defaultData = result;
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
                            DirectoryData = currentTreeNode.DirectoryData,
                            Offset = left,
                            Path = currentTreeNode.Path
                        });
                    }

                    if (right != 0)
                    {
                        treeNodes.Add(new TreeNodeInfo
                        {
                            DirectoryData = currentTreeNode.DirectoryData,
                            Offset = right,
                            Path = currentTreeNode.Path
                        });
                    }
                }
            }
            return false;
        }
        


    }
}
