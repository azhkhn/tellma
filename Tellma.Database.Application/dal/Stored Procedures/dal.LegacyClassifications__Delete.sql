﻿CREATE PROCEDURE [dal].[LegacyClassifications__Delete]
	@Ids [dbo].[IdList] READONLY
AS
	IF NOT EXISTS(SELECT * FROM @Ids) RETURN;

	-- Delete the entites and their children
	WITH EntitiesWithDescendants
	AS (
		SELECT T2.[Id]
		FROM [dbo].[LegacyClassifications] T1
		JOIN [dbo].[LegacyClassifications] T2
		ON T2.[Node].IsDescendantOf(T1.[Node]) = 1
		WHERE T1.[Id] IN (SELECT [Id] FROM @Ids)
	)
	DELETE FROM [dbo].[LegacyClassifications]
	WHERE [Id] IN (SELECT [Id] FROM EntitiesWithDescendants);

	-- TODO: reorganize the nodes
	--WITH Children ([Id], [ParentId], [Num]) AS (
	--	SELECT E.[Id], E2.[Id] As ParentId, ROW_NUMBER() OVER (PARTITION BY E2.[Id] ORDER BY E2.[Id])
	--	FROM [dbo].[AccountClassifications] E
	--	LEFT JOIN [dbo].[AccountClassifications] E2 ON E.[ParentId] = E2.[Id]
	--),
	--Paths ([Node], [Id]) AS (  
	--	-- This section provides the value for the roots of the hierarchy  
	--	SELECT CAST(('/'  + CAST(C.Num AS VARCHAR(30)) + '/') AS HIERARCHYID) AS [Node], [Id]
	--	FROM Children AS C   
	--	WHERE [ParentId] IS NULL
	--	UNION ALL   
	--	-- This section provides values for all nodes except the root  
	--	SELECT CAST(P.[Node].ToString() + CAST(C.Num AS VARCHAR(30)) + '/' AS HIERARCHYID), C.[Id]
	--	FROM Children C
	--	JOIN Paths P ON C.[ParentId] = P.[Id]
	--)
	--MERGE INTO [dbo].[AccountClassifications] As t
	--USING Paths As s ON (t.[Id] = s.[Id] AND t.[Node] <> s.[Node])
	--WHEN MATCHED THEN UPDATE SET t.[Node] = s.[Node];
