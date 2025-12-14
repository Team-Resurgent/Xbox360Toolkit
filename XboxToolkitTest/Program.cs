using Microsoft.VisualBasic;
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


        static void Main()
        {
            ProcessPngs();


            var z = ContainerUtility.TryAutoDetectContainerType(@"U:\Done\Gun (USA) (En,Fr,Es,It).cci", out var cc);

            {
                var m = cc.TryMount();
                var n = cc.TryGetDefault(out var defaultData2, out var container);
                File.WriteAllBytes(@"J:\iso.xex", defaultData2);
                var qq = XexUtility.TryExtractXexMetaData(defaultData2, out  var metadata);

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
    }
}