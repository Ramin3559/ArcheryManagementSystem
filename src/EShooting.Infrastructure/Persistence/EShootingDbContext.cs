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
    public DbSet<ServicePackage> ServicePackages => Set<ServicePackage>();
    public DbSet<EquipmentItem> EquipmentItems => Set<EquipmentItem>();
    public DbSet<SessionEquipmentIssue> SessionEquipmentIssues => Set<SessionEquipmentIssue>();
    public DbSet<EquipmentSaleReceipt> EquipmentSaleReceipts => Set<EquipmentSaleReceipt>();
    public DbSet<EquipmentSaleReceiptLine> EquipmentSaleReceiptLines => Set<EquipmentSaleReceiptLine>();
    public DbSet<StaffPosition> StaffPositions => Set<StaffPosition>();
    public DbSet<AccessProfile> AccessProfiles => Set<AccessProfile>();
    public DbSet<StaffMember> StaffMembers => Set<StaffMember>();
    public DbSet<CustomerPackageRecord> CustomerPackageRecords => Set<CustomerPackageRecord>();

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
            entity.Property(x => x.ClubCardNumber).HasMaxLength(40);
            entity.Property(x => x.Category)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(CustomerCategory.Amateur);
            entity.Property(x => x.MembershipType)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(MembershipType.FullCombo);
            entity.Property(x => x.IsFullPackage).HasDefaultValue(false);
            entity.Property(x => x.IsVip).HasDefaultValue(false);
            entity.Property(x => x.IsGroupPlaceholder).HasDefaultValue(false);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.CreatedAtUtc).HasDefaultValueSql("GETUTCDATE()");
            entity.HasIndex(x => x.RegisteredByStaffId);
        });

        modelBuilder.Entity<CustomerPackageRecord>(entity =>
        {
            entity.ToTable("CustomerPackageRecords");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PackageName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.BillingTypeLabel).HasMaxLength(40).IsRequired();
            entity.Property(x => x.PriceDue).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaidCash).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaidCard).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaid).HasPrecision(18, 2);
            entity.HasIndex(x => x.AthleteId);
            entity.HasIndex(x => x.CreatedAtUtc);
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
            entity.Property(x => x.ActivatedAtUtc).IsRequired(false);

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
            entity.Property(x => x.ExcludedOccurrenceDatesJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.OccurrenceOverridesJson).HasColumnType("nvarchar(max)");

            entity.HasOne<Athlete>()
                .WithMany()
                .HasForeignKey(x => x.AthleteId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ServicePackage>(entity =>
        {
            entity.ToTable("ServicePackages");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.BillingType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Scope).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.SchedulingMode).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.WeeklyDaysCsv).HasMaxLength(30);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.Property(x => x.UnlimitedGym).HasDefaultValue(false);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.IsDeleted);
        });

        modelBuilder.Entity<EquipmentItem>(entity =>
        {
            entity.ToTable("EquipmentItems");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Category).HasMaxLength(80);
            entity.Property(x => x.UsageMode)
                .HasConversion<int>()
                .HasDefaultValue(EquipmentUsageMode.Both);
            entity.Property(x => x.Price).HasPrecision(18, 2);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.DamagedQuantity).HasDefaultValue(0);
            entity.Property(x => x.RentalQuantity).HasDefaultValue(0);
            entity.Property(x => x.SaleQuantity).HasDefaultValue(0);
            entity.HasIndex(x => x.IsActive);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => x.IsDeleted);
        });

        modelBuilder.Entity<SessionEquipmentIssue>(entity =>
        {
            entity.ToTable("SessionEquipmentIssues");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.IssueType).HasConversion<string>().HasMaxLength(20);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.Quantity).HasDefaultValue(1);
            entity.Property(x => x.ReturnedAtUtc).IsRequired(false);
            entity.HasIndex(x => x.SessionId);
            entity.HasIndex(x => x.EquipmentItemId);
            entity.HasOne<TrainingSession>()
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EquipmentItem>()
                .WithMany()
                .HasForeignKey(x => x.EquipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EquipmentSaleReceipt>(entity =>
        {
            entity.ToTable("EquipmentSaleReceipts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasConversion<int>();
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaidCash).HasPrecision(18, 2);
            entity.Property(x => x.AmountPaidCard).HasPrecision(18, 2);
            entity.HasIndex(x => x.AthleteId);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasOne<Athlete>()
                .WithMany()
                .HasForeignKey(x => x.AthleteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<EquipmentSaleReceiptLine>(entity =>
        {
            entity.ToTable("EquipmentSaleReceiptLines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.DiscountAmount).HasPrecision(18, 2);
            entity.Property(x => x.Quantity).HasDefaultValue(1);
            entity.HasIndex(x => x.ReceiptId);
            entity.HasIndex(x => x.EquipmentItemId);
            entity.HasOne<EquipmentSaleReceipt>()
                .WithMany()
                .HasForeignKey(x => x.ReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<EquipmentItem>()
                .WithMany()
                .HasForeignKey(x => x.EquipmentItemId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StaffPosition>(entity =>
        {
            entity.ToTable("StaffPositions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.IsActive);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => x.IsDeleted);
            entity.HasOne(x => x.DefaultAccessProfile)
                .WithMany()
                .HasForeignKey(x => x.DefaultAccessProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AccessProfile>(entity =>
        {
            entity.ToTable("AccessProfiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(80).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(240);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.IsActive);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => x.IsDeleted);
        });

        modelBuilder.Entity<StaffMember>(entity =>
        {
            entity.ToTable("StaffMembers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FirstName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(80).IsRequired();
            entity.Property(x => x.PhoneNumber).HasMaxLength(40);
            entity.Property(x => x.PinHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PinPlain).HasMaxLength(6);
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.Property(x => x.IsDeleted).HasDefaultValue(false);
            entity.HasIndex(x => x.IsActive);
            entity.HasIndex(x => x.IsDeleted);
            entity.HasIndex(x => x.StaffPositionId);
            entity.HasIndex(x => x.AccessProfileId);

            entity.HasOne(x => x.StaffPosition)
                .WithMany()
                .HasForeignKey(x => x.StaffPositionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.AccessProfile)
                .WithMany()
                .HasForeignKey(x => x.AccessProfileId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
