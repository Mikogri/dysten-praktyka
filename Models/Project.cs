using DystenSoftwateManagment.Interfaces;

namespace DystenSoftwateManagment.Models
{
	public class Project : IHasName
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public string Number { get; set; }
		public string Description { get; set; } = string.Empty;
		public DateTime CreationDate { get; set; } = DateTime.UtcNow;

		public List<Device> Devices { get; set; } = [];
		public List<ProjectDocument> Documents { get; set; } = [];
	}
}
