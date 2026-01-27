using DystenSoftwateManagment.Models;
using DystenSoftwateManagment.Utility;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace DystenSoftwateManagment.Data
{
	public class AppDbContext(DbContextOptions<AppDbContext> options)
		: IdentityDbContext<IdentityUser>
	{
		public DbSet<Project> Projects { get; set; }
		public DbSet<Device> Devices { get; set; }
		public DbSet<DeviceSoftware> DevicesSoftwares { get; set; }
		public DbSet<SoftwareType> SoftwareTypes { get; set; }
		public DbSet<SoftwarePlatform> SoftwarePlatforms { get; set; }
		public DbSet<Platform> Platforms { get; set; }
		public DbSet<SoftwarePlatformFile> SoftwarePlatformFiles { get; set; }

		public DbSet<DeviceComponent> DeviceComponents { get; set; }

		public DbSet<ProtocolRS> ProtocolsRS { get; set; }
		public DbSet<ProtocolRSFile> ProtocolsRSFiles { get; set; }

		public DbSet<ProjectDocument> ProjectDocuments { get; set; }
		public DbSet<ProjectDocumentFile> ProjectDocumentsFiles { get; set; }

		protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
		{
			if (!optionsBuilder.IsConfigured)
			{
				optionsBuilder.UseSqlite($"Data Source={ConstData.DATA_BASE_FILE_NAME}");
			}
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			//modelBuilder.Entity<Device>()
			//	.HasOne(d => d.Project)
			//	.WithMany(p => p.Devices)
			//	.HasForeignKey(d => d.ProjectId)
			//	.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<DeviceSoftware>()
				.HasOne(ds => ds.Device)
				.WithMany(d => d.Softwares)
				.HasForeignKey(ds => ds.DeviceId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<DeviceSoftware>()
				.HasOne(ds => ds.SoftwareType)
				.WithMany(st => st.DeviceSoftwares)
				.HasForeignKey(ds => ds.SoftwareTypeId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<SoftwarePlatform>()
				.HasOne(sp => sp.DeviceSoftware)
				.WithMany(ds => ds.SoftwarePlatforms)
				.HasForeignKey(sp => sp.DeviceSoftwareId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<SoftwarePlatform>()
				.HasOne(sp => sp.Platform)
				.WithMany(p => p.SoftwarePlatforms)
				.HasForeignKey(sp => sp.PlatformId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<DeviceSoftware>()
				.HasOne(ds => ds.ProtocolRS)
				.WithMany(prs => prs.DeviceSoftwares)
				.HasForeignKey(ds => ds.ProtocolRSId)
				.OnDelete(DeleteBehavior.Restrict);

			modelBuilder.Entity<ProtocolRS>()
				.HasOne(prs => prs.FileData)
				.WithOne(f => f.ProtocolRS)
				.HasForeignKey<ProtocolRSFile>(f => f.ProtocolRSId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<ProjectDocument>()
				.HasOne(dd => dd.Project)
				.WithMany(d => d.Documents)
				.HasForeignKey(dd => dd.ProjectId)
				.OnDelete(DeleteBehavior.Cascade);

			modelBuilder.Entity<ProjectDocument>()
				.HasOne(dd => dd.FileData)
				.WithOne(f => f.ProjectDocument)
				.HasForeignKey<ProjectDocumentFile>(f => f.ProjectDocumentId)
				.OnDelete(DeleteBehavior.Cascade);
		}
	}
}
