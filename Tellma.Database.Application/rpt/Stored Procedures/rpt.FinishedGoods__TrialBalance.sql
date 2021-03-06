﻿CREATE PROCEDURE [rpt].[FinishedGoods__TrialBalance]
	@FromDate Date = '01.01.2020',
	@ToDate Date = '01.01.2020',
	@ResponsibilityCenterId INT = NULL,
	@CountUnitId INT,
	@MassUnitId INT,
	@VolumeUnitId INT
AS
-- WARNING: Useful only when all the FG accounts have HasResource = 1
BEGIN
	WITH JournalSummary
	AS (
		SELECT ResourceId,
			SUM(OpeningCount) AS OpeningCount, SUM(CountIn) AS CountIn, SUM(CountOut) AS CountOut, SUM(EndingCount) AS EndingCount,
			SUM(OpeningMass) AS OpeningMass, SUM(MassIn) AS MassIn, SUM(MassOut) AS MassOut, SUM(EndingMass) AS EndingMass
		FROM [map].[SummaryEntries](
			@FromDate,
			@ToDate,
			@ResponsibilityCenterId,
			NULL,
			N'FinishedGoods', 
			@CountUnitId,
			@MassUnitId,
			@VolumeUnitId
		)
		GROUP BY ResourceId
	)
	SELECT JS.*, R.[Code], R.[Name], R.[Name2], R.[Name3]
	FROM JournalSummary JS
	JOIN dbo.Resources R ON JS.ResourceId = R.Id
END;
GO;