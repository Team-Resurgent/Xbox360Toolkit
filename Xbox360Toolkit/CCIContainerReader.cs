using System;
using System.Collections.Generic;
using System.IO;
using Xbox360Toolkit.Interface;
using Xbox360Toolkit.Internal.Decoders;
using Xbox360Toolkit.Internal.Models;

namespace Xbox360Toolkit
{
    public class CCIContainerReader : ContainerReader, IDisposable
    {
        private string mFilePath;
        private int mMountCount;
        private SectorDecoder? mSectorDecoder;
        private bool mDisposed;

        public CCIContainerReader(string filePath)
        {
            mFilePath = filePath;
            mMountCount = 0;
        }

        public override SectorDecoder GetDecoder()
        {
            if (mSectorDecoder == null)
            {
                throw new Exception("Container not mounted.");
            }
            return mSectorDecoder;
        }

        public static bool IsCCI(string filePath)
        {
            if (File.Exists(filePath) == false)
            {
                return false;
            }
            return Path.GetExtension(filePath).Equals(".cci", StringComparison.CurrentCultureIgnoreCase);
        }

        public override bool TryMount()
        {
            try
            {
                if (mMountCount > 0)
                {
                    mMountCount++;
                    return true;
                }

                if (IsCCI(mFilePath) == false)
                {
                    return false;
                }

                using (var fileStream = new FileStream(mFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var binaryReader = new BinaryReader(fileStream))
                {
                    var header = binaryReader.ReadUInt32();
                    if (header != 0x4D494343)
                    {
                        throw new IOException("Invalid magic value in cci header.");
                    }

                    uint headerSize = binaryReader.ReadUInt32();
                    if (headerSize != 32)
                    {
                        throw new IOException("Invalid header size in cci header.");
                    }

                    ulong uncompressedSize = binaryReader.ReadUInt64();

                    ulong indexOffset = binaryReader.ReadUInt64();

                    uint blockSize = binaryReader.ReadUInt32();
                    if (blockSize != 2048)
                    {
                        throw new IOException("Invalid block size in cci header.");
                    }

                    byte version = binaryReader.ReadByte();
                    if (version != 1)
                    {
                        throw new IOException("Invalid version in cci header.");
                    }

                    byte indexAlignment = binaryReader.ReadByte();
                    if (indexAlignment != 2)
                    {
                        throw new IOException("Invalid index alignment in cci header.");
                    }

                    var entries = (int)(uncompressedSize / (ulong)blockSize);

                    fileStream.Position = (long)indexOffset;

                    var indexInfo = new List<CCIIndex>();
                    for (var i = 0; i <= entries; i++)
                    {
                        var index = binaryReader.ReadUInt32();
                        indexInfo.Add(new CCIIndex
                        {
                            Value = (ulong)(index & 0x7FFFFFFF) << indexAlignment,
                            LZ4Compressed = (index & 0x80000000) > 0
                        });
                    }

                    var cciDetails = new CCIDetails
                    {
                        FilePath = mFilePath,
                        IndexInfo = indexInfo.ToArray()
                    };

                    mSectorDecoder = new CCISectorDecoder(cciDetails);
                    if (mSectorDecoder.Init() == false)
                    {
                        return false;
                    }

                    mMountCount++;
                    return true;

                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Print(ex.ToString());
                return false;
            }
        }

        public override void Dismount()
        {
            if (mMountCount == 0)
            {
                return;
            }
            mMountCount--;
        }

        public override int GetMountCount()
        {
            return mMountCount;
        }

        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (mDisposed == false)
            {
                if (disposing)
                {
                    mSectorDecoder?.Dispose();
                }
                mDisposed = true;
            }
        }
    }
}
