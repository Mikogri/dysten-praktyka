namespace DystenSoftwateManagment.Interfaces
{
	public interface IHasFile<TFile>
		where TFile : class
	{
		int Id { get; set; }
		string Name { get; set; }
		string FileName { get; set; }
		bool IsActive { get; set; }
		TFile? FileData { get; set; }
	}
}
