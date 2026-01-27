using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.EntityFrameworkCore;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class ProjectsManager(IDialogService dialogService, IDbContextFactory<AppDbContext> dbFactory)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		public async Task<List<Project>> GetAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Projects.ToListAsync();
		}

		public async Task<Project?> GetAsync(int? id)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
		}

		public async Task<int> CountAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Projects.CountAsync();
		}

		public async Task<(OperationResult OperationStatus, string Message)> AddAsync()
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();
				var allProjects = await db.Projects.ToListAsync();

				var project = new Project();

				var parameters = new DialogParameters<DialogProject>
				{
					{ x => x.ButtonText, "Dodaj" },
					{ x => x.Project, project },
					{ x => x.ValidationResult, FormValidation },
				};

				var dialog = await _dialogService.ShowAsync<DialogProject>("Nowy projekt", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is Project newProject)
				{
					db.Projects.Add(newProject);
					await db.SaveChangesAsync();

					return (OperationResult.Success, "Dodano projekt");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać projektu");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(Project project)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var entity = await db.Projects
					.FirstOrDefaultAsync(x => x.Id == project.Id);

				if (entity is null)
				{
					return (OperationResult.Error, "Nie znaleziono komponentu");
				}

				var parameters = new DialogParameters<DialogProject>
				{
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.Project, project },
					{ x => x.ValidationResult, FormValidation }
				};

				var dialog = await _dialogService.ShowAsync<DialogProject>(
					"Edytuj projekt",
					parameters);

				var result = await dialog.Result;

				if (result.Canceled || result.Data is not Project edited)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				entity.Name = edited.Name;
				entity.Number = edited.Number;

				await db.SaveChangesAsync();

				return (OperationResult.Success, "Zmiany zapisane");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się zapisać zmian");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditNoteAsync(Project project)
		{
			try
			{
				await using var db = await _dbFactory.CreateDbContextAsync();

				var parameters = new DialogParameters<DialogDecsription>
				{
					{ x => x.ContentText, project.Description },
				};

				var dialog = await _dialogService.ShowAsync<DialogDecsription>("Opis projektu", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string desc)
				{
					project.Description = desc;

					var projectToUpdate = await db.Projects.FindAsync(project.Id);

					if (projectToUpdate is not null)
					{
						projectToUpdate.Description = desc;
						await db.SaveChangesAsync();

						return (OperationResult.Success, "Zmiany zapisane");
					}

					return (OperationResult.Error, "Nie znaleziono takiego projektu");
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

				var project = await db.Projects.FindAsync(id);

				if (project is null)
				{
					return (OperationResult.Error, "Nie znaleziono tego projektu");
				}

				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, "Czy na pewno chcesz usunąć ten projekt?" },
					{ x => x.ButtonText, "Usuń" },
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń projekt", parameters);
				var result = await dialog.Result;

				if (result is null || result.Canceled)
				{
					return (OperationResult.Canceled, string.Empty);
				}

				db.Projects.Remove(project);
				await db.SaveChangesAsync();

				return (OperationResult.Success, "Projekt został usunięty");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć projektu");
			}
		}

		private async Task<string> FormValidation(Project model)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (string.IsNullOrWhiteSpace(model.Name))
				return "Nazwa nie może być pusta";

			// CREATE
			if (model.Id == 0)
			{
				return await db.Projects.AnyAsync(p => p.Name == model.Name)
					? "Ta nazwa jest już zajęta"
					: string.Empty;
			}

			// EDIT
			return await db.Projects.AnyAsync(p =>
					p.Name == model.Name &&
					p.Id != model.Id)
				? "Ta nazwa jest już zajęta"
				: string.Empty;
		}
	}
}
