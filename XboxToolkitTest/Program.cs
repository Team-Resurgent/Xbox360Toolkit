using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using XboxToolkit;
using XboxToolkit.Interface;

namespace XboxToolkitTest
{
    internal class Program
    {
        //public static bool GetSecuritySectorsFromXiso(ContainerReader input, bool compareMode, out HashSet<uint> securitySectors)
        //{
        //    securitySectors = new HashSet<uint>();

        //    var decoder = input.GetDecoder();
        //    var xgdInfo = decoder.GetXgdInfo();


        //    for (var j = 12544; j < 16640; j++)
        //    {
        //        if (decoder.TryReadSector(j, out var sectorData2) == false)
        //        {
        //            return false;
        //        }

        //        var isEmptySector = true;
        //        for (var i = 0; i < sectorData2.Length; i++)
        //        {
        //            if (sectorData2[i] != 0)
        //            {
        //                isEmptySector = false;
        //                break;
        //            }
        //        }
        //    }


        //    var flag = false;
        //    var start = 0U;

        //    var totalSectors = decoder.TotalSectors();
        //    for (uint sectorIndex = xgdInfo.BaseSector; sectorIndex <= totalSectors; sectorIndex++)
        //    {

        //        if (decoder.TryReadSector(sectorIndex, out  var sectorData) == false)
        //        {
        //            return false;
        //        }

        //        var isEmptySector = true;
        //        for (var i = 0; i < sectorData.Length; i++)
        //        {
        //            if (sectorData[i] != 0)
        //            {
        //                isEmptySector = false;
        //                break;
        //            }
        //        }

        //        var isDataSector = securitySectors.Contains(sectorIndex);
        //        if (isEmptySector == true && flag == false && !isDataSector)
        //        {
        //            start = sectorIndex;
        //            flag = true;
        //        }
        //        else if (isEmptySector == false && flag == true)
        //        {
        //            var end = sectorIndex - 1;
        //            flag = false;
        //            if (end - start == 0xFFF)
        //            {
        //                for (var i = start; i <= end; i++)
        //                {
        //                    securitySectors.Add(i);
        //                }
        //            }
        //            else if (compareMode && end - start > 0xFFF)      // if more than 0xFFF, we "guess" this image is scrubbed so we stop
        //            {
        //                return true;
        //            }
        //        }
        //    }

        //    return true;
        //}


        public static void ProcessPngs()
        {
            //var input = @"J:\FailedXEX";
            var input = @"J:\XEXDump\XEXDump";
            var output = @"J:\XEXDump\PNGS";
            var files = Directory.GetFiles(input, "*.xex");
            foreach (var file in files)
            {
                try
                {
                    var defaultData = File.ReadAllBytes(file);
                    if (XexUtility.TryExtractXexMetaData(defaultData, out var metadata) == false)
                    {
                        File.Copy(file, $"J:\\FailedXEX\\{Path.GetFileName(file)}");
                        System.Diagnostics.Debug.Print($"Failed: {file}");
                        continue;
                    }
                    File.WriteAllBytes(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".png"), metadata.Thumbnail);
                }
                catch (Exception ex)
                {
                    File.Copy(file, $"J:\\FailedXEX\\{Path.GetFileName(file)}");
                    System.Diagnostics.Debug.Print(ex.ToString());
                }
            }
        }


        static void MainOld()
        {
            ProcessPngs();


            var z = ContainerUtility.TryAutoDetectContainerType(@"U:\Done\Gun (USA) (En,Fr,Es,It).cci", out var cc);

            {
                var m = cc.TryMount();
                var n = cc.TryGetDefault(out var defaultData2, out var container);
                File.WriteAllBytes(@"J:\iso.xex", defaultData2);
                var qq = XexUtility.TryExtractXexMetaData(defaultData2, out var metadata);

                //XboxToolkit.ContainerUtility.ConvertContainerToCCI(cc, ProcessingOptions.All, @"J:\Army of Two (USA)\Army of Two (USA).cci", null);

                //var x = ContainerUtility.ConvertContainerToCCI(isoContainer, ProcessingOptions.All, @"G:\Xbox360\Test\Fifa.cci", p =>
                //{
                //    Console.WriteLine(p.ToString());
                //});



                var filePath3 = @"G:\Xbox360\Test\Fifa.cci";
                using var CCIContainer = new CCIContainerReader(filePath3);
                var q = CCIContainer.TryMount();
                var xx = ContainerUtility.ExtractFilesFromContainer(CCIContainer, @"G:\Xbox360\Test3");
            }
            // var x = ContainerUtility.ConvertContainerToCCI(isoContainer, ProcessingOptions.All, @"G:\Xbox360\far cry.cci");


            ///3597756

            //var cciContainer = new GODContainerReader(@"G:\Xbox360CCi's\Crysis.1.RF.X360-ZTM\45410968\00007000\801FA873A37D786F8DB0879D89FCEF5245");
            // var cciContainer = new CCIContainerReader(@"G:\Xbox360\Crysis-trim.cci");
            // var y = cciContainer.Mount();
            // var q = cciContainer.TryGetDefault(out var d, out var xx);
            ////var x = ContainerUtility.ConvertContainerToISO(cciContainer, false, @"G:\Xbox360\Crysis.iso");
            // var x = ContainerUtility.ConvertContainerToCCI(cciContainer, false, true, @"G:\Xbox360\Crysis-trim.cci");

            //var isoContainer = new ISOContainerReader(@"G:\Xbox360\Crysis.iso");
            //isoContainer.Mount();
            ////  var x = ContainerUtility.ConvertContainerToCCI(cciContainer, true, @"G:\Xbox360\Crysis.cci");
            //isoContainer.TryGetDefault(out var xdefaultData, out var xcontainerType);

            var isoTest = true;

            var defaultData = Array.Empty<byte>();
            var containerType = ContainerType.Unknown;

            if (isoTest)
            {
                //var filePath = @"G:\Xbox360\Crysis.1.RF.X360-ZTM\45410968\00007000\801FA873A37D786F8DB0879D89FCEF5245";
                //var filePath = @"G:\Xbox360\MEMORICK - KNIGHTS APPRENT (USA-PAL).iso";
                //var filePath = @"G:\Xbox360\Barbie Horse Adventures - Wild Horse Rescue (USA).iso";
                //var filePath = @"G:\Xbox360\Far Cry 3 (USA, Europe) (En,Fr,De,Es,It,Nl,Pt,Sv,No,Da).iso";
                //var filePath = @"G:\Xbox360\007 Legends (USA) (En,Fr,De).iso";
                //var filePath = @"G:\Xbox360\MEMORICK - KNIGHTS APPRENT (USA-PAL).iso";
                var filePath = @"G:\Xbox360\Burnout Paradise (USA).iso";

                using var xisoContainerUtility = new ISOContainerReader(filePath);
                if (xisoContainerUtility.TryMount() == true && xisoContainerUtility.TryGetDefault(out defaultData, out containerType) == true)
                {
                    //CCIUtility.ConvertContainerToCCI(xisoContainerUtility, false, @"G:\Xbox360\Burnout Paradise (USA)-noscrub.cci");
                    //CCIUtility.ConvertContainerToCCI(xisoContainerUtility, true, @"G:\Xbox360\Burnout Paradise (USA)-scrub.cci");
                    Console.WriteLine("Xiso format detected.");
                }
                else
                {
                    using var godContainerUtility = new GODContainerReader(filePath);
                    if (godContainerUtility.TryMount() == true && godContainerUtility.TryGetDefault(out defaultData, out containerType) == true)
                    {
                        Console.WriteLine("God format detected.");
                    }
                    else
                    {
                        Console.WriteLine("Unrecognized file format.");
                        return;
                    }
                }
            }
            else
            {
                defaultData = File.ReadAllBytes("default.xex");
            }

            var result = XexUtility.TryExtractXexMetaData(defaultData, out var metaData);
            if (result == false)
            {
                Console.WriteLine("Failed.");
                return;
            }
            Console.WriteLine($"GameRegion: {metaData.GameRegion}");
            Console.WriteLine();
            Console.WriteLine($"TitleName: {metaData.TitleName}");
            Console.WriteLine();
            Console.WriteLine($"TitleId: {metaData.TitleId}");
            Console.WriteLine();
            Console.WriteLine($"MediaId: {metaData.MediaId}");
            Console.WriteLine();
            Console.WriteLine($"Version: {metaData.Version}");
            Console.WriteLine();
            Console.WriteLine($"BaseVersion: {metaData.BaseVersion}");
            Console.WriteLine();
            Console.WriteLine($"DiscNum: {metaData.DiscNum}");
            Console.WriteLine();
            Console.WriteLine($"DiscTotal: {metaData.DiscTotal}");
            Console.WriteLine();
            Console.WriteLine($"Description: {metaData.Description}");
            Console.WriteLine();
            Console.WriteLine($"Developer: {metaData.Developer}");
            Console.WriteLine();
            Console.WriteLine($"Genre: {metaData.Genre}");
            Console.WriteLine();
            Console.WriteLine($"ThumbnailLength: {metaData.Thumbnail.Length}");
            Console.WriteLine();

            File.WriteAllBytes("thumb.png", metaData.Thumbnail);
            Console.WriteLine("Success.");
        }

        static void Main()
        {
            TestFolderToISO();
        }

        static void TestFolderToISO()
        {
            // Hardcoded folder path - change this to your test folder
            var sourceFolder = @"E:\isotest\game";
            var isoOutputPath = @"E:\isotest\TestFolder.iso";
            var extractOutputPath = @"E:\isotest\TestFolder_Extracted";
            var comparisonOutputPath = @"E:\isotest\TestFolder_Comparison.txt";

            Console.WriteLine("=== Xbox Original ISO Creation and Verification Test ===");
            Console.WriteLine();

            // Step 1: Create ISO from folder
            Console.WriteLine($"Step 1: Creating Xbox Original ISO from folder: {sourceFolder}");
            if (!Directory.Exists(sourceFolder))
            {
                Console.WriteLine($"ERROR: Source folder does not exist: {sourceFolder}");
                return;
            }

            // Get file info before creating ISO
            var sourceFilesInfo = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories);
            var totalSourceSize = sourceFilesInfo.Sum(f => new FileInfo(f).Length);
            Console.WriteLine($"Source folder contains {sourceFilesInfo.Length} files, total size: {totalSourceSize / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine();

            var createSuccess = ContainerUtility.ConvertFolderToISO(
                sourceFolder,
                ISOFormat.XboxOriginal,
                isoOutputPath,
                0, // No split point
                progress => Console.Write($"\rProgress: {progress:P2}")
            );

            Console.WriteLine();
            if (!createSuccess)
            {
                Console.WriteLine("ERROR: Failed to create ISO file.");
                return;
            }

            if (!File.Exists(isoOutputPath))
            {
                Console.WriteLine("ERROR: ISO file was not created.");
                return;
            }

            var isoFileInfo = new FileInfo(isoOutputPath);
            Console.WriteLine($"SUCCESS: ISO created at: {isoOutputPath}");
            Console.WriteLine($"ISO file size: {isoFileInfo.Length / 1024.0:F2} KB ({isoFileInfo.Length / 1024.0 / 1024.0:F2} MB)");
            Console.WriteLine($"Expected size (approx): {totalSourceSize / 1024.0 / 1024.0:F2} MB + overhead");
            Console.WriteLine();

            // Step 2: Extract files from ISO
            Console.WriteLine($"Step 2: Extracting files from ISO to: {extractOutputPath}");
            using var isoContainer = new ISOContainerReader(isoOutputPath);
            if (!isoContainer.TryMount())
            {
                Console.WriteLine("ERROR: Failed to mount ISO container.");
                return;
            }

            var extractSuccess = ContainerUtility.ExtractFilesFromContainer(isoContainer, extractOutputPath);
            if (!extractSuccess)
            {
                Console.WriteLine("ERROR: Failed to extract files from ISO.");
                return;
            }

            Console.WriteLine($"SUCCESS: Files extracted to: {extractOutputPath}");
            Console.WriteLine();

            // Step 3: Compare original and extracted files
            Console.WriteLine("Step 3: Comparing original and extracted files...");
            var comparisonResults = new List<string>();
            var totalFiles = 0;
            var matchingFiles = 0;
            var mismatchedFiles = 0;
            var missingFiles = 0;
            var extraFiles = 0;

            // Get all files from source folder
            var sourceFilesList = Directory.GetFiles(sourceFolder, "*", SearchOption.AllDirectories)
                .Select(f => new
                {
                    FullPath = f,
                    RelativePath = Path.GetRelativePath(sourceFolder, f).Replace('\\', '/')
                })
                .ToList();

            // Get all files from extracted folder
            var extractedFiles = Directory.GetFiles(extractOutputPath, "*", SearchOption.AllDirectories)
                .Select(f => new
                {
                    FullPath = f,
                    RelativePath = Path.GetRelativePath(extractOutputPath, f).Replace('\\', '/')
                })
                .ToDictionary(f => f.RelativePath, f => f.FullPath);

            // Compare each source file
            foreach (var sourceFile in sourceFilesList)
            {
                totalFiles++;
                var relativePath = sourceFile.RelativePath;

                if (!extractedFiles.ContainsKey(relativePath))
                {
                    missingFiles++;
                    comparisonResults.Add($"MISSING: {relativePath} (exists in source but not in extracted)");
                    continue;
                }

                var extractedPath = extractedFiles[relativePath];
                extractedFiles.Remove(relativePath); // Mark as found

                // Compare file contents
                var sourceBytes = File.ReadAllBytes(sourceFile.FullPath);
                var extractedBytes = File.ReadAllBytes(extractedPath);

                if (sourceBytes.Length != extractedBytes.Length)
                {
                    mismatchedFiles++;
                    comparisonResults.Add($"SIZE MISMATCH: {relativePath} (Source: {sourceBytes.Length} bytes, Extracted: {extractedBytes.Length} bytes)");
                    continue;
                }

                var bytesMatch = true;
                var firstMismatch = -1;
                for (var i = 0; i < sourceBytes.Length; i++)
                {
                    if (sourceBytes[i] != extractedBytes[i])
                    {
                        bytesMatch = false;
                        firstMismatch = i;
                        break;
                    }
                }

                if (!bytesMatch)
                {
                    mismatchedFiles++;
                    comparisonResults.Add($"CONTENT MISMATCH: {relativePath} (First difference at byte offset: {firstMismatch})");
                }
                else
                {
                    matchingFiles++;
                }
            }

            // Check for extra files in extracted folder
            foreach (var extraFile in extractedFiles)
            {
                extraFiles++;
                comparisonResults.Add($"EXTRA: {extraFile.Key} (exists in extracted but not in source)");
            }

            // Write comparison results
            using var writer = new StreamWriter(comparisonOutputPath);
            writer.WriteLine("=== File Comparison Results ===");
            writer.WriteLine($"Total files in source: {totalFiles}");
            writer.WriteLine($"Matching files: {matchingFiles}");
            writer.WriteLine($"Mismatched files: {mismatchedFiles}");
            writer.WriteLine($"Missing files: {missingFiles}");
            writer.WriteLine($"Extra files: {extraFiles}");
            writer.WriteLine();

            if (comparisonResults.Count > 0)
            {
                writer.WriteLine("=== Detailed Results ===");
                foreach (var result in comparisonResults)
                {
                    writer.WriteLine(result);
                }
            }
            else
            {
                writer.WriteLine("All files match perfectly!");
            }

            // Print summary to console
            Console.WriteLine($"Total files: {totalFiles}");
            Console.WriteLine($"Matching: {matchingFiles}");
            Console.WriteLine($"Mismatched: {mismatchedFiles}");
            Console.WriteLine($"Missing: {missingFiles}");
            Console.WriteLine($"Extra: {extraFiles}");
            Console.WriteLine();

            if (mismatchedFiles == 0 && missingFiles == 0 && extraFiles == 0)
            {
                Console.WriteLine("SUCCESS: All files match perfectly!");
            }
            else
            {
                Console.WriteLine($"WARNING: Found {mismatchedFiles + missingFiles + extraFiles} issues. See {comparisonOutputPath} for details.");
            }

            Console.WriteLine($"Detailed results written to: {comparisonOutputPath}");
        }
    }
}