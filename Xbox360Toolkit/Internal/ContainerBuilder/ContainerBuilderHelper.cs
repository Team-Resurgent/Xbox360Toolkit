using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xbox360Toolkit.Internal;

namespace Xbox360Toolkit.Internal.ContainerBuilder
{
    internal static class ContainerBuilderHelper
    {
        public static void ScanFolder(string basePath, string relativePath, List<FileEntry> fileEntries, List<DirectoryEntry> directoryEntries)
        {
            var fullPath = string.IsNullOrEmpty(relativePath) ? basePath : Path.Combine(basePath, relativePath);
            
            var dirEntry = new DirectoryEntry { Path = relativePath };
            directoryEntries.Add(dirEntry);

            var files = Directory.GetFiles(fullPath);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileName(file);
                var fileRelativePath = string.IsNullOrEmpty(relativePath) ? fileName : Path.Combine(relativePath, fileName).Replace('\\', '/');
                
                fileEntries.Add(new FileEntry
                {
                    RelativePath = fileRelativePath,
                    FullPath = file,
                    Size = (uint)fileInfo.Length
                });
                dirEntry.Files.Add(fileEntries.Last());
            }

            var subdirs = Directory.GetDirectories(fullPath);
            foreach (var subdir in subdirs)
            {
                var dirName = Path.GetFileName(subdir);
                var subdirRelativePath = string.IsNullOrEmpty(relativePath) ? dirName : Path.Combine(relativePath, dirName).Replace('\\', '/');
                ScanFolder(basePath, subdirRelativePath, fileEntries, directoryEntries);
            }
        }

        public static DirectoryEntry BuildDirectoryTree(List<DirectoryEntry> directoryEntries, List<FileEntry> fileEntries, string rootPath)
        {
            var root = directoryEntries.FirstOrDefault(d => d.Path == rootPath);
            if (root == null)
            {
                root = new DirectoryEntry { Path = rootPath };
            }

            // Get files in this directory
            root.Files = fileEntries.Where(f => 
            {
                var fileDir = Path.GetDirectoryName(f.RelativePath)?.Replace('\\', '/') ?? string.Empty;
                if (string.IsNullOrEmpty(rootPath))
                {
                    return string.IsNullOrEmpty(fileDir) || fileDir == ".";
                }
                return fileDir == rootPath;
            }).ToList();

            // Get subdirectories
            root.Subdirectories = directoryEntries
                .Where(d => 
                {
                    if (d.Path == rootPath) return false;
                    var parentPath = Path.GetDirectoryName(d.Path)?.Replace('\\', '/') ?? string.Empty;
                    if (string.IsNullOrEmpty(rootPath))
                    {
                        return !d.Path.Contains('/');
                    }
                    return parentPath == rootPath;
                })
                .Select(d => BuildDirectoryTree(directoryEntries, fileEntries, d.Path))
                .ToList();

            return root;
        }

        public static void CalculateDirectorySizes(DirectoryEntry directory, Dictionary<string, uint> directorySizes, uint sectorSize)
        {
            uint totalSize = 0;

            // Calculate size needed for all entries in this directory
            var entries = new List<(bool isDir, string name, uint size)>();
            
            foreach (var file in directory.Files)
            {
                var fileName = Path.GetFileName(file.RelativePath);
                entries.Add((false, fileName, file.Size));
            }

            foreach (var subdir in directory.Subdirectories)
            {
                CalculateDirectorySizes(subdir, directorySizes, sectorSize);
                var dirName = Path.GetFileName(subdir.Path);
                entries.Add((true, dirName, directorySizes[subdir.Path]));
            }

            // Each entry is: left(2) + right(2) + sector(4) + size(4) + attribute(1) + nameLength(1) + filename
            foreach (var entry in entries)
            {
                var nameBytes = Encoding.ASCII.GetBytes(entry.name);
                var entrySize = 2 + 2 + 4 + 4 + 1 + 1 + (uint)nameBytes.Length;
                totalSize += entrySize;
            }

            // Round up to sector size
            totalSize = Helpers.RoundToMultiple(totalSize, sectorSize);
            directorySizes[directory.Path] = totalSize;
        }

        public static void AllocateDirectorySectors(DirectoryEntry directory, Dictionary<string, uint> directorySizes, SectorAllocator allocator, uint baseSector)
        {
            // Root directory is already allocated, skip it
            if (directory.Path != string.Empty)
            {
                var dirSize = directorySizes[directory.Path];
                var dirSectors = Helpers.RoundToMultiple(dirSize, Constants.XGD_SECTOR_SIZE) / Constants.XGD_SECTOR_SIZE;
                directory.Sector = allocator.AllocateDirectorySectors(dirSectors) - baseSector;
            }

            foreach (var subdir in directory.Subdirectories)
            {
                AllocateDirectorySectors(subdir, directorySizes, allocator, baseSector);
            }
        }

        public static uint GetDirectorySector(string path, DirectoryEntry root, uint baseSector)
        {
            if (root.Path == path)
            {
                return root.Sector + baseSector;
            }

            foreach (var subdir in root.Subdirectories)
            {
                var result = GetDirectorySector(path, subdir, baseSector);
                if (result != 0)
                {
                    return result;
                }
            }

            return 0;
        }

        public static void BuildDirectoryData(DirectoryEntry directory, List<FileEntry> fileEntries, Dictionary<string, uint> directorySizes, Dictionary<string, byte[]> directoryData, uint baseSector)
        {
            var entries = new List<(bool isDir, string name, uint sector, uint size, string path)>();
            
            foreach (var file in directory.Files)
            {
                var fileName = Path.GetFileName(file.RelativePath);
                entries.Add((false, fileName, file.Sector, file.Size, string.Empty));
            }

            foreach (var subdir in directory.Subdirectories)
            {
                BuildDirectoryData(subdir, fileEntries, directorySizes, directoryData, baseSector);
                var dirName = Path.GetFileName(subdir.Path);
                entries.Add((true, dirName, subdir.Sector, directorySizes[subdir.Path], subdir.Path));
            }

            // Sort entries by name for binary tree
            entries.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

            // Build binary tree structure
            var dirSize = directorySizes[directory.Path];
            var dirData = new byte[dirSize];
            var offset = 0u;

            BuildBinaryTree(entries, dirData, ref offset, 0, entries.Count - 1, baseSector);

            directoryData[directory.Path] = dirData;
        }

        private static void BuildBinaryTree(List<(bool isDir, string name, uint sector, uint size, string path)> entries, byte[] dirData, ref uint offset, int start, int end, uint baseSector)
        {
            if (start > end)
            {
                return;
            }

            var currentOffset = offset;
            var mid = (start + end) / 2;
            var entry = entries[mid];

            offset += (uint)(2 + 2 + 4 + 4 + 1 + 1 + entry.name.Length);

            // Calculate left and right child offsets
            ushort leftOffset = 0xFFFF;
            ushort rightOffset = 0xFFFF;

            if (start < mid)
            {
                var leftStartOffset = offset;
                BuildBinaryTree(entries, dirData, ref offset, start, mid - 1, baseSector);
                leftOffset = (ushort)(leftStartOffset / 4);
            }

            if (mid < end)
            {
                var rightStartOffset = offset;
                BuildBinaryTree(entries, dirData, ref offset, mid + 1, end, baseSector);
                rightOffset = (ushort)(rightStartOffset / 4);
            }

            // Write entry at currentOffset
            var nameBytes = Encoding.ASCII.GetBytes(entry.name);
            using (var stream = new MemoryStream(dirData))
            using (var writer = new BinaryWriter(stream))
            {
                stream.Position = currentOffset;
                writer.Write(leftOffset);
                writer.Write(rightOffset);
                writer.Write(entry.sector);
                writer.Write(entry.size);
                writer.Write((byte)(entry.isDir ? 0x10 : 0x00));
                writer.Write((byte)nameBytes.Length);
                writer.Write(nameBytes);
            }
        }

        public static void WriteXgdHeader(byte[] sector, uint rootDirSector, uint rootDirSize)
        {
            using (var stream = new MemoryStream(sector))
            using (var writer = new BinaryWriter(stream))
            {
                var magic = Encoding.UTF8.GetBytes(Constants.XGD_IMAGE_MAGIC);
                writer.Write(magic);
                if (magic.Length < 20)
                {
                    writer.Write(new byte[20 - magic.Length]);
                }

                writer.Write(rootDirSector);
                writer.Write(rootDirSize);
                writer.Write(DateTime.Now.ToFileTime());
                writer.Write(new byte[0x7c8]); // Padding
                
                writer.Write(magic); // MagicTail
                if (magic.Length < 20)
                {
                    writer.Write(new byte[20 - magic.Length]);
                }
            }
        }
    }
}

