using DystenSoftwateManagment.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;

namespace DystenSoftwateManagment.Models
{
	public class ProtocolRS : IHasFile<ProtocolRSFile>, IHasName, IHasIsActive
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public bool IsActive { get; set; } = true;

		public string? FileName { get; set; }

		[NotMapped]
		public int FileLength { get; set; }

		public List<DeviceSoftware> DeviceSoftwares { get; set; } = [];

		public ProtocolRSFile? FileData { get; set; }
	}
}
