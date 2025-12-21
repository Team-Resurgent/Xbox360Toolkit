using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using XboxToolkit;
using XboxToolkit.Internal;
using XboxToolkit.Internal.Models;
using Xunit;

namespace XboxToolkitTest;

/// <summary>
/// Dummy test to ensure the test directory is created before any tests run.
/// </summary>
public class TestDirectorySetupTests
{
    private static readonly string TestDirectoryPath;
    private static readonly string TestIsoPath;
    private static readonly object LockObject = new object();

    static TestDirectorySetupTests()
    {
        // Find solution root and create tests directory there
        var solutionRoot = FindSolutionRoot();
        var testsDirectory = Path.Combine(solutionRoot, "tests");
        
        // Ensure the tests directory exists
        if (!Directory.Exists(testsDirectory))
        {
            Directory.CreateDirectory(testsDirectory);
        }
        
        // Generate the test directory structure under tests
        TestDirectoryPath = TestDirectoryGenerator.GenerateTestDirectory(testsDirectory);
        
        // Convert TestDirectory to ISO
        TestIsoPath = ConvertTestDirectoryToIso(testsDirectory, TestDirectoryPath);
    }

    /// <summary>
    /// Finds the solution root directory by searching for the .sln file starting from the application directory.
    /// </summary>
    /// <returns>The path to the solution root directory.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown if the solution root cannot be found.</exception>
    private static string FindSolutionRoot()
    {
        var currentDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var directory = new DirectoryInfo(currentDirectory);

        // Traverse up the directory tree looking for the .sln file
        while (directory != null)
        {
            var slnFiles = directory.GetFiles("*.sln");
            if (slnFiles.Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find solution root directory (no .sln file found)");
    }

    /// <summary>
    /// Converts the TestDirectory to ISO format using extract-xiso.exe.
    /// </summary>
    /// <param name="testsDirectory">The tests directory where the ISO will be created.</param>
    /// <param name="testDirectoryPath">The path to the TestDirectory to convert.</param>
    /// <returns>The path to the created ISO file.</returns>
    private static string ConvertTestDirectoryToIso(string testsDirectory, string testDirectoryPath)
    {
        var isoPath = Path.Combine(testsDirectory, "TestDirectory.iso");
        
        // If ISO already exists, reuse it
        if (File.Exists(isoPath))
        {
            return isoPath;
        }

        // Find extract-xiso.exe in the output directory
        var outputDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var extractXisoPath = Path.Combine(outputDirectory, "extract-xiso.exe");
        
        if (!File.Exists(extractXisoPath))
        {
            throw new FileNotFoundException($"extract-xiso.exe not found at: {extractXisoPath}");
        }

        // Get just the directory name for the pack command
        var testDirectoryName = Path.GetFileName(testDirectoryPath);
        var testDirectoryParent = Path.GetDirectoryName(testDirectoryPath);
        
        // Run extract-xiso.exe pack command
        var processStartInfo = new ProcessStartInfo
        {
            FileName = extractXisoPath,
            Arguments = $"-c {testDirectoryName} {testDirectoryName}.iso",
            WorkingDirectory = testDirectoryParent ?? testsDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(processStartInfo);
        if (process == null)
        {
            throw new InvalidOperationException("Failed to start extract-xiso.exe process");
        }

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var errorOutput = process.StandardError.ReadToEnd();
            var standardOutput = process.StandardOutput.ReadToEnd();
            throw new InvalidOperationException(
                $"extract-xiso.exe failed with exit code {process.ExitCode}. " +
                $"Error: {errorOutput}. Output: {standardOutput}");
        }

        if (!File.Exists(isoPath))
        {
            throw new FileNotFoundException($"ISO file was not created at expected path: {isoPath}");
        }

        return isoPath;
    }

    /// <summary>
    /// Gets the file listing from an ISO file using our own ISOContainerReader.
    /// </summary>
    /// <param name="isoPath">Path to the ISO file.</param>
    /// <returns>List of file paths (relative to ISO root) found in the ISO.</returns>
    private static List<string> GetIsoFileListing(string isoPath)
    {
        var fileList = new List<string>();
        const uint XGD_SECTOR_SIZE = 0x800; // 2048 bytes
        
        using var isoReader = new ISOContainerReader(isoPath);
        if (!isoReader.TryMount())
        {
            throw new InvalidOperationException($"Failed to mount ISO: {isoPath}");
        }
        
        try
        {
            var decoder = isoReader.GetDecoder();
            var xgdInfo = decoder.GetXgdInfo();
            
            // Debug: log XGD info
            Console.WriteLine($"DEBUG: XGD Info for {isoPath}: BaseSector={xgdInfo.BaseSector}, RootDirSector={xgdInfo.RootDirSector}, RootDirSize={xgdInfo.RootDirSize}");
            
            // Read root directory
            var rootSectors = (xgdInfo.RootDirSize + XGD_SECTOR_SIZE - 1) / XGD_SECTOR_SIZE;
            var rootData = new byte[xgdInfo.RootDirSize];
            
            // Debug: log first few bytes of root directory
            Console.WriteLine($"DEBUG: Reading {rootSectors} sectors starting at sector {xgdInfo.BaseSector + xgdInfo.RootDirSector}");
            for (var i = 0; i < rootSectors; i++)
            {
                var currentRootSector = xgdInfo.BaseSector + xgdInfo.RootDirSector + (uint)i;
                if (decoder.TryReadSector(currentRootSector, out var sectorData) == false)
                {
                    throw new InvalidOperationException($"Failed to read root directory sector {i}");
                }
                var offset = i * XGD_SECTOR_SIZE;
                var length = Math.Min(XGD_SECTOR_SIZE, xgdInfo.RootDirSize - offset);
                Array.Copy(sectorData, 0, rootData, offset, length);
                
                // Debug: log first few bytes of first sector
                if (i == 0)
                {
                    var hexBytes = string.Join(" ", sectorData.Take(32).Select(b => $"{b:X2}"));
                    Console.WriteLine($"DEBUG: First 32 bytes of root directory: {hexBytes}");
                }
            }
            
            // Traverse directory tree (similar to ContainerReader.TryExtractFiles)
            // Using a simple struct to hold tree node info
            var treeNodes = new List<(byte[] DirectoryData, uint Offset, string Path)>
            {
                (rootData, 0, string.Empty)
            };
            
            while (treeNodes.Count > 0)
            {
                // Use stack (LIFO) for preorder traversal - pop from end
                var currentTreeNode = treeNodes[treeNodes.Count - 1];
                treeNodes.RemoveAt(treeNodes.Count - 1);
                
                using (var directoryDataStream = new MemoryStream(currentTreeNode.DirectoryData))
                using (var directoryDataDataReader = new BinaryReader(directoryDataStream))
                {
                    if (currentTreeNode.Offset * 4 >= directoryDataStream.Length)
                    {
                        continue;
                    }
                    
                    var entryOffset = currentTreeNode.Offset * 4;
                    directoryDataStream.Position = entryOffset;
                    
                    // Read first 2 bytes to check for padding (matches extract-xiso XISO_PAD_SHORT = 0xFFFF)
                    var leftOffsetBytes = new byte[2];
                    if (directoryDataStream.Read(leftOffsetBytes, 0, 2) != 2)
                    {
                        continue;
                    }
                    
                    var left = (ushort)(leftOffsetBytes[0] | (leftOffsetBytes[1] << 8));
                    
                    // Check if this is padding (0xFFFF) - matches extract-xiso's check
                    if (left == 0xFFFF)
                    {
                        // This is padding, skip to next sector boundary if needed
                        // In extract-xiso, it calculates: l_offset * 4 + (sector_size - (l_offset * 4) % sector_size)
                        // But for our traversal, we just continue to next entry
                        continue;
                    }
                    
                    // Read remaining 12 bytes of header
                    var headerBuffer = new byte[12];
                    if (directoryDataStream.Read(headerBuffer, 0, 12) != 12)
                    {
                        continue;
                    }
                    
                    // Parse the rest of the header
                    var right = (ushort)(headerBuffer[0] | (headerBuffer[1] << 8));
                    var sector = (uint)(headerBuffer[2] | (headerBuffer[3] << 8) | (headerBuffer[4] << 16) | (headerBuffer[5] << 24));
                    var size = (uint)(headerBuffer[6] | (headerBuffer[7] << 8) | (headerBuffer[8] << 16) | (headerBuffer[9] << 24));
                    var attribute = headerBuffer[10];
                    var nameLength = headerBuffer[11];
                    
                    // Validate nameLength
                    if (nameLength == 0 || nameLength > 255)
                    {
                        // Debug: log why we're skipping
                        if (nameLength == 0)
                        {
                            Console.WriteLine($"DEBUG: Skipping entry at offset {entryOffset} with nameLength=0, attribute=0x{attribute:X2}");
                        }
                        continue;
                    }
                    
                    // Validate we have enough bytes to read the filename
                    var filenameOffset = entryOffset + 14;
                    if (filenameOffset + nameLength > directoryDataStream.Length)
                    {
                        Console.WriteLine($"DEBUG: Skipping entry at offset {entryOffset} - filename would exceed directory data length");
                        continue;
                    }
                    
                    // Read filename
                    directoryDataStream.Position = filenameOffset;
                    var filenameBytes = directoryDataDataReader.ReadBytes(nameLength);
                    var filename = Encoding.ASCII.GetString(filenameBytes);
                    
                    // Skip empty filenames (shouldn't happen, but be safe)
                    if (string.IsNullOrEmpty(filename))
                    {
                        Console.WriteLine($"DEBUG: Skipping entry at offset {entryOffset} with empty filename, nameLength={nameLength}, attribute=0x{attribute:X2}");
                        continue;
                    }
                    
                    // Debug: log first few entries
                    if (fileList.Count < 5)
                    {
                        Console.WriteLine($"DEBUG: Found entry: offset={entryOffset}, filename='{filename}', attribute=0x{attribute:X2}, isDir={(attribute & 0x10) != 0}, size={size}, sector={sector}");
                    }
                    
                    // Build relative path
                    var relativePath = string.IsNullOrEmpty(currentTreeNode.Path) 
                        ? filename 
                        : Path.Combine(currentTreeNode.Path, filename).Replace('\\', '/');
                    
                    // Add to list if it's a file (not a directory)
                    if ((attribute & 0x10) == 0) // Not a directory
                    {
                        fileList.Add(relativePath);
                    }
                    
                    // Push right first, then left (so left is processed first when popping)
                    if (right != 0 && right != 0xFFFF)
                    {
                        var rightOffsetBytes = right * 4;
                        if (rightOffsetBytes < directoryDataStream.Length)
                        {
                            treeNodes.Add((currentTreeNode.DirectoryData, right, currentTreeNode.Path));
                        }
                    }
                    
                    if (left != 0 && left != 0xFFFF)
                    {
                        var leftOffsetByteCount = left * 4;
                        if (leftOffsetByteCount < directoryDataStream.Length)
                        {
                            treeNodes.Add((currentTreeNode.DirectoryData, left, currentTreeNode.Path));
                        }
                    }
                    
                    // If directory, load its data and add to stack
                    if ((attribute & 0x10) != 0) // Is directory
                    {
                        if (size > 0)
                        {
                            var directorySectors = (size + XGD_SECTOR_SIZE - 1) / XGD_SECTOR_SIZE;
                            var directoryData = new byte[size];
                            for (var i = 0; i < directorySectors; i++)
                            {
                                var currentDirectorySector = xgdInfo.BaseSector + sector + (uint)i;
                                if (decoder.TryReadSector(currentDirectorySector, out var sectorData) == false)
                                {
                                    throw new InvalidOperationException($"Failed to read directory sector {i} for {relativePath}");
                                }
                                var offset = i * XGD_SECTOR_SIZE;
                                var length = Math.Min(XGD_SECTOR_SIZE, size - offset);
                                Array.Copy(sectorData, 0, directoryData, offset, length);
                            }
                            
                            treeNodes.Add((directoryData, 0, Path.Combine(currentTreeNode.Path, filename)));
                        }
                    }
                }
            }
        }
        finally
        {
            isoReader.Dismount();
        }
        
        return fileList;
    }

    [Fact]
    public void GeneratedIso_MatchesExtractXiso()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var testsDirectory = Path.Combine(solutionRoot, "tests");
        var generatedIsoPath = Path.Combine(testsDirectory, "TestDirectory_Generated.iso");
        
        // Delete generated ISO if it exists (clean before test)
        if (File.Exists(generatedIsoPath))
        {
            File.Delete(generatedIsoPath);
        }
        
        // Act - Generate ISO from TestDirectory using C# code
        var success = ContainerUtility.ConvertFolderToISO(
            TestDirectoryPath,
            ISOFormat.XboxOriginal,
            generatedIsoPath,
            0, // No split point
            null // No progress callback
        );
        
        // Assert
        Assert.True(Directory.Exists(TestDirectoryPath), $"Test directory should exist at: {TestDirectoryPath}");
        if (!success)
        {
            // If ISO generation failed, check if the file was created anyway
            if (File.Exists(generatedIsoPath))
            {
                var fileInfo = new FileInfo(generatedIsoPath);
                Assert.Fail($"ISO generation returned false but file exists. File size: {fileInfo.Length} bytes. Check Debug output for exception details.");
            }
            else
            {
                Assert.Fail("ISO generation returned false and no file was created. Check Debug output for exception details.");
            }
        }
        Assert.True(success, "ISO generation should succeed");
        Assert.True(File.Exists(generatedIsoPath), $"Generated ISO should exist at: {generatedIsoPath}");
        Assert.True(File.Exists(TestIsoPath), $"Reference ISO should exist at: {TestIsoPath}");
        
        // Binary compare generated ISO with reference ISO
        var generatedIsoInfo = new FileInfo(generatedIsoPath);
        var referenceIsoInfo = new FileInfo(TestIsoPath);
        
        Assert.True(referenceIsoInfo.Length == generatedIsoInfo.Length, 
            $"ISO file sizes should match. Generated: {generatedIsoInfo.Length}, Reference: {referenceIsoInfo.Length}");
        
        // Compare file listings from both ISOs
        var generatedFileList = GetIsoFileListing(generatedIsoPath);
        var referenceFileList = GetIsoFileListing(TestIsoPath);
        
        // Debug output - show all files from both ISOs
        Console.WriteLine($"Generated ISO file count: {generatedFileList.Count}");
        Console.WriteLine($"Reference ISO file count: {referenceFileList.Count}");
        
        Console.WriteLine($"\nGenerated ISO files ({generatedFileList.Count} total):");
        foreach (var file in generatedFileList.OrderBy(f => f))
        {
            Console.WriteLine($"  - {file}");
        }
        
        Console.WriteLine($"\nReference ISO files ({referenceFileList.Count} total):");
        foreach (var file in referenceFileList.OrderBy(f => f))
        {
            Console.WriteLine($"  - {file}");
        }
        if (generatedFileList.Any(f => string.IsNullOrEmpty(f)))
        {
            Console.WriteLine($"WARNING: Generated ISO contains {generatedFileList.Count(f => string.IsNullOrEmpty(f))} empty file names!");
        }
        
        // Sort both lists for comparison
        generatedFileList.Sort();
        referenceFileList.Sort();
        
        // Compare file lists
        var missingInGenerated = referenceFileList.Except(generatedFileList).ToList();
        var extraInGenerated = generatedFileList.Except(referenceFileList).ToList();
        
        if (missingInGenerated.Count > 0 || extraInGenerated.Count > 0)
        {
            var diffMessage = "File listing comparison found differences:\n";
            if (missingInGenerated.Count > 0)
            {
                diffMessage += $"  Missing in generated ISO ({missingInGenerated.Count} files):\n";
                foreach (var file in missingInGenerated.Take(20))
                {
                    diffMessage += $"    - {file}\n";
                }
                if (missingInGenerated.Count > 20)
                {
                    diffMessage += $"    ... and {missingInGenerated.Count - 20} more\n";
                }
            }
            if (extraInGenerated.Count > 0)
            {
                diffMessage += $"  Extra in generated ISO ({extraInGenerated.Count} files):\n";
                foreach (var file in extraInGenerated.Take(20))
                {
                    diffMessage += $"    + {file}\n";
                }
                if (extraInGenerated.Count > 20)
                {
                    diffMessage += $"    ... and {extraInGenerated.Count - 20} more\n";
                }
            }
            Assert.Fail(diffMessage);
        }
        
        // Compare byte-by-byte, skipping magic sector (sector 0x20 = offset 0x10000) due to timestamp differences
        using var generatedStream = File.OpenRead(generatedIsoPath);
        using var referenceStream = File.OpenRead(TestIsoPath);
        
        const uint magicSector = 0x20; // Xbox Original magic sector
        const long magicSectorOffset = magicSector * 2048L; // 0x10000
        const long magicSectorSize = 2048L;
        
        var buffer1 = new byte[1024 * 1024]; // 1MB buffer
        var buffer2 = new byte[1024 * 1024];
        var offset = 0L;
        var differences = new List<(long offset, byte generated, byte reference)>();
        
        while (offset < generatedIsoInfo.Length)
        {
            // Skip magic sector during comparison
            if (offset >= magicSectorOffset && offset < magicSectorOffset + magicSectorSize)
            {
                // Skip this range in both streams
                generatedStream.Seek(magicSectorSize, SeekOrigin.Current);
                referenceStream.Seek(magicSectorSize, SeekOrigin.Current);
                offset += magicSectorSize;
                continue;
            }
            
            var bytesToRead = (int)Math.Min(buffer1.Length, generatedIsoInfo.Length - offset);
            // Adjust if we're about to enter the magic sector
            if (offset < magicSectorOffset && offset + bytesToRead > magicSectorOffset)
            {
                bytesToRead = (int)(magicSectorOffset - offset);
            }
            
            var bytesRead1 = generatedStream.Read(buffer1, 0, bytesToRead);
            var bytesRead2 = referenceStream.Read(buffer2, 0, bytesToRead);
            
            Assert.True(bytesRead1 == bytesRead2, $"Should read same number of bytes at offset {offset}");
            
            for (int i = 0; i < bytesRead1; i++)
            {
                if (buffer1[i] != buffer2[i])
                {
                    differences.Add((offset + i, buffer1[i], buffer2[i]));
                    if (differences.Count >= 10) // Limit to first 10 differences for error message
                    {
                        break;
                    }
                }
            }
            
            if (differences.Count >= 10)
            {
                break;
            }
            
            offset += bytesRead1;
        }
        
        if (differences.Count > 0)
        {
            var diffMessage = "Binary comparison found differences:\n";
            foreach (var (diffOffset, genByte, refByte) in differences)
            {
                diffMessage += $"  Offset 0x{diffOffset:X8}: Generated=0x{genByte:X2}, Reference=0x{refByte:X2}\n";
            }
            if (differences.Count == 10)
            {
                diffMessage += $"  ... (showing first 10 differences, there may be more)\n";
            }
            Assert.Fail(diffMessage);
        }
        
        // Test passed - generated ISO matches reference ISO
        // Generated ISO remains after test as requested
    }

    [Fact]
    public void GeneratedCci_CanExtractAllFiles()
    {
        // Arrange
        var solutionRoot = FindSolutionRoot();
        var testsDirectory = Path.Combine(solutionRoot, "tests");
        var generatedCciPath = Path.Combine(testsDirectory, "TestDirectory_Generated.cci");
        var extractedDirectoryPath = Path.Combine(testsDirectory, "TestDirectory_Extracted");
        
        // Clean up previous generated CCI and extracted directory before running the test
        if (File.Exists(generatedCciPath))
        {
            File.Delete(generatedCciPath);
        }
        if (Directory.Exists(extractedDirectoryPath))
        {
            Directory.Delete(extractedDirectoryPath, true);
        }
        
        // Act - Generate CCI from TestDirectory using C# code
        bool success;
        try
        {
            success = ContainerUtility.ConvertFolderToCCI(
                TestDirectoryPath,
                ISOFormat.XboxOriginal,
                generatedCciPath,
                0, // No split point
                null // No progress callback
            );
        }
        catch (Exception ex)
        {
            Assert.Fail($"CCI generation threw an exception: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return;
        }
        
        // Assert CCI was created successfully
        Assert.True(Directory.Exists(TestDirectoryPath), $"Test directory should exist at: {TestDirectoryPath}");
        Assert.True(success, "CCI generation should succeed");
        Assert.True(File.Exists(generatedCciPath), $"Generated CCI should exist at: {generatedCciPath}");
        
        // Mount and extract files from CCI
        using var cciReader = new CCIContainerReader(generatedCciPath);
        Assert.True(cciReader.TryMount(), "CCI should mount successfully");
        
        try
        {
            Assert.True(ContainerUtility.ExtractFilesFromContainer(cciReader, extractedDirectoryPath), 
                "Files should extract successfully from CCI");
        }
        finally
        {
            cciReader.Dismount();
        }
        
        Assert.True(Directory.Exists(extractedDirectoryPath), $"Extracted directory should exist at: {extractedDirectoryPath}");
        
        // Compare file structure between original and extracted
        var originalFiles = GetAllFiles(TestDirectoryPath);
        var extractedFiles = GetAllFiles(extractedDirectoryPath);
        
        // Normalize paths for comparison (remove TestDirectory prefix from original, remove TestDirectory_Extracted from extracted)
        var originalRelativeFiles = originalFiles.Select(f => 
            Path.GetRelativePath(TestDirectoryPath, f).Replace('\\', '/')).OrderBy(f => f).ToList();
        var extractedRelativeFiles = extractedFiles.Select(f => 
            Path.GetRelativePath(extractedDirectoryPath, f).Replace('\\', '/')).OrderBy(f => f).ToList();
        
        // Debug output
        Console.WriteLine($"Original directory file count: {originalRelativeFiles.Count}");
        Console.WriteLine($"Extracted directory file count: {extractedRelativeFiles.Count}");
        
        Console.WriteLine($"\nOriginal files ({originalRelativeFiles.Count} total):");
        foreach (var file in originalRelativeFiles)
        {
            Console.WriteLine($"  - {file}");
        }
        
        Console.WriteLine($"\nExtracted files ({extractedRelativeFiles.Count} total):");
        foreach (var file in extractedRelativeFiles)
        {
            Console.WriteLine($"  - {file}");
        }
        
        // Compare file lists
        var missingInExtracted = originalRelativeFiles.Except(extractedRelativeFiles).ToList();
        var extraInExtracted = extractedRelativeFiles.Except(originalRelativeFiles).ToList();
        
        if (missingInExtracted.Count > 0 || extraInExtracted.Count > 0)
        {
            var diffMessage = "File listing comparison found differences:\n";
            if (missingInExtracted.Count > 0)
            {
                diffMessage += $"  Missing in extracted CCI ({missingInExtracted.Count} files):\n";
                foreach (var file in missingInExtracted.Take(20))
                {
                    diffMessage += $"    - {file}\n";
                }
                if (missingInExtracted.Count > 20)
                {
                    diffMessage += $"    ... and {missingInExtracted.Count - 20} more\n";
                }
            }
            if (extraInExtracted.Count > 0)
            {
                diffMessage += $"  Extra in extracted CCI ({extraInExtracted.Count} files):\n";
                foreach (var file in extraInExtracted.Take(20))
                {
                    diffMessage += $"    + {file}\n";
                }
                if (extraInExtracted.Count > 20)
                {
                    diffMessage += $"    ... and {extraInExtracted.Count - 20} more\n";
                }
            }
            Assert.Fail(diffMessage);
        }
        
        // Compare file sizes and contents
        foreach (var originalFile in originalFiles)
        {
            var relativePath = Path.GetRelativePath(TestDirectoryPath, originalFile).Replace('\\', '/');
            var extractedFile = Path.Combine(extractedDirectoryPath, relativePath);
            
            Assert.True(File.Exists(extractedFile), $"Extracted file should exist: {relativePath}");
            
            var originalInfo = new FileInfo(originalFile);
            var extractedInfo = new FileInfo(extractedFile);
            
            Assert.True(originalInfo.Length == extractedInfo.Length, 
                $"File size should match for {relativePath}. Original: {originalInfo.Length}, Extracted: {extractedInfo.Length}");
            
            // Compare file contents byte-by-byte
            using var originalStream = File.OpenRead(originalFile);
            using var extractedStream = File.OpenRead(extractedFile);
            
            Assert.True(originalStream.Length == extractedStream.Length, 
                $"Stream lengths should match for {relativePath}");
            
            const int bufferSize = 1024 * 1024; // 1MB buffer
            var originalBuffer = new byte[bufferSize];
            var extractedBuffer = new byte[bufferSize];
            var offset = 0L;
            
            while (offset < originalStream.Length)
            {
                var bytesToRead = (int)Math.Min(bufferSize, originalStream.Length - offset);
                var originalBytesRead = originalStream.Read(originalBuffer, 0, bytesToRead);
                var extractedBytesRead = extractedStream.Read(extractedBuffer, 0, bytesToRead);
                
                Assert.True(originalBytesRead == extractedBytesRead, 
                    $"Bytes read should match for {relativePath} at offset {offset}");
                
                for (int i = 0; i < originalBytesRead; i++)
                {
                    if (originalBuffer[i] != extractedBuffer[i])
                    {
                        Assert.Fail($"File content mismatch for {relativePath} at offset {offset + i}. " +
                            $"Original: 0x{originalBuffer[i]:X2}, Extracted: 0x{extractedBuffer[i]:X2}");
                    }
                }
                
                offset += originalBytesRead;
            }
        }
        
        // Clean up extracted directory after test (but keep the CCI)
        if (Directory.Exists(extractedDirectoryPath))
        {
            Directory.Delete(extractedDirectoryPath, true);
        }
    }
    
    /// <summary>
    /// Gets all files recursively from a directory.
    /// </summary>
    /// <param name="directoryPath">The directory to scan.</param>
    /// <returns>List of all file paths.</returns>
    private static List<string> GetAllFiles(string directoryPath)
    {
        var files = new List<string>();
        
        if (!Directory.Exists(directoryPath))
        {
            return files;
        }
        
        try
        {
            files.AddRange(Directory.GetFiles(directoryPath));
            
            foreach (var subdirectory in Directory.GetDirectories(directoryPath))
            {
                files.AddRange(GetAllFiles(subdirectory));
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error scanning directory {directoryPath}: {ex.Message}", ex);
        }
        
        return files;
    }

}

