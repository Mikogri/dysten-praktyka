using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class DevicesManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		public async Task<List<Device>> GetAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Devices.ToListAsync();
		}

		public async Task<Device?> GetAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			return await db.Devices
				.Include(d => d.Softwares)
					.ThenInclude(s => s.SoftwarePlatforms)
						.ThenInclude(sp => sp.Platform)
				.Include(d => d.Softwares)
					.ThenInclude(s => s.SoftwareType)
				.Include(d => d.Softwares)
					.ThenInclude(s => s.ProtocolRS)
				.Include(d => d.Platforms)
				.FirstOrDefaultAsync(d => d.Id == id);
		}

		public async Task<List<Device>> GetAllForProjectAsync(int projectId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			return await db.Devices.Include(d => d.Platforms)
				.AsNoTracking()
				.Where(dc => dc.Projects.Any(d => d.Id == projectId))
				.ToListAsync();
		}

		public async Task<(OperationResult OperationStatus, string Message)> SelectForDeviceAsync(int projectId)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var project = await db.Projects.Include(d => d.Devices)
					.FirstOrDefaultAsync(d => d.Id == projectId);

				if (project == null)
				{
					return (OperationResult.Error, "Nie znaleziono projektu");
				}

				var parameters = new DialogParameters<DialogSelectItems<Device>>
				{
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.Items, await GetAllAsync() },
					{ x => x.SelectedItems, project.Devices.ToList() },
				};

				var dialog = await _dialogService.ShowAsync<DialogSelectItems<Device>>(
					"Wybierz urządzenia",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not List<Device> newDevices)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				project.Devices.RemoveAll(c => !newDevices.Any(nc => nc.Id == c.Id));

				foreach (var dev in newDevices)
				{
					if (!project.Devices.Any(c => c.Id == dev.Id))
					{
						var existingComponent = await db.Devices.FindAsync(dev.Id);
						if (existingComponent != null)
						{
							project.Devices.Add(existingComponent);
						}
					}
				}

				await db.SaveChangesAsync();

				return (OperationResult.Success, "Urządzenia zostały zaktualizowane");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać urządzeń");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync(int projectId)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var allPlatforms = await db.Platforms.ToListAsync();

				var parameters = new DialogParameters<DialogDevice>
				{
					{ x => x.ValidationResult, FormValidation },
					{ x => x.Options, allPlatforms },
				};

				var dialog = await _dialogService.ShowAsync<DialogDevice>("Nowe urządzenie", parameters);
				var result = await dialog.Result;

				if (result is not null && !result.Canceled && result.Data is Device deviceFromDialog)
				{
					var project = await db.Projects.FindAsync(projectId);

					if (project == null)
					{
						return (OperationResult.Error, "Projekt nie istnieje");
					}

					var newDevice = new Device
					{
						Name = deviceFromDialog.Name.Trim(),
						Platforms = deviceFromDialog.Platforms,
						ModificationDate = DateTime.UtcNow,
						Projects = [project]
					};

					db.Devices.Add(newDevice);
					await db.SaveChangesAsync();

					return (OperationResult.Success, "Dodano urządzenie do projektu");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać urządzenia");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(Device device)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();
				var allPlatforms = await db.Platforms.ToListAsync();

				var parameters = new DialogParameters<DialogDevice>
				{
					{ x => x.Name, device.Name },
					{ x => x.ValidationResult, FormValidation },
					{ x => x.Options, allPlatforms },
					{ x => x.SelectedOptions, device.Platforms.AsEnumerable() },
					{ x => x.ButtonText, "Zapisz" },
				};

				var dialog = await _dialogService.ShowAsync<DialogDevice>("Edytuj urządzenie", parameters);
				var result = await dialog.Result;

				if (result is null || result.Canceled)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				var deviceFromDialog = result.Data as Device;

				var deviceToUpdate = await db.Devices
					.Include(d => d.Platforms)
					.Include(d => d.Softwares)
						.ThenInclude(s => s.SoftwarePlatforms)
					.FirstOrDefaultAsync(d => d.Id == device.Id);

				if (deviceToUpdate is null)
				{
					return (OperationResult.Error, "Nie znaleziono urządzenia");
				}

				var oldPlatformIds = deviceToUpdate.Platforms.Select(p => p.Id).ToList();
				var newPlatformIds = deviceFromDialog.Platforms.Select(p => p.Id).ToList();
				var removedPlatformIds = oldPlatformIds.Except(newPlatformIds).ToList();

				var softwarePlatformsToDelete = deviceToUpdate.Softwares
					.SelectMany(s => s.SoftwarePlatforms)
					.Where(sp => removedPlatformIds.Contains(sp.PlatformId))
					.ToList();

				if (softwarePlatformsToDelete.Count > 0)
				{
					var confirmParameters = new DialogParameters<DialogConfirm>
					{
						{ x => x.ContentText, "Usunięcie platform z urządzenia spowoduje również usunięcie powiązanych wersji oprogramowania. Czy kontynuować?" },
						{ x => x.ButtonText, "Usuń" },
					};

					var confirmDialog = await _dialogService.ShowAsync<DialogConfirm>("Potwierdzenie usunięcia danych", confirmParameters);
					var confirmResult = await confirmDialog.Result;

					if (confirmResult.Canceled || confirmResult.Data is not bool confirmed || !confirmed)
					{
						return (OperationResult.Canceled, string.Empty);
					}
				}

				deviceToUpdate.Name = deviceFromDialog!.Name.Trim();
				deviceToUpdate.ModificationDate = DateTime.UtcNow;

				deviceToUpdate.Platforms.Clear();
				foreach (var platform in deviceFromDialog.Platforms)
				{
					var attachedPlatform = await db.Platforms.FindAsync(platform.Id);
					if (attachedPlatform != null)
					{
						deviceToUpdate.Platforms.Add(attachedPlatform);
					}
				}

				db.RemoveRange(softwarePlatformsToDelete);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Zmiany zapisane");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się zapisać zmian");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditNoteAsync(Device device)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogDecsription>
				{
					{ x => x.ContentText, device.Description },
				};

				var dialog = await _dialogService.ShowAsync<DialogDecsription>("Opis urządzenia", parameters);
				var result = await dialog.Result;

				if (result is not null && !result.Canceled && result.Data is string desc)
				{
					device.Description = desc;

					var deviceToUpdate = await db.Devices.FindAsync(device.Id);

					if (deviceToUpdate is not null)
					{
						deviceToUpdate.Description = desc;
						deviceToUpdate.ModificationDate = DateTime.UtcNow;
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Zmiany zapisane");
					}

					return (OperationResult.Error, "Nie znaleziono tego urządzenia");
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

				if (result is not null && !result.Canceled)
				{
					var device = await db.Devices.FindAsync(id);

					if (device is not null)
					{
						db.Devices.Remove(device);
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Urządzenie usunięte");
					}

					return (OperationResult.Error, "Nie znaleziono tego urządzenia");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć tego urządzenia");
			}
		}

		private async Task<string> FormValidation(string name, string orginalName, int platformsCount)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (platformsCount < 1) return "Urządzenie musi mieć przypisaną co najmniej jedną platformę";
			if (name == orginalName) return string.Empty;
			if (string.IsNullOrWhiteSpace(name)) return "Nazwa nie może być pusta";
			return await db.Devices.AnyAsync(d => d.Name == name)
				? "Urządzenie o tej nazwie już istnieje"
				: string.Empty;
		}
	}
}
