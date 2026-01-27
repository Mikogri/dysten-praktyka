using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class ProjectDocumentsManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		private readonly string fileAcceptance = ".txt, .zip, .doc, .docx, .pdf";

		public async Task<List<ProjectDocument>> GetAllAsync(int projectId)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			var result = await db.ProjectDocuments.Where(d => d.Project.Id == projectId)
				.Include(p => p.FileData)
				.Select(p => new ProjectDocument
				{
					Id = p.Id,
					Name = p.Name,
					IsActive = p.IsActive,
					FileData = p.FileData != null ? new ProjectDocumentFile
					{
						Id = p.FileData.Id,
						Data = Array.Empty<byte>()
					} : null
				})
				.ToListAsync();

			foreach (var p in result)
			{
				if (p.FileData == null)
					continue;

				var fileInfo = await db.ProjectDocumentsFiles
					.Where(f => f.Id == p.FileData.Id)
					.Select(f => new
					{
						Length = f.Data != null ? f.Data.Length : 0
					})
					.FirstOrDefaultAsync();

				p.FileLength = fileInfo?.Length ?? 0;
				p.FileData.Data = [];
			}

			return result;
		}

		public async Task<ProjectDocument?> GetAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.ProjectDocuments.FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task<int> CountAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.ProjectDocuments.CountAsync();
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync(int projectId)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var document = new ProjectDocument();

				var parameters = new DialogParameters<DialogFile<ProjectDocument, ProjectDocumentFile>>
				{
					{ x => x.ButtonText, "Dodaj" },
					{ x => x.InputLabel, "Nazwa dokumentu" },
					{ x => x.Model, document },
					{ x => x.FileAcceptance, fileAcceptance },
					{ x => x.ValidationResult, FormValidation }
				};

				var dialog = await _dialogService.ShowAsync<DialogFile<ProjectDocument, ProjectDocumentFile>>(
					"Dodaj dokument",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not ProjectDocument newDeviceDocument)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				newDeviceDocument.ProjectId = projectId;

				db.ProjectDocuments.Add(newDeviceDocument);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Dokument został dodany");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać dokumentu");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(ProjectDocument deviceDocument)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var entity = await db.ProjectDocuments
					.Include(x => x.FileData)
					.FirstOrDefaultAsync(x => x.Id == deviceDocument.Id);

				if (entity is null)
				{
					return (OperationResult.Error, "Nie znaleziono dokumentu");
				}

				var parameters = new DialogParameters<DialogFile<ProjectDocument, ProjectDocumentFile>>
				{
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.Model, entity },
					{ x => x.InputLabel, "Nazwa dokumentu" },
					{ x => x.FileAcceptance, fileAcceptance },
					{ x => x.ValidationResult, FormValidation }
				};

				var dialog = await _dialogService.ShowAsync<DialogFile<ProjectDocument, ProjectDocumentFile>>(
					"Edytuj dokument",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not ProjectDocument edited)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				entity.Name = edited.Name;
				entity.IsActive = edited.IsActive;
				entity.FileName = edited.FileName;

				if (edited.FileData?.Data is not null)
				{
					if (entity.FileData is null)
					{
						entity.FileData = new ProjectDocumentFile
						{
							ProjectDocumentId = entity.Id
						};
					}

					entity.FileData.Data = edited.FileData.Data;
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

				var document = await db.ProjectDocuments
					.Include(p => p.FileData)
					.FirstOrDefaultAsync(p => p.Id == id);

				if (document is null)
					return (OperationResult.Error, "Nie znaleziono dokumentu");

				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, "Czy na pewno chcesz usunąć ten dokument?" },
					{ x => x.ButtonText, "Usuń" }
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń dokument", parameters);
				var result = await dialog.Result;

				if (result.Canceled)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				db.ProjectDocuments.Remove(document);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Dokument został usunięty");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć dokumentu");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message, byte[]? FileData, string? FileName)> GetFileAsync(ProjectDocument projectDoucment)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var document = await db.ProjectDocuments
					.Include(p => p.FileData)
					.FirstOrDefaultAsync(p => p.Id == projectDoucment.Id);

				if (document is null)
				{
					return (OperationResult.Error, "Nie znaleziono dokumentu", null, null);
				}

				if (document.FileData is null || document.FileData.Data.Length == 0)
				{
					return (OperationResult.Error, "Ten dokument nie ma przypisanego pliku", null, null);
				}

				return (OperationResult.Success, "Plik pobrany pomyślnie", document.FileData.Data, document.FileName);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się pobrać pliku", null, null);
			}
		}

		public async Task<ProjectDocument?> GetWithFileDataAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.ProjectDocuments.Include(f => f.FileData).FirstOrDefaultAsync(p => p.Id == id);
		}

		private async Task<string> FormValidation(ProjectDocument model)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (string.IsNullOrWhiteSpace(model.Name))
				return "Nazwa nie może być pusta";

			// CREATE
			if (model.Id == 0)
			{
				return await db.ProjectDocuments.AnyAsync(p => p.Name == model.Name)
					? "Ta nazwa jest już zajęta"
					: string.Empty;
			}

			// EDIT
			return await db.ProjectDocuments.AnyAsync(p =>
					p.Name == model.Name &&
					p.Id != model.Id)
				? "Ta nazwa jest już zajęta"
				: string.Empty;
		}
	}
}
