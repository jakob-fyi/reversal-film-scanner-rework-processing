using System;
using System.Drawing;
using System.IO;

namespace reversal_film_scanner_orientation_analyzer
{

    class Program
    {
        // Sum of (R, G, B) Values < Grenzwert => Black
        private const int RrgbIsBlackLimit = 45;
        private const string InputPath = "/Users/Jakob/Desktop/DiaScanner/Input";
        private const string OutputPath = "/Users/Jakob/Desktop/DiaScanner/Output";
        private const string AcceptedFileExtension = "JPG";

        static void Main(string[] args)
        {
            try
            {
                string[] images = Directory.GetFiles(InputPath, $"*.{AcceptedFileExtension}");

                foreach (var image in images)
                {
                    string imageName = Path.GetFileName(image);
                    Console.Write($"File '{imageName}' has Orientation ");

                    Orientation orientation = GetImageOrientation(image);
                    Console.WriteLine($"{orientation}");

                    File.Move(image, $"{OutputPath}/{orientation}/{imageName}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The process failed: {0}", e.ToString());
            }
        }



        public static Orientation GetImageOrientation(string path)
        {
            double left = GetImagePartBlackPercentage(path, 1070, 1670, 700, 3200);
            double right = GetImagePartBlackPercentage(path, 4170, 4770, 700, 3200);

            double percantageAvg = (left + right) / 2;

            if (percantageAvg > 95.00)
            {
                return Orientation.PORTRAIT;
            }
            else
            {
                return Orientation.LANDSCAPE;
            }

        }

        public static double GetImagePartBlackPercentage(string imagePath, int xStart, int xEnd, int yStart, int yEnd)
        {
            Bitmap image = new Bitmap(imagePath, true);

            int totalPixel = (xEnd - xStart) * (yEnd - yStart);
            int blackPixel = 0;

            for (int x = xStart; x < xEnd; x++)
            {
                for (int y = yStart; y < yEnd; y++)
                {
                    Color pixelColor = image.GetPixel(x, y);
                    int rgbSum = pixelColor.R + pixelColor.G + pixelColor.B;

                    if (rgbSum <= RrgbIsBlackLimit)
                    {
                        blackPixel++;
                    }
                }
            }

            return (double)blackPixel / totalPixel * 100.00;
        }
    }
}
