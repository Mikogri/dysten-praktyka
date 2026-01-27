using DystenSoftwateManagment.Interfaces;

namespace DystenSoftwateManagment.Models
{
	public class Platform : IHasName
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }

		public List<SoftwarePlatform> SoftwarePlatforms { get; set; } = [];
		public List<Device> Devices { get; set; } = [];

		public override bool Equals(object? obj) => obj is Platform p && p.Id == Id;

		public override int GetHashCode() => Id;
	}
}
