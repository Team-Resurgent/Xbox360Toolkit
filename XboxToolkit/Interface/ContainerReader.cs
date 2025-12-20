using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using XboxToolkit.Internal.Models;
using XboxToolkit.Internal;

namespace XboxToolkit.Interface
{
    public abstract class ContainerReader : IContainerReader, IDisposable
    {
        public abstract SectorDecoder GetDecoder();

        public abstract bool TryMount();

        public abstract void Dismount();

        public abstract int GetMountCount();

        public bool TryExtractFiles(string destFilePath, Action<string>? progress = null)
        {
            try
            {
                Directory.CreateDirectory(destFilePath);

                var decoder = GetDecoder();
                var xgdInfo = decoder.GetXgdInfo();

                var rootSectors = (xgdInfo.RootDirSize + Constants.XGD_SECTOR_SIZE - 1) / Constants.XGD_SECTOR_SIZE;
                var rootData = new byte[xgdInfo.RootDirSize];
                for (var i = 0; i < rootSectors; i++)
                {
                    var currentRootSector = xgdInfo.BaseSector + xgdInfo.RootDirSector + (uint)i;
                    if (decoder.TryReadSector(currentRootSector, out var sectorData) == false)
                    {
                        return false;
                    }
                    var offset = i * Constants.XGD_SECTOR_SIZE;
                    var length = Math.Min(Constants.XGD_SECTOR_SIZE, xgdInfo.RootDirSize - offset);
                    Array.Copy(sectorData, 0, rootData, offset, length);
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

                        // Read first 14 bytes (DirectoryEntryDiskNode structure)
                        var headerBuffer = new byte[14];
                        if (directoryDataStream.Read(headerBuffer, 0, 14) != 14)
                        {
                            continue;
                        }

                        // Check if entry is empty (all 0xFF or all 0x00) - matches xdvdfs deserialize check
                        bool allFF = true;
                        bool allZero = true;
                        for (int i = 0; i < 14; i++)
                        {
                            if (headerBuffer[i] != 0xFF) allFF = false;
                            if (headerBuffer[i] != 0x00) allZero = false;
                        }
                        if (allFF || allZero)
                        {
                            continue;
                        }

                        // Parse the header from buffer (matches xdvdfs deserialize)
                        // xdvdfs uses little-endian deserialization
                        var left = (ushort)(headerBuffer[0] | (headerBuffer[1] << 8));
                        var right = (ushort)(headerBuffer[2] | (headerBuffer[3] << 8));
                        var sector = (uint)(headerBuffer[4] | (headerBuffer[5] << 8) | (headerBuffer[6] << 16) | (headerBuffer[7] << 24));
                        var size = (uint)(headerBuffer[8] | (headerBuffer[9] << 8) | (headerBuffer[10] << 16) | (headerBuffer[11] << 24));
                        var attribute = headerBuffer[12];
                        var nameLength = headerBuffer[13];

                        // Validate nameLength is reasonable
                        if (nameLength == 0 || nameLength > 255)
                        {
                            continue;
                        }

                        // Validate we have enough bytes to read the filename
                        var filenameOffset = entryOffset + 14; // 0xe in xdvdfs
                        if (filenameOffset + nameLength > directoryDataStream.Length)
                        {
                            continue;
                        }

                        // Read filename at offset + 0xe (matches xdvdfs read_from_disk)
                        directoryDataStream.Position = filenameOffset;
                        var filenameBytes = directoryDataDataReader.ReadBytes(nameLength);
                        var filename = Encoding.ASCII.GetString(filenameBytes);

                        // Process current entry first (preorder traversal)
                        var path = Path.Combine(destFilePath, currentTreeNode.Path, filename);
                        var relativePath = string.IsNullOrEmpty(currentTreeNode.Path) ? filename : Path.Combine(currentTreeNode.Path, filename).Replace('\\', '/');
                        
                        // Report progress with current filename
                        progress?.Invoke(relativePath);
                        
                        if ((attribute & 0x10) != 0)
                        {
                            Directory.CreateDirectory(path);
                        }
                        else
                        {
                            if (size > 0)
                            {
                                using (var fileStream = File.OpenWrite(path))
                                {
                                    var readSector = sector + xgdInfo.BaseSector;
                                    var processed = 0U;
                                    while (processed < size)
                                    {
                                        if (decoder.TryReadSector(readSector, out var buffer) == false)
                                        {
                                            return false;
                                        }
                                        var bytesToSave = Math.Min(size - processed, Constants.XGD_SECTOR_SIZE);
                                        fileStream.Write(buffer, 0, (int)bytesToSave);
                                        readSector++;
                                        processed += bytesToSave;
                                    }
                                }
                            }
                            else
                            {
                                File.Create(path).Close();
                            }
                        }

                        // Push right first, then left (so left is processed first when popping)
                        // Validate right offset is within bounds
                        if (right != 0 && right != 0xFFFF)
                        {
                            var rightOffsetBytes = right * 4;
                            if (rightOffsetBytes < directoryDataStream.Length)
                            {
                                treeNodes.Add(new TreeNodeInfo
                                {
                                    DirectoryData = currentTreeNode.DirectoryData,
                                    Offset = right,
                                    Path = currentTreeNode.Path
                                });
                            }
                        }

                        // Validate left offset is within bounds
                        if (left != 0 && left != 0xFFFF)
                        {
                            var leftOffsetBytes = left * 4;
                            if (leftOffsetBytes < directoryDataStream.Length)
                            {
                                treeNodes.Add(new TreeNodeInfo
                                {
                                    DirectoryData = currentTreeNode.DirectoryData,
                                    Offset = left,
                                    Path = currentTreeNode.Path
                                });
                            }
                        }

                        // If directory, load its data and add to stack
                        if ((attribute & 0x10) != 0)
                        {
                            if (size > 0)
                            {
                                var directorySectors = (size + Constants.XGD_SECTOR_SIZE - 1) / Constants.XGD_SECTOR_SIZE;
                                var directoryData = new byte[size];
                                for (var i = 0; i < directorySectors; i++)
                                {
                                    var currentDirectorySector = xgdInfo.BaseSector + sector + (uint)i;
                                    if (decoder.TryReadSector(currentDirectorySector, out var sectorData) == false)
                                    {
                                        return false;
                                    }
                                    var offset = i * Constants.XGD_SECTOR_SIZE;
                                    var length = Math.Min(Constants.XGD_SECTOR_SIZE, size - offset);
                                    Array.Copy(sectorData, 0, directoryData, offset, length);
                                }

                                treeNodes.Add(new TreeNodeInfo
                                {
                                    DirectoryData = directoryData,
                                    Offset = 0,
                                    Path = Path.Combine(currentTreeNode.Path, filename)
                                });
                            }
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
        }

        public bool TryGetDataSectors(out HashSet<uint> dataSectors)
        {
            dataSectors = new HashSet<uint>();

            try
            {
                var decoder = GetDecoder();
                var xgdInfo = decoder.GetXgdInfo();

                dataSectors.Add(xgdInfo.BaseSector + Constants.XGD_ISO_BASE_SECTOR);
                dataSectors.Add(xgdInfo.BaseSector + Constants.XGD_ISO_BASE_SECTOR + 1);

                var rootSectors = xgdInfo.RootDirSize / Constants.XGD_SECTOR_SIZE;
                var rootData = new byte[xgdInfo.RootDirSize];
                for (var i = 0; i < rootSectors; i++)
                {
                    var currentRootSector = xgdInfo.BaseSector + xgdInfo.RootDirSector + (uint)i;
                    dataSectors.Add(currentRootSector);
                    if (decoder.TryReadSector(currentRootSector, out var sectorData) == false)
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
                                    if (decoder.TryReadSector(currentDirectorySector, out var sectorData) == false)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
        }

        public bool TryGetDefault(out byte[] defaultData, out ContainerType containerType)
        {
            defaultData = Array.Empty<byte>();
            containerType = ContainerType.Unknown;

            try
            {
                var decoder = GetDecoder();
                var xgdInfo = decoder.GetXgdInfo();

                var rootSectors = xgdInfo.RootDirSize / Constants.XGD_SECTOR_SIZE;
                var rootData = new byte[xgdInfo.RootDirSize];
                for (var i = 0; i < rootSectors; i++)
                {
                    if (decoder.TryReadSector(xgdInfo.BaseSector + xgdInfo.RootDirSector + (uint)i, out var sectorData) == false)
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
                                    if (decoder.TryReadSector(readSector, out var buffer) == false)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
        }

        public virtual void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
