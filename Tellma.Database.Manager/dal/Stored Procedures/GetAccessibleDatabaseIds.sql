﻿CREATE PROCEDURE [dal].[GetAccessibleDatabaseIds]
AS
	DECLARE @UserId INT = CONVERT(INT, SESSION_CONTEXT(N'UserId'));

	SELECT [D].[Id] FROM [dbo].[SqlDatabases] AS [D]
	JOIN [dbo].[GlobalUserMemberships] AS [M] ON [D].[Id] = [M].[DatabaseId]
	WHERE [M].[UserId] = @UserId