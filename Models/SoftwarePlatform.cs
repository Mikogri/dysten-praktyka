using DystenSoftwateManagment.Interfaces;
using System.ComponentModel.DataAnnotations.Schema;

namespace DystenSoftwateManagment.Models
{
	public class SoftwarePlatform : IHasFile<SoftwarePlatformFile>, IHasName
	{
		public int Id { get; set; }

		public string Name { get; set; }

		// Aktywnie najnowsza wersja oprogramowania dla danej platformy, nazwa taka z wezględu na interfejs IHasFile
		public bool IsActive { get; set; } = true;

		public string? Description { get; set; }
		public string? FileName { get; set; }

		public DateTime ModificationDate { get; set; }

		public int DeviceSoftwareId { get; set; }
		public DeviceSoftware DeviceSoftware { get; set; }

		public int PlatformId { get; set; }
		public Platform Platform { get; set; }

		public SoftwarePlatformFile? FileData { get; set; }

		[NotMapped]
		public int FileLength { get; set; }
	}
}
