using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class ProtocolsRSManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		private readonly string fileAcceptance = ".txt, .zip, .doc, .docx, .pdf";

		public async Task<List<ProtocolRS>> GetAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			return await db.ProtocolsRS
				.Select(x => new ProtocolRS
				{
					Id = x.Id,
					Name = x.Name,
					IsActive = x.IsActive,
					FileName = x.FileName,

					FileLength = x.FileData != null && x.FileData.Data != null
						? x.FileData.Data.Length
						: 0
				})
				.ToListAsync();
		}

		public async Task<ProtocolRS?> GetAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.ProtocolsRS.FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task<ProtocolRS?> GetWithFileDataAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.ProtocolsRS.Include(f => f.FileData).FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task<ProtocolRS?> GetSoftwareProtocolAsync(int? softwareId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			return await db.ProtocolsRS.Include((d) => d.DeviceSoftwares)
			.FirstOrDefaultAsync((d) => d.Id == softwareId);
		}

		public async Task<int> CountAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.ProtocolsRS.CountAsync();
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync()
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var protocolRS = new ProtocolRS();

				var parameters = new DialogParameters<DialogFile<ProtocolRS, ProtocolRSFile>>
				{
					{ x => x.ButtonText, "Dodaj" },
					{ x => x.InputLabel, "Nazwa protokołu RS" },
					{ x => x.Model, protocolRS },
					{ x => x.FileAcceptance, fileAcceptance },
					{ x => x.ValidationResult, FormValidation }
				};

				var dialog = await _dialogService.ShowAsync<DialogFile<ProtocolRS, ProtocolRSFile>>(
					"Dodaj protokół RS",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not ProtocolRS newProtocol)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				if (newProtocol.FileData is { Data.Length: 0 })
				{
					newProtocol.FileData = null;
				}

				newProtocol.Id = 0;

				db.ProtocolsRS.Add(newProtocol);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Protokół został dodany");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać protokołu");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(ProtocolRS protocolRS)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var entity = await db.ProtocolsRS
					.Include(x => x.FileData)
					.FirstOrDefaultAsync(x => x.Id == protocolRS.Id);

				if (entity is null)
				{
					return (OperationResult.Error, "Nie znaleziono protokołu");
				}

				var parameters = new DialogParameters<DialogFile<ProtocolRS, ProtocolRSFile>>
				{
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.InputLabel, "Nazwa protokołu RS" },
					{ x => x.Model, entity },
					{ x => x.FileAcceptance, fileAcceptance },
					{ x => x.ValidationResult, FormValidation }
				};

				var dialog = await _dialogService.ShowAsync<DialogFile<ProtocolRS, ProtocolRSFile>>(
					"Edytuj protokół RS",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not ProtocolRS edited)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				entity.Name = edited.Name;
				entity.IsActive = edited.IsActive;

				if (edited.FileData == null)
				{
					if (entity.FileData != null)
					{
						db.ProtocolsRSFiles.Remove(entity.FileData);
						entity.FileData = null;
					}
				}
				else
				{
					if (entity.FileData == null)
					{
						entity.FileData = new ProtocolRSFile
						{
							ProtocolRSId = entity.Id
						};
					}

					entity.FileData.Data = edited.FileData.Data;
					entity.FileName = edited.FileName;
				}

				await db.SaveChangesAsync();

				return (OperationResult.Success, "Zmiany zapisane");
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

				var protocol = await db.ProtocolsRS
					.Include(p => p.FileData)
					.FirstOrDefaultAsync(p => p.Id == id);

				if (protocol is null)
				{
					return (OperationResult.Error, "Nie znaleziono protokołu");
				}

				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, "Czy na pewno chcesz usunąć ten protokół?" },
					{ x => x.ButtonText, "Usuń" }
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń protokół RS", parameters);
				var result = await dialog.Result;

				if (result.Canceled)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				db.ProtocolsRS.Remove(protocol);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Protokół został usunięty");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć protokołu");
			}
		}

		private async Task<string> FormValidation(ProtocolRS model)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (string.IsNullOrWhiteSpace(model.Name))
				return "Nazwa nie może być pusta";

			// CREATE
			if (model.Id == 0)
			{
				return await db.ProtocolsRS.AnyAsync(p => p.Name == model.Name)
					? "Ta nazwa jest już zajęta"
					: string.Empty;
			}

			// EDIT
			return await db.ProtocolsRS.AnyAsync(p =>
					p.Name == model.Name &&
					p.Id != model.Id)
				? "Ta nazwa jest już zajęta"
				: string.Empty;
		}
	}
}
