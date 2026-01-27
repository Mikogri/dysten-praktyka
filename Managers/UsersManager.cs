using Blazored.LocalStorage;
using DystenSoftwateManagment.Components.Controls.Dialogs;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class UsersManager(IDialogService dialogService, 
		IDbContextFactory<AppDbContext> dbFactory,
		UserManager<IdentityUser> UserManager,
		RoleManager<IdentityRole> RoleManager)
	{
		private readonly IDialogService _dialogService = dialogService;
		private readonly IDbContextFactory<AppDbContext> _dbFactory = dbFactory;

		public async Task<List<IdentityUser>> GetAllAsync()
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await db.Users.ToListAsync();
		}

		public async Task<List<string>> GetAllRolesAsync(bool includeAdmin = false)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await RoleManager.Roles.Where((r) => r.Name! != "Admin").Select(r => r.Name!).ToListAsync();
		}

		public async Task<IList<string>> GetUserRolesAsync(IdentityUser user)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();
			return await UserManager.GetRolesAsync(user);
		}

		public async Task<(OperationResult OperationStatus, string Message)> CreateAsync()
		{
			try
			{
				var allRoles = await GetAllRolesAsync();

				var parameters = new DialogParameters<DialogUser>
				{
					{ x => x.ButtonText, "Dodaj" },
					{ x => x.Roles, allRoles },
					{ x => x.ValidationResult, UserValidation },
				};

				var dialog = await _dialogService.ShowAsync<DialogUser>("Dodaj użytkownika", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var data = result.Data as Dictionary<string, string>;
					var selectedRole = data["Role"] ?? "User";

					var user = new IdentityUser { UserName = data["UserName"] };
					var identityResult = await UserManager.CreateAsync(user, data["Password"]);

					if (identityResult.Succeeded)
					{
						var roleResult = await UserManager.AddToRoleAsync(user, selectedRole);

						if (!roleResult.Succeeded)
						{
							return (OperationResult.Error, "Błąd podczas próby przypisania roli użytkownikowi");
						}

						return (OperationResult.Success, "Użytkownik dodany");
					}
					else
					{
						return (OperationResult.Error, "Błąd podczas próby dodania użytkownik");
					}
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się dodać nowego użytkownika");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditAsync(IdentityUser user)
		{
			try
			{
				var allRoles = await GetAllRolesAsync();

				var currentRoles = await GetUserRolesAsync(user);
				var currentRole = currentRoles.FirstOrDefault() ?? "";

				var parameters = new DialogParameters<DialogUser>
				{
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.Roles, allRoles },
					{ x => x.UserName, user.UserName },
					{ x => x.IsEdting, true },
					{ x => x.Role, currentRole },
					{ x => x.ValidationResult, UserValidation },
				};

				var dialog = await _dialogService.ShowAsync<DialogUser>("Edytuj użytkownika", parameters);
				var result = await dialog.Result;

				if (!result.Canceled)
				{
					var data = result.Data as Dictionary<string, string>;

					if (data is null)
					{
						return (OperationResult.Error, "Błąd podczas aktualizacji użytkownika");
					}

					var selectedRole = data["Role"] ?? "User";

					var userToUpdate = await UserManager.FindByIdAsync(user.Id);

					if (userToUpdate == null)
					{
						return (OperationResult.Error, "Nie znaleziono użytkownika");
					}

					if (userToUpdate.UserName != data["UserName"])
					{
						userToUpdate.UserName = data["UserName"];
					}

					if (currentRole != selectedRole)
					{
						if (!string.IsNullOrEmpty(currentRole))
							await UserManager.RemoveFromRoleAsync(userToUpdate, currentRole);
						if (!string.IsNullOrEmpty(selectedRole))
							await UserManager.AddToRoleAsync(userToUpdate, selectedRole);
					}

					var updateResult = await UserManager.UpdateAsync(userToUpdate);

					if (updateResult.Succeeded)
					{
						return (OperationResult.Success, "Użytkownik został zaktualizowany");
					}

					return (OperationResult.Error, "Błąd podczas aktualizacji użytkownika");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Błąd podczas aktualizacji użytkownika");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> DeleteAsync(IdentityUser user)
		{
			try
			{
				var parameters = new DialogParameters<DialogConfirm>
				{
					{ x => x.ContentText, "Czy napewno chcesz trwale usunąć tego użytkownika?" },
					{ x => x.ButtonText, "Usuń" },
				};

				var dialog = await _dialogService.ShowAsync<DialogConfirm>("Usuń użytkownika", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is bool confirmed && confirmed)
				{
					var userToDelete = await UserManager.FindByIdAsync(user.Id);

					if (userToDelete is not null)
					{
						var identityResult = await UserManager.DeleteAsync(userToDelete);

						if (identityResult.Succeeded)
						{
							return (OperationResult.Success, "Użytkownik został usunięty");
						}

						return (OperationResult.Error, "Błąd podczas próby usuwanięcia użytkownika");
					}
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Błąd podczas próby usuwanięcia użytkownika");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> EditPasswordAsync(IdentityUser user)
		{
			try
			{
				var parameters = new DialogParameters<DialogTextField>
				{
					{ x => x.Label, "Nowe hasło" },
					{ x => x.ButtonText, "Zapisz" },
					{ x => x.Placeholder, "Hasło" },
					{ x => x.IsPassword, true }
				};

				var dialog = await _dialogService.ShowAsync<DialogTextField>("Zmień hasło", parameters);
				var result = await dialog.Result;

				if (!result.Canceled && result.Data is string newPassword)
				{
					var userFromManager = await UserManager.FindByIdAsync(user.Id);

					if (userFromManager == null)
					{
						return (OperationResult.Error, "Nie znaleziono użytkownika");
					}

					var token = await UserManager.GeneratePasswordResetTokenAsync(userFromManager);
					var passwordResult = await UserManager.ResetPasswordAsync(userFromManager, token, newPassword);

					if (passwordResult.Succeeded)
					{
						return (OperationResult.Success, "Hasło zostało zaktualizowane");
					}

					return (OperationResult.Error, "Błąd podczas próby edycji hasła użytkownika");
				}

				return (OperationResult.Canceled, string.Empty);
			}
			catch
			{
				return (OperationResult.Error, "Błąd podczas próby edycji hasła użytkownika");
			}
		}

		public async Task<string> UserValidation(string name, string orginalName, string password, string role, bool edit)
		{
			await using var db = await _dbFactory.CreateDbContextAsync();

			if (string.IsNullOrWhiteSpace(password) && !edit) return "Hasło nie może być puste";
			if (string.IsNullOrWhiteSpace(role)) return "Rola jest wymagana";
			if (name == orginalName) return string.Empty;
			if (string.IsNullOrWhiteSpace(name)) return "Nazwa nie może być pusta";
			return await db.Users.AnyAsync(d => d.UserName == name)
				? "Taki użytkownik już istnieje"
				: string.Empty;
		}
	}
}
