using Xbox360Toolkit;

namespace Xbox360ToolkitTest
{
    internal class Program
    {
        static void Main()
        {
            var isoTest = true;

            var xexData = Array.Empty<byte>();
            var defaultType = DefaultType.None;

            if (isoTest)
            {
                //var filePath = @"G:\Xbox360\Crysis.1.RF.X360-ZTM\45410968\00007000\801FA873A37D786F8DB0879D89FCEF5245";
                //var filePath = @"G:\Xbox360\MEMORICK - KNIGHTS APPRENT (USA-PAL).iso";
                var filePath = @"G:\Xbox360\Barbie Horse Adventures - Wild Horse Rescue (USA).iso";
                //var filePath = @"G:\Xbox360\Far Cry 3 (USA, Europe) (En,Fr,De,Es,It,Nl,Pt,Sv,No,Da).iso";
                //var filePath = @"G:\Xbox360\Burnout Paradise (USA).iso";

                var xisoUtility = new XisoReader(filePath);
                if (xisoUtility.Mount() == true && xisoUtility.TryGetDefault(out xexData, out defaultType) == true)
                {
                    Console.WriteLine("Xiso format detected.");
                }
                else
                {
                    var godUtility = new GodReader(filePath);
                    if (godUtility.Mount() == true && godUtility.TryGetDefault(out xexData, out defaultType) == true)
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
                xexData = File.ReadAllBytes("default.xex");
            }

            var result = XexUtility.TryExtractXexMetaData(xexData, out var metaData);
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