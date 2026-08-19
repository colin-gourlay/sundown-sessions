using Microsoft.EntityFrameworkCore;

namespace SundownSessions.Showrunner.Persistence;

public sealed class ShowrunnerDbContext(DbContextOptions<ShowrunnerDbContext> options) : DbContext(options)
{
    internal DbSet<RecordingEntity> Recordings => Set<RecordingEntity>();

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
            builder.Property(item => item.Title).HasMaxLength(256).IsRequired();
            builder.Property(item => item.Artist).HasMaxLength(256);
            builder.Property(item => item.Notes).HasMaxLength(2000);
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
            builder.Property(item => item.Source).HasMaxLength(64).IsRequired();
            builder.Property(item => item.Value).HasMaxLength(512).IsRequired();
            builder.HasIndex(item => new { item.RecordingId, item.Source, item.Value }).IsUnique();
        });

        modelBuilder.Entity<BacklogItemEntity>(builder =>
        {
            builder.ToTable("BacklogItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.Property(item => item.Summary).HasMaxLength(256).IsRequired();
            builder.Property(item => item.Notes).HasMaxLength(2000);
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
            builder.Property(item => item.Slug).HasMaxLength(128).IsRequired();
            builder.Property(item => item.Title).HasMaxLength(256).IsRequired();
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
            builder.Property(item => item.Notes).HasMaxLength(2000);
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
        });

        modelBuilder.Entity<ReconciliationItemEntity>(builder =>
        {
            builder.ToTable("ReconciliationItems");
            builder.HasKey(item => item.Id);
            builder.Property(item => item.Id).ValueGeneratedNever();
            builder.HasIndex(item => new { item.ReconciliationId, item.PlannedRecordingId }).IsUnique();
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
            builder.HasIndex(item => new { item.ShowId, item.PlannedRecordingId }).IsUnique();
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
            builder.Property(item => item.Reason).HasMaxLength(1000).IsRequired();
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
