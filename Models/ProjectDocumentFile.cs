namespace DystenSoftwateManagment.Models
{
	public class ProjectDocumentFile
	{
		public int Id { get; set; }

		public byte[]? Data { get; set; }

		public int ProjectDocumentId { get; set; }
		public ProjectDocument ProjectDocument { get; set; } = null!;

		public ProjectDocumentFile() { }

		public ProjectDocumentFile(byte[] data)
		{
			Data = data;
		}
	}
}
