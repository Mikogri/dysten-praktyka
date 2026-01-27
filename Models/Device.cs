using DystenSoftwateManagment.Interfaces;

namespace DystenSoftwateManagment.Models
{
	public class Device : IHasName, IHasId
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public string? Description { get; set; }
		public DateTime ModificationDate { get; set; }

		public List<Project> Projects { get; set; } = [];
		public List<DeviceSoftware> Softwares { get; set; } = [];
		public List<Platform> Platforms { get; set; } = [];
		public List<ProjectDocument> Documents { get; set; } = [];
		public List<DeviceComponent> Components { get; set; } = [];
	}
}
