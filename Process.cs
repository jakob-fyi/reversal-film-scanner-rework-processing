using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace reversal_film_scanner_rework_processing
{
    class Process
    {
		private Guid ProcessId { get; set; }
		public DateTime Started { get; set; }
		public DateTime Ended { get; set; }
		public string MainDirectory { get; set; }
		public string ProcessDirectory { get; set; }
		public string BackupDirectory { get; set; }
		public string WorkingDirectory { get; set; }
		public string ImagesAcceptedFileExtension { get; set; }
		public bool Renameing { get; set; }
		public bool Cropping { get; set; }
		public bool Rotating { get; set; }
		public bool FlipHorizontal { get; set; }
		public int RenameStartIndex { get; set; }
		public int ProcessedImageCounter { get; set; }

		public Process(string pathToMainDirectory, string imagesFileExtension, bool rename, bool crop, bool rotate, bool flipHorizontal, int renameStartIndex)
		{
			this.ProcessId = Guid.NewGuid();
			this.MainDirectory = pathToMainDirectory;
			this.ProcessDirectory = $"{MainDirectory}/{this.ProcessId}";
			this.BackupDirectory = $"{this.ProcessDirectory}/backup";
			this.WorkingDirectory = $"{this.ProcessDirectory}/working";
			this.ImagesAcceptedFileExtension = imagesFileExtension;
			this.Renameing = rename;
			this.Cropping = crop;
			this.Rotating = rotate;
			this.FlipHorizontal = flipHorizontal;
			this.RenameStartIndex = renameStartIndex;
			this.ProcessedImageCounter = 0;
		}

		public void Start()
		{
			System.Console.WriteLine("--------------------------------------------------------------------------------");
			System.Console.WriteLine("Process Start");
			System.Console.WriteLine("--------------------------------------------------------------------------------");
			System.Console.WriteLine("");
			System.Console.WriteLine("--- Prepareing -----------------------------------------------------------------");
			this.Started = DateTime.Now;

			Directory.CreateDirectory(this.BackupDirectory);
			Directory.CreateDirectory(this.WorkingDirectory);
			System.Console.WriteLine("Created Working and Backup Directories");

			CopyAllFilesFromToDorectories(this.MainDirectory, this.BackupDirectory);
			MoveAllFilesFromToDorectories(this.MainDirectory, this.WorkingDirectory);
			System.Console.WriteLine("Copied & Moved to Working and Backup Directories");

			System.Console.WriteLine("--- Image Processing -----------------------------------------------------------");
			foreach (string imagePath in GetImagePathsFromDirectory(this.WorkingDirectory))
			{
				string fileName = Path.GetFileName(imagePath);
				ScannedImage image = new ScannedImage (imagePath);

				System.Console.Write($"#{String.Format("{0:000}", this.ProcessedImageCounter)}: {fileName}");

				if (this.Renameing == true) image.Rename("GDA", "{0:000000}", this.RenameStartIndex + this.ProcessedImageCounter);
				System.Console.Write($" - Renameing: [{this.Renameing}]");
					
				if (this.Cropping == true) image.CropByAutoOriantation();
				System.Console.Write($" - Cropping: [{this.Cropping}]");
					
				image.RotateFlip(this.Rotating, this.FlipHorizontal);
				System.Console.Write($" - Rotate & Flip Horizontal: [{this.Rotating}, {this.FlipHorizontal}]");

				System.Console.WriteLine("");
				this.ProcessedImageCounter++;
			}

			Finish();

		}

		private void MoveAllFilesFromToDorectories(string fromDirectory, string toDirectory)
		{
			foreach (string imagePath in GetImagePathsFromDirectory(fromDirectory))
			{
				string fileName = Path.GetFileName(imagePath);
				File.Move(imagePath, $"{toDirectory}/{fileName}");
			}
		}

		private void CopyAllFilesFromToDorectories(string fromDirectory, string toDirectory)
		{
			foreach (string imagePath in GetImagePathsFromDirectory(fromDirectory))
			{
				string fileName = Path.GetFileName(imagePath);
				File.Copy(imagePath, $"{toDirectory}/{fileName}");
			}
		}

		public string[] GetImagePathsFromDirectory(string directory)
		{
			string[] images = Directory.GetFiles(directory, $"*.{this.ImagesAcceptedFileExtension}");
			return images.OrderBy(x => x).ToArray();
		}

		public void Finish()
		{
			MoveAllFilesFromToDorectories(this.WorkingDirectory, this.MainDirectory);
			Directory.Delete(this.ProcessDirectory, true);

			this.Ended = DateTime.Now;

			System.Console.WriteLine("--------------------------------------------------------------------------------");
			System.Console.WriteLine("Process Ended - Result");
			System.Console.WriteLine("--------------------------------------------------------------------------------");
			System.Console.WriteLine($"ID:        {this.ProcessId}");
			System.Console.WriteLine($"Images:    {this.ProcessedImageCounter}");
			System.Console.WriteLine($"Started:   {this.Started.ToLongTimeString()}");
			System.Console.WriteLine($"Ended:     {this.Ended.ToLongTimeString()}");
			System.Console.WriteLine($"Exec Time: {(this.Ended - this.Started)}");
			System.Console.WriteLine("--------------------------------------------------------------------------------");
		}
    }
}
