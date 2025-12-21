namespace XboxToolkit.Internal.ContainerBuilder
{
    internal class SectorAllocator
    {
        private uint mCurrentSector;
        private readonly uint mMagicSector;
        private readonly uint mBaseSector;
        private uint mDirectoryStartSector;
        private bool mDirectoriesAllocated;
        private uint mFixedRootDirSector;
        private uint mFixedRootDirSectors;

        public SectorAllocator(uint magicSector, uint baseSector)
        {
            mMagicSector = magicSector;
            mBaseSector = baseSector;
            mCurrentSector = baseSector;
            mDirectoriesAllocated = false;
            mFixedRootDirSector = 0;
            mFixedRootDirSectors = 0;
        }

        public void SetFixedRootDirectory(uint sector, uint sectors)
        {
            mFixedRootDirSector = sector;
            mFixedRootDirSectors = sectors;
        }

        public uint AllocateSectors(uint count)
        {
            // Skip magic sector if we would write over it
            if (mCurrentSector <= mMagicSector && mCurrentSector + count > mMagicSector)
            {
                mCurrentSector = mMagicSector + 1;
            }

            var startSector = mCurrentSector;
            mCurrentSector += count;
            return startSector;
        }

        public uint AllocateDirectorySectors(uint count)
        {
            // Directories should be allocated after magic sector
            // Store where directories start for later file allocation
            if (!mDirectoriesAllocated)
            {
                mDirectoryStartSector = mMagicSector + 1;
                
                // If there's a fixed root directory, skip past it
                if (mFixedRootDirSector > 0)
                {
                    var fixedRootDirEnd = mFixedRootDirSector + mFixedRootDirSectors;
                    // Make sure we start after the fixed root directory
                    if (mDirectoryStartSector < fixedRootDirEnd)
                    {
                        mDirectoryStartSector = fixedRootDirEnd;
                    }
                }
                
                mCurrentSector = mDirectoryStartSector;
                mDirectoriesAllocated = true;
            }

            var startSector = mCurrentSector;
            mCurrentSector += count;
            return startSector;
        }

        public uint AllocateFileSectors(uint count)
        {
            // ALWAYS check if allocation would conflict with fixed root directory (regardless of current sector)
            if (mFixedRootDirSector > 0)
            {
                // Check if current allocation overlaps with fixed root directory
                // Overlap occurs if allocation starts before the end of fixed root directory AND ends after the start
                var allocationStart = mCurrentSector;
                var allocationEnd = mCurrentSector + count;
                var fixedRootDirEnd = mFixedRootDirSector + mFixedRootDirSectors;
                
                // Check for overlap: allocation overlaps if it starts before fixed root ends AND ends after fixed root starts
                if (allocationStart < fixedRootDirEnd && allocationEnd > mFixedRootDirSector)
                {
                    // Skip past fixed root directory
                    mCurrentSector = fixedRootDirEnd;
                }
            }
            
            // Try to allocate files in the gap between base sector and magic sector first
            // If that space is full, allocate after magic sector (and after directories if they exist)
            if (mCurrentSector < mMagicSector)
            {
                // Check if we can fit in the gap before magic sector (after skipping fixed root directory if needed)
                if (mCurrentSector < mMagicSector && mCurrentSector + count <= mMagicSector)
                {
                    var startSector = mCurrentSector;
                    mCurrentSector += count;
                    return startSector;
                }
                else if (mCurrentSector < mMagicSector)
                {
                    // Gap is too small, skip to after magic sector
                    mCurrentSector = mMagicSector + 1;
                }
            }

            // If we're at or past magic sector, allocate after directories (if they exist) or right after magic sector
            if (mDirectoriesAllocated && mCurrentSector < mDirectoryStartSector)
            {
                // Fill space between magic sector and directories
                var startSector = mCurrentSector;
                mCurrentSector += count;
                return startSector;
            }

            // If directories are allocated, make sure we're after them
            if (mDirectoriesAllocated && mCurrentSector < mDirectoryStartSector)
            {
                mCurrentSector = mDirectoryStartSector;
            }

            // Allocate after magic sector (and after directories if they exist)
            var fileStartSector = mCurrentSector;
            mCurrentSector += count;
            return fileStartSector;
        }

        public uint GetTotalSectors()
        {
            return mCurrentSector;
        }
    }
}

