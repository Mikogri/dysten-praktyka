using DystenSoftwateManagment.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;

namespace DystenSoftwateManagment.Models
{
	public class ProjectDocument : IHasFile<ProjectDocumentFile>, IHasName, IHasIsActive
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public bool IsActive { get; set; } = true;

		public string? FileName { get; set; }

		public int ProjectId { get; set; }
		public Project Project { get; set; }

		public ProjectDocumentFile? FileData { get; set; }

		[NotMapped]
		public int FileLength { get; set; }
	}
}
