using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using SQLitePCL;
namespace DystenSoftwateManagment.Managers
{
	public class SoftwaresPlatformsManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		private readonly string fileAcceptance = ".zip";

		public async Task<DeviceSoftware?> GetAsync(int? softwareId, int? platformId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			var deviceSoftware = await db.DevicesSoftwares
				.Include(d => d.SoftwareType)
				.Include(d => d.SoftwarePlatforms.Where(s => s.PlatformId == platformId))
					.ThenInclude(sp => sp.Platform)
				.FirstOrDefaultAsync(d => d.Id == softwareId);

			if (deviceSoftware != null)
			{
				var platformIds = deviceSoftware.SoftwarePlatforms
					.Select(sp => sp.Id)
					.ToList();

				var filesInfo = await db.Set<SoftwarePlatformFile>()
					.Where(f => platformIds.Contains(f.SoftwarePlatformId))
					.Select(f => new
					{
						f.SoftwarePlatformId,
						FileLength = f.Data != null ? f.Data.Length : 0
					})
					.ToListAsync();

				foreach (var sp in deviceSoftware.SoftwarePlatforms)
				{
					var match = filesInfo.FirstOrDefault(s => s.SoftwarePlatformId == sp.Id);
					if (match != null)
					{
						sp.FileLength = match.FileLength;
					}
				}
			}

			return deviceSoftware;
		}

		public async Task<SoftwarePlatformFile?> GetFileData(int id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.SoftwarePlatformFiles.Include(f => f.SoftwarePlatform)
			.FirstOrDefaultAsync(f => f.SoftwarePlatformId == id);
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync(int platformId, int softwareId)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var newPlatform = new SoftwarePlatform();

				var parameters = new DialogParameters<DialogFile<SoftwarePlatform, SoftwarePlatformFile>>
				{
					{ x => x.Model, newPlatform },
					{ x => x.ButtonText, "Dodaj" },
					{ x => x.InputLabel, "Wersja oprogramowania" },
					{ x => x.FileAcceptance, ".zip" },
					{ x => x.ShowBoolInput, true },
					{ x => x.BoolInputLabel, "Najnowsza wersja" },
					{ x => x.ValidationResult, new Func<SoftwarePlatform, Task<string>>(async sp =>
					{
						var fileData = sp.FileData?.Data;
						return await FormValidation(sp, platformId, softwareId);
					})}
				};

				var dialog = await _dialogService.ShowAsync<DialogFile<SoftwarePlatform, SoftwarePlatformFile>>(
					"Dodaj oprogramowanie", 
					parameters);

				var result = await dialog.Result;

				if (!result.Canceled && result.Data is SoftwarePlatform spAdded)
				{
					spAdded.DeviceSoftwareId = softwareId;
					spAdded.PlatformId = platformId;

					await UnsetOtherLatestAsync(spAdded);

					spAdded.ModificationDate = DateTime.UtcNow;
					db.SoftwarePlatforms.Add(spAdded);

					await db.SaveChangesAsync();

					var file = new SoftwarePlatformFile(spAdded.Id, spAdded.FileData!.Data);
					db.SoftwarePlatformFiles.Add(file);

					await db.SaveChangesAsync();

					return (OperationResult.Success, "Oprogramowanie zostało dodane");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać oprogramowania");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddMultiPlatformsAsync(int deviceId, int softwareId, string version, string description, HashSet<PlatformRow> platformRows)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var platforms = (await db.Devices
					.Include((d) => d.Platforms)
					.FirstOrDefaultAsync((d) => d.Id == deviceId))?.Platforms ?? [];

				if (string.IsNullOrWhiteSpace(version))
				{
					return (OperationResult.Error, "Wersja jest wymagana");
				}

				if (platformRows.Count == 0)
				{
					return (OperationResult.Error, "Dodaj przynajmniej jedną platformę");
				}

				foreach (var row in platformRows)
				{
					if (row.Data == null || row.Data.Length == 0)
					{
						return (OperationResult.Error, "Wgraj plik dla każdej platformy");
					}
				}

				var existingPlatforms = await db.SoftwarePlatforms
					.Where(sp => sp.DeviceSoftwareId == softwareId && sp.Name == version)
					.ToListAsync();

				foreach (var row in platformRows)
				{
					if (existingPlatforms.Any(sp => sp.PlatformId == row.PlatformId))
					{
						var platformName = platforms.FirstOrDefault(p => p.Id == row.PlatformId)?.Name ?? "Wybrana platforma";
						return (OperationResult.Error, $"Dla platformy '{platformName}' istnieje już oprogramowanie o wersji '{version}'.");
					}
				}

				if (platformRows.Any(p => p.IsLatest))
				{
					var selectedPlatformIds = platformRows.Where(p => p.IsLatest).Select(r => r.PlatformId).ToList();

					var toUpdate = await db.SoftwarePlatforms
						.Where(sp => sp.DeviceSoftwareId == softwareId && selectedPlatformIds.Contains(sp.PlatformId) && sp.IsActive)
						.ToListAsync();

					foreach (var sp in toUpdate)
					{
						sp.IsActive = false;
					}
				}

				var softwarePlatforms = new List<SoftwarePlatform>();

				foreach (var row in platformRows)
				{
					var softwarePlatform = new SoftwarePlatform
					{
						Name = version,
						Description = description,
						FileName = row.FileName,
						IsActive = row.IsLatest,
						ModificationDate = DateTime.UtcNow,
						DeviceSoftwareId = softwareId,
						PlatformId = row.PlatformId
					};

					db.SoftwarePlatforms.Add(softwarePlatform);
					softwarePlatforms.Add(softwarePlatform);
				}

				await db.SaveChangesAsync();

				for (int i = 0; i < softwarePlatforms.Count; i++)
				{
					var sp = softwarePlatforms[i];
					var row = platformRows.ElementAt(i);

					var fileData = new SoftwarePlatformFile(sp.Id, row.Data);
					db.SoftwarePlatformFiles.Add(fileData);
				}

				await db.SaveChangesAsync();

				return (OperationResult.Success, "Dodano oprogramowania");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać oprogramowania");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(int id, int platformId, int softwareId)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var softwarePlatform = await db.SoftwarePlatforms
					.Include(sp => sp.FileData)
					.FirstOrDefaultAsync(s => s.Id == id);

				if (softwarePlatform == null)
				{
					return (OperationResult.Error, "Nie znaleziono tego oprogramowania");
				}

				var editable = new SoftwarePlatform
				{
					Id = softwarePlatform.Id,
					Name = softwarePlatform.Name,
					FileName = softwarePlatform.FileName,
					Description = softwarePlatform.Description,
					IsActive = softwarePlatform.IsActive,
					FileData = softwarePlatform.FileData != null ? CopyFile(softwarePlatform.FileData) : null,
					ModificationDate = softwarePlatform.ModificationDate
				};

				var parameters = new DialogParameters<DialogFile<SoftwarePlatform, SoftwarePlatformFile>>
				{
					{ x => x.Model, editable },
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.InputLabel, "Wersja oprogramowania" },
					{ x => x.FileAcceptance, fileAcceptance },
					{ x => x.ShowBoolInput, true },
					{ x => x.BoolInputLabel, "Najnowsza wersja" },
					{ x => x.ValidationResult, new Func<SoftwarePlatform, Task<string>>(async sp =>
					{
						var fileData = sp.FileData?.Data;
						return await FormValidation(sp, platformId, softwareId);
					})}
				};

				var dialog = await _dialogService.ShowAsync<DialogFile<SoftwarePlatform, SoftwarePlatformFile>>(
					"Edytuj oprogramowanie",
					parameters);

				var result = await dialog.Result;

				if (!result.Canceled && result.Data is SoftwarePlatform updated)
				{
					softwarePlatform.Name = updated.Name;
					softwarePlatform.Description = updated.Description;
					softwarePlatform.IsActive = updated.IsActive;
					softwarePlatform.ModificationDate = DateTime.UtcNow;
					softwarePlatform.FileName = updated.FileName;

					if (updated.FileData?.Data is not null)
					{
						softwarePlatform.FileData = CopyFile(updated.FileData, true);
					}

					updated.DeviceSoftwareId = softwarePlatform.DeviceSoftwareId;
					updated.PlatformId = softwarePlatform.PlatformId;

					await UnsetOtherLatestAsync(updated);
					await db.SaveChangesAsync();

					return (OperationResult.Success, "Zmiany zapisane");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się zapisać zmian");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditNoteAsync(SoftwarePlatform softwarePlatform)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogDecsription>
				{
					{ x => x.ContentText, softwarePlatform.Description },
				};

				var dialog = await _dialogService.ShowAsync<DialogDecsription>("Opis oprogramowania", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string desc)
				{
					var softwarePlatformToUpdate = await db.SoftwarePlatforms.FindAsync(softwarePlatform.Id);
					
					if (softwarePlatformToUpdate is not null)
					{
						softwarePlatformToUpdate.Description = desc;
						softwarePlatformToUpdate.ModificationDate = DateTime.UtcNow;
						await db.SaveChangesAsync();
						return (OperationResult.Success, "Zmiany zapisane");
					}

					return (OperationResult.Error, "Nie znaleziono tego oprogramowania");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się zapisać zmian");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> DeleteAsync(int id)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, "Czy napewno chcesz trwale usunąć te urządzenie?" },
					{ x => x.ButtonText, "Usuń" },
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń urządzenie", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var softwarePlatform = await db.SoftwarePlatforms.FindAsync(id);

					if (softwarePlatform is not null)
					{
						db.SoftwarePlatforms.Remove(softwarePlatform);
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Oprogramowanie usunięte");
					}

					return (OperationResult.Error, "Nie znaleziono tego Oprogramowania");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć tego Oprogramowania");
			}
		}

		private async Task UnsetOtherLatestAsync(SoftwarePlatform sf)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (!sf.IsActive)
			{
				return;
			}

			var currentLatest = await db.SoftwarePlatforms
				.Where(s => s.PlatformId == sf.PlatformId
					&& s.DeviceSoftwareId == sf.DeviceSoftwareId
					&& s.IsActive
					&& s.Id != sf.Id)
				.FirstOrDefaultAsync();

			if (currentLatest != null)
			{
				currentLatest.IsActive = false;
				await db.SaveChangesAsync();
			}
		}

		private static SoftwarePlatformFile CopyFile(SoftwarePlatformFile file, bool copyData = false)
		{
			var result =  new SoftwarePlatformFile
			{
				Id = file.Id,
			};

			if (copyData && file.Data is not null)
			{
				result.Data = [.. file.Data];
			}

			return result;
		}

		private async Task<string> FormValidation(SoftwarePlatform sP, int platformId, int softwareId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (string.IsNullOrWhiteSpace(sP.Name))
				return "Wersja jest wymagana";
			if (sP.FileData is null || string.IsNullOrWhiteSpace(sP.FileName))
				return "Plik jest wymagany";
			if (await db.SoftwarePlatforms
				.AnyAsync(x => x.PlatformId == platformId && x.DeviceSoftwareId == softwareId && x.Id != sP.Id && x.Name == sP.Name))
				return "Oprogramowanie z tą wersją już istnieje";
			return string.Empty;
		}
	}
}
