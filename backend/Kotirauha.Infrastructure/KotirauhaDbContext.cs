using Kotirauha.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Kotirauha.Infrastructure;

public class KotirauhaDbContext : DbContext
{
    public KotirauhaDbContext(DbContextOptions<KotirauhaDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MagicLinkToken> MagicLinkTokens => Set<MagicLinkToken>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<BuildingMembership> Memberships => Set<BuildingMembership>();
    public DbSet<IncidentEntry> Entries => Set<IncidentEntry>();
    public DbSet<IncidentTranslation> Translations => Set<IncidentTranslation>();
    public DbSet<IncidentAttachment> Attachments => Set<IncidentAttachment>();
    public DbSet<IncidentRevision> Revisions => Set<IncidentRevision>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<User>(e =>
        {
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.Property(u => u.DisplayName).HasMaxLength(120);
            e.Property(u => u.PreferredLanguage).HasMaxLength(16);
        });

        b.Entity<MagicLinkToken>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            e.HasIndex(x => x.TokenHash);
        });

        b.Entity<Building>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.SharedLanguage).HasMaxLength(16).IsRequired();
            e.Property(x => x.JoinCode).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.JoinCode).IsUnique();
        });

        b.Entity<BuildingMembership>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.BuildingId }).IsUnique();
            e.Property(x => x.ApartmentNumber).HasMaxLength(32);
            e.HasOne(x => x.User).WithMany(u => u.Memberships).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Building).WithMany(g => g.Memberships).HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<IncidentEntry>(e =>
        {
            e.Property(x => x.OriginalText).IsRequired();
            e.Property(x => x.OriginalLanguage).HasMaxLength(16).IsRequired();
            e.Property(x => x.SubjectApartment).HasMaxLength(32);
            e.HasIndex(x => new { x.BuildingId, x.OccurredAt });
            e.HasOne(x => x.Building).WithMany().HasForeignKey(x => x.BuildingId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<IncidentTranslation>(e =>
        {
            e.Property(x => x.TargetLanguage).HasMaxLength(16).IsRequired();
            e.Property(x => x.Provider).HasMaxLength(64);
            e.Property(x => x.Model).HasMaxLength(128);
            e.HasIndex(x => new { x.EntryId, x.TargetLanguage }).IsUnique();
            e.HasOne(x => x.Entry).WithMany(en => en.Translations).HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<IncidentAttachment>(e =>
        {
            e.Property(x => x.StorageKey).HasMaxLength(256).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            e.HasOne(x => x.Entry).WithMany(en => en.Attachments).HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<IncidentRevision>(e =>
        {
            e.Property(x => x.PreviousText).IsRequired();
            e.HasOne(x => x.Entry).WithMany(en => en.Revisions).HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
