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
        await PurgeLegacyWalkInMonthlyPackageAsync(cancellationToken);
        await EnsureEquipmentItemsTableAsync(cancellationToken);
        await EnsureEquipmentItemSeedAsync(cancellationToken);
        await EnsureSessionEquipmentIssuesTableAsync(cancellationToken);
        await EnsureStaffPositionsTableAsync(cancellationToken);
        await EnsureStaffPositionSeedAsync(cancellationToken);
        await EnsureAccessProfilesTableAsync(cancellationToken);
        await EnsureAccessProfileSeedAsync(cancellationToken);
        await EnsureStaffMembersTableAsync(cancellationToken);

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
                [IdCardNumber] = NULLIF(LTRIM(RTRIM([IdCardNumber])), '')
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
                BillingType = PackageBillingType.Yearly,
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
                Quantity = 12,
                Price = 5m,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new EquipmentItem
            {
                Name = "Ox (standart)",
                Category = "Ox",
                Quantity = 20,
                Price = 3m,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new EquipmentItem
            {
                Name = "Qoruyucu əlcək",
                Category = "Qoruyucu",
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
                Name = "Məşqçi",
                Description = "Planset ekranı (yalnız baxış)",
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
                Description = "Müştəri, paket, zolaq, avadanlıq və tarixçə",
                CanRegisterCustomers = true,
                CanManageSubscriptions = true,
                CanManageSessions = true,
                CanManageEquipment = true,
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
                CanManageSubscriptions = false,
                CanManageSessions = true,
                CanManageEquipment = true,
                CanViewHistory = true,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        };

        await dbContext.AccessProfiles.AddRangeAsync(seed, cancellationToken);
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
}
