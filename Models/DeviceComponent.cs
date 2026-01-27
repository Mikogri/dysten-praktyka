using DystenSoftwateManagment.Interfaces;

namespace DystenSoftwateManagment.Models
{
	public class DeviceComponent : IHasName, IHasIsActive, IHasId
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public string? Model { get; set; }
		public string? Manufacturer { get; set; }
		public string? Description { get; set; } = string.Empty;
		public bool IsActive { get; set; } = true;

		public List<Device> Devices { get; set; }
	}
}
