using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

        public static string[] GetSlicesFromFile(string filename)
        {
            var slices = new List<string>();
            var extension = Path.GetExtension(filename);
            var fileWithoutExtension = Path.GetFileNameWithoutExtension(filename);
            var subExtension = Path.GetExtension(fileWithoutExtension);
            if (subExtension?.Length == 2 && char.IsNumber(subExtension[1]))
            {
                var fileWithoutSubExtension = Path.GetFileNameWithoutExtension(fileWithoutExtension);
                return Directory.GetFiles(Path.GetDirectoryName(filename), $"{fileWithoutSubExtension}.?{extension}").OrderBy(s => s).ToArray();
            }
            return new string[] { filename };
        }

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

        public static bool ConvertContainerToISO(ContainerReader containerReader, ProcessingOptions processingOptions, string outputFile, long splitPoint, Action<float>? progress)
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

            var currentSector = startSector;
            var iteration = 0;

            while (currentSector < totalSectors)
            {
                var splitting = false;
                using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                using (var outputWriter = new BinaryWriter(outputStream))
                {
                    while (currentSector < totalSectors)
                    {
                        var estimatedSize = outputStream.Position + 2048;
                        if (splitPoint > 0 && estimatedSize > splitPoint)
                        {
                            splitting = true;
                            break;
                        }

                        var sectorToWrite = scrubbedSector;
                        var writeSector = (processingOptions.HasFlag(ProcessingOptions.ScrubSectors) == false) || dataSectors.Contains(currentSector);
                        if (writeSector == true)
                        {
                            if (decoder.TryReadSector(currentSector, out sectorToWrite) == false)
                            {
                                return false;
                            }
                        }
                        outputStream.Write(sectorToWrite, 0, sectorToWrite.Length);

                        var currentProgressPercent = (float)Math.Round((currentSector - startSector) / (float)totalSectors, 4);
                        if (Helpers.IsEqualTo(currentProgressPercent, progressPercent) == false)
                        {
                            progress?.Invoke(currentProgressPercent);
                            Interlocked.Exchange(ref progressPercent, currentProgressPercent);
                        }

                        currentSector++;
                    }
                }

                if (splitting || iteration > 0)
                {
                    var destFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + $".{iteration + 1}{Path.GetExtension(outputFile)}");
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                    File.Move(outputFile, destFile);
                }

                iteration++;
            }

            progress?.Invoke(1);
            return true;
        }

        public static bool ConvertContainerToCCI(ContainerReader containerReader, ProcessingOptions processingOptions, string outputFile, long splitPoint, Action<float>? progress)
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

            var compressedData = new byte[2048];
            var currentSector = startSector;
            var iteration = 0;

            while (currentSector < totalSectors)
            {
                var cciIndices = new List<CCIIndex>();

                var splitting = false;
                using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                using (var outputWriter = new BinaryWriter(outputStream))
                {
                    uint header = 0x4D494343U;
                    outputWriter.Write(header);

                    uint headerSize = 32;
                    outputWriter.Write(headerSize);

                    ulong uncompressedSize = 0UL;
                    outputWriter.Write(uncompressedSize);

                    ulong indexOffset = 0UL;
                    outputWriter.Write(indexOffset);

                    uint blockSize = 2048;
                    outputWriter.Write(blockSize);

                    byte version = 1;
                    outputWriter.Write(version);

                    byte indexAlignment = 2;
                    outputWriter.Write(indexAlignment);

                    ushort unused = 0;
                    outputWriter.Write(unused);

                    while (currentSector < totalSectors)
                    {
                        var sectorToWrite = scrubbedSector;
                        var writeSector = (processingOptions.HasFlag(ProcessingOptions.ScrubSectors) == false) || dataSectors.Contains(currentSector);
                        if (writeSector == true)
                        {
                            if (decoder.TryReadSector(currentSector, out sectorToWrite) == false)
                            {
                                return false;
                            }
                        }

                        using (var memoryStream = new MemoryStream())
                        {
                            var compressed = false;
                            var compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(sectorToWrite, compressedData, K4os.Compression.LZ4.LZ4Level.L12_MAX);
                            if (compressedSize > 0 && compressedSize < (2048 - (4 + (1 << indexAlignment))))
                            {
                                var multiple = (1 << indexAlignment);
                                var padding = ((compressedSize + 1 + multiple - 1) / multiple * multiple) - (compressedSize + 1);

                                memoryStream.WriteByte((byte)padding);
                                memoryStream.Write(compressedData, 0, compressedSize);
                                if (padding != 0)
                                {
                                    memoryStream.Write(new byte[padding], 0, padding);
                                }
                                compressed = true;
                            }
                            else
                            {
                                memoryStream.Write(sectorToWrite, 0, sectorToWrite.Length);
                            }

                            var currentIndexSize = cciIndices.Count * 8;
                            var estimatedSize = outputStream.Position + currentIndexSize + memoryStream.Length;
                            if (splitPoint > 0 && estimatedSize > splitPoint)
                            {
                                splitting = true;
                                break;
                            }

                            outputWriter.Write(memoryStream.ToArray(), 0, (int)memoryStream.Length);
                            cciIndices.Add(new CCIIndex { Value = (ulong)memoryStream.Length, LZ4Compressed = compressed });
                        }

                        uncompressedSize += 2048;
                        currentSector++;
                       
                        if (progress != null)
                        {
                            progress((currentSector - startSector) / (float)(totalSectors - startSector));
                        }
                    }

                    indexOffset = (ulong)outputStream.Position;

                    var position = (ulong)headerSize;
                    for (var i = 0; i < cciIndices.Count; i++)
                    {
                        var index = (uint)(position >> indexAlignment) | (cciIndices[i].LZ4Compressed ? 0x80000000U : 0U);
                        outputWriter.Write(index); 
                        position += cciIndices[i].Value;
                    }
                    var indexEnd = (uint)(position >> indexAlignment);
                    outputWriter.Write(indexEnd);

                    outputStream.Position = 8;
                    outputWriter.Write(uncompressedSize);
                    outputWriter.Write(indexOffset);
                }

                if (splitting || iteration > 0)
                {
                    var destFile = Path.Combine(Path.GetDirectoryName(outputFile), Path.GetFileNameWithoutExtension(outputFile) + $".{iteration + 1}{Path.GetExtension(outputFile)}");
                    if (File.Exists(destFile))
                    {
                        File.Delete(destFile);
                    }
                    File.Move(outputFile, destFile);
                }

                iteration++;
            }

            progress?.Invoke(1);
            return true;
        }
    }
}
