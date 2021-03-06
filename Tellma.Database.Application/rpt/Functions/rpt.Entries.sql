﻿CREATE FUNCTION [rpt].[Entries] (
-- This is actually only needed for SQL prototyping. map.EntriesDetails() is good enough for reporting.
	@fromDate Date = '2000.01.01', 
	@toDate Date = '2100.01.01',
	@CountUnitId INT,
	@MassUnitId INT,
	@VolumeUnitId INT
) RETURNS TABLE
AS
RETURN
	SELECT
		E.[Id],
		E.[LineId],
		L.[DocumentId],
		D.[DocumentDate],
		D.[SerialNumber],
		D.[VoucherNumericReference],
		D.[DocumentLookup1Id],
		D.[DocumentLookup2Id],
		D.[DocumentLookup3Id],
		D.[DocumentText1],
		D.[DocumentText2],
		D.[State] AS DocumentState,
		D.[DefinitionId] AS [DocumentDefinitionId],
		L.[DefinitionId] AS [LineDefinitionId],
		L.[State] AS [LineState],
		E.[ResponsibilityCenterId],
		--E.[EntryNumber],
		E.[Direction],
		E.[AccountId],
		A.[LegacyTypeId],
		A.[IsCurrent],
		A.[LegacyClassificationId],
		A.[AccountTypeId],
		A.[AgentDefinitionId],
		--E.[AccountIdentifier]
		E.[AgentId],
		E.[EntryTypeId],
		E.[ResourceId],
		--E.[ResourceIdentifier],
		E.[DueDate],
		E.[MonetaryValue],
		E.[AlgebraicMonetaryValue],
		E.[CurrencyId],

		E.[Count],
		E.[NormalizedCount],
		E.[AlgebraicCount],
		E.[AlgebraicNormalizedCount],
		R.[CountUnitId],

		E.[Mass],
		E.[NormalizedMass],	
		E.[AlgebraicMass],
		E.[AlgebraicNormalizedMass],		
		R.[MassUnitId],

		E.[Volume],
		E.[AlgebraicVolume],
		E.[NormalizedVolume],
		E.[AlgebraicNormalizedVolume],
		R.[VolumeUnitId],

		E.[Time],
		E.[AlgebraicTime],
		R.[TimeUnitId],

		E.[Value],
		E.[AlgebraicValue],
		
		L.[Memo],
		E.[ExternalReference],
		E.[AdditionalReference],
		E.[NotedAgentId],
		E.[NotedAgentName],
		E.[NotedAmount],
		E.[NotedDate]
	FROM
		[map].[DetailsEntries](@CountUnitId, @MassUnitId, @VolumeUnitId)  E
		JOIN [dbo].[Lines] L ON E.[LineId] = L.Id
		JOIN [dbo].[Documents] D ON L.[DocumentId] = D.[Id]
		JOIN dbo.Accounts A ON E.AccountId = A.Id
		LEFT JOIN dbo.Resources R ON E.ResourceId = R.Id
	WHERE
		(@fromDate IS NULL OR D.[DocumentDate] >= @fromDate)
	AND (@toDate IS NULL OR D.[DocumentDate] < DATEADD(DAY, 1, @toDate));