using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit
{
    public static class ContainerUtility
    {
        public static bool ConvertContainerToISO(ContainerReader containerReader, ProcessingOptions processingOptions, string outputFile)
        {
            if (containerReader.GetMountCount() == 0)
            {
                return false;
            }

            if (containerReader.TryGetDataSectors(out var dataSectors) == false)
            {
                return false;
            }

            var decoder = containerReader.GetDecoder();
            var xgdInfo = decoder.GetXgdInfo();

            var startSector = 0u;
            if (processingOptions.HasFlag(ProcessingOptions.RemoveVideoPartition) == false)
            {
                for (var i = 0u; i < xgdInfo.BaseSector; i++)
                {
                    dataSectors.Add(i);
                }
            }
            else
            {
                startSector = xgdInfo.BaseSector;
            }

            var totalSectors = processingOptions.HasFlag(ProcessingOptions.TrimSectors) == true ? Math.Min(Helpers.RoundToMultiple(dataSectors.Max() + 1, 2), decoder.TotalSectors()) : decoder.TotalSectors();

            using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            using (var outputWriter = new BinaryWriter(outputStream))
            {
                for (var i = startSector; i < totalSectors; i++)
                {
                    var sectorToWrite = new byte[2048];
                    var writeSector = (processingOptions.HasFlag(ProcessingOptions.ScrubSectors) == false) || dataSectors.Contains(i);
                    if (writeSector == true)
                    {
                        if (decoder.TryReadSector(i, out sectorToWrite) == false)
                        {
                            return false;
                        }
                    }
                    outputStream.Write(sectorToWrite, 0, sectorToWrite.Length);
                }
            }

            return true;
        }

        public static bool ConvertContainerToCCI(ContainerReader containerReader, ProcessingOptions processingOptions, string outputFile)
        {
            if (containerReader.GetMountCount() == 0)
            {
                return false;
            }

            if (containerReader.TryGetDataSectors(out var dataSectors) == false)
            {
                return false;
            }

            var emptySector = new byte[2048];
            var compressedData = new byte[2048];

            var decoder = containerReader.GetDecoder();
            var xgdInfo = decoder.GetXgdInfo();

            var startSector = 0u;
            if (processingOptions.HasFlag(ProcessingOptions.RemoveVideoPartition) == false)
            {
                for ( var i = 0u; i < xgdInfo.BaseSector; i++)
                {
                    dataSectors.Add(i);
                }
            }
            else
            {
                startSector = xgdInfo.BaseSector;
            }

            var totalSectors = processingOptions.HasFlag(ProcessingOptions.TrimSectors) == true ? Math.Min(Helpers.RoundToMultiple(dataSectors.Max() + 1, 2), decoder.TotalSectors()) : decoder.TotalSectors();

            using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
            using (var outputWriter = new BinaryWriter(outputStream))
            {
                var indexInfo = new List<CCIIndex>();

                var header = (uint)0x4D494343;
                outputWriter.Write(header);

                var headerSize = (uint)32;
                outputWriter.Write(headerSize);

                var uncompressedSize = (ulong)0;
                outputWriter.Write(uncompressedSize);

                var indexOffset = (ulong)0;
                outputWriter.Write(indexOffset);

                var blockSize = (uint)Constants.XGD_SECTOR_SIZE;
                outputWriter.Write(blockSize);

                var version = (byte)1;
                outputWriter.Write(version);

                var indexAlignment = (byte)2;
                outputWriter.Write(indexAlignment);

                var unused = (ushort)0;
                outputWriter.Write(unused);

                for (var i = startSector; i < totalSectors; i++)
                {
                    var sectorToWrite = emptySector;
                    var writeSector = (processingOptions.HasFlag(ProcessingOptions.ScrubSectors) == false) || dataSectors.Contains(i);
                    if (writeSector == true)
                    {
                        if (decoder.TryReadSector(i, out sectorToWrite) == false)
                        {
                            return false;
                        }
                    }

                    var multiple = (1 << indexAlignment);
                    var compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(sectorToWrite, compressedData, K4os.Compression.LZ4.LZ4Level.L12_MAX);
                    if (compressedSize > 0 && compressedSize < (Constants.XGD_SECTOR_SIZE - (2 * multiple)))
                    {
                        var padding = Helpers.RoundToMultiple((uint)compressedSize + 1, (uint)multiple) - (compressedSize + 1);
                        outputWriter.Write((byte)padding);
                        outputWriter.Write(compressedData, 0, compressedSize);
                        if (padding != 0)
                        {
                            outputWriter.Write(new byte[padding]);
                        }
                        indexInfo.Add(new CCIIndex { Value = (ulong)(compressedSize + 1 + padding), LZ4Compressed = true });
                    }
                    else
                    {
                        outputWriter.Write(sectorToWrite);
                        indexInfo.Add(new CCIIndex { Value = Constants.XGD_SECTOR_SIZE, LZ4Compressed = false });
                    }

                    uncompressedSize += 2048;
                }

                indexOffset = (ulong)outputStream.Position;

                var position = (ulong)headerSize;
                for (var i = 0; i < indexInfo.Count; i++)
                {
                    var index = (uint)(position >> indexAlignment) | (indexInfo[i].LZ4Compressed ? 0x80000000U : 0U);
                    outputWriter.Write(index);
                    position += indexInfo[i].Value;
                }
                var indexEnd = (uint)(position >> indexAlignment);
                outputWriter.Write(indexEnd);

                outputStream.Position = 8;
                outputWriter.Write(uncompressedSize);
                outputWriter.Write(indexOffset);
            }

            return true;
        }
    }
}
