-- Birdəfəlik: 4 növ × 001–500 stok + mövcud müştəri kartları.
-- Cədvəl proqram açılışında yaranır; bu skript yalnız sətirləri doldurur.
-- SQL Server, verilənlər bazası: EShootingDb

SET NOCOUNT ON;

IF OBJECT_ID(N'[dbo].[ClubCardStock]', N'U') IS NULL
BEGIN
    RAISERROR(N'ClubCardStock cədvəli yoxdur. Əvvəl saytı bir dəfə açın (IIS/App Pool), sonra bu skripti yenidən işlədin.', 16, 1);
    RETURN;
END

;WITH nums AS (
    SELECT 1 AS n
    UNION ALL
    SELECT n + 1 FROM nums WHERE n < 500
),
types AS (
    SELECT 0 AS CardType UNION ALL
    SELECT 1 UNION ALL
    SELECT 2 UNION ALL
    SELECT 3
)
INSERT INTO [dbo].[ClubCardStock] ([Id], [CardType], [CardNumber], [CreatedAtUtc])
SELECT
    NEWID(),
    t.CardType,
    RIGHT(N'000' + CAST(n.n AS NVARCHAR(10)), 3),
    GETUTCDATE()
FROM types t
CROSS JOIN nums n
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[ClubCardStock] s
    WHERE s.[CardType] = t.CardType
      AND s.[CardNumber] = RIGHT(N'000' + CAST(n.n AS NVARCHAR(10)), 3)
)
OPTION (MAXRECURSION 500);

-- Müştəridə olan nömrələri 001 formatına sal
UPDATE a
SET a.[ClubCardNumber] = CASE
    WHEN TRY_CONVERT(INT, LTRIM(RTRIM(a.[ClubCardNumber]))) IS NULL THEN LTRIM(RTRIM(a.[ClubCardNumber]))
    WHEN TRY_CONVERT(INT, LTRIM(RTRIM(a.[ClubCardNumber]))) < 1000
        THEN RIGHT(N'000' + CAST(TRY_CONVERT(INT, LTRIM(RTRIM(a.[ClubCardNumber]))) AS NVARCHAR(10)), 3)
    ELSE CAST(TRY_CONVERT(INT, LTRIM(RTRIM(a.[ClubCardNumber]))) AS NVARCHAR(20))
END
FROM [dbo].[Athletes] a
WHERE a.[ClubCardNumber] IS NOT NULL
  AND LTRIM(RTRIM(a.[ClubCardNumber])) <> N'';

-- Stokda olmayan, amma müştəridə duran kartları da kataloqa yaz
INSERT INTO [dbo].[ClubCardStock] ([Id], [CardType], [CardNumber], [CreatedAtUtc])
SELECT
    NEWID(),
    ISNULL(a.[ClubCardType], 0),
    LTRIM(RTRIM(a.[ClubCardNumber])),
    GETUTCDATE()
FROM [dbo].[Athletes] a
WHERE a.[ClubCardNumber] IS NOT NULL
  AND LTRIM(RTRIM(a.[ClubCardNumber])) <> N''
  AND NOT EXISTS (
      SELECT 1
      FROM [dbo].[ClubCardStock] s
      WHERE s.[CardType] = ISNULL(a.[ClubCardType], 0)
        AND s.[CardNumber] = LTRIM(RTRIM(a.[ClubCardNumber]))
  );

SELECT t.CardType,
       COUNT(*) AS Total
FROM [dbo].[ClubCardStock] t
GROUP BY t.CardType
ORDER BY t.CardType;

SELECT a.[ClubCardType], a.[ClubCardNumber], a.[FirstName], a.[LastName]
FROM [dbo].[Athletes] a
WHERE a.[ClubCardNumber] IS NOT NULL AND LTRIM(RTRIM(a.[ClubCardNumber])) <> N''
ORDER BY a.[ClubCardType], a.[ClubCardNumber];
