using Microsoft.EntityFrameworkCore;

namespace SundownSessions.Showrunner.Persistence;

public sealed class ShowrunnerDbContext(DbContextOptions<ShowrunnerDbContext> options) : DbContext(options)
{
    internal DbSet<RecordingEntity> Recordings => Set<RecordingEntity>();

    internal DbSet<RecordingExternalIdentifierEntity> RecordingExternalIdentifiers => Set<RecordingExternalIdentifierEntity>();

    internal DbSet<BacklogItemEntity> BacklogItems => Set<BacklogItemEntity>();

    internal DbSet<ShowEntity> Shows => Set<ShowEntity>();

    internal DbSet<ReconciliationEntity> Reconciliations => Set<ReconciliationEntity>();

    internal DbSet<BroadcastRecordingEntity> BroadcastRecordings => Set<BroadcastRecordingEntity>();

    internal DbSet<RepeatExceptionEntity> RepeatExceptions => Set<RepeatExceptionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecordingEntity>(builder =>
        {
            builder.ToTable("Recordings");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.Title).HasMaxLength(FieldLimits.Title).IsRequired();
            builder.Property(item => item.Artist).HasMaxLength(FieldLimits.Artist);
            builder.Property(item => item.ReleaseTitle).HasMaxLength(FieldLimits.Title);
            builder.Property(item => item.Notes).HasMaxLength(FieldLimits.Notes);
            builder.HasMany(item => item.ExternalIdentifiers)
                .WithOne(item => item.Recording)
                .HasForeignKey(item => item.RecordingId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecordingExternalIdentifierEntity>(builder =>
        {
            builder.ToTable("RecordingExternalIdentifiers");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.Source).HasMaxLength(FieldLimits.ExternalIdentifierSource).IsRequired();
            builder.Property(item => item.Value).HasMaxLength(FieldLimits.ExternalIdentifierValue).IsRequired();
            builder.HasIndex(item => new { item.Source, item.Value }).IsUnique();
        });

        modelBuilder.Entity<BacklogItemEntity>(builder =>
        {
            builder.ToTable("BacklogItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.Summary).HasMaxLength(FieldLimits.Title).IsRequired();
            builder.Property(item => item.Notes).HasMaxLength(FieldLimits.Notes);
            builder.HasOne<RecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.RecordingId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<ShowEntity>(builder =>
        {
            builder.ToTable("Shows");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.Slug).HasMaxLength(FieldLimits.ShowSlug).IsRequired();
            builder.Property(item => item.Title).HasMaxLength(FieldLimits.Title).IsRequired();
            builder.HasIndex(item => item.Slug).IsUnique();
            builder.HasMany(item => item.PlannedRecordings)
                .WithOne(item => item.Show)
                .HasForeignKey(item => item.ShowId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(item => item.Reconciliation)
                .WithOne(item => item.Show)
                .HasForeignKey<ReconciliationEntity>(item => item.ShowId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(item => item.BroadcastRecordings)
                .WithOne(item => item.Show)
                .HasForeignKey(item => item.ShowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlannedRecordingEntity>(builder =>
        {
            builder.ToTable("PlannedRecordings");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.Notes).HasMaxLength(FieldLimits.Notes);
            builder.ToTable(table => table.HasCheckConstraint("CK_PlannedRecordings_Position", "Position >= 1"));
            builder.HasIndex(item => new { item.ShowId, item.Position }).IsUnique();
            builder.HasOne<RecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.RecordingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReconciliationEntity>(builder =>
        {
            builder.ToTable("Reconciliations");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.HasMany(item => item.Items)
                .WithOne(item => item.Reconciliation)
                .HasForeignKey(item => item.ReconciliationId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(item => item.ConfirmedPlayback)
                .WithOne(item => item.Reconciliation)
                .HasForeignKey(item => item.ReconciliationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReconciliationItemEntity>(builder =>
        {
            builder.ToTable("ReconciliationItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.HasIndex(item => new { item.ReconciliationId, item.PlannedRecordingId }).IsUnique();
            builder.ToTable(table => table.HasCheckConstraint("CK_ReconciliationItems_Outcome", "Outcome IN (0, 1, 2)"));
            builder.HasOne<PlannedRecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.PlannedRecordingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ConfirmedPlaybackItemEntity>(builder =>
        {
            builder.ToTable("ConfirmedPlaybackItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.ToTable(table => table.HasCheckConstraint("CK_ConfirmedPlaybackItems_Position", "Position >= 1"));
            builder.HasIndex(item => new { item.ReconciliationId, item.Position }).IsUnique();
            builder.HasIndex(item => new { item.ReconciliationId, item.PlannedRecordingId })
                .IsUnique()
                .HasFilter("\"PlannedRecordingId\" IS NOT NULL");
            builder.HasOne<RecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.RecordingId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PlannedRecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.PlannedRecordingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<BroadcastRecordingEntity>(builder =>
        {
            builder.ToTable("BroadcastRecordings");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.ToTable(table => table.HasCheckConstraint("CK_BroadcastRecordings_Position", "Position >= 1"));
            builder.HasIndex(item => new { item.ShowId, item.Position }).IsUnique();
            builder.HasIndex(item => new { item.ShowId, item.PlannedRecordingId })
                .IsUnique()
                .HasFilter("\"PlannedRecordingId\" IS NOT NULL");
            builder.HasOne<RecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.RecordingId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<PlannedRecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.PlannedRecordingId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RepeatExceptionEntity>(builder =>
        {
            builder.ToTable("RepeatExceptions");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.Reason).HasMaxLength(FieldLimits.RepeatExceptionReason).IsRequired();
            builder.HasIndex(item => new { item.ShowId, item.RecordingId }).IsUnique();
            builder.HasOne<ShowEntity>()
                .WithMany()
                .HasForeignKey(item => item.ShowId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<RecordingEntity>()
                .WithMany()
                .HasForeignKey(item => item.RecordingId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
