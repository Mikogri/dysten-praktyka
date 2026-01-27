using DystenSoftwateManagment.Models;
using MudBlazor;
using System.Globalization;

namespace DystenSoftwateManagment.Utility
{
	public enum OperationResult
	{
		Success,
		Error,
		Canceled,
	}

	public static class Utilities
	{
		public static Severity ToSeverity(this OperationResult result) => result switch
		{
			OperationResult.Success => Severity.Success,
			OperationResult.Error => Severity.Error,
			OperationResult.Canceled => Severity.Normal,
			_ => Severity.Normal
		};

		public static string ByteToMb(int dataLength)
		{
			double sizeInMB = (double)dataLength / (1024 * 1024);
			return sizeInMB.ToString("F2", CultureInfo.InvariantCulture) + " MB";
		}

		public static string ConvertBytes(long bytes)
		{
			if (bytes >= 1 << 20)
				return string.Format(CultureInfo.InvariantCulture, "{0:F2} MB", bytes / (1024.0 * 1024.0));
			if (bytes >= 1 << 10)
				return string.Format(CultureInfo.InvariantCulture, "{0:F2} KB", bytes / 1024.0);
			return $"{bytes} B";
		}


		public static bool IsDbSaveManual(string fileName) => fileName.Contains("_manual");

		public static string AdjustDbFileName(FileInfo file)
		{
			var nameWithoutExtension = Path.GetFileNameWithoutExtension(file.Name);

			if (nameWithoutExtension.EndsWith("_manual"))
			{
				nameWithoutExtension = nameWithoutExtension[..^7];
			}

			return nameWithoutExtension + file.Extension;
		}

		public static string GetMimeType(string fileName)
		{
			var ext = Path.GetExtension(fileName)?.ToLowerInvariant();

			return ext switch
			{
				".txt" => "text/plain",
				".pdf" => "application/pdf",
				".zip" => "application/zip",
				".doc" => "application/msword",
				".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
				_ => "application/octet-stream"
			};
		}
	}
}
