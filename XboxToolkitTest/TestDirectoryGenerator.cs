using System;
using System.IO;

namespace XboxToolkitTest;

/// <summary>
/// Generates a deterministic test directory structure with ~64MB files.
/// Structure: 2 folders, each containing 2 subfolders, each containing 5 files.
/// Each folder level (root, folders, and subfolders) also contains 5 files.
/// Each file is approximately 60-64MB (not a multiple of 2048 bytes) with variance based on file content.
/// Each file is filled with a single byte value starting from 0x01 and incrementing.
/// </summary>
public static class TestDirectoryGenerator
{
    // Base file size at 60MB to allow variance between 60-64MB
    private const long BaseFileSizeBytes = 60L * 1024 * 1024; // 60MB base
    private const int FoldersPerLevel = 2;
    private const int FilesPerDirectory = 5;
    private const byte StartingByte = 0x01;

    /// <summary>
    /// Generates the test directory structure at the specified path.
    /// If the directory already exists, it will be reused without recreation.
    /// </summary>
    /// <param name="rootPath">The root path where the test directory structure will be created.</param>
    /// <returns>The path to the created test directory.</returns>
    public static string GenerateTestDirectory(string rootPath)
    {
        var testDirPath = Path.Combine(rootPath, "TestDirectory");

        // If directory exists, reuse it without recreation
        if (Directory.Exists(testDirPath))
        {
            return testDirPath;
        }

        Directory.CreateDirectory(testDirPath);

        byte currentByte = StartingByte;

        // Create 5 files in root directory
        currentByte = CreateFilesInDirectory(testDirPath, currentByte);

        // Create 2 top-level folders
        for (int folderIndex = 1; folderIndex <= FoldersPerLevel; folderIndex++)
        {
            var folderPath = Path.Combine(testDirPath, $"Folder{folderIndex:D2}");
            Directory.CreateDirectory(folderPath);

            // Create 5 files in each top-level folder
            currentByte = CreateFilesInDirectory(folderPath, currentByte);

            // Create 2 subfolders in each top-level folder
            for (int subFolderIndex = 1; subFolderIndex <= FoldersPerLevel; subFolderIndex++)
            {
                var subFolderPath = Path.Combine(folderPath, $"SubFolder{subFolderIndex:D2}");
                Directory.CreateDirectory(subFolderPath);

                // Create 5 files in each subfolder
                currentByte = CreateFilesInDirectory(subFolderPath, currentByte);
            }
        }

        return testDirPath;
    }

    /// <summary>
    /// Creates files in a directory and returns the next byte value to use.
    /// </summary>
    /// <param name="directoryPath">The directory where files should be created.</param>
    /// <param name="startingByte">The byte value to start with.</param>
    /// <returns>The next byte value to use after creating the files.</returns>
    private static byte CreateFilesInDirectory(string directoryPath, byte startingByte)
    {
        byte currentByte = startingByte;
        
        for (int fileIndex = 1; fileIndex <= FilesPerDirectory; fileIndex++)
        {
            var filePath = Path.Combine(directoryPath, $"file{fileIndex:D2}.dat");
            var fileSize = CalculateFileSize(currentByte);
            CreateFileWithByteValue(filePath, currentByte, fileSize);
            
            // Increment byte value, wrapping at 0xFF back to 0x01
            currentByte++;
            if (currentByte == 0x00)
            {
                currentByte = 0x01;
            }
        }
        
        return currentByte;
    }

    /// <summary>
    /// Calculates a deterministic file size based on the byte value.
    /// Size varies between 60-64MB and is not a multiple of 2048 bytes.
    /// </summary>
    /// <param name="byteValue">The byte value used to determine file size variance.</param>
    /// <returns>The file size in bytes.</returns>
    private static long CalculateFileSize(byte byteValue)
    {
        // Create variance: add 0-4MB based on byte value
        // Use byteValue to create a deterministic but varied size
        // Multiply by a prime number to create better distribution
        var variance = (long)(byteValue * 131) % (4L * 1024 * 1024); // 0 to ~4MB variance
        
        // Base size + variance, ensuring we stay within 60-64MB range
        var size = BaseFileSizeBytes + variance;
        
        // Clamp to ensure we stay within 60-64MB range
        var minSize = 60L * 1024 * 1024;
        var maxSize = 64L * 1024 * 1024;
        if (size < minSize)
        {
            size = minSize;
        }
        if (size > maxSize)
        {
            size = maxSize;
        }
        
        // Ensure not a multiple of 2048 by adding a small offset based on byte value
        // This ensures each file has a unique size that's not sector-aligned
        var remainder = size % 2048;
        if (remainder == 0)
        {
            // Add a small offset (1-2047 bytes) to break alignment
            size += (byteValue % 2047) + 1;
        }
        
        return size;
    }

    /// <summary>
    /// Creates a file of the specified size filled with the given byte value.
    /// </summary>
    /// <param name="filePath">The path where the file should be created.</param>
    /// <param name="value">The byte value to fill the file with.</param>
    /// <param name="fileSize">The size of the file in bytes.</param>
    private static void CreateFileWithByteValue(string filePath, byte value, long fileSize)
    {
        const int bufferSize = 1024 * 1024; // 1MB buffer for efficient writing
        var buffer = new byte[bufferSize];
        
        // Fill buffer with the byte value
        Array.Fill(buffer, value);

        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        
        var bytesWritten = 0L;
        while (bytesWritten < fileSize)
        {
            var bytesToWrite = (int)Math.Min(bufferSize, fileSize - bytesWritten);
            fileStream.Write(buffer, 0, bytesToWrite);
            bytesWritten += bytesToWrite;
        }
    }

}

