namespace DystenSoftwateManagment.Models
{
	public class ProtocolRSFile
	{
		public int Id { get; set; }

		public byte[]? Data { get; set; }

		public int ProtocolRSId { get; set; }
		public ProtocolRS ProtocolRS { get; set; } = null!;

		public ProtocolRSFile() { }

		public ProtocolRSFile(byte[]? data)
		{
			Data = data;
		}
	}
}
