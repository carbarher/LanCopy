using System;

namespace SlskDown
{
    public class TestSearchResultItem
    {
        public static void Test()
        {
            var item = new SearchResultItem
            {
                Filename = "test.mp3",
                Bitrate = 320,
                Length = 180,
                QueueLength = 5,
                FreeUploadSlots = 2,
                QualityScore = 95
            };
            
            Console.WriteLine($"Bitrate: {item.Bitrate}");
            Console.WriteLine($"Length: {item.Length}");
            Console.WriteLine($"QueueLength: {item.QueueLength}");
            Console.WriteLine($"FreeUploadSlots: {item.FreeUploadSlots}");
            Console.WriteLine($"QualityScore: {item.QualityScore}");
        }
    }
}
