using Blazored.LocalStorage;
using DystenSoftwateManagment.Components;
using DystenSoftwateManagment.Data;
using DystenSoftwateManagment.Managers;
using DystenSoftwateManagment.Utility;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MudBlazor;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

var dbConString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddRazorComponents()
	.AddInteractiveServerComponents();

builder.Services.AddRazorPages();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
	options.UseSqlite(dbConString));

builder.Services.AddScoped(provider =>
{
	var factory = provider.GetRequiredService<IDbContextFactory<AppDbContext>>();
	return factory.CreateDbContext();
});

builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
	options.Password.RequireDigit = false;
	options.Password.RequireLowercase = false;
	options.Password.RequireNonAlphanumeric = false;
	options.Password.RequireUppercase = false;
	options.Password.RequiredLength = 0;
	options.Password.RequiredUniqueChars = 0;
})
	.AddEntityFrameworkStores<AppDbContext>()
	.AddDefaultTokenProviders();

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();

builder.Services.AddQuickGridEntityFrameworkAdapter();
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped<DevicesManager>();
builder.Services.AddScoped<DevicesSoftwaresManager>();
builder.Services.AddScoped<PlatformsManager>();
builder.Services.AddScoped<SoftwaresPlatformsManager>();
builder.Services.AddScoped<SoftwareTypesManager>();
builder.Services.AddScoped<UsersManager>();
builder.Services.AddScoped<DataBaseManager>();
builder.Services.AddScoped<ProjectsManager>();
builder.Services.AddScoped<ProtocolsRSManager>();
builder.Services.AddScoped<ProjectDocumentsManager>();
builder.Services.AddScoped<DeviceComponentsManager>();

builder.Services.AddBlazoredLocalStorage();

builder.Services.AddMudServices(config =>
{
	config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopRight;

	config.SnackbarConfiguration.PreventDuplicates = false;
	config.SnackbarConfiguration.NewestOnTop = false;
	config.SnackbarConfiguration.ShowCloseIcon = false;
	config.SnackbarConfiguration.ClearAfterNavigation = false;
	config.SnackbarConfiguration.NormalIcon = Icons.Material.Outlined.Check;
	config.SnackbarConfiguration.IconSize = Size.Small;
	config.SnackbarConfiguration.VisibleStateDuration = 3000;
	config.SnackbarConfiguration.HideTransitionDuration = 100;
	config.SnackbarConfiguration.ShowTransitionDuration = 100;
	config.SnackbarConfiguration.SnackbarVariant = Variant.Filled;
});

builder.Services.Configure<FormOptions>(options =>
{
	options.MultipartBodyLengthLimit = long.MaxValue;
});

builder.Services.AddAuthorizationBuilder()
	.AddPolicy("CanExportDatabase", policy =>
		policy.RequireRole("Admin"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Error", createScopeForErrors: true);
	app.UseHsts();
	app.UseMigrationsEndPoint();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/account/logout", async (HttpContext context, SignInManager<IdentityUser> signInManager) =>
{
	await signInManager.SignOutAsync();
	context.Response.Redirect("/");
});

app.MapGet("/download/{id:int}", async (int id, SoftwaresPlatformsManager manager) =>
{
	var file = await manager.GetFileData(id);

	if (file?.Data == null || file.Data.Length == 0)
	{
		return Results.NotFound();
	}

	var stream = new MemoryStream(file.Data);
	return Results.Stream(stream, "application/zip", file.SoftwarePlatform.FileName);
});

app.MapGet("/download/rs-protocol/{id:int}", async (int id, ProtocolsRSManager manager) =>
{
	var protocol = await manager.GetWithFileDataAsync(id);

	var data = protocol?.FileData?.Data;
	if (data == null || data.Length == 0)
	{
		return Results.NotFound();
	}

	var stream = new MemoryStream(data);
	return Results.Stream(stream, "application/zip", protocol.FileName);
});

app.MapGet("/download/document/{id:int}", async (int id, ProjectDocumentsManager manager) =>
{
	var document = await manager.GetWithFileDataAsync(id);

	var data = document?.FileData?.Data;
	if (data == null || data.Length == 0)
	{
		return Results.NotFound();
	}

	var stream = new MemoryStream(data);
	return Results.Stream(stream, "application/zip", document.FileName);
});

var sqliteConnBuilder = new SqliteConnectionStringBuilder(dbConString);

app.MapGet("/export-db/{fileNameExport}", (string fileNameExport, IWebHostEnvironment env) =>
{
	var backupFolder = Path.Combine(env.ContentRootPath, "Backups");
	var filePath = Path.Combine(backupFolder, fileNameExport);

	if (!File.Exists(filePath))
	{
		return Results.NotFound("File not found.");
	}

	var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileNameExport);
	var extension = Path.GetExtension(fileNameExport);
	var exportFileName = $"{nameWithoutExtension}-export{extension}";

	var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
	return Results.Stream(stream, "application/zip", exportFileName);
}).RequireAuthorization("CanExportDatabase");

app.MapGet("/export-db", (IWebHostEnvironment env) =>
{
	string dbPath = Path.Combine(env.ContentRootPath, ConstData.DATA_BASE_FILE_NAME);

	if (!File.Exists(dbPath))
		return Results.NotFound();

	var stream = new MemoryStream();

	using (var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Create, true))
	{
		var entry = archive.CreateEntry($"db-export-{DateTime.Now:yyyyMMddHHmmss}.db");

		using var entryStream = entry.Open();
		using var fileStream = new FileStream(dbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
		fileStream.CopyTo(entryStream);
	}

	stream.Position = 0;
	return Results.Stream(stream, "application/zip", "exported-db.zip");
}).RequireAuthorization("CanExportDatabase");

var dbManager = new DataBaseManager();

var cts = new CancellationTokenSource();
dbManager.StartBackupLoop(cts.Token);

app.Lifetime.ApplicationStopping.Register(() =>
{
	cts.Cancel();
});

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
	.AddInteractiveServerRenderMode();

using (var scope = app.Services.CreateScope())
{
	var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
	var rootSection = config.GetSection("RootUser");

	var userName = rootSection["UserName"];
	var password = rootSection["Password"];

	if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
	{
		throw new Exception("RootUser UserName or Password is missing in configuration");
	}

	var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
	string[] roles = ["Admin", "Developer", "User"];

	foreach (var role in roles)
	{
		if (!await roleManager.RoleExistsAsync(role))
		{
			await roleManager.CreateAsync(new IdentityRole(role));
		}
	}

	var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
	var rootUser = await userManager.FindByNameAsync(userName!);

	if (rootUser == null)
	{
		rootUser = new IdentityUser { UserName = userName };
		var result = await userManager.CreateAsync(rootUser, password!);

		if (result.Succeeded)
		{
			await userManager.AddToRolesAsync(rootUser, ["Admin", "Developer"]);
		}
	}
}

app.Run();