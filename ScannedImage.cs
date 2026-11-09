using System;
using System.IO;
using SkiaSharp;

namespace reversal_film_scanner_rework_processing
{
    class ScannedImage
    {
		private const double RrgbIsBlackLimit = 45; // Sum of R, G, B
		private const double OriantationConvictionLevel = 95.00; // Percent
		public string ImagePath { get; set; }
        private SKBitmap Image { get; set; }
		public ScannedImage(string path)
        {
            this.UpdatePath(path);
        }

        private void UpdatePath(string path){
            this.ImagePath = path;
            this.Image = SKBitmap.Decode(this.ImagePath);
        }

        public void Rename(string prefix, string numberFormat, int index)
        {
            string fileName = Path.GetFileName(ImagePath);
            string directory = Path.GetDirectoryName(ImagePath);
            string extension = Path.GetExtension(ImagePath);

            string newFileName = prefix + String.Format(numberFormat, index) + extension;
            string newPath = $"{directory}/{newFileName}";

            File.Move(ImagePath, newPath);
            this.UpdatePath(newPath);
        }

        public Orientation GetOriantation()
        {
            double left = GetPartBlackPercentage(1070, 1670, 700, 3200);
            double right = GetPartBlackPercentage(4170, 4770, 700, 3200);

            double percantageAvg = (left + right) / 2;

            if (percantageAvg > OriantationConvictionLevel)
            {
                return Orientation.PORTRAIT;
            }
            else
            {
                return Orientation.LANDSCAPE;
            }

        }

        public double GetPartBlackPercentage(int xStart, int xEnd, int yStart, int yEnd)
        {
            int totalPixel = (xEnd - xStart) * (yEnd - yStart);
            int blackPixel = 0;

            for (int x = xStart; x < xEnd; x++)
            {
                for (int y = yStart; y < yEnd; y++)
                {
                    SKColor pixelColor = this.Image.GetPixel(x, y);
                    int rgbSum = pixelColor.Red + pixelColor.Green + pixelColor.Blue;

                    if (rgbSum <= RrgbIsBlackLimit)
                    {
                        blackPixel++;
                    }
                }
            }

            return (double) blackPixel / totalPixel * 100.00;
        }

        public void CropByAutoOriantation()
        {
            SKRectI croppingArea;

            if (this.GetOriantation() == Orientation.PORTRAIT)
            {
                croppingArea = ToRect(1800, 170, 2270, 3540);
            }
            else
            {
                croppingArea = ToRect(1170, 800, 3540, 2270);
            }

            this.Crop(croppingArea);
        }

        private static SKRectI ToRect(int x, int y, int width, int height)
        {
            return new SKRectI(x, y, x + width, y + height);
        }

        public void Crop(SKRectI croppingArea)
        {
            SKBitmap cropped = new SKBitmap(croppingArea.Width, croppingArea.Height);
            this.Image.ExtractSubset(cropped, croppingArea);

            this.Image = cropped;
            this.Save(this.Image, this.ImagePath);
            this.UpdatePath(this.ImagePath);
        }

        public void RotateFlip(bool rotate180, bool flipHorizontal)
        {
            if (rotate180 || flipHorizontal)
            {
                this.Image = ApplyRotateFlip(this.Image, rotate180, flipHorizontal);
                this.Save(this.Image, this.ImagePath);
                this.UpdatePath(this.ImagePath);
            }
        }

        // Pixel-wise reimplementation of System.Drawing's RotateFlipType.Rotate180FlipNone / RotateNoneFlipX / Rotate180FlipX
        private static SKBitmap ApplyRotateFlip(SKBitmap source, bool rotate180, bool flipHorizontal)
        {
            int width = source.Width;
            int height = source.Height;
            SKBitmap result = new SKBitmap(width, height, source.ColorType, source.AlphaType);

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    int srcX = x;
                    int srcY = y;

                    if (rotate180)
                    {
                        srcX = width - 1 - srcX;
                        srcY = height - 1 - srcY;
                    }

                    if (flipHorizontal)
                    {
                        srcX = width - 1 - srcX;
                    }

                    result.SetPixel(x, y, source.GetPixel(srcX, srcY));
                }
            }

            return result;
        }

        private void Save(SKBitmap bitmap, string path)
        {
            using (SKImage image = SKImage.FromBitmap(bitmap))
            using (SKData data = image.Encode(SKEncodedImageFormat.Jpeg, 100))
            using (FileStream stream = File.Create(path))
            {
                data.SaveTo(stream);
            }
        }

    }
}
