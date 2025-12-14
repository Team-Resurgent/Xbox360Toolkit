using System.Collections.Generic;

namespace Xbox360Toolkit.Internal.ContainerBuilder
{
    internal class DirectoryEntry
    {
        public string Path { get; set; } = string.Empty;
        public List<FileEntry> Files { get; set; } = new List<FileEntry>();
        public List<DirectoryEntry> Subdirectories { get; set; } = new List<DirectoryEntry>();
        public uint Sector { get; set; }
    }
}

