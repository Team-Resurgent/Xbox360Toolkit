using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XboxToolkit;
using XboxToolkit.Interface;

namespace XboxToolkitTest
{
    internal static class DirectoryStructureTest
    {
        public static void TestDirectoryStructure()
        {
            Console.WriteLine("=== Testing Directory Structure ===");
            Console.WriteLine();

            // Create a simple test ISO with known files
            var testFolder = Path.Combine(Path.GetTempPath(), "XboxToolkitTest_" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Directory.CreateDirectory(testFolder);

            try
            {
                // Create test files
                File.WriteAllText(Path.Combine(testFolder, "file1.txt"), "Test file 1");
                File.WriteAllText(Path.Combine(testFolder, "file2.txt"), "Test file 2");
                File.WriteAllText(Path.Combine(testFolder, "file3.txt"), "Test file 3");

                var testIso = Path.Combine(Path.GetTempPath(), "XboxToolkitTest_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".iso");
                
                Console.WriteLine($"Creating test ISO from: {testFolder}");
                Console.WriteLine($"Output ISO: {testIso}");
                Console.WriteLine();

                // Create ISO
                var success = ContainerUtility.ConvertFolderToISO(
                    testFolder,
                    ISOFormat.XboxOriginal,
                    testIso,
                    0,
                    null
                );

                if (!success)
                {
                    Console.WriteLine("ERROR: Failed to create ISO");
                    return;
                }

                Console.WriteLine($"ISO created: {new FileInfo(testIso).Length} bytes");
                Console.WriteLine();

                // Try to read it back
                Console.WriteLine("Reading ISO structure:");
                Console.WriteLine("----------------------");

                using (var isoReader = new ISOContainerReader(testIso))
                {
                    if (!isoReader.TryMount())
                    {
                        Console.WriteLine("ERROR: Failed to mount ISO");
                        return;
                    }

                    var decoder = isoReader.GetDecoder();
                    var xgdInfo = decoder.GetXgdInfo();

                    Console.WriteLine($"BaseSector: {xgdInfo.BaseSector}");
                    Console.WriteLine($"RootDirSector: {xgdInfo.RootDirSector}");
                    Console.WriteLine($"RootDirSize: {xgdInfo.RootDirSize}");
                    Console.WriteLine();

                    // Read root directory
                    const uint SECTOR_SIZE = 0x800;
                    var rootSectors = xgdInfo.RootDirSize / SECTOR_SIZE;
                    var rootData = new byte[xgdInfo.RootDirSize];
                    for (var i = 0; i < rootSectors; i++)
                    {
                        var currentRootSector = xgdInfo.BaseSector + xgdInfo.RootDirSector + (uint)i;
                        if (decoder.TryReadSector(currentRootSector, out var sectorData) == false)
                        {
                            Console.WriteLine($"ERROR: Failed to read root sector {i}");
                            return;
                        }
                        Array.Copy(sectorData, 0, rootData, i * SECTOR_SIZE, SECTOR_SIZE);
                    }

                    Console.WriteLine($"Root directory data: {rootData.Length} bytes");
                    Console.WriteLine();

                    // Try to read entries
                    var treeNodes = new List<(byte[] data, uint offset, string path)>
                    {
                        (rootData, 0, string.Empty)
                    };

                    int entryCount = 0;
                    int errorCount = 0;

                    while (treeNodes.Count > 0)
                    {
                        var (data, offset, path) = treeNodes[0];
                        treeNodes.RemoveAt(0);

                        using (var stream = new MemoryStream(data))
                        using (var reader = new BinaryReader(stream))
                        {
                            if (offset * 4 >= stream.Length)
                            {
                                Console.WriteLine($"  WARNING: Offset {offset} * 4 = {offset * 4} >= {stream.Length}, skipping");
                                continue;
                            }

                            stream.Position = offset * 4;

                            // Check if we can read at least 14 bytes
                            if (stream.Position + 14 > stream.Length)
                            {
                                Console.WriteLine($"  ERROR: Cannot read full entry at offset {offset} (position {stream.Position}, length {stream.Length})");
                                errorCount++;
                                continue;
                            }

                            var left = reader.ReadUInt16();
                            var right = reader.ReadUInt16();
                            var sector = reader.ReadUInt32();
                            var size = reader.ReadUInt32();
                            var attribute = reader.ReadByte();
                            var nameLength = reader.ReadByte();

                            Console.WriteLine($"  Entry #{entryCount + 1} at offset {offset} (byte {offset * 4}):");
                            Console.WriteLine($"    Left: {left} (0x{left:X4}), Right: {right} (0x{right:X4})");
                            Console.WriteLine($"    Sector: {sector}, Size: {size}");
                            Console.WriteLine($"    Attribute: {attribute:X2}, NameLength: {nameLength}");

                            if (nameLength == 0 || nameLength > 255)
                            {
                                Console.WriteLine($"    ERROR: Invalid nameLength: {nameLength}");
                                errorCount++;
                                break;
                            }

                            if (stream.Position + nameLength > stream.Length)
                            {
                                Console.WriteLine($"    ERROR: Cannot read filename (position {stream.Position}, need {nameLength} bytes, length {stream.Length})");
                                errorCount++;
                                break;
                            }

                            var filenameBytes = reader.ReadBytes(nameLength);
                            var filename = Encoding.ASCII.GetString(filenameBytes);

                            Console.WriteLine($"    Filename: '{filename}'");
                            
                            // Debug: show raw bytes at this position and calculate next entry position
                            var entryEndPosition = stream.Position;
                            var entrySize = entryEndPosition - (offset * 4);
                            Console.WriteLine($"    Entry size: {entrySize} bytes, ends at byte {entryEndPosition}");
                            
                            // Show what's at the next expected offset
                            if (left != 0xFFFF && left != 0)
                            {
                                var nextOffsetByte = left * 4;
                                if (nextOffsetByte < stream.Length)
                                {
                                    stream.Position = nextOffsetByte;
                                    var nextRawBytes = reader.ReadBytes(Math.Min(32, (int)(stream.Length - stream.Position)));
                                    var nextHexBytes = string.Join(" ", nextRawBytes.Select(b => b.ToString("X2")));
                                    Console.WriteLine($"    Next entry (left) should be at offset {left} (byte {nextOffsetByte}): {nextHexBytes}");
                                }
                            }
                            Console.WriteLine();

                            if (string.IsNullOrEmpty(filename))
                            {
                                Console.WriteLine($"    ERROR: Empty filename at offset {offset}!");
                                Console.WriteLine($"    nameLength={nameLength}, but filename is empty");
                                Console.WriteLine($"    This suggests the entry wasn't written correctly or we're reading from wrong location");
                                errorCount++;
                                break;
                            }

                            entryCount++;

                            if (left == 0xFFFF)
                            {
                                Console.WriteLine($"    No children (left=0xFFFF)");
                                continue;
                            }

                            if (left != 0 && left != 0xFFFF)
                            {
                                Console.WriteLine($"    Adding left child at offset {left}");
                                treeNodes.Add((data, left, path));
                            }

                            if (right != 0 && right != 0xFFFF)
                            {
                                Console.WriteLine($"    Adding right child at offset {right}");
                                treeNodes.Add((data, right, path));
                            }
                        }
                    }

                    Console.WriteLine("----------------------");
                    Console.WriteLine($"Total entries read: {entryCount}");
                    Console.WriteLine($"Expected entries: 3");
                    Console.WriteLine($"Errors: {errorCount}");
                    Console.WriteLine();

                    if (errorCount > 0)
                    {
                        Console.WriteLine("ERROR: Found errors reading directory structure!");
                    }
                    else if (entryCount != 3)
                    {
                        Console.WriteLine($"ERROR: Entry count mismatch! Expected 3, got {entryCount}");
                    }
                    else
                    {
                        Console.WriteLine("SUCCESS: All entries read correctly");
                    }
                }
            }
            finally
            {
                // Cleanup
                if (Directory.Exists(testFolder))
                {
                    Directory.Delete(testFolder, true);
                }
            }
        }
    }
}
