namespace DystenSoftwateManagment.Models
{
	public class SoftwarePlatformFile
	{
		public int Id { get; set; }

		public byte[]? Data { get; set; }

		public int SoftwarePlatformId { get; set; }
		public SoftwarePlatform SoftwarePlatform { get; set; } = null!;

		public SoftwarePlatformFile() { }

		public SoftwarePlatformFile(int softwarePlatformId, byte[]? data)
		{
			SoftwarePlatformId = softwarePlatformId;
			Data = data;
		}
	}
}
