﻿CREATE PROCEDURE [dal].[AccountTypes__Delete]
	@Ids [IdList] READONLY
AS
	IF NOT EXISTS(SELECT * FROM @Ids) RETURN;
	

	DELETE FROM [dbo].[AccountTypes]
	WHERE [Id] IN (SELECT [Id] FROM @Ids);

	-- TODO: needs testing, it is not working after deleting ParentId
	-- OR, we may simply prevent using Delete and use Delete With Descendants only
	-- reorganize the nodes
	WITH Children ([Id], [ParentId], [Num]) AS (
		SELECT E.[Id], E2.[Id] As ParentId, ROW_NUMBER() OVER (PARTITION BY E2.[Id] ORDER BY E2.[Id])
		FROM [dbo].[AccountTypes] E
		LEFT JOIN [dbo].[AccountTypes] E2 ON E.[ParentNode] = E2.[Node]
	),
	Paths ([Node], [Id]) AS (  
		-- This section provides the value for the roots of the hierarchy  
		SELECT CAST(('/'  + CAST(C.Num AS VARCHAR(30)) + '/') AS HIERARCHYID) AS [Node], [Id]
		FROM Children AS C   
		WHERE [ParentId] IS NULL
		UNION ALL   
		-- This section provides values for all nodes except the root  
		SELECT CAST(P.[Node].ToString() + CAST(C.Num AS VARCHAR(30)) + '/' AS HIERARCHYID), C.[Id]
		FROM Children C
		JOIN Paths P ON C.[ParentId] = P.[Id]
	)
	MERGE INTO [dbo].[AccountTypes] As t
	USING Paths As s ON (t.[Id] = s.[Id] AND t.[Node] <> s.[Node])
	WHEN MATCHED THEN UPDATE SET t.[Node] = s.[Node];
