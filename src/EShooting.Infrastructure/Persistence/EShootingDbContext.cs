using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EShooting.Infrastructure.Persistence;

public sealed class EShootingDbContext(DbContextOptions<EShootingDbContext> options) : DbContext(options)
{
    public DbSet<Athlete> Athletes => Set<Athlete>();
    public DbSet<Lane> Lanes => Set<Lane>();
    public DbSet<TrainingSession> Sessions => Set<TrainingSession>();
    public DbSet<ScoreEntry> Scores => Set<ScoreEntry>();
    public DbSet<SubscriptionSchedule> SubscriptionSchedules => Set<SubscriptionSchedule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Athlete>(entity =>
        {
            entity.ToTable("Athletes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200);
            entity.Property(x => x.IdCardNumber).HasMaxLength(60);
            entity.Property(x => x.Category)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(CustomerCategory.Amateur);
            entity.Property(x => x.MembershipType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(MembershipType.FullCombo);
            entity.Property(x => x.IsFullPackage).HasDefaultValue(false);
        });

        modelBuilder.Entity<Lane>(entity =>
        {
            entity.ToTable("Lanes");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Number).IsUnique();
            entity.Property(x => x.LaneType)
                .HasConversion<string>()
                .HasMaxLength(20);
        });

        modelBuilder.Entity<TrainingSession>(entity =>
        {
            entity.ToTable("TrainingSessions");
            entity.HasKey(x => x.Id);

            entity.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(x => x.IsEquipmentIssued).HasDefaultValue(false);
            entity.Property(x => x.EquipmentReturnedAtUtc).IsRequired(false);

            entity.HasOne<Athlete>()
                .WithMany()
                .HasForeignKey(x => x.AthleteId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Lane>()
                .WithMany()
                .HasForeignKey(x => x.LaneId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(x => x.SubscriptionScheduleId).IsRequired(false);
            entity.HasOne<SubscriptionSchedule>()
                .WithMany()
                .HasForeignKey(x => x.SubscriptionScheduleId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(x => new { x.SubscriptionScheduleId, x.StartTimeUtc });
        });

        modelBuilder.Entity<ScoreEntry>(entity =>
        {
            entity.ToTable("ScoreEntries");
            entity.HasKey(x => x.Id);

            entity.HasOne<TrainingSession>()
                .WithMany(x => x.Scores)
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SubscriptionSchedule>(entity =>
        {
            entity.ToTable("SubscriptionSchedules");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.StartTimeLocal).HasConversion<TimeSpan>();
            entity.HasIndex(x => new { x.DayOfWeek, x.StartTimeLocal, x.IsEnabled });
            entity.Property(x => x.PreferredLaneType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(PreferredLaneType.Any);
            entity.Property(x => x.IsFullPackage).HasDefaultValue(false);

            entity.HasOne<Athlete>()
                .WithMany()
                .HasForeignKey(x => x.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
