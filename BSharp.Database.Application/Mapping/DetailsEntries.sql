﻿CREATE FUNCTION [map].[DetailsEntries] (
	@fromDate Date = '2000.01.01', 
	@toDate Date = '2100.01.01',
	@CountUnitId INT,
	@MassUnitId INT,
	@VolumeUnitId INT
) RETURNS TABLE
AS
RETURN
	WITH UnitRatios AS (
		SELECT [Id], [UnitAmount] * (SELECT [BaseAmount] FROM  dbo.MeasurementUnits WHERE [Id] = @CountUnitId)
		/ ([BaseAmount] * (SELECT [UnitAmount] FROM  dbo.MeasurementUnits WHERE [Id] = @CountUnitId)) AS [Ratio]
		FROM dbo.MeasurementUnits
		WHERE UnitType = N'Count'
		UNION
		SELECT [Id], [UnitAmount] * (SELECT [BaseAmount] FROM  dbo.MeasurementUnits WHERE [Id] = @MassUnitId)
		/ ([BaseAmount] * (SELECT [UnitAmount] FROM  dbo.MeasurementUnits WHERE [Id] = @MassUnitId)) As [Ratio]
		FROM dbo.MeasurementUnits
		WHERE UnitType = N'Mass'
		UNION
		SELECT [Id], [UnitAmount] * (SELECT [BaseAmount] FROM  dbo.MeasurementUnits WHERE [Id] = @MassUnitId)
		/ ([BaseAmount] * (SELECT [UnitAmount] FROM  dbo.MeasurementUnits WHERE [Id] = @MassUnitId)) As [Ratio]
		FROM dbo.MeasurementUnits
		WHERE UnitType = N'Volume'
	)
	SELECT
		J.[Id],
		J.[LineId],
		J.[DocumentId],
		J.[DocumentDate],
		J.[SerialNumber],
		J.[VoucherNumericReference],
		J.[DocumentDefinitionId],
		J.[LineDefinitionId],
		J.[Direction],
		J.[AccountId],
		J.[ContractType],
		J.[ResourceClassificationId],
		J.[AgentDefinitionId],
		J.[AgentId],
		J.[EntryClassificationId],
		J.[ResourceId],
		--J.[ResourceIdentifier],
		J.[DueDate],
		J.[MonetaryValue],
		J.[CurrencyId],
		J.[Count] * ISNULL(CR.[Ratio], 0) AS [Count],
		J.[Mass] * ISNULL(MR.[Ratio], 0) AS [Mass],
		J.[Volume] * ISNULL(MR.[Ratio], 0) AS [Volume],
		J.[Time],
		J.[Value],
		J.[Memo],
		J.[ExternalReference],
		J.[AdditionalReference],
		J.[RelatedAgentId],
		J.[RelatedAmount]
	FROM dbo.fi_Journal(@fromDate, @toDate) J
	LEFT JOIN dbo.Resources R ON J.ResourceId = R.Id
	LEFT JOIN UnitRatios CR ON R.CountUnitId = CR.Id
	LEFT JOIN UnitRatios MR ON R.MassUnitId = MR.Id
	LEFT JOIN UnitRatios CV ON R.VolumeUnitId = CV.Id
		--WHERE
		--J.[DocumentState] = 5 -- N'Closed'
		--AND J.[DocumentState] = 4; -- N'Reviewed';
	
	;