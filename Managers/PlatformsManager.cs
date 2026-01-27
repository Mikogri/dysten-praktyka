using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class PlatformsManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		public async Task<List<Platform>> GetAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Platforms.Include((p) => p.Devices).ToListAsync();
		}

		public async Task<Platform?> GetAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Platforms.FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task<List<Platform>> GetDevicePlatformsAsync(int? deviceId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			return (await db.Devices.Include((d) => d.Platforms)
			.FirstOrDefaultAsync((d) => d.Id == deviceId))?.Platforms ?? [];
		}

		public async Task<int> CountAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Devices.CountAsync();
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync()
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();
				var allPlatforms = await db.Platforms.ToListAsync();

				var parameters = new DialogParameters<DialogTextField>
				{
					{ x => x.Label, "Platforma" },
					{ x => x.ValidationResult, PlatformNameValidation },
				};

				var dialog = await _dialogService.ShowAsync<DialogTextField>("Nowa platforma", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string name)
				{
					var platform = new Platform { Name = name.Trim() };
					db.Platforms.Add(platform);
					await db.SaveChangesAsync();

					return (OperationResult.Success, "Dodano platformę");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać platformy");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(Platform platform)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogTextField>
				{
					{ x => x.Label, "Platforma" },
					{ x => x.Text, platform.Name },
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.ValidationResult, PlatformNameValidation },
				};

				var dialog = await _dialogService.ShowAsync<DialogTextField>("Edytuj platformę", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string newName)
				{
					var platformToUpdate = await db.Platforms.FindAsync(platform.Id);

					if (platformToUpdate is not null)
					{
						platformToUpdate.Name = newName;
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Zmiany zapisane");
					}
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się zapisać zmian");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditNoteAsync(Platform platform)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogDecsription>
				{
					{ x => x.ContentText, platform.Description },
				};

				var dialog = await _dialogService.ShowAsync<DialogDecsription>("Opis platformy", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string desc)
				{
					platform.Description = desc;

					var platformToUpdate = await db.Platforms.FindAsync(platform.Id);

					if (platformToUpdate is not null)
					{
						platformToUpdate.Description = desc;
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Zmiany zapisane");
					}

					return (OperationResult.Error, "Nie znaleziono tej platformy");
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

				var isPlatformUsed = db.DevicesSoftwares
					.Include((s) => s.SoftwarePlatforms)
					.Any(s => s.SoftwarePlatforms
						.Any(sp => sp.PlatformId == id));

				string message = isPlatformUsed
					? "Usunięcie tej platformy sprawi że zostaną usunięte wszystkie " +
					"oprogramowania, które są z nią powiązane. Czy mimo to chcesz ją usunąć?"
					: "Czy napewno chcesz usunąć tą platformę?";

				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, message },
					{ x => x.ButtonText, "Usuń" },
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń platformę", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var platform = await db.Platforms.FindAsync(id);

					if (platform is not null)
					{
						db.Platforms.Remove(platform);
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Platforma zostła usunięta");
					}

					return (OperationResult.Error, "Nie znaleziono tej platformy");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć tej platformy");
			}
		}

		private async Task<string> PlatformNameValidation(string name, string orginalName)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (name == orginalName) return string.Empty;
			if (string.IsNullOrWhiteSpace(name)) return "Nazwa nie może być pusta";
			return await db.Platforms.AnyAsync(d => d.Name == name)
				? "Ta platforma już istnieje"
				: string.Empty;
		}
	}
}
