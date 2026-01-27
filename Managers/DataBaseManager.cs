using DystenSoftwateManagment.Utility;
using Microsoft.Data.Sqlite;
using MudBlazor;

namespace DystenSoftwateManagment.Managers
{
	public class DataBaseManager
	{
		private readonly string backupFolderPath;
		private readonly TimeSpan backupTime = new(6, 0, 0);

		public DataBaseManager()
		{
			var dbPath = Path.GetFullPath(ConstData.DATA_BASE_FILE_NAME);
			var dbDirectory = Path.GetDirectoryName(dbPath)!;
			backupFolderPath = Path.Combine(dbDirectory, "Backups");

			Directory.CreateDirectory(backupFolderPath);
		}

		public async Task<bool> ImportAsync(string importDbPath)
		{
			await using var conn = new SqliteConnection($"Data Source={ConstData.DATA_BASE_FILE_NAME}");

			try
			{
				await conn.OpenAsync();

				var attachCmd = conn.CreateCommand();
				attachCmd.CommandText = $"ATTACH DATABASE '{importDbPath}' AS import_db;";
				await attachCmd.ExecuteNonQueryAsync();

				List<string> tableNames = [];
				var getTablesCmd = conn.CreateCommand();
				getTablesCmd.CommandText = @"
					SELECT name FROM import_db.sqlite_master 
					WHERE type='table' AND name NOT LIKE 'sqlite_%';";

				await using (var reader = await getTablesCmd.ExecuteReaderAsync())
				{
					while (await reader.ReadAsync())
					{
						tableNames.Add(reader.GetString(0));
					}
				}

				foreach (var table in tableNames)
				{
					var deleteCmd = conn.CreateCommand();
					deleteCmd.CommandText = $"DELETE FROM {table};";
					await deleteCmd.ExecuteNonQueryAsync();

					var insertCmd = conn.CreateCommand();
					insertCmd.CommandText = $"INSERT INTO {table} SELECT * FROM import_db.{table};";
					await insertCmd.ExecuteNonQueryAsync();
				}

				var detachCmd = conn.CreateCommand();
				detachCmd.CommandText = $"DETACH DATABASE import_db;";
				await detachCmd.ExecuteNonQueryAsync();

				return true;
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return false;
			}
			finally
			{
				if (conn.State == System.Data.ConnectionState.Open)
				{
					try
					{
						var detachCmd = conn.CreateCommand();
						detachCmd.CommandText = $"DETACH DATABASE import_db;";
						await detachCmd.ExecuteNonQueryAsync();
					}
					catch { }
				}
			}
		}

		public void CreateManualBackup()
		{
			CreateBackup(suffix: "_manual");
		}

		public void StartBackupLoop(CancellationToken cancellationToken)
		{
			Task.Run(async () =>
			{
				var now = DateTime.Now;
				var targetTime = DateTime.Today.Add(backupTime);

				if (now >= targetTime && !BackupWasCreatedToday())
				{
					CreateBackup();
					CleanOldBackups();
				}

				while (!cancellationToken.IsCancellationRequested)
				{
					now = DateTime.Now;
					targetTime = DateTime.Today.Add(backupTime);

					if (now >= targetTime && !BackupWasCreatedToday())
					{
						CreateBackup();
						CleanOldBackups();
					}

					await Task.Delay(TimeSpan.FromMinutes(30), cancellationToken);
				}
			}, cancellationToken);
		}

		private bool BackupWasCreatedToday()
		{
			var today = DateTime.Today;

			return Directory.GetFiles(backupFolderPath, "Backup_*.db")
				.Select(f => new FileInfo(f))
				.Any(f => f.CreationTime.Date == today && !f.Name.Contains("_manual"));
		}

		private bool CreateBackup(string? suffix = null)
		{
			try
			{
				var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
				var baseFilename = suffix == null
					? $"Backup_{timestamp}"
					: $"Backup_{timestamp}{suffix}";

				var backupPath = Path.Combine(backupFolderPath, baseFilename + ".db");

				using var source = new SqliteConnection($"Data Source={ConstData.DATA_BASE_FILE_NAME}");
				using var destination = new SqliteConnection($"Data Source={backupPath}");

				source.Open();
				destination.Open();

				source.BackupDatabase(destination);
				SqliteConnection.ClearPool(destination);

				return true;
			}
			catch (Exception e)
			{
				Console.WriteLine($"Error while creating database backup: {e.Message}");
				return false;
			}
		}

		private void CleanOldBackups()
		{
			try
			{
				var files = Directory.GetFiles(backupFolderPath, "Backup_*.db")
									 .Select(path => new FileInfo(path))
									 .Where(f => !f.Name.Contains("_manual"))
									 .OrderByDescending(f => f.CreationTime)
									 .ToList();

				var now = DateTime.Now;

				foreach (var file in files)
				{
					var age = now - file.CreationTime;

					if (age.TotalDays <= 14)
						continue;

					var sameMonth = files.Where(f =>
						f.CreationTime.Year == file.CreationTime.Year &&
						f.CreationTime.Month == file.CreationTime.Month).ToList();

					var newestInMonth = sameMonth.OrderByDescending(f => f.CreationTime).First();

					if (file.FullName != newestInMonth.FullName)
					{
						file.Delete();
					}
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error while cleaning data base backup: {ex.Message}");
			}
		}

		public async Task<List<FileInfo>> GetBackupFilesAsync()
		{
			if (!Directory.Exists(backupFolderPath))
			{
				return [];
			}

			return await Task.Run(() =>
			{
				var zipFiles = Directory.GetFiles(backupFolderPath, "*.db");
				return zipFiles.Select(path => new FileInfo(path)).OrderByDescending(f => f.CreationTimeUtc).ToList();
			});
		}

		public async Task<(OperationResult OperationStatus, string Message)> DeleteBackupFileAsync(FileInfo file)
		{
			try
			{
				if (file is not null && file.Exists)
				{
					await Task.Run(() => file!.Delete());
					return (OperationResult.Success, "Plik kopi zapasowej został usunięty");
				}

				return (OperationResult.Error, "Nie znaleziono takiej kopi zapasowej");
			}
			catch
			{
				return (OperationResult.Error, "Nie udało się usunąć tej kopi zapasowej");
			}
		}

		public async Task<(OperationResult OperationStatus, string Message)> RestoreFromDbBackupAsync(FileInfo file)
		{
			var result = await ImportAsync(file.FullName);

			if (result)
			{
				return (OperationResult.Success, "Skutecznie zastosowano nową baze danych");
			}

			return (OperationResult.Error, "Nie udało się zastosować nowej bazy danych");
		}
	}
}
