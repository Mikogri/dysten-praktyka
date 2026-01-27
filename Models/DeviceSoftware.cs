using DystenSoftwateManagment.Interfaces;

namespace DystenSoftwateManagment.Models
{
	public class DeviceSoftware : IHasName
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public DateTime ModificationDate { get; set; }

		public int DeviceId { get; set; }
		public Device Device { get; set; }

		public int SoftwareTypeId { get; set; }
		public SoftwareType SoftwareType { get; set; }

		public int? ProtocolRSId { get; set; }
		public ProtocolRS ProtocolRS { get; set; }

		public List<SoftwarePlatform> SoftwarePlatforms { get; set; } = [];
	}
}
