using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using XboxToolkit.Interface;
using XboxToolkit.Internal;
using XboxToolkit.Internal.Models;
using XboxToolkit.Internal.ContainerBuilder;

namespace XboxToolkit
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
                containerReader = new GODContainerReader(filePath);
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

        //Files to iso
        //Files to cci

        public static bool ConvertFolderToISO(string inputFolder, ISOFormat format, string outputFile, long splitPoint, Action<float>? progress)
        {
            try
            {
                var isoFormat = format;

                if (Directory.Exists(inputFolder) == false)
                {
                    return false;
                }

                progress?.Invoke(0.0f);

                // Determine magic sector based on format
                uint magicSector;
                uint baseSector;
                if (isoFormat == ISOFormat.XboxOriginal)
                {
                    magicSector = Constants.XGD_ISO_BASE_SECTOR;
                    baseSector = 0;
                }
                else // Xbox360
                {
                    // Use XGD3 as default (most common)
                    magicSector = Constants.XGD_MAGIC_SECTOR_XGD3;
                    baseSector = magicSector - Constants.XGD_ISO_BASE_SECTOR;
                }

                // Scan folder structure
                var fileEntries = new List<FileEntry>();
                var directoryEntries = new List<DirectoryEntry>();
                ContainerBuilderHelper.ScanFolder(inputFolder, string.Empty, fileEntries, directoryEntries);

                progress?.Invoke(0.1f);

                // Build directory tree structure
                var rootDirectory = ContainerBuilderHelper.BuildDirectoryTree(directoryEntries, fileEntries, string.Empty);
                
                // Calculate directory sizes
                var directorySizes = new Dictionary<string, uint>();
                ContainerBuilderHelper.CalculateDirectorySizes(rootDirectory, directorySizes, Constants.XGD_SECTOR_SIZE);

                progress?.Invoke(0.2f);

                // Allocate sectors efficiently - maximize usage between magic sector and directory tables
                var sectorAllocator = new SectorAllocator(magicSector, baseSector);
                
                // Allocate sectors for files first - try to fill space between base and magic sector
                // Sort files by size (largest first) to better fill sectors
                var sortedFiles = fileEntries.OrderByDescending(f => f.Size).ToList();
                foreach (var fileEntry in sortedFiles)
                {
                    var fileSectors = Helpers.RoundToMultiple(fileEntry.Size, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                    fileEntry.Sector = sectorAllocator.AllocateFileSectors(fileSectors) - baseSector;
                }

                // Allocate directories after magic sector (they need to be in a known location)
                var rootDirSize = directorySizes[string.Empty];
                var rootDirSectors = Helpers.RoundToMultiple(rootDirSize, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                var rootDirSector = sectorAllocator.AllocateDirectorySectors(rootDirSectors);

                // Allocate sectors for all subdirectories
                ContainerBuilderHelper.AllocateDirectorySectors(rootDirectory, directorySizes, sectorAllocator, baseSector);

                progress?.Invoke(0.4f);

                // Build directory data
                var directoryData = new Dictionary<string, byte[]>();
                ContainerBuilderHelper.BuildDirectoryData(rootDirectory, fileEntries, directorySizes, directoryData, baseSector);

                progress?.Invoke(0.6f);

                // Write ISO
                var totalSectors = sectorAllocator.GetTotalSectors();
                var iteration = 0;
                var currentSector = 0u;

                while (currentSector < totalSectors)
                {
                    var splitting = false;
                    using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                    using (var outputWriter = new BinaryWriter(outputStream))
                    {
                        // Build sector map for directory tables and files
                        var sectorMap = new Dictionary<uint, byte[]>();
                        
                        // Add directory sectors to map
                        foreach (var dir in directoryData)
                        {
                            var dirSectors = Helpers.RoundToMultiple((uint)dir.Value.Length, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                            var dirSector = dir.Key == string.Empty ? rootDirSector : ContainerBuilderHelper.GetDirectorySector(dir.Key, rootDirectory, baseSector);
                            
                            for (var i = 0u; i < dirSectors; i++)
                            {
                                var sectorIndex = dirSector + i;
                                var offset = (int)(i * Constants.XGD_SECTOR_SIZE);
                                var length = Math.Min(Constants.XGD_SECTOR_SIZE, dir.Value.Length - offset);
                                var sectorData = new byte[Constants.XGD_SECTOR_SIZE];
                                if (length > 0)
                                {
                                    Array.Copy(dir.Value, offset, sectorData, 0, length);
                                }
                                sectorMap[sectorIndex] = sectorData;
                            }
                        }

                        // Add file sectors to map
                        foreach (var fileEntry in fileEntries)
                        {
                            var fileSector = fileEntry.Sector + baseSector;
                            var fileSectors = Helpers.RoundToMultiple(fileEntry.Size, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                            
                            using (var fileStream = File.OpenRead(fileEntry.FullPath))
                            {
                                for (var i = 0u; i < fileSectors; i++)
                                {
                                    var sectorIndex = fileSector + i;
                                    var sectorData = new byte[Constants.XGD_SECTOR_SIZE];
                                    var bytesRead = fileStream.Read(sectorData, 0, (int)Constants.XGD_SECTOR_SIZE);
                                    if (bytesRead < Constants.XGD_SECTOR_SIZE)
                                    {
                                        Helpers.FillArray(sectorData, (byte)0, bytesRead, (int)(Constants.XGD_SECTOR_SIZE - bytesRead));
                                    }
                                    sectorMap[sectorIndex] = sectorData;
                                }
                            }
                        }

                        // Write sectors before base sector (empty/scrubbed)
                        for (var i = 0u; i < baseSector && currentSector < totalSectors; i++)
                        {
                            var scrubbedSector = new byte[Constants.XGD_SECTOR_SIZE];
                            for (var j = 0; j < scrubbedSector.Length; j++)
                            {
                                scrubbedSector[j] = 0xff;
                            }
                            outputWriter.Write(scrubbedSector);
                            currentSector++;
                        }

                        // Write sectors up to magic sector
                        while (currentSector < magicSector && currentSector < totalSectors)
                        {
                            byte[] sectorToWrite;
                            if (sectorMap.ContainsKey(currentSector))
                            {
                                sectorToWrite = sectorMap[currentSector];
                            }
                            else
                            {
                                sectorToWrite = new byte[Constants.XGD_SECTOR_SIZE];
                                Helpers.FillArray(sectorToWrite, (byte)0xff);
                            }
                            outputWriter.Write(sectorToWrite);
                            currentSector++;
                        }

                        // Write magic sector with XGD header
                        if (currentSector == magicSector && currentSector < totalSectors)
                        {
                            var headerSector = new byte[Constants.XGD_SECTOR_SIZE];
                            ContainerBuilderHelper.WriteXgdHeader(headerSector, rootDirSector - baseSector, rootDirSize);
                            outputWriter.Write(headerSector);
                            currentSector++;
                        }
                        
                        // Add directory sectors to map
                        foreach (var dir in directoryData)
                        {
                            var dirSectors = Helpers.RoundToMultiple((uint)dir.Value.Length, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                            var dirSector = dir.Key == string.Empty ? rootDirSector : ContainerBuilderHelper.GetDirectorySector(dir.Key, rootDirectory, baseSector);
                            
                            for (var i = 0u; i < dirSectors; i++)
                            {
                                var sectorIndex = dirSector + i;
                                var offset = (int)(i * Constants.XGD_SECTOR_SIZE);
                                var length = Math.Min(Constants.XGD_SECTOR_SIZE, dir.Value.Length - offset);
                                var sectorData = new byte[Constants.XGD_SECTOR_SIZE];
                                if (length > 0)
                                {
                                    Array.Copy(dir.Value, offset, sectorData, 0, length);
                                }
                                sectorMap[sectorIndex] = sectorData;
                            }
                        }

                        // Add file sectors to map
                        foreach (var fileEntry in fileEntries)
                        {
                            var fileSector = fileEntry.Sector + baseSector;
                            var fileSectors = Helpers.RoundToMultiple(fileEntry.Size, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                            
                            using (var fileStream = File.OpenRead(fileEntry.FullPath))
                            {
                                for (var i = 0u; i < fileSectors; i++)
                                {
                                    var sectorIndex = fileSector + i;
                                    var sectorData = new byte[Constants.XGD_SECTOR_SIZE];
                                    var bytesRead = fileStream.Read(sectorData, 0, (int)Constants.XGD_SECTOR_SIZE);
                                    if (bytesRead < Constants.XGD_SECTOR_SIZE)
                                    {
                                        Helpers.FillArray(sectorData, (byte)0, bytesRead, (int)(Constants.XGD_SECTOR_SIZE - bytesRead));
                                    }
                                    sectorMap[sectorIndex] = sectorData;
                                }
                            }
                        }

                        // Write all sectors in order
                        while (currentSector < totalSectors)
                        {
                            var estimatedSize = outputStream.Position + Constants.XGD_SECTOR_SIZE;
                            if (splitPoint > 0 && estimatedSize > splitPoint)
                            {
                                splitting = true;
                                break;
                            }

                            byte[] sectorToWrite;
                            if (sectorMap.ContainsKey(currentSector))
                            {
                                sectorToWrite = sectorMap[currentSector];
                            }
                            else
                            {
                                sectorToWrite = new byte[Constants.XGD_SECTOR_SIZE];
                                Helpers.FillArray(sectorToWrite, (byte)0xff);
                            }

                            outputWriter.Write(sectorToWrite);
                            currentSector++;

                            var currentProgress = 0.6f + 0.4f * (currentSector / (float)totalSectors);
                            progress?.Invoke(currentProgress);
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

                progress?.Invoke(1.0f);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
        }

        public static bool ConvertFolderToCCI(string inputFolder, ISOFormat format, string outputFile, long splitPoint, Action<float>? progress)
        {
            try
            {
                var isoFormat = format;

                if (Directory.Exists(inputFolder) == false)
                {
                    return false;
                }

                progress?.Invoke(0.0f);

                // Determine magic sector based on format
                uint magicSector;
                uint baseSector;
                if (isoFormat == ISOFormat.XboxOriginal)
                {
                    magicSector = Constants.XGD_ISO_BASE_SECTOR;
                    baseSector = 0;
                }
                else // Xbox360
                {
                    // Use XGD3 as default (most common)
                    magicSector = Constants.XGD_MAGIC_SECTOR_XGD3;
                    baseSector = magicSector - Constants.XGD_ISO_BASE_SECTOR;
                }

                // Scan folder structure
                var fileEntries = new List<FileEntry>();
                var directoryEntries = new List<DirectoryEntry>();
                ContainerBuilderHelper.ScanFolder(inputFolder, string.Empty, fileEntries, directoryEntries);

                progress?.Invoke(0.1f);

                // Build directory tree structure
                var rootDirectory = ContainerBuilderHelper.BuildDirectoryTree(directoryEntries, fileEntries, string.Empty);
                
                // Calculate directory sizes
                var directorySizes = new Dictionary<string, uint>();
                ContainerBuilderHelper.CalculateDirectorySizes(rootDirectory, directorySizes, Constants.XGD_SECTOR_SIZE);

                progress?.Invoke(0.2f);

                // Allocate sectors efficiently - maximize usage between magic sector and directory tables
                var sectorAllocator = new SectorAllocator(magicSector, baseSector);
                
                // Allocate sectors for files first - try to fill space between base and magic sector
                // Sort files by size (largest first) to better fill sectors
                var sortedFiles = fileEntries.OrderByDescending(f => f.Size).ToList();
                foreach (var fileEntry in sortedFiles)
                {
                    var fileSectors = Helpers.RoundToMultiple(fileEntry.Size, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                    fileEntry.Sector = sectorAllocator.AllocateFileSectors(fileSectors) - baseSector;
                }

                // Allocate directories after magic sector (they need to be in a known location)
                var rootDirSize = directorySizes[string.Empty];
                var rootDirSectors = Helpers.RoundToMultiple(rootDirSize, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                var rootDirSector = sectorAllocator.AllocateDirectorySectors(rootDirSectors);

                // Allocate sectors for all subdirectories
                ContainerBuilderHelper.AllocateDirectorySectors(rootDirectory, directorySizes, sectorAllocator, baseSector);

                progress?.Invoke(0.4f);

                // Build directory data
                var directoryData = new Dictionary<string, byte[]>();
                ContainerBuilderHelper.BuildDirectoryData(rootDirectory, fileEntries, directorySizes, directoryData, baseSector);

                progress?.Invoke(0.6f);

                // Write CCI
                var totalSectors = sectorAllocator.GetTotalSectors();
                var iteration = 0;
                var currentSector = 0u;

                while (currentSector < totalSectors)
                {
                    var cciIndices = new List<CCIIndex>();
                    var splitting = false;
                    var compressedData = new byte[Constants.XGD_SECTOR_SIZE];
                    var indexAlignment = 2;
                    var multiple = (1 << indexAlignment);

                    using (var outputStream = new FileStream(outputFile, FileMode.Create, FileAccess.Write))
                    using (var outputWriter = new BinaryWriter(outputStream))
                    {
                        // Build sector map for directory tables and files
                        var sectorMap = new Dictionary<uint, byte[]>();
                        
                        // Add directory sectors to map
                        foreach (var dir in directoryData)
                        {
                            var dirSectors = Helpers.RoundToMultiple((uint)dir.Value.Length, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                            var dirSector = dir.Key == string.Empty ? rootDirSector : ContainerBuilderHelper.GetDirectorySector(dir.Key, rootDirectory, baseSector);
                            
                            for (var i = 0u; i < dirSectors; i++)
                            {
                                var sectorIndex = dirSector + i;
                                var offset = (int)(i * Constants.XGD_SECTOR_SIZE);
                                var length = Math.Min(Constants.XGD_SECTOR_SIZE, dir.Value.Length - offset);
                                var sectorData = new byte[Constants.XGD_SECTOR_SIZE];
                                if (length > 0)
                                {
                                    Array.Copy(dir.Value, offset, sectorData, 0, length);
                                }
                                sectorMap[sectorIndex] = sectorData;
                            }
                        }

                        // Add file sectors to map
                        foreach (var fileEntry in fileEntries)
                        {
                            var fileSector = fileEntry.Sector + baseSector;
                            var fileSectors = Helpers.RoundToMultiple(fileEntry.Size, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                            
                            using (var fileStream = File.OpenRead(fileEntry.FullPath))
                            {
                                for (var i = 0u; i < fileSectors; i++)
                                {
                                    var sectorIndex = fileSector + i;
                                    var sectorData = new byte[Constants.XGD_SECTOR_SIZE];
                                    var bytesRead = fileStream.Read(sectorData, 0, (int)Constants.XGD_SECTOR_SIZE);
                                    if (bytesRead < Constants.XGD_SECTOR_SIZE)
                                    {
                                        Helpers.FillArray(sectorData, (byte)0, bytesRead, (int)(Constants.XGD_SECTOR_SIZE - bytesRead));
                                    }
                                    sectorMap[sectorIndex] = sectorData;
                                }
                            }
                        }

                        // Write CCI header (placeholder values, will update later)
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

                        outputWriter.Write((byte)indexAlignment);

                        ushort unused = 0;
                        outputWriter.Write(unused);

                        // Write all sectors in order with compression
                        while (currentSector < totalSectors)
                        {
                            var currentIndexSize = cciIndices.Count * 4;
                            var estimatedSize = outputStream.Position + currentIndexSize + Constants.XGD_SECTOR_SIZE;
                            if (splitPoint > 0 && estimatedSize > splitPoint)
                            {
                                splitting = true;
                                break;
                            }

                            byte[] sectorToWrite;
                            if (currentSector < baseSector)
                            {
                                // Scrubbed sector before base (0xFF filled)
                                sectorToWrite = new byte[Constants.XGD_SECTOR_SIZE];
                                Helpers.FillArray(sectorToWrite, (byte)0xff);
                            }
                            else if (sectorMap.ContainsKey(currentSector))
                            {
                                sectorToWrite = sectorMap[currentSector];
                                
                                // Write magic sector with XGD header if needed
                                if (currentSector == magicSector)
                                {
                                    ContainerBuilderHelper.WriteXgdHeader(sectorToWrite, rootDirSector - baseSector, rootDirSize);
                                }
                            }
                            else
                            {
                                // Scrubbed sector (0xFF filled)
                                sectorToWrite = new byte[Constants.XGD_SECTOR_SIZE];
                                Helpers.FillArray(sectorToWrite, (byte)0xff);
                            }

                            // Try to compress the sector
                            using (var memoryStream = new MemoryStream())
                            {
                                var compressed = false;
                                var compressedSize = K4os.Compression.LZ4.LZ4Codec.Encode(sectorToWrite, compressedData, K4os.Compression.LZ4.LZ4Level.L12_MAX);
                                if (compressedSize > 0 && compressedSize < (Constants.XGD_SECTOR_SIZE - (4 + multiple)))
                                {
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

                                var blockData = memoryStream.ToArray();
                                outputWriter.Write(blockData, 0, blockData.Length);
                                cciIndices.Add(new CCIIndex { Value = (ulong)blockData.Length, LZ4Compressed = compressed });
                            }

                            uncompressedSize += Constants.XGD_SECTOR_SIZE;
                            currentSector++;

                            var currentProgress = 0.6f + 0.4f * (currentSector / (float)totalSectors);
                            progress?.Invoke(currentProgress);
                        }

                        // Write index table
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

                        // Update header with actual values
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

                progress?.Invoke(1.0f);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
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
