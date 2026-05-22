using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BlazorWebForms.Infrastructure.SqlServer;

public sealed class BlazorWebFormsDbContext : DbContext
{
    public BlazorWebFormsDbContext(DbContextOptions<BlazorWebFormsDbContext> options) : base(options)
    {
    }

    public DbSet<FormEntity> Forms { get; set; } = null!;
    public DbSet<FormVersionEntity> FormVersions { get; set; } = null!;
    public DbSet<EntryEntity> Entries { get; set; } = null!;
    public DbSet<EntrySearchIndexEntity> EntrySearchIndex { get; set; } = null!;
    public DbSet<EntryRevisionEntity> EntryRevisions { get; set; } = null!;
    public DbSet<ApprovalStepEntity> ApprovalSteps { get; set; } = null!;
    public DbSet<FormPermissionEntity> FormPermissions { get; set; } = null!;
    public DbSet<FormNotificationEntity> FormNotifications { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var dictStringConverter = new ValueConverter<Dictionary<string, string>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, jsonOptions) ?? new());

        var dictNullableConverter = new ValueConverter<Dictionary<string, string?>, string>(
            v => JsonSerializer.Serialize(v, jsonOptions),
            v => JsonSerializer.Deserialize<Dictionary<string, string?>>(v, jsonOptions) ?? new());

        modelBuilder.Entity<FormEntity>(b =>
        {
            b.ToTable("Forms");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).IsRequired();
            b.Property(x => x.Name).IsRequired();
            b.Property(x => x.Description).HasMaxLength(2000);
            b.Property(x => x.DraftDefinitionJson).HasColumnType("nvarchar(max)");
            b.HasMany(x => x.Versions).WithOne(v => v.Form).HasForeignKey(v => v.FormId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Permissions).WithOne(p => p.Form).HasForeignKey(p => p.FormId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.Notifications).WithOne(n => n.Form).HasForeignKey(n => n.FormId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<FormVersionEntity>(b =>
        {
            b.ToTable("FormVersions");
            b.HasKey(x => x.Id);
            b.Property(x => x.DefinitionJson).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<EntryEntity>(b =>
        {
            b.ToTable("Entries");
            b.HasKey(x => x.Id);
            b.Property(x => x.SubmittedBy).HasMaxLength(256);
            b.Property(x => x.SubmittedByEmail).HasMaxLength(256);
            b.Property(x => x.Answers).HasConversion(dictNullableConverter).HasColumnType("nvarchar(max)");
            b.Property(x => x.SearchIndex).HasConversion(dictStringConverter).HasColumnType("nvarchar(max)");
            b.HasMany(x => x.Revisions).WithOne(r => r.Entry).HasForeignKey(r => r.EntryId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.ApprovalSteps).WithOne(a => a.Entry).HasForeignKey(a => a.EntryId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.SearchIndexEntries).WithOne(s => s.Entry).HasForeignKey(s => s.EntryId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EntryRevisionEntity>(b =>
        {
            b.ToTable("EntryRevisions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Answers).HasConversion(dictNullableConverter).HasColumnType("nvarchar(max)");
        });

        modelBuilder.Entity<ApprovalStepEntity>(b =>
        {
            b.ToTable("ApprovalSteps");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<FormPermissionEntity>(b =>
        {
            b.ToTable("FormPermissions");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<FormNotificationEntity>(b =>
        {
            b.ToTable("FormNotifications");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<EntrySearchIndexEntity>(b =>
        {
            b.ToTable("EntrySearchIndex");
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).IsRequired();
            b.Property(x => x.Value).IsRequired();
            b.HasIndex(x => new { x.Key, x.Value });
            b.HasOne(x => x.Entry).WithMany(e => e.SearchIndexEntries).HasForeignKey(x => x.EntryId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
