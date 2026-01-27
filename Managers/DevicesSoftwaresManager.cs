using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class DevicesSoftwaresManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync(int deviceId)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();
				var allSoftwareTypes = await db.SoftwareTypes.ToListAsync();
				var allProtocolsRS = await db.ProtocolsRS.ToListAsync();

				var parameters = new DialogParameters<DialogDeviceSoftware>
				{
					{ x => x.OptionsSoftwareTypes, allSoftwareTypes },
					{ x => x.OptionsProtocolsRS, allProtocolsRS },
					{ x => x.DeviceId, deviceId },
					{ x => x.ValidationResult, DeviceSoftwareValidationResult },
				};

				var dialog = await _dialogService.ShowAsync<DialogDeviceSoftware>("Nowe oprogramowanie", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var dataList = result.Data as List<object?>;

					string name = dataList[0]?.ToString() ?? string.Empty;
					var softwareType = dataList[1] as SoftwareType;
					var prtocolRS = dataList[2] as ProtocolRS;

					var newDeviceSoftware = new DeviceSoftware
					{
						Name = name,
						SoftwareTypeId = softwareType.Id,
						DeviceId = deviceId,
						ProtocolRSId = prtocolRS?.Id,
						ModificationDate = DateTime.UtcNow
					};

					db.DevicesSoftwares.Add(newDeviceSoftware);
					await db.SaveChangesAsync();

					return (OperationResult.Success, "Dodano oprogramowanie");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać oprogramowania");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(DeviceSoftware deviceSoftware)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var allSoftwareTypes = await db.SoftwareTypes.ToListAsync();
				var allProtocolsRS = await db.ProtocolsRS.ToListAsync();

				var parameters = new DialogParameters<DialogDeviceSoftware>
				{
					{ x => x.OptionsSoftwareTypes, allSoftwareTypes },
					{ x => x.OptionsProtocolsRS, allProtocolsRS },
					{ x => x.Name, deviceSoftware.Name },
					{ x => x.SoftwareType, deviceSoftware.SoftwareType },
					{ x => x.ProtocolRS, deviceSoftware.ProtocolRS },
					{ x => x.ValidationResult, DeviceSoftwareValidationResult },
					{ x => x.ButtonText, "Zapisz" },
				};

				var dialog = await _dialogService.ShowAsync<DialogDeviceSoftware>("Edytuj oprogramowanie", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var dataList = result.Data as List<object?>;

					string name = dataList[0]?.ToString() ?? string.Empty;

					var softwareTypeId = (dataList[1] as SoftwareType).Id;
					var softwareType = await db.SoftwareTypes.FindAsync(softwareTypeId);

					var protocolRSId = (dataList[2] as ProtocolRS).Id;
					var protocolRS = await db.ProtocolsRS.FindAsync(protocolRSId);

					var deviceSoftwareToUpdate = await db.DevicesSoftwares.FindAsync(deviceSoftware.Id);

					if (deviceSoftwareToUpdate is not null)
					{
						deviceSoftwareToUpdate.ModificationDate = DateTime.UtcNow;
						deviceSoftwareToUpdate.Name = name;
						deviceSoftwareToUpdate.SoftwareType = softwareType;
						deviceSoftwareToUpdate.ProtocolRS = protocolRS;

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
					{ x => x.ContentText, "Czy napewno chcesz trwale usunąć to oprogramowanie?" },
					{ x => x.ButtonText, "Usuń" },
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń oprogramowanie", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var deviceSoftware = await db.DevicesSoftwares.FindAsync(id);

					if (deviceSoftware is not null)
					{
						db.DevicesSoftwares.Remove(deviceSoftware);
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Oprogramowanie zostało usunięte");
					}

					return (OperationResult.Error, "Nie znaleziono tego oprogramowania");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć tego oprogramowania");
			}
		}

		private async Task<string> DeviceSoftwareValidationResult(string name, string orginalName, SoftwareType sf, SoftwareType oldSf, int deviceId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (string.IsNullOrWhiteSpace(name)) return "Nazwa nie może być pusta";
			if (sf?.Id == null) return "Nie wybrano typu oprogramowania";
			if (name == orginalName && sf?.Id == oldSf?.Id) return string.Empty;
			return await db.DevicesSoftwares.Where((s) => s.DeviceId == deviceId)
				.AnyAsync(d => d.Name == name && d.SoftwareTypeId == sf.Id)
				? "Takie oprogramowanie już istnieje"
				: string.Empty;
		}
	}
}
