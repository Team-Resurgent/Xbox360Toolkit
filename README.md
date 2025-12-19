# XboxToolkit

A comprehensive .NET library for working with Xbox 360 and Xbox Original game containers, executables, and metadata.

## Overview

XboxToolkit provides a complete set of tools for reading, extracting, and creating Xbox game container formats. The library supports multiple container types, XEX file processing, and marketplace metadata integration.

## Features

### Container Support
- **ISO Containers**: Read Xbox Original and Xbox 360 ISO files
- **CCI Containers**: Read and decode compressed container images (LZ4 compressed)
- **GOD Containers**: Read Game on Demand (GOD) container formats
- **Multi-slice Support**: Automatic handling of split container files

### XEX File Processing
- Extract metadata from XEX executables
- Decrypt retail and devkit XEX files
- Decompress compressed XEX data
- Extract game information (Title ID, Media ID, version, regions)
- Extract thumbnails and localized strings
- Parse XDBF (Xbox Data Base Format) structures
- Extract XSRC (Xbox Source) XML data

### Container Building
- Create ISO files from folder structures
- Support for both Xbox Original and Xbox 360 ISO formats
- Progress reporting for long operations

### Marketplace Integration
- Fetch game metadata from Xbox Marketplace
- Download game images and thumbnails
- Parse marketplace XML data

### Additional Features
- XGD (Xbox Game Disc) information extraction
- Sector-level decoding and reading
- Cross-platform native library support (Windows, Linux, macOS)

## Installation

### NuGet Package

```bash
Install-Package XboxToolkit
```

The package includes native libraries for Windows (x64), Linux (x64), and macOS.

## Requirements

- .NET Standard 2.0 or higher
- Native libraries are included in the NuGet package

## Usage Examples

### Reading an ISO Container

```csharp
using XboxToolkit;

// Auto-detect container type
if (ContainerUtility.TryAutoDetectContainerType("game.iso", out var containerReader))
{
    if (containerReader.TryMount())
    {
        // Extract files from container
        ContainerUtility.ExtractFilesFromContainer(containerReader, "output_folder");
        
        containerReader.Dismount();
        containerReader.Dispose();
    }
}
```

### Reading a CCI Container

```csharp
var cciReader = new CCIContainerReader("game.cci");
if (cciReader.TryMount())
{
    var decoder = cciReader.GetDecoder();
    // Use decoder to read sectors...
    cciReader.Dismount();
    cciReader.Dispose();
}
```

### Extracting XEX Metadata

```csharp
byte[] xexData = File.ReadAllBytes("default.xex");
if (XexUtility.TryExtractXexMetaData(xexData, out var metaData))
{
    Console.WriteLine($"Title: {metaData.TitleName}");
    Console.WriteLine($"Title ID: {metaData.TitleId:X8}");
    Console.WriteLine($"Publisher: {metaData.Publisher}");
    Console.WriteLine($"Developer: {metaData.Developer}");
    Console.WriteLine($"Version: {metaData.Version}");
}
```

### Creating an ISO from Folder

```csharp
// Create Xbox 360 ISO
ContainerUtility.ConvertFolderToISO(
    "input_folder",
    ISOFormat.Xbox360,
    "output.iso",
    splitPoint: 0,
    progress: (percent) => Console.WriteLine($"Progress: {percent:P}")
);
```

### Fetching Marketplace Metadata

```csharp
uint titleId = 0x41560817; // Example Title ID
string marketplaceUrl = MarketplaceUtility.GetMarketPlaceUrl(titleId);
// Use the URL to fetch additional metadata
```

## Supported Formats

- **ISO**: Xbox Original and Xbox 360 disc images
- **CCI**: Compressed Container Image (LZ4 compressed)
- **GOD**: Game on Demand container format
- **XEX**: Xbox 360 executable format
- **XDBF**: Xbox Data Base Format
- **XSRC**: Xbox Source XML format

## Project Structure

The main library is located in the `XboxToolkit` directory. This project focuses on the core library functionality and does not include test projects.

## License

This project is licensed under the GPL-3.0-only License.

## Authors

- **EqUiNoX** - Team Resurgent

## Repository

- **GitHub**: [https://github.com/Team-Resurgent/XboxToolkit](https://github.com/Team-Resurgent/XboxToolkit)

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## Acknowledgments

This library uses the following dependencies:
- **K4os.Compression.LZ4** - LZ4 compression support
- **LibDeflate.NET** - GZIP decompression support
