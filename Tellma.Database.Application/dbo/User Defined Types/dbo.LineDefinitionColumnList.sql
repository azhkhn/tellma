﻿CREATE TYPE [dbo].[LineDefinitionColumnList] AS TABLE
(
	[Index]					INT				DEFAULT 0,
	[HeaderIndex]			INT				DEFAULT 0,
	PRIMARY KEY ([Index], [HeaderIndex]),
	[Id]					INT				DEFAULT 0,
	[SortKey]				DECIMAL (9,4),
	[ColumnName]			NVARCHAR (50),
	[Label]					NVARCHAR (50),
	[Label2]				NVARCHAR (50),
	[Label3]				NVARCHAR (50),
	[IsRequiredForStateId]	SMALLINT		NOT NULL DEFAULT 4,
	[IsReadOnlyFromStateId]	SMALLINT		NOT NULL DEFAULT 4
);