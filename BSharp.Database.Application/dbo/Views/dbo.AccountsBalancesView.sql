﻿CREATE VIEW [dbo].[AccountsBalancesView]
AS
	SELECT
		E.[AccountId], E.[ResourceId], E.[ResourcePickId], E.[BatchCode], 
		SUM(E.[Direction] * E.[MonetaryValue]) AS [MonetaryValue],
		SUM(E.[Direction] * E.[Mass]) AS [Mass],
		SUM(E.[Direction] * E.[Volume]) AS [Volume],
		SUM(E.[Direction] * E.[Count]) AS [Count],
		SUM(E.[Direction] * E.[Time]) AS [Time],
		SUM(E.[Direction] * E.[Value]) AS [Value]
	FROM dbo.[DocumentLineEntries] E
	JOIN dbo.[Documents] D ON E.[DocumentLineId] = D.[Id]
	JOIN dbo.[DocumentDefinitions] DT ON D.[DocumentDefinitionId] = DT.[Id]
	WHERE D.[State] = N'Posted'
	GROUP BY
		E.[AccountId], E.[ResourceId], E.[ResourcePickId], E.[BatchCode]
	HAVING
		SUM(E.[Direction] * E.[MonetaryValue]) <> 0 OR
		SUM(E.[Direction] * E.[Mass]) <> 0 OR 
		SUM(E.[Direction] * E.[Volume]) <> 0 OR
		SUM(E.[Direction] * E.[Count]) <> 0 OR
		SUM(E.[Direction] * E.[Time]) <> 0 OR
		SUM(E.[Direction] * E.[Value]) <> 0;
GO;