using EShooting.Application.Common;
using EShooting.Domain.Entities;
using EShooting.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace EShooting.Infrastructure.Persistence;

public sealed class EShootingDbInitializer(EShootingDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureSubscriptionScheduleTableAsync(cancellationToken);
        await EnsureAthleteMembershipTypeAsync(cancellationToken);
        await EnsureAthleteProfileFieldsAsync(cancellationToken);
        await EnsureAthleteUniqueIndexesAsync(cancellationToken);
        await EnsureSessionReservationUniquenessAsync(cancellationToken);
        await EnsureTrainingSessionsSubscriptionLinkAsync(cancellationToken);
        await EnsureServicePackagesTableAsync(cancellationToken);
        await EnsureServicePackageSeedAsync(cancellationToken);
        await NormalizeVipServicePackagesAsync(cancellationToken);
        await PurgeLegacyWalkInMonthlyPackageAsync(cancellationToken);
        await EnsureEquipmentItemsTableAsync(cancellationToken);
        await EnsureEquipmentItemSeedAsync(cancellationToken);
        await EnsureSessionEquipmentIssuesTableAsync(cancellationToken);
        await EnsureEquipmentItemUsageModeColumnAsync(cancellationToken);
        await EnsureEquipmentSplitStockColumnsAsync(cancellationToken);
        await EnsureEquipmentPriceAsUnitAsync(cancellationToken);
        await EnsureSessionEquipmentIssueJournalColumnsAsync(cancellationToken);
        await EnsureEquipmentSaleReceiptsTablesAsync(cancellationToken);
        await EnsureEquipmentSaleReceiptLineDiscountColumnAsync(cancellationToken);
        await EnsureStaffPositionsTableAsync(cancellationToken);
        await EnsureStaffPositionSeedAsync(cancellationToken);
        await EnsureAccessProfilesTableAsync(cancellationToken);
        await EnsureAccessProfilePermissionColumnsAsync(cancellationToken);
        await EnsureAccessProfileSeedAsync(cancellationToken);
        await EnsureStaffPositionDefaultProfileLinksAsync(cancellationToken);
        await EnsureStaffMembersTableAsync(cancellationToken);
        await EnsureStaffMemberPinPlainColumnAsync(cancellationToken);
        await EnsureCustomerPackageRecordsTableAsync(cancellationToken);
        await EnsureBillingDiscountColumnsAsync(cancellationToken);
        await EnsureAthleteCreatedAtBackfillAsync(cancellationToken);

        await EnsureShootingLanesSeedAsync(cancellationToken);
        await EnsureGymLaneAsync(cancellationToken);
    }

    private async Task EnsureShootingLanesSeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.Lanes.AnyAsync(l => l.Number >= 1 && l.Number <= 11, cancellationToken))
        {
            return;
        }

        var lanes = Enumerable.Range(1, 11)
            .Select(number => new Lane
            {
                Number = number,
                LaneType = number <= 8 ? LaneType.Amateur : LaneType.Professional
            });

        await dbContext.Lanes.AddRangeAsync(lanes, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureGymLaneAsync(CancellationToken cancellationToken)
    {
        const int gymLaneNumber = 12;
        if (await dbContext.Lanes.AnyAsync(l => l.Number == gymLaneNumber, cancellationToken))
        {
            return;
        }

        await dbContext.Lanes.AddAsync(new Lane
        {
            Number = gymLaneNumber,
            LaneType = LaneType.Gym
        }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAthleteMembershipTypeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF COL_LENGTH(N'[dbo].[Athletes]', N'MembershipType') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes]
                ADD [MembershipType] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Athletes_MembershipType] DEFAULT ('FullCombo');
            END
            ELSE
            BEGIN
                -- Fill possible NULLs from older DBs
                UPDATE [dbo].[Athletes]
                SET [MembershipType] = 'FullCombo'
                WHERE [MembershipType] IS NULL;
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureAthleteProfileFieldsAsync(CancellationToken cancellationToken)
    {
        // Add missing columns for extended athlete profile.
        const string sql = """
            IF COL_LENGTH(N'[dbo].[Athletes]', N'FirstName') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [FirstName] NVARCHAR(100) NOT NULL CONSTRAINT [DF_Athletes_FirstName] DEFAULT ('');
            END;
            IF COL_LENGTH(N'[dbo].[Athletes]', N'LastName') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [LastName] NVARCHAR(100) NOT NULL CONSTRAINT [DF_Athletes_LastName] DEFAULT ('');
            END;
            IF COL_LENGTH(N'[dbo].[Athletes]', N'PhoneNumber') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [PhoneNumber] NVARCHAR(40) NOT NULL CONSTRAINT [DF_Athletes_PhoneNumber] DEFAULT ('');
            END;
            IF COL_LENGTH(N'[dbo].[Athletes]', N'Email') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [Email] NVARCHAR(200) NULL;
            END;
            IF COL_LENGTH(N'[dbo].[Athletes]', N'IdCardNumber') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [IdCardNumber] NVARCHAR(60) NULL;
            END;
            IF COL_LENGTH(N'[dbo].[Athletes]', N'Category') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [Category] NVARCHAR(20) NOT NULL CONSTRAINT [DF_Athletes_Category] DEFAULT ('Amateur');
            END;
            IF COL_LENGTH(N'[dbo].[Athletes]', N'IsFullPackage') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [IsFullPackage] BIT NOT NULL CONSTRAINT [DF_Athletes_IsFullPackage] DEFAULT (0);
            END;
            IF COL_LENGTH(N'[dbo].[Athletes]', N'IsVip') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [IsVip] BIT NOT NULL CONSTRAINT [DF_Athletes_IsVip] DEFAULT (0);
            END

            IF COL_LENGTH(N'[dbo].[Athletes]', N'IsGroupPlaceholder') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [IsGroupPlaceholder] BIT NOT NULL CONSTRAINT [DF_Athletes_IsGroupPlaceholder] DEFAULT (0);
            END
            IF COL_LENGTH(N'[dbo].[Athletes]', N'IsActive') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [IsActive] BIT NOT NULL CONSTRAINT [DF_Athletes_IsActive] DEFAULT (1);
            END
            IF COL_LENGTH(N'[dbo].[Athletes]', N'ClubCardNumber') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [ClubCardNumber] NVARCHAR(40) NULL;
            END
            IF COL_LENGTH(N'[dbo].[Athletes]', N'CreatedAtUtc') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_Athletes_CreatedAtUtc] DEFAULT (GETUTCDATE());
            END
            IF COL_LENGTH(N'[dbo].[Athletes]', N'RegisteredByStaffId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[Athletes] ADD [RegisteredByStaffId] UNIQUEIDENTIFIER NULL;
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        const string backfillSql = """
            IF COL_LENGTH(N'[dbo].[Athletes]', N'IsGroupPlaceholder') IS NOT NULL
            BEGIN
                UPDATE [dbo].[Athletes]
                SET [IsGroupPlaceholder] = 1
                WHERE [IsGroupPlaceholder] = 0
                  AND [FullName] LIKE N'%, %'
                  AND LTRIM(RTRIM(ISNULL([PhoneNumber], N''))) = N''
                  AND LTRIM(RTRIM(ISNULL([FirstName], N''))) = N''
                  AND LTRIM(RTRIM(ISNULL([LastName], N''))) = N'';
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(backfillSql, cancellationToken);
    }

    private async Task EnsureAthleteUniqueIndexesAsync(CancellationToken cancellationToken)
    {
        // Keep a clean lookup experience by enforcing uniqueness on identifiers.
        // We first normalize existing values (trim, empty->NULL, email lower).
        const string sql = """
            UPDATE [dbo].[Athletes]
            SET
                [PhoneNumber] = LTRIM(RTRIM([PhoneNumber])),
                [Email] = NULLIF(LOWER(LTRIM(RTRIM([Email]))), ''),
                [IdCardNumber] = NULLIF(LTRIM(RTRIM([IdCardNumber])), ''),
                [ClubCardNumber] = NULLIF(LTRIM(RTRIM([ClubCardNumber])), '')
            WHERE 1=1;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Athletes_PhoneNumber' AND object_id = OBJECT_ID(N'[dbo].[Athletes]'))
            BEGIN
                CREATE UNIQUE INDEX [UX_Athletes_PhoneNumber]
                ON [dbo].[Athletes]([PhoneNumber])
                WHERE [PhoneNumber] IS NOT NULL AND [PhoneNumber] <> '';
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Athletes_Email' AND object_id = OBJECT_ID(N'[dbo].[Athletes]'))
            BEGIN
                CREATE UNIQUE INDEX [UX_Athletes_Email]
                ON [dbo].[Athletes]([Email])
                WHERE [Email] IS NOT NULL AND [Email] <> '';
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Athletes_IdCardNumber' AND object_id = OBJECT_ID(N'[dbo].[Athletes]'))
            BEGIN
                CREATE UNIQUE INDEX [UX_Athletes_IdCardNumber]
                ON [dbo].[Athletes]([IdCardNumber])
                WHERE [IdCardNumber] IS NOT NULL AND [IdCardNumber] <> '';
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Athletes_ClubCardNumber' AND object_id = OBJECT_ID(N'[dbo].[Athletes]'))
            BEGIN
                CREATE UNIQUE INDEX [UX_Athletes_ClubCardNumber]
                ON [dbo].[Athletes]([ClubCardNumber])
                WHERE [ClubCardNumber] IS NOT NULL AND [ClubCardNumber] <> '';
            END;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureSessionReservationUniquenessAsync(CancellationToken cancellationToken)
    {
        // Prevent the same athlete being scheduled on the same lane at the same start time (manual duplicates).
        const string sql = """
            IF OBJECT_ID(N'[dbo].[TrainingSessions]', N'U') IS NOT NULL
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TrainingSessions_Athlete_Lane_Start' AND object_id = OBJECT_ID(N'[dbo].[TrainingSessions]'))
                BEGIN
                    CREATE UNIQUE INDEX [UX_TrainingSessions_Athlete_Lane_Start]
                    ON [dbo].[TrainingSessions]([AthleteId], [LaneId], [StartTimeUtc]);
                END
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureTrainingSessionsSubscriptionLinkAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[TrainingSessions]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'[dbo].[TrainingSessions]', N'IsEquipmentIssued') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TrainingSessions]
                    ADD [IsEquipmentIssued] BIT NOT NULL CONSTRAINT [DF_TrainingSessions_IsEquipmentIssued] DEFAULT (0);
                END;

                IF COL_LENGTH(N'[dbo].[TrainingSessions]', N'EquipmentReturnedAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TrainingSessions]
                    ADD [EquipmentReturnedAtUtc] DATETIME2 NULL;
                END;

                IF COL_LENGTH(N'[dbo].[TrainingSessions]', N'SubscriptionScheduleId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TrainingSessions]
                    ADD [SubscriptionScheduleId] UNIQUEIDENTIFIER NULL;
                END;

                IF COL_LENGTH(N'[dbo].[TrainingSessions]', N'ActivatedAtUtc') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[TrainingSessions]
                    ADD [ActivatedAtUtc] DATETIME2 NULL;
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_TrainingSessions_SubscriptionSchedules_SubscriptionScheduleId')
                BEGIN
                    ALTER TABLE [dbo].[TrainingSessions] WITH NOCHECK
                    ADD CONSTRAINT [FK_TrainingSessions_SubscriptionSchedules_SubscriptionScheduleId]
                    FOREIGN KEY([SubscriptionScheduleId]) REFERENCES [dbo].[SubscriptionSchedules]([Id])
                    ON DELETE SET NULL;
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TrainingSessions_SubscriptionScheduleId_Start' AND object_id = OBJECT_ID(N'[dbo].[TrainingSessions]'))
                BEGIN
                    CREATE INDEX [IX_TrainingSessions_SubscriptionScheduleId_Start]
                    ON [dbo].[TrainingSessions]([SubscriptionScheduleId], [StartTimeUtc]);
                END;
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureSubscriptionScheduleTableAsync(CancellationToken cancellationToken)
    {
        const string createTableSql = """
            IF OBJECT_ID(N'[dbo].[SubscriptionSchedules]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SubscriptionSchedules](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [AthleteId] UNIQUEIDENTIFIER NOT NULL,
                    [LaneNumber] INT NOT NULL,
                    [DayOfWeek] INT NOT NULL,
                    [StartTimeLocal] TIME NOT NULL,
                    [DurationMinutes] INT NOT NULL,
                    [ActiveFromDateLocal] DATETIME2 NOT NULL,
                    [ActiveToDateLocal] DATETIME2 NOT NULL,
                    [IsEnabled] BIT NOT NULL,
                    [PreferredLaneType] NVARCHAR(20) NOT NULL CONSTRAINT [DF_SubscriptionSchedules_PreferredLaneType] DEFAULT ('Any'),
                    [IsFullPackage] BIT NOT NULL CONSTRAINT [DF_SubscriptionSchedules_IsFullPackage] DEFAULT (0),
                    [LastAssignedLaneNumber] INT NULL,
                    [LastAutoStartedAtUtc] DATETIME2 NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    CONSTRAINT [FK_SubscriptionSchedules_Athletes_AthleteId]
                        FOREIGN KEY([AthleteId]) REFERENCES [dbo].[Athletes]([Id]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_SubscriptionSchedules_Day_Start_Lane_Enabled]
                    ON [dbo].[SubscriptionSchedules]([DayOfWeek], [StartTimeLocal], [LaneNumber], [IsEnabled]);

                CREATE UNIQUE INDEX [UX_SubscriptionSchedules_Athlete_Day_Time_Lane_Enabled]
                    ON [dbo].[SubscriptionSchedules]([AthleteId], [DayOfWeek], [StartTimeLocal], [LaneNumber])
                    WHERE [IsEnabled] = 1;
            END
            ELSE
            BEGIN
                IF COL_LENGTH(N'[dbo].[SubscriptionSchedules]', N'LastAssignedLaneNumber') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[SubscriptionSchedules]
                    ADD [LastAssignedLaneNumber] INT NULL;
                END
                IF COL_LENGTH(N'[dbo].[SubscriptionSchedules]', N'PreferredLaneType') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[SubscriptionSchedules]
                    ADD [PreferredLaneType] NVARCHAR(20) NOT NULL CONSTRAINT [DF_SubscriptionSchedules_PreferredLaneType] DEFAULT ('Any');
                END
                IF COL_LENGTH(N'[dbo].[SubscriptionSchedules]', N'IsFullPackage') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[SubscriptionSchedules]
                    ADD [IsFullPackage] BIT NOT NULL CONSTRAINT [DF_SubscriptionSchedules_IsFullPackage] DEFAULT (0);
                END

                IF COL_LENGTH(N'[dbo].[SubscriptionSchedules]', N'ExcludedOccurrenceDatesJson') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[SubscriptionSchedules]
                    ADD [ExcludedOccurrenceDatesJson] NVARCHAR(MAX) NULL;
                END
                IF COL_LENGTH(N'[dbo].[SubscriptionSchedules]', N'OccurrenceOverridesJson') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[SubscriptionSchedules]
                    ADD [OccurrenceOverridesJson] NVARCHAR(MAX) NULL;
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SubscriptionSchedules_Athlete_Day_Time_Lane_Enabled' AND object_id = OBJECT_ID(N'[dbo].[SubscriptionSchedules]'))
                BEGIN
                    -- Deduplicate old data before creating a unique index (keep newest enabled row).
                    ;WITH d AS (
                        SELECT
                            [Id],
                            ROW_NUMBER() OVER (
                                PARTITION BY [AthleteId], [DayOfWeek], [StartTimeLocal], [LaneNumber]
                                ORDER BY [CreatedAtUtc] DESC, [Id] DESC
                            ) AS rn
                        FROM [dbo].[SubscriptionSchedules]
                        WHERE [IsEnabled] = 1
                    )
                    UPDATE s
                    SET [IsEnabled] = 0
                    FROM [dbo].[SubscriptionSchedules] s
                    INNER JOIN d ON d.[Id] = s.[Id]
                    WHERE d.rn > 1;

                    CREATE UNIQUE INDEX [UX_SubscriptionSchedules_Athlete_Day_Time_Lane_Enabled]
                        ON [dbo].[SubscriptionSchedules]([AthleteId], [DayOfWeek], [StartTimeLocal], [LaneNumber])
                        WHERE [IsEnabled] = 1;
                END
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(createTableSql, cancellationToken);
    }

    private async Task EnsureServicePackagesTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[ServicePackages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ServicePackages](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(120) NOT NULL,
                    [BillingType] NVARCHAR(20) NOT NULL,
                    [Scope] NVARCHAR(20) NOT NULL,
                    [SchedulingMode] NVARCHAR(20) NOT NULL,
                    [Price] DECIMAL(18, 2) NOT NULL,
                    [SessionDurationMinutes] INT NOT NULL,
                    [PeriodMinutesQuota] INT NULL,
                    [WeeklyDaysCsv] NVARCHAR(30) NULL,
                    [ValidityDays] INT NULL,
                    [UnlimitedGym] BIT NOT NULL CONSTRAINT [DF_ServicePackages_UnlimitedGym] DEFAULT (0),
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_ServicePackages_IsActive] DEFAULT (1),
                    [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ServicePackages_IsDeleted] DEFAULT (0),
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL
                );

                CREATE INDEX [IX_ServicePackages_IsActive] ON [dbo].[ServicePackages]([IsActive]);
                CREATE INDEX [IX_ServicePackages_IsDeleted] ON [dbo].[ServicePackages]([IsDeleted]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH(N'[dbo].[ServicePackages]', N'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[ServicePackages]
                    ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_ServicePackages_IsDeleted] DEFAULT (0);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ServicePackages_IsDeleted' AND object_id = OBJECT_ID(N'[dbo].[ServicePackages]'))
                BEGIN
                    CREATE INDEX [IX_ServicePackages_IsDeleted] ON [dbo].[ServicePackages]([IsDeleted]);
                END
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        const string alterSql = """
            IF OBJECT_ID(N'[dbo].[ServicePackages]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[ServicePackages]', N'WeeklyDaysCsv') IS NULL
            BEGIN
                ALTER TABLE [dbo].[ServicePackages] ADD [WeeklyDaysCsv] NVARCHAR(30) NULL;
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(alterSql, cancellationToken);
    }

    private async Task EnsureServicePackageSeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.ServicePackages.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var seed = new[]
        {
            new ServicePackage
            {
                Name = "1 saat oxatma (birdefəlik)",
                BillingType = PackageBillingType.OneTime,
                Scope = PackageScope.Archery,
                SchedulingMode = PackageSchedulingMode.None,
                Price = 15m,
                SessionDurationMinutes = 90,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new ServicePackage
            {
                Name = "Aylıq 12 saat oxatma",
                BillingType = PackageBillingType.Monthly,
                Scope = PackageScope.Archery,
                SchedulingMode = PackageSchedulingMode.FixedWeekly,
                Price = 120m,
                SessionDurationMinutes = 90,
                PeriodMinutesQuota = 720,
                WeeklyDaysCsv = "1,3,5",
                ValidityDays = 30,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new ServicePackage
            {
                Name = "VIP illik — limitsiz zal + çevik oxatma",
                BillingType = PackageBillingType.Vip,
                Scope = PackageScope.Vip,
                SchedulingMode = PackageSchedulingMode.WalkInFlexible,
                Price = 800m,
                SessionDurationMinutes = 0,
                ValidityDays = 365,
                UnlimitedGym = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        await dbContext.ServicePackages.AddRangeAsync(seed, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task NormalizeVipServicePackagesAsync(CancellationToken cancellationToken)
    {
        var vipPackages = await dbContext.ServicePackages
            .Where(p => !p.IsDeleted && p.Scope == PackageScope.Vip)
            .ToListAsync(cancellationToken);
        if (vipPackages.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var anyChanged = false;
        foreach (var package in vipPackages)
        {
            var packageChanged = false;

            if (package.BillingType != PackageBillingType.Vip)
            {
                package.BillingType = PackageBillingType.Vip;
                packageChanged = true;
            }

            if (package.SchedulingMode != PackageSchedulingMode.WalkInFlexible)
            {
                package.SchedulingMode = PackageSchedulingMode.WalkInFlexible;
                packageChanged = true;
            }

            if (package.SessionDurationMinutes != 0)
            {
                package.SessionDurationMinutes = 0;
                packageChanged = true;
            }

            if (!string.IsNullOrWhiteSpace(package.WeeklyDaysCsv))
            {
                package.WeeklyDaysCsv = null;
                packageChanged = true;
            }

            if (package.PeriodMinutesQuota is not null)
            {
                package.PeriodMinutesQuota = null;
                packageChanged = true;
            }

            if (!package.UnlimitedGym)
            {
                package.UnlimitedGym = true;
                packageChanged = true;
            }

            if (packageChanged)
            {
                package.UpdatedAtUtc = now;
                anyChanged = true;
            }
        }

        if (anyChanged)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task PurgeLegacyWalkInMonthlyPackageAsync(CancellationToken cancellationToken)
    {
        const string packageName = "Aylıq çevik oxatma — limitsiz gəliş";
        var legacy = await dbContext.ServicePackages
            .Where(p => !p.IsDeleted && p.Name == packageName)
            .ToListAsync(cancellationToken);
        if (legacy.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var package in legacy)
        {
            package.IsDeleted = true;
            package.IsActive = false;
            package.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureEquipmentItemsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[EquipmentItems]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[EquipmentItems](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(120) NOT NULL,
                    [Category] NVARCHAR(80) NULL,
                    [UsageMode] INT NOT NULL CONSTRAINT [DF_EquipmentItems_UsageMode] DEFAULT (2),
                    [Quantity] INT NOT NULL CONSTRAINT [DF_EquipmentItems_Quantity] DEFAULT (0),
                    [Price] DECIMAL(18, 2) NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_EquipmentItems_IsActive] DEFAULT (1),
                    [IsDeleted] BIT NOT NULL CONSTRAINT [DF_EquipmentItems_IsDeleted] DEFAULT (0),
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL
                );

                CREATE INDEX [IX_EquipmentItems_IsActive] ON [dbo].[EquipmentItems]([IsActive]);
                CREATE INDEX [IX_EquipmentItems_IsDeleted] ON [dbo].[EquipmentItems]([IsDeleted]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH(N'[dbo].[EquipmentItems]', N'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[EquipmentItems]
                    ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_EquipmentItems_IsDeleted] DEFAULT (0);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EquipmentItems_IsDeleted' AND object_id = OBJECT_ID(N'[dbo].[EquipmentItems]'))
                BEGIN
                    CREATE INDEX [IX_EquipmentItems_IsDeleted] ON [dbo].[EquipmentItems]([IsDeleted]);
                END

                IF COL_LENGTH(N'[dbo].[EquipmentItems]', N'DamagedQuantity') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[EquipmentItems]
                    ADD [DamagedQuantity] INT NOT NULL CONSTRAINT [DF_EquipmentItems_DamagedQuantity] DEFAULT (0);
                END
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureEquipmentItemSeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.EquipmentItems.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var seed = new[]
        {
            new EquipmentItem
            {
                Name = "Yay (rekursiv)",
                Category = "Yay",
                RentalQuantity = 12,
                SaleQuantity = 12,
                Quantity = 24,
                Price = 5m,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new EquipmentItem
            {
                Name = "Ox (standart)",
                Category = "Ox",
                RentalQuantity = 20,
                SaleQuantity = 20,
                Quantity = 40,
                Price = 3m,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new EquipmentItem
            {
                Name = "Qoruyucu əlcək",
                Category = "Qoruyucu",
                RentalQuantity = 30,
                SaleQuantity = 0,
                Quantity = 30,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        await dbContext.EquipmentItems.AddRangeAsync(seed, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureSessionEquipmentIssuesTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[SessionEquipmentIssues]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[SessionEquipmentIssues](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [SessionId] UNIQUEIDENTIFIER NOT NULL,
                    [EquipmentItemId] UNIQUEIDENTIFIER NOT NULL,
                    [IssueType] NVARCHAR(20) NOT NULL,
                    [Quantity] INT NOT NULL CONSTRAINT [DF_SessionEquipmentIssues_Quantity] DEFAULT (1),
                    [UnitPrice] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_SessionEquipmentIssues_UnitPrice] DEFAULT (0),
                    [IssuedByStaffId] UNIQUEIDENTIFIER NULL,
                    [ReturnedByStaffId] UNIQUEIDENTIFIER NULL,
                    [ReturnedAtUtc] DATETIME2 NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    CONSTRAINT [FK_SessionEquipmentIssues_Sessions] FOREIGN KEY ([SessionId])
                        REFERENCES [dbo].[TrainingSessions]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_SessionEquipmentIssues_EquipmentItems] FOREIGN KEY ([EquipmentItemId])
                        REFERENCES [dbo].[EquipmentItems]([Id])
                );

                CREATE INDEX [IX_SessionEquipmentIssues_SessionId] ON [dbo].[SessionEquipmentIssues]([SessionId]);
                CREATE INDEX [IX_SessionEquipmentIssues_EquipmentItemId] ON [dbo].[SessionEquipmentIssues]([EquipmentItemId]);
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureEquipmentSplitStockColumnsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[EquipmentItems]', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH(N'[dbo].[EquipmentItems]', N'RentalQuantity') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[EquipmentItems]
                    ADD [RentalQuantity] INT NOT NULL CONSTRAINT [DF_EquipmentItems_RentalQuantity] DEFAULT (0);
                END

                IF COL_LENGTH(N'[dbo].[EquipmentItems]', N'SaleQuantity') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[EquipmentItems]
                    ADD [SaleQuantity] INT NOT NULL CONSTRAINT [DF_EquipmentItems_SaleQuantity] DEFAULT (0);
                END
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        var items = await dbContext.EquipmentItems
            .Where(x => x.RentalQuantity == 0 && x.SaleQuantity == 0 && x.Quantity > 0)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
        {
            switch (item.UsageMode)
            {
                case EquipmentUsageMode.Sale:
                    item.SaleQuantity = item.Quantity;
                    break;
                case EquipmentUsageMode.Rental:
                    item.RentalQuantity = item.Quantity;
                    break;
                default:
                    item.RentalQuantity = item.Quantity;
                    break;
            }

            item.Quantity = item.RentalQuantity + item.SaleQuantity;
        }

        if (items.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task EnsureEquipmentPriceAsUnitAsync(CancellationToken cancellationToken)
    {
        var items = await dbContext.EquipmentItems
            .Where(x => x.Price != null
                        && x.Price > 0
                        && x.SaleQuantity > 1
                        && x.Price > x.SaleQuantity * 10)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var item in items)
        {
            item.Price = Math.Round(item.Price!.Value / item.SaleQuantity, 2, MidpointRounding.AwayFromZero);
            item.UpdatedAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureEquipmentItemUsageModeColumnAsync(CancellationToken cancellationToken)
    {
        const string addUsageModeSql = """
            IF OBJECT_ID(N'[dbo].[EquipmentItems]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[EquipmentItems]', N'UsageMode') IS NULL
            BEGIN
                ALTER TABLE [dbo].[EquipmentItems]
                ADD [UsageMode] INT NOT NULL CONSTRAINT [DF_EquipmentItems_UsageMode] DEFAULT (2);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addUsageModeSql, cancellationToken);
    }

    private async Task EnsureSessionEquipmentIssueJournalColumnsAsync(CancellationToken cancellationToken)
    {
        const string addUnitPriceSql = """
            IF OBJECT_ID(N'[dbo].[SessionEquipmentIssues]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[SessionEquipmentIssues]', N'UnitPrice') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SessionEquipmentIssues]
                ADD [UnitPrice] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_SessionEquipmentIssues_UnitPrice] DEFAULT (0);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addUnitPriceSql, cancellationToken);

        const string addQuantitySql = """
            IF OBJECT_ID(N'[dbo].[SessionEquipmentIssues]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[SessionEquipmentIssues]', N'Quantity') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SessionEquipmentIssues]
                ADD [Quantity] INT NOT NULL CONSTRAINT [DF_SessionEquipmentIssues_Quantity] DEFAULT (1);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addQuantitySql, cancellationToken);

        const string addIssuedBySql = """
            IF OBJECT_ID(N'[dbo].[SessionEquipmentIssues]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[SessionEquipmentIssues]', N'IssuedByStaffId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SessionEquipmentIssues] ADD [IssuedByStaffId] UNIQUEIDENTIFIER NULL;
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addIssuedBySql, cancellationToken);

        const string addReturnedBySql = """
            IF OBJECT_ID(N'[dbo].[SessionEquipmentIssues]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[SessionEquipmentIssues]', N'ReturnedByStaffId') IS NULL
            BEGIN
                ALTER TABLE [dbo].[SessionEquipmentIssues] ADD [ReturnedByStaffId] UNIQUEIDENTIFIER NULL;
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addReturnedBySql, cancellationToken);

        const string backfillUnitPriceSql = """
            IF OBJECT_ID(N'[dbo].[SessionEquipmentIssues]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[SessionEquipmentIssues]', N'UnitPrice') IS NOT NULL
            BEGIN
                UPDATE sei
                SET sei.[UnitPrice] = ISNULL(ei.[Price], 0)
                FROM [dbo].[SessionEquipmentIssues] sei
                INNER JOIN [dbo].[EquipmentItems] ei ON ei.[Id] = sei.[EquipmentItemId]
                WHERE sei.[UnitPrice] = 0;
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(backfillUnitPriceSql, cancellationToken);
    }

    private async Task EnsureEquipmentSaleReceiptsTablesAsync(CancellationToken cancellationToken)
    {
        const string receiptsSql = """
            IF OBJECT_ID(N'[dbo].[EquipmentSaleReceipts]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[EquipmentSaleReceipts](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [AthleteId] UNIQUEIDENTIFIER NOT NULL,
                    [Type] INT NOT NULL CONSTRAINT [DF_EquipmentSaleReceipts_Type] DEFAULT (0),
                    [OriginalReceiptId] UNIQUEIDENTIFIER NULL,
                    [TotalAmount] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_EquipmentSaleReceipts_TotalAmount] DEFAULT (0),
                    [AmountPaidCash] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_EquipmentSaleReceipts_AmountPaidCash] DEFAULT (0),
                    [AmountPaidCard] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_EquipmentSaleReceipts_AmountPaidCard] DEFAULT (0),
                    [CreatedByStaffId] UNIQUEIDENTIFIER NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL
                );
                CREATE INDEX [IX_EquipmentSaleReceipts_AthleteId] ON [dbo].[EquipmentSaleReceipts]([AthleteId]);
                CREATE INDEX [IX_EquipmentSaleReceipts_CreatedAtUtc] ON [dbo].[EquipmentSaleReceipts]([CreatedAtUtc]);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(receiptsSql, cancellationToken);

        const string linesSql = """
            IF OBJECT_ID(N'[dbo].[EquipmentSaleReceiptLines]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[EquipmentSaleReceiptLines](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [ReceiptId] UNIQUEIDENTIFIER NOT NULL,
                    [EquipmentItemId] UNIQUEIDENTIFIER NOT NULL,
                    [Quantity] INT NOT NULL CONSTRAINT [DF_EquipmentSaleReceiptLines_Quantity] DEFAULT (1),
                    [UnitPrice] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_EquipmentSaleReceiptLines_UnitPrice] DEFAULT (0),
                    CONSTRAINT [FK_EquipmentSaleReceiptLines_Receipts] FOREIGN KEY ([ReceiptId])
                        REFERENCES [dbo].[EquipmentSaleReceipts]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_EquipmentSaleReceiptLines_EquipmentItems] FOREIGN KEY ([EquipmentItemId])
                        REFERENCES [dbo].[EquipmentItems]([Id])
                );
                CREATE INDEX [IX_EquipmentSaleReceiptLines_ReceiptId] ON [dbo].[EquipmentSaleReceiptLines]([ReceiptId]);
                CREATE INDEX [IX_EquipmentSaleReceiptLines_EquipmentItemId] ON [dbo].[EquipmentSaleReceiptLines]([EquipmentItemId]);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(linesSql, cancellationToken);
    }

    private async Task EnsureEquipmentSaleReceiptLineDiscountColumnAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[EquipmentSaleReceiptLines]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[EquipmentSaleReceiptLines]', N'DiscountAmount') IS NULL
            BEGIN
                ALTER TABLE [dbo].[EquipmentSaleReceiptLines]
                ADD [DiscountAmount] DECIMAL(18, 2) NOT NULL
                    CONSTRAINT [DF_EquipmentSaleReceiptLines_DiscountAmount] DEFAULT (0);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureStaffPositionsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[StaffPositions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[StaffPositions](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(80) NOT NULL,
                    [Description] NVARCHAR(240) NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_StaffPositions_IsActive] DEFAULT (1),
                    [IsDeleted] BIT NOT NULL CONSTRAINT [DF_StaffPositions_IsDeleted] DEFAULT (0),
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL
                );

                CREATE INDEX [IX_StaffPositions_IsActive] ON [dbo].[StaffPositions]([IsActive]);
                CREATE INDEX [IX_StaffPositions_IsDeleted] ON [dbo].[StaffPositions]([IsDeleted]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH(N'[dbo].[StaffPositions]', N'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[StaffPositions]
                    ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_StaffPositions_IsDeleted] DEFAULT (0);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StaffPositions_IsDeleted' AND object_id = OBJECT_ID(N'[dbo].[StaffPositions]'))
                BEGIN
                    CREATE INDEX [IX_StaffPositions_IsDeleted] ON [dbo].[StaffPositions]([IsDeleted]);
                END

                IF COL_LENGTH(N'[dbo].[StaffPositions]', N'DefaultAccessProfileId') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[StaffPositions]
                    ADD [DefaultAccessProfileId] UNIQUEIDENTIFIER NULL;
                END
            END

            IF OBJECT_ID(N'[dbo].[StaffRoles]', N'U') IS NOT NULL
               AND OBJECT_ID(N'[dbo].[StaffPositions]', N'U') IS NOT NULL
               AND NOT EXISTS (SELECT 1 FROM [dbo].[StaffPositions])
            BEGIN
                INSERT INTO [dbo].[StaffPositions] ([Id], [Name], [Description], [IsActive], [CreatedAtUtc], [UpdatedAtUtc])
                SELECT [Id], [Name], [Description], [IsActive], [CreatedAtUtc], [UpdatedAtUtc]
                FROM [dbo].[StaffRoles];
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureStaffPositionSeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.StaffPositions.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var seed = new[]
        {
            new StaffPosition
            {
                Name = "Resepsiya",
                Description = "Resepsiya masası işçisi",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new StaffPosition
            {
                Name = "Nəzarətçi",
                Description = "Planşet ekranı — zolaq nəzarəti",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new StaffPosition
            {
                Name = "Məşqçi",
                Description = "Məhdud giriş",
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        await dbContext.StaffPositions.AddRangeAsync(seed, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAccessProfilesTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[AccessProfiles]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AccessProfiles](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [Name] NVARCHAR(80) NOT NULL,
                    [Description] NVARCHAR(240) NULL,
                    [CanRegisterCustomers] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_CanRegisterCustomers] DEFAULT (0),
                    [CanManageSubscriptions] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_CanManageSubscriptions] DEFAULT (0),
                    [CanManageSessions] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_CanManageSessions] DEFAULT (0),
                    [CanManageEquipment] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_CanManageEquipment] DEFAULT (0),
                    [CanViewHistory] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_CanViewHistory] DEFAULT (0),
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_IsActive] DEFAULT (1),
                    [IsDeleted] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_IsDeleted] DEFAULT (0),
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL
                );

                CREATE INDEX [IX_AccessProfiles_IsActive] ON [dbo].[AccessProfiles]([IsActive]);
                CREATE INDEX [IX_AccessProfiles_IsDeleted] ON [dbo].[AccessProfiles]([IsDeleted]);
            END
            ELSE
            BEGIN
                IF COL_LENGTH(N'[dbo].[AccessProfiles]', N'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[AccessProfiles]
                    ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_AccessProfiles_IsDeleted] DEFAULT (0);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AccessProfiles_IsDeleted' AND object_id = OBJECT_ID(N'[dbo].[AccessProfiles]'))
                BEGIN
                    CREATE INDEX [IX_AccessProfiles_IsDeleted] ON [dbo].[AccessProfiles]([IsDeleted]);
                END
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureAccessProfilePermissionColumnsAsync(CancellationToken cancellationToken)
    {
        await EnsureAccessProfileBitColumnAsync(
            "CanViewCustomerDetails",
            "DF_AccessProfiles_CanViewCustomerDetails",
            "CanRegisterCustomers",
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanEditCustomerDetails",
            "DF_AccessProfiles_CanEditCustomerDetails",
            "CanRegisterCustomers",
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanRecordPayments",
            "DF_AccessProfiles_CanRecordPayments",
            "CanManageSubscriptions",
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanApplyDiscount",
            "DF_AccessProfiles_CanApplyDiscount",
            "CanRecordCreditPayments",
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanGrantComplimentarySession",
            "DF_AccessProfiles_CanGrantComplimentarySession",
            backfillFromColumn: null,
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanSellEquipment",
            "DF_AccessProfiles_CanSellEquipment",
            backfillFromColumn: null,
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanReturnEquipment",
            "DF_AccessProfiles_CanReturnEquipment",
            "CanManageEquipment",
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanAccessPlanset",
            "DF_AccessProfiles_CanAccessPlanset",
            backfillFromColumn: null,
            cancellationToken);
        await EnsureAccessProfileBitColumnAsync(
            "CanIssueEquipmentRental",
            "DF_AccessProfiles_CanIssueEquipmentRental",
            backfillFromColumn: null,
            cancellationToken);
    }

    private async Task EnsureAccessProfileBitColumnAsync(
        string column,
        string defaultConstraint,
        string? backfillFromColumn,
        CancellationToken cancellationToken)
    {
        var addSql = $"""
            IF COL_LENGTH(N'[dbo].[AccessProfiles]', N'{column}') IS NULL
            BEGIN
                ALTER TABLE [dbo].[AccessProfiles]
                ADD [{column}] BIT NOT NULL CONSTRAINT [{defaultConstraint}] DEFAULT (0);
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(addSql, cancellationToken);

        if (string.IsNullOrWhiteSpace(backfillFromColumn))
        {
            return;
        }

        var backfillSql = $"""
            UPDATE [dbo].[AccessProfiles]
            SET [{column}] = [{backfillFromColumn}]
            WHERE [{backfillFromColumn}] = 1 AND [{column}] = 0;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(backfillSql, cancellationToken);
    }

    private async Task EnsureAccessProfileSeedAsync(CancellationToken cancellationToken)
    {
        if (await dbContext.AccessProfiles.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var seed = new[]
        {
            new AccessProfile
            {
                Name = "Tam resepsiya",
                Description = "Müştəri, paket, zolaq, avadanlıq, ödəniş və tarixçə",
                CanRegisterCustomers = true,
                CanViewCustomerDetails = true,
                CanEditCustomerDetails = true,
                CanManageSubscriptions = true,
                CanRecordPayments = true,
                CanApplyDiscount = true,
                CanGrantComplimentarySession = true,
                CanManageSessions = true,
                CanManageEquipment = true,
                CanSellEquipment = true,
                CanReturnEquipment = true,
                CanViewHistory = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new AccessProfile
            {
                Name = "Məhdud resepsiya",
                Description = "Müştəri və zolaq — paket satışı yoxdur",
                CanRegisterCustomers = true,
                CanViewCustomerDetails = true,
                CanEditCustomerDetails = false,
                CanManageSubscriptions = false,
                CanRecordPayments = false,
                CanApplyDiscount = false,
                CanGrantComplimentarySession = false,
                CanManageSessions = true,
                CanManageEquipment = true,
                CanSellEquipment = false,
                CanReturnEquipment = true,
                CanViewHistory = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new AccessProfile
            {
                Name = "Planşet nəzarətçi",
                Description = "Planşetə giriş və avadanlıq icarəsi",
                CanAccessPlanset = true,
                CanIssueEquipmentRental = true,
                CanViewHistory = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        await dbContext.AccessProfiles.AddRangeAsync(seed, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureStaffPositionDefaultProfileLinksAsync(CancellationToken cancellationToken)
    {
        var profiles = await dbContext.AccessProfiles.AsNoTracking().ToListAsync(cancellationToken);
        var positions = await dbContext.StaffPositions.ToListAsync(cancellationToken);

        var fullReception = profiles.FirstOrDefault(x => x.Name == "Tam resepsiya");
        var limitedReception = profiles.FirstOrDefault(x => x.Name == "Məhdud resepsiya");
        var supervisor = profiles.FirstOrDefault(x => x.Name == "Planşet nəzarətçi");

        foreach (var position in positions.Where(x => x.DefaultAccessProfileId is null))
        {
            position.DefaultAccessProfileId = position.Name switch
            {
                "Resepsiya" => fullReception?.Id ?? limitedReception?.Id,
                "Nəzarətçi" => supervisor?.Id,
                "Məşqçi" => supervisor?.Id,
                _ => null
            };
            position.UpdatedAtUtc = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureStaffMembersTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[StaffMembers]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[StaffMembers](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [FirstName] NVARCHAR(80) NOT NULL,
                    [LastName] NVARCHAR(80) NOT NULL,
                    [StaffPositionId] UNIQUEIDENTIFIER NOT NULL,
                    [AccessProfileId] UNIQUEIDENTIFIER NOT NULL,
                    [PhoneNumber] NVARCHAR(40) NULL,
                    [PinHash] NVARCHAR(128) NOT NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_StaffMembers_IsActive] DEFAULT (1),
                    [IsDeleted] BIT NOT NULL CONSTRAINT [DF_StaffMembers_IsDeleted] DEFAULT (0),
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [UpdatedAtUtc] DATETIME2 NOT NULL,
                    CONSTRAINT [FK_StaffMembers_StaffPositions] FOREIGN KEY ([StaffPositionId]) REFERENCES [dbo].[StaffPositions]([Id]),
                    CONSTRAINT [FK_StaffMembers_AccessProfiles] FOREIGN KEY ([AccessProfileId]) REFERENCES [dbo].[AccessProfiles]([Id])
                );

                CREATE INDEX [IX_StaffMembers_IsActive] ON [dbo].[StaffMembers]([IsActive]);
                CREATE INDEX [IX_StaffMembers_IsDeleted] ON [dbo].[StaffMembers]([IsDeleted]);
                CREATE INDEX [IX_StaffMembers_StaffPositionId] ON [dbo].[StaffMembers]([StaffPositionId]);
                CREATE INDEX [IX_StaffMembers_AccessProfileId] ON [dbo].[StaffMembers]([AccessProfileId]);
                CREATE UNIQUE INDEX [UX_StaffMembers_PhoneNumber] ON [dbo].[StaffMembers]([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL;
            END
            ELSE
            BEGIN
                IF COL_LENGTH(N'dbo.StaffMembers', N'IsDeleted') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[StaffMembers] ADD [IsDeleted] BIT NOT NULL CONSTRAINT [DF_StaffMembers_IsDeleted] DEFAULT (0);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StaffMembers_IsDeleted' AND object_id = OBJECT_ID(N'dbo.StaffMembers'))
                BEGIN
                    CREATE INDEX [IX_StaffMembers_IsDeleted] ON [dbo].[StaffMembers]([IsDeleted]);
                END

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_StaffMembers_PhoneNumber' AND object_id = OBJECT_ID(N'dbo.StaffMembers'))
                BEGIN
                    -- Deduplicate phone numbers before creating a unique index.
                    -- Keep one row per phone number; mutate the others to preserve a "never reuse" rule.
                    ;WITH d AS (
                        SELECT
                            [Id],
                            [PhoneNumber],
                            ROW_NUMBER() OVER (
                                PARTITION BY [PhoneNumber]
                                ORDER BY [UpdatedAtUtc] DESC, [CreatedAtUtc] DESC, [Id] DESC
                            ) AS rn
                        FROM [dbo].[StaffMembers]
                        WHERE [PhoneNumber] IS NOT NULL AND LTRIM(RTRIM([PhoneNumber])) <> ''
                    )
                    UPDATE s
                    SET [PhoneNumber] = CONCAT(LTRIM(RTRIM(s.[PhoneNumber])), N'-DUP-', LEFT(CONVERT(NVARCHAR(36), s.[Id]), 8))
                    FROM [dbo].[StaffMembers] s
                    INNER JOIN d ON d.[Id] = s.[Id]
                    WHERE d.rn > 1;

                    CREATE UNIQUE INDEX [UX_StaffMembers_PhoneNumber] ON [dbo].[StaffMembers]([PhoneNumber]) WHERE [PhoneNumber] IS NOT NULL;
                END
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureStaffMemberPinPlainColumnAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[StaffMembers]', N'U') IS NOT NULL
               AND COL_LENGTH(N'dbo.StaffMembers', N'PinPlain') IS NULL
            BEGIN
                ALTER TABLE [dbo].[StaffMembers] ADD [PinPlain] NVARCHAR(6) NULL;
            END
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }

    private async Task EnsureCustomerPackageRecordsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[CustomerPackageRecords]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CustomerPackageRecords](
                    [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                    [AthleteId] UNIQUEIDENTIFIER NOT NULL,
                    [ServicePackageId] UNIQUEIDENTIFIER NULL,
                    [PackageName] NVARCHAR(200) NOT NULL,
                    [BillingTypeLabel] NVARCHAR(40) NOT NULL,
                    [PriceDue] DECIMAL(18, 2) NOT NULL,
                    [AmountPaidCash] DECIMAL(18, 2) NOT NULL,
                    [AmountPaidCard] DECIMAL(18, 2) NOT NULL,
                    [AmountPaid] DECIMAL(18, 2) NOT NULL,
                    [IsComplimentary] BIT NOT NULL,
                    [SessionId] UNIQUEIDENTIFIER NULL,
                    [SubscriptionScheduleId] UNIQUEIDENTIFIER NULL,
                    [CreatedByStaffId] UNIQUEIDENTIFIER NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL,
                    [IsActive] BIT NOT NULL
                );
                CREATE INDEX [IX_CustomerPackageRecords_AthleteId] ON [dbo].[CustomerPackageRecords]([AthleteId]);
                CREATE INDEX [IX_CustomerPackageRecords_CreatedAtUtc] ON [dbo].[CustomerPackageRecords]([CreatedAtUtc]);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        const string paymentMethodSql = """
            IF OBJECT_ID(N'[dbo].[CustomerPackageRecords]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[CustomerPackageRecords]', N'PaymentMethod') IS NULL
            BEGIN
                ALTER TABLE [dbo].[CustomerPackageRecords] ADD [PaymentMethod] INT NULL;
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(paymentMethodSql, cancellationToken);

        const string addAmountPaidCashSql = """
            IF OBJECT_ID(N'[dbo].[CustomerPackageRecords]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[CustomerPackageRecords]', N'AmountPaidCash') IS NULL
            BEGIN
                ALTER TABLE [dbo].[CustomerPackageRecords] ADD [AmountPaidCash] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_CustomerPackageRecords_AmountPaidCash] DEFAULT (0);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addAmountPaidCashSql, cancellationToken);

        const string addAmountPaidCardSql = """
            IF OBJECT_ID(N'[dbo].[CustomerPackageRecords]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[CustomerPackageRecords]', N'AmountPaidCard') IS NULL
            BEGIN
                ALTER TABLE [dbo].[CustomerPackageRecords] ADD [AmountPaidCard] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_CustomerPackageRecords_AmountPaidCard] DEFAULT (0);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addAmountPaidCardSql, cancellationToken);

        const string backfillSplitPaymentSql = """
            IF OBJECT_ID(N'[dbo].[CustomerPackageRecords]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[CustomerPackageRecords]', N'AmountPaidCash') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[CustomerPackageRecords]', N'AmountPaidCard') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[CustomerPackageRecords]', N'PaymentMethod') IS NOT NULL
            BEGIN
                UPDATE [dbo].[CustomerPackageRecords]
                SET [AmountPaidCash] = [AmountPaid]
                WHERE [PaymentMethod] = 0 AND [AmountPaid] > 0 AND [AmountPaidCash] = 0 AND [AmountPaidCard] = 0;

                UPDATE [dbo].[CustomerPackageRecords]
                SET [AmountPaidCard] = [AmountPaid]
                WHERE [PaymentMethod] = 1 AND [AmountPaid] > 0 AND [AmountPaidCash] = 0 AND [AmountPaidCard] = 0;

                UPDATE [dbo].[CustomerPackageRecords]
                SET [AmountPaidCash] = [AmountPaid]
                WHERE [PaymentMethod] IS NULL AND [AmountPaid] > 0 AND [AmountPaidCash] = 0 AND [AmountPaidCard] = 0;
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(backfillSplitPaymentSql, cancellationToken);
    }

    private async Task EnsureBillingDiscountColumnsAsync(CancellationToken cancellationToken)
    {
        const string addPackageDiscountSql = """
            IF OBJECT_ID(N'[dbo].[CustomerPackageRecords]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[CustomerPackageRecords]', N'DiscountAmount') IS NULL
            BEGIN
                ALTER TABLE [dbo].[CustomerPackageRecords]
                ADD [DiscountAmount] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_CustomerPackageRecords_DiscountAmount] DEFAULT (0);

                DELETE FROM [dbo].[EquipmentSaleReceiptLines];
                DELETE FROM [dbo].[EquipmentSaleReceipts];
                DELETE FROM [dbo].[CustomerPackageRecords];
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addPackageDiscountSql, cancellationToken);

        const string addReceiptDiscountSql = """
            IF OBJECT_ID(N'[dbo].[EquipmentSaleReceipts]', N'U') IS NOT NULL
               AND COL_LENGTH(N'[dbo].[EquipmentSaleReceipts]', N'DiscountAmount') IS NULL
            BEGIN
                ALTER TABLE [dbo].[EquipmentSaleReceipts]
                ADD [DiscountAmount] DECIMAL(18, 2) NOT NULL CONSTRAINT [DF_EquipmentSaleReceipts_DiscountAmount] DEFAULT (0);
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(addReceiptDiscountSql, cancellationToken);
    }

    private async Task EnsureAthleteCreatedAtBackfillAsync(CancellationToken cancellationToken)
    {
        // CreatedAtUtc sütunu əlavə olunanda bütün köhnə sətirlərə eyni tarix yazılırdı.
        // Mümkün olanda ən köhnə seans/abunə/paket tarixinə geri düzəldirik.
        const string sql = """
            IF COL_LENGTH(N'[dbo].[Athletes]', N'CreatedAtUtc') IS NOT NULL
            BEGIN
                UPDATE a
                SET a.[CreatedAtUtc] = src.[BestUtc]
                FROM [dbo].[Athletes] a
                INNER JOIN (
                    SELECT
                        a2.[Id],
                        (
                            SELECT MIN(v.[Dt])
                            FROM (
                                SELECT a2.[CreatedAtUtc] AS [Dt]
                                UNION ALL
                                SELECT MIN(ts.[StartTimeUtc])
                                FROM [dbo].[TrainingSessions] ts
                                WHERE ts.[AthleteId] = a2.[Id]
                                UNION ALL
                                SELECT MIN(ss.[CreatedAtUtc])
                                FROM [dbo].[SubscriptionSchedules] ss
                                WHERE ss.[AthleteId] = a2.[Id]
                                UNION ALL
                                SELECT MIN(cpr.[CreatedAtUtc])
                                FROM [dbo].[CustomerPackageRecords] cpr
                                WHERE cpr.[AthleteId] = a2.[Id]
                            ) v
                            WHERE v.[Dt] IS NOT NULL
                        ) AS [BestUtc]
                    FROM [dbo].[Athletes] a2
                ) src ON src.[Id] = a.[Id]
                WHERE src.[BestUtc] IS NOT NULL
                  AND src.[BestUtc] < a.[CreatedAtUtc];
            END
            """;
        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
