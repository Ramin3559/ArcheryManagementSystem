-- EShootingDb secilsin, sonra F5
SET NOCOUNT ON;
GO

IF COL_LENGTH('[dbo].[Athletes]', 'ClubCardNumber') IS NULL
    ALTER TABLE [dbo].[Athletes] ADD [ClubCardNumber] NVARCHAR(40);
GO

IF OBJECT_ID('[dbo].[ClubCardAssignments]', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[ClubCardAssignments](
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [CardNumber] NVARCHAR(40) NOT NULL,
        [AthleteId] UNIQUEIDENTIFIER NOT NULL,
        [IssuedAtUtc] DATETIME2 NOT NULL,
        [ReturnedAtUtc] DATETIME2 NULL,
        [IssuedByStaffId] UNIQUEIDENTIFIER NULL,
        [ReturnedByStaffId] UNIQUEIDENTIFIER NULL,
        CONSTRAINT [FK_ClubCardAssignments_Athletes_AthleteId]
            FOREIGN KEY([AthleteId]) REFERENCES [dbo].[Athletes]([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_ClubCardAssignments_AthleteId] ON [dbo].[ClubCardAssignments]([AthleteId]);
    CREATE INDEX [IX_ClubCardAssignments_CardNumber] ON [dbo].[ClubCardAssignments]([CardNumber]);
END;
GO

IF OBJECT_ID('[dbo].[TrainingSessions]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[TrainingSessions]', 'ActivatedAtUtc') IS NULL
    ALTER TABLE [dbo].[TrainingSessions] ADD [ActivatedAtUtc] DATETIME2;
GO

IF OBJECT_ID('[dbo].[TrainingSessions]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[TrainingSessions]', 'ActivatedAtUtc') IS NOT NULL
BEGIN
    UPDATE [dbo].[TrainingSessions]
    SET [ActivatedAtUtc] = [StartTimeUtc]
    WHERE [ActivatedAtUtc] IS NULL
      AND ([Status] = N'Active' OR TRY_CONVERT(int, [Status]) = 2);
END;
GO

IF OBJECT_ID('[dbo].[EquipmentSaleReceiptLines]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentSaleReceiptLines]', 'DiscountAmount') IS NULL
    ALTER TABLE [dbo].[EquipmentSaleReceiptLines] ADD [DiscountAmount] DECIMAL(18,2) NOT NULL DEFAULT (0);
GO

IF OBJECT_ID('[dbo].[EquipmentSaleReceipts]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentSaleReceipts]', 'ReceiptIssued') IS NULL
    ALTER TABLE [dbo].[EquipmentSaleReceipts] ADD [ReceiptIssued] BIT NOT NULL DEFAULT (0);
GO

IF OBJECT_ID('[dbo].[EquipmentItems]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentItems]', 'RentalQuantity') IS NULL
    ALTER TABLE [dbo].[EquipmentItems] ADD [RentalQuantity] INT NOT NULL DEFAULT (0);
GO

IF OBJECT_ID('[dbo].[EquipmentItems]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentItems]', 'SaleQuantity') IS NULL
    ALTER TABLE [dbo].[EquipmentItems] ADD [SaleQuantity] INT NOT NULL DEFAULT (0);
GO

IF OBJECT_ID('[dbo].[EquipmentItems]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentItems]', 'WarehouseQuantity') IS NULL
    ALTER TABLE [dbo].[EquipmentItems] ADD [WarehouseQuantity] INT NOT NULL DEFAULT (0);
GO

IF OBJECT_ID('[dbo].[EquipmentItems]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentItems]', 'PurchasePrice') IS NULL
    ALTER TABLE [dbo].[EquipmentItems] ADD [PurchasePrice] DECIMAL(18,2);
GO

IF OBJECT_ID('[dbo].[EquipmentItems]', 'U') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentItems]', 'WarehouseQuantity') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentItems]', 'RentalQuantity') IS NOT NULL
   AND COL_LENGTH('[dbo].[EquipmentItems]', 'SaleQuantity') IS NOT NULL
BEGIN
    UPDATE [dbo].[EquipmentItems]
    SET [Quantity] = ISNULL([WarehouseQuantity], 0) + ISNULL([RentalQuantity], 0) + ISNULL([SaleQuantity], 0);
END;
GO

UPDATE [dbo].[Athletes]
SET
    [PhoneNumber] = LTRIM(RTRIM([PhoneNumber])),
    [Email] = NULLIF(LOWER(LTRIM(RTRIM([Email]))), N''),
    [IdCardNumber] = NULLIF(LTRIM(RTRIM([IdCardNumber])), N''),
    [ClubCardNumber] = NULLIF(LTRIM(RTRIM([ClubCardNumber])), N'');
GO

IF COL_LENGTH('[dbo].[Athletes]', 'CreatedAtUtc') IS NOT NULL
BEGIN
    ;WITH dupCards AS (
        SELECT [Id],
               ROW_NUMBER() OVER (
                   PARTITION BY LOWER(LTRIM(RTRIM([ClubCardNumber])))
                   ORDER BY ISNULL([CreatedAtUtc], CAST('19000101' AS datetime2)), [Id]
               ) AS rn
        FROM [dbo].[Athletes]
        WHERE [ClubCardNumber] IS NOT NULL
          AND LTRIM(RTRIM([ClubCardNumber])) <> N''
    )
    UPDATE a
    SET a.[ClubCardNumber] = NULL
    FROM [dbo].[Athletes] a
    INNER JOIN dupCards d ON d.[Id] = a.[Id]
    WHERE d.rn > 1;
END;
GO

IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = N'UX_Athletes_ClubCardNumber'
          AND object_id = OBJECT_ID(N'[dbo].[Athletes]')
    )
   AND NOT EXISTS (
        SELECT 1
        FROM [dbo].[Athletes]
        WHERE [ClubCardNumber] IS NOT NULL AND LTRIM(RTRIM([ClubCardNumber])) <> N''
        GROUP BY LOWER(LTRIM(RTRIM([ClubCardNumber])))
        HAVING COUNT(*) > 1
   )
BEGIN
    CREATE UNIQUE INDEX [UX_Athletes_ClubCardNumber]
    ON [dbo].[Athletes]([ClubCardNumber])
    WHERE [ClubCardNumber] IS NOT NULL AND [ClubCardNumber] <> N'';
END;
GO

PRINT N'Done: fix-clubcard-schema.sql';
GO
