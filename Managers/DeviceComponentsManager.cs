using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class DeviceComponentsManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		public async Task<List<DeviceComponent>> GetAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.DeviceComponents.ToListAsync();
		}

		public async Task<List<DeviceComponent>> GetAllForDevice(int deviceId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			return await db.DeviceComponents
				.AsNoTracking()
				.Where(dc => dc.Devices.Any(d => d.Id == deviceId))
				.ToListAsync();
		}

		public async Task<DeviceComponent?> GetAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.DeviceComponents.FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task<int> CountAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.DeviceComponents.CountAsync();
		}

		public async Task<(OperationResult OperationStatus, string Message)> SelectForDeviceAsync(int deviceId)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var device = await db.Devices
					.Include(d => d.Components)
					.FirstOrDefaultAsync(d => d.Id == deviceId);

				if (device == null)
				{
					return (OperationResult.Error, "Nie znaleziono urządzenia");
				}

				var parameters = new DialogParameters<DialogSelectItems<DeviceComponent>>
				{
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.Items, await GetAllAsync() },
					{ x => x.SelectedItems, device.Components.ToList() },
				};

				var dialog = await _dialogService.ShowAsync<DialogSelectItems<DeviceComponent>>(
					"Wybierz komponenty",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not List<DeviceComponent> newDeviceComponents)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				device.Components.RemoveAll(c => !newDeviceComponents.Any(nc => nc.Id == c.Id));

				foreach (var comp in newDeviceComponents)
				{
					if (!device.Components.Any(c => c.Id == comp.Id))
					{
						var existingComponent = await db.DeviceComponents.FindAsync(comp.Id);
						if (existingComponent != null)
						{
							device.Components.Add(existingComponent);
						}
					}
				}

				await db.SaveChangesAsync();

				return (OperationResult.Success, "Komponenty zostały zaktualizowane");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać komponentu");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync()
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var component = new DeviceComponent();

				var parameters = new DialogParameters<DialogDeviceComponent>
				{
					{ x => x.ButtonText, "Dodaj" },
					{ x => x.Component, component },
					{ x => x.ValidationResult, FormValidation }
				};

				var dialog = await _dialogService.ShowAsync<DialogDeviceComponent>(
					"Dodaj komponent",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not DeviceComponent newDeviceComponent)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				newDeviceComponent.Id = 0;

				db.DeviceComponents.Add(newDeviceComponent);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Komponent został dodany");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać komponentu");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(DeviceComponent component)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var entity = await db.DeviceComponents
					.FirstOrDefaultAsync(x => x.Id == component.Id);

				if (entity is null)
				{
					return (OperationResult.Error, "Nie znaleziono komponentu");
				}

				var parameters = new DialogParameters<DialogDeviceComponent>
				{
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.Component, component },
					{ x => x.ValidationResult, FormValidation }
				};

				var dialog = await _dialogService.ShowAsync<DialogDeviceComponent>(
					"Edytuj komponent",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not DeviceComponent edited)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				entity.Name = edited.Name;
				entity.Model = edited.Model;
				entity.Manufacturer = edited.Manufacturer;
				entity.IsActive = edited.IsActive;

				await db.SaveChangesAsync();

				return (OperationResult.Success, "Zmiany zapisane");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się zapisać zmian");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditNoteAsync(DeviceComponent component)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogDecsription>
				{
					{ x => x.ContentText, component.Description },
				};

				var dialog = await _dialogService.ShowAsync<DialogDecsription>("Opis komponentu", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string desc)
				{
					component.Description = desc;

					var componentToUpdate = await db.Platforms.FindAsync(component.Id);

					if (componentToUpdate is not null)
					{
						componentToUpdate.Description = desc;
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Zmiany zapisane");
					}

					return (OperationResult.Error, "Nie znaleziono tego komponentu");
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

				var entity = await db.DeviceComponents
					.FirstOrDefaultAsync(p => p.Id == id);

				if (entity is null)
					return (OperationResult.Error, "Nie znaleziono komponentu");

				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, "Czy na pewno chcesz usunąć ten komponent?" },
					{ x => x.ButtonText, "Usuń" }
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń komponent", parameters);
				var result = await dialog.Result;

				if (result.Canceled)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				db.DeviceComponents.Remove(entity);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Komponent został usunięty");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć komponent");
			}
		}

		private async Task<string> FormValidation(DeviceComponent model)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (string.IsNullOrWhiteSpace(model.Name))
				return "Nazwa nie może być pusta";

			// CREATE
			if (model.Id == 0)
			{
				return await db.DeviceComponents.AnyAsync(p => p.Name == model.Name)
					? "Ta nazwa jest już zajęta"
					: string.Empty;
			}

			// EDIT
			return await db.DeviceComponents.AnyAsync(p =>
					p.Name == model.Name &&
					p.Id != model.Id)
				? "Ta nazwa jest już zajęta"
				: string.Empty;
		}
	}
}
