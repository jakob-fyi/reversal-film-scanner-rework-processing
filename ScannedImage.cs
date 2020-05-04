using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace reversal_film_scanner_rework_processing
{
    class ScannedImage
    {
        private const double RrgbIsBlackLimit = 45; // Sum of R, G, B
        private const double OriantationConvictionLevel = 95.00; // Percent
        public string ImagePath { get; set; }
        private Bitmap Image { get; set; }
		public ScannedImage(string path)
        {
            this.UpdatePath(path);
        }

        private void UpdatePath(string path){
            this.ImagePath = path;
            this.Image = new Bitmap(this.ImagePath, true);
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
                    Color pixelColor = this.Image.GetPixel(x, y);
                    int rgbSum = pixelColor.R + pixelColor.G + pixelColor.B;

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
            Rectangle croppingArea;

            if (this.GetOriantation() == Orientation.PORTRAIT)
            {
                croppingArea = new Rectangle(1800, 170, 2270, 3540);
            }
            else
            {
                croppingArea = new Rectangle(1170, 800, 3540, 2270);
            }

            this.Crop(croppingArea);
        }

        public void Crop(Rectangle croppingArea)
        {
            using (Bitmap nb = new Bitmap(croppingArea.Width, croppingArea.Height))
            {
                using (Graphics g = Graphics.FromImage(nb))
                {
                    g.DrawImage(this.Image, -croppingArea.X, -croppingArea.Y);
                    this.Image = nb;
                    this.Image.Save(this.ImagePath);
                    this.UpdatePath(this.ImagePath);
                }
            }
        }

        public void RotateFlip(bool rotate180, bool flipHorizontal)
        {
            if (rotate180 || flipHorizontal)
            {
                RotateFlipType rotateFlipType = RotateFlipType.RotateNoneFlipNone;

                if (rotate180 == true && flipHorizontal == false)
                {
                    rotateFlipType = RotateFlipType.Rotate180FlipNone;
                }
                else if (rotate180 == false && flipHorizontal == true) 
                {
                    rotateFlipType = RotateFlipType.RotateNoneFlipX;
                }
                else if (rotate180 == true && flipHorizontal == true) 
                {
                    rotateFlipType = RotateFlipType.Rotate180FlipX;
                }

                this.Image.RotateFlip(rotateFlipType);
                this.Image.Save(this.ImagePath);
                this.UpdatePath(this.ImagePath);
            }
        }

    }
}
