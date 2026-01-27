using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class SoftwareTypesManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		public async Task<List<SoftwareType>> GetAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.SoftwareTypes.Include((s) => s.DeviceSoftwares).ToListAsync();
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync()
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();
				var allSoftwareTypes = await db.SoftwareTypes.ToListAsync();

				var parameters = new DialogParameters<DialogTextField>
				{
					{ x => x.Label, "Typ opgrogramowania" },
					{ x => x.ValidationResult, SoftwareTypeNameValidation },
				};

				var dialog = await _dialogService.ShowAsync<DialogTextField>("Nowy typ oprogramowania", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string name)
				{
					var softwareType = new SoftwareType { Name = name.Trim() };
					db.SoftwareTypes.Add(softwareType);
					await db.SaveChangesAsync();

					return (OperationResult.Success, "Dodano typ oprogramowania");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać tego typu oprogramowania");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(SoftwareType softwareType)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogTextField>
				{
					{ x => x.Label, "Typ opgrogramowania" },
					{ x => x.Text, softwareType.Name },
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.ValidationResult, SoftwareTypeNameValidation },
				};

				var dialog = await _dialogService.ShowAsync<DialogTextField>("Edytuj typ oprogramowania", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string newName)
				{
					var softwareTypeToUpdate = await db.SoftwareTypes.FindAsync(softwareType.Id);

					if (softwareTypeToUpdate is not null)
					{
						softwareTypeToUpdate.Name = newName;
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

		public async Task<(OperationResult OperationStatus, string Message)> EditNoteAsync(SoftwareType softwareType)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogDecsription>
				{
					{ x => x.ContentText, softwareType.Description },
				};

				var dialog = await _dialogService.ShowAsync<DialogDecsription>("Opis typu softu", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string desc)
				{
					softwareType.Description = desc;

					var softwareTypeToUpdate = await db.SoftwareTypes.FindAsync(softwareType.Id);

					if (softwareTypeToUpdate is not null)
					{
						softwareTypeToUpdate.Description = desc;
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Zmiany zapisane");
					}

					return (OperationResult.Error, "Nie znaleziono tego typu oprogramowania");
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

				var isSoftwareTypeUsed = db.DevicesSoftwares
					.Include((s) => s.SoftwareType)
					.Any(s => s.SoftwareTypeId == id);

				string message = isSoftwareTypeUsed
					? "Usunięcie tego typu opg. sprawi że zostaną usunięte wszystkie " +
					"oprogramowania powiązane z tym typem opgrogramowania. Czy mimo to chcesz go usunąć?"
					: "Czy napewno chcesz usunąć ten typ oprogramowania?";

				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, message },
					{ x => x.ButtonText, "Usuń" },
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń typ oprogramowania", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var softwareType = await db.SoftwareTypes.FindAsync(id);

					if (softwareType is not null)
					{
						db.SoftwareTypes.Remove(softwareType);
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Typ oprogramowania został usunięty");
					}

					return (OperationResult.Error, "Nie znaleziono tego typu oprogramowania");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć tego typu typu oprogramowania");
			}
		}

		private async Task<string> SoftwareTypeNameValidation(string name, string orginalName)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (name == orginalName) return string.Empty;
			if (string.IsNullOrWhiteSpace(name)) return "Nazwa nie może być pusta";
			return await db.SoftwareTypes.AnyAsync(d => d.Name == name)
				? "Ta platforma już istnieje"
				: string.Empty;
		}
	}
}
