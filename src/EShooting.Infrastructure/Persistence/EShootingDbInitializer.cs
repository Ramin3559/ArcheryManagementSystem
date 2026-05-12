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

        if (await dbContext.Lanes.AnyAsync(cancellationToken))
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
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
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
}
