namespace DystenSoftwateManagment.Models
{
	public class PlatformRow(int platformId, string name)
	{
		public int PlatformId { get; set; } = platformId;
		public string Name { get; set; } = name;
		public bool IsLatest { get; set; } = true;
		public string FileName { get; set; }
		public byte[] Data { get; set; }
	}
}
