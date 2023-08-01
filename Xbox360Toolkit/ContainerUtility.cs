using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit
{
    public static class ContainerUtility
    {
        //public static bool RepackContainerToISO(ContainerReader containerReader, string outputFile)
        //{
        //    if (containerReader.GetMountCount() == 0)
        //    {
        //        return false;
        //    }

        //    if (containerReader.TryGetDataSectors(out var dataSectors) == false)
        //    {
        //        return false;
        //    }




        //    return true;
        //}

        public static bool TryAutoDetectContainerType(string filePath, out ContainerReader? containerReader)
        {
            if (ISOContainerReader.IsISO(filePath))
            {
                containerReader = new ISOContainerReader(filePath);
                return true;
            }
            else if (CCIContainerReader.IsCCI(filePath))
            {
                containerReader = new CCIContainerReader(filePath);
                return true;
            }
            else if (GODContainerReader.IsGOD(filePath))
            {
                containerReader = new CCIContainerReader(filePath);
                return true;
            }
 
            containerReader = null;
            return false;
        }

        public static bool ExtractFilesFromContainer(ContainerReader containerReader, string destFilePath)
        {
            if (containerReader.GetMountCount() == 0)
            {
                return false;
            }

            if (containerReader.TryExtractFiles(destFilePath) == false)
            {
                return false;
            }

            return true;
        }

        public static bool ConvertContainerToISO(ContainerReader containerReader, ProcessingOptions processingOptions, string outputFile, Action<float>? progress)
        {
            var progressPercent = 0.0f;
            progress?.Invoke(progressPercent);

            if (containerReader.GetMountCount() == 0)
            {
                return false;
            }

            if (containerReader.TryGetDataSectors(out var dataSectors) == false)
            {
                return false;
            }

            var scrubbedSector = new byte[2048];
            for (var i = 0; i < scrubbedSector.Length; i++)
            {
                scrubbedSector[i] = 0xff;
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
                    var sectorToWrite = scrubbedSector;
                    var writeSector = (processingOptions.HasFlag(ProcessingOptions.ScrubSectors) == false) || dataSectors.Contains(i);
                    if (writeSector == true)
                    {
                        if (decoder.TryReadSector(i, out sectorToWrite) == false)
                        {
                            return false;
                        }
                    }
                    outputStream.Write(sectorToWrite, 0, sectorToWrite.Length);

                    var currentProgressPercent = (float)Math.Round((i - startSector) / (float)totalSectors, 4);
                    if (Helpers.IsEqualTo(currentProgressPercent, progressPercent) == false)
                    {
                        progress?.Invoke(currentProgressPercent);
                        Interlocked.Exchange(ref progressPercent, currentProgressPercent);
                    }
                }
            }

            progress?.Invoke(1);
            return true;
        }

        public static bool ConvertContainerToCCI(ContainerReader containerReader, ProcessingOptions processingOptions, string outputFile, Action<float>? progress)
        {
            var progressPercent = 0.0f;
            progress?.Invoke(progressPercent);

            if (containerReader.GetMountCount() == 0)
            {
                return false;
            }

            if (containerReader.TryGetDataSectors(out var dataSectors) == false)
            {
                return false;
            }

            var scrubbedSector = new byte[Constants.XGD_SECTOR_SIZE];
            for (var i = 0; i < scrubbedSector.Length; i++)
            {
                scrubbedSector[i] = 0xff;
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
                var indexInfo = new CCIIndex[totalSectors - startSector];

                var header = (uint)0x4D494343;
                outputWriter.Write(header);

                var headerSize = (uint)32;
                outputWriter.Write(headerSize);

                var uncompressedSize = (long)0;
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

                var batchSize = (Environment.ProcessorCount / 2) + 1;
                var options = new ParallelOptions { MaxDegreeOfParallelism = batchSize };

                for (var i = startSector; i < totalSectors; i += (uint)batchSize)
                {
                    var batchSectors = Math.Min(totalSectors - i, batchSize);
                    var batch = new CCIBatch[batchSectors];

                    Parallel.For(0, batchSectors, batchIndex =>
                    {
                        var batchSector = (uint)(i + batchIndex);

                        var sectorToWrite = scrubbedSector;
                        var writeSector = (processingOptions.HasFlag(ProcessingOptions.ScrubSectors) == false) || dataSectors.Contains(batchSector);
                        if (writeSector == true)
                        {
                            if (decoder.TryReadSector(batchSector, out sectorToWrite) == false)
                            {
                                batch[batchIndex].Failed = true;
                                return;
                            }
                        }

                        var compressedData = new byte[Constants.XGD_SECTOR_SIZE];
                        var multiple = (1 << indexAlignment);
                        var compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(sectorToWrite, compressedData, K4os.Compression.LZ4.LZ4Level.L12_MAX);
                        if (compressedSize > 0 && compressedSize < (Constants.XGD_SECTOR_SIZE - (2 * multiple)))
                        {
                            var padding = Helpers.RoundToMultiple((uint)compressedSize + 1, (uint)multiple) - (compressedSize + 1);

                            var sectorData = new byte[compressedSize + 1 + padding];
                            sectorData[0] = (byte)padding;
                            Array.Copy(compressedData, 0, sectorData, 1, compressedSize);

                            batch[batchIndex].Failed = false;
                            batch[batchIndex].Buffer = sectorData;
                            batch[batchIndex].Index = new CCIIndex { Value = (ulong)sectorData.Length, LZ4Compressed = true };
                        }
                        else
                        {
                            batch[batchIndex].Failed = false;
                            batch[batchIndex].Buffer = sectorToWrite;
                            batch[batchIndex].Index = new CCIIndex { Value = (ulong)sectorToWrite.Length, LZ4Compressed = false };
                        }

                        Interlocked.Add(ref uncompressedSize, Constants.XGD_SECTOR_SIZE);

                        var currentProgressPercent = (float)Math.Round(batchSector / (float)totalSectors, 4);
                        if (Helpers.IsEqualTo(currentProgressPercent, progressPercent) == false)
                        {
                            progress?.Invoke(currentProgressPercent);
                            Interlocked.Exchange(ref progressPercent, currentProgressPercent);
                        }
                    });

                    for (var batchIndex = 0; batchIndex < batchSectors; batchIndex++)
                    {
                        var batchItem = batch[batchIndex];
                        if (batchItem.Failed == true)
                        {
                            return false;
                        }
                        outputWriter.Write(batchItem.Buffer);
                        indexInfo[(i - startSector) + batchIndex] = batchItem.Index;
                    }
                }

                indexOffset = (ulong)outputStream.Position;

                var position = (ulong)headerSize;
                for (var i = 0; i < indexInfo.Length; i++)
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

            progress?.Invoke(1);
            return true;
        }
    }
}
