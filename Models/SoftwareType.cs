using DystenSoftwateManagment.Interfaces;

namespace DystenSoftwateManagment.Models
{
	public class SoftwareType : IHasName
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }

		public List<DeviceSoftware> DeviceSoftwares { get; set; } = [];
	}
}