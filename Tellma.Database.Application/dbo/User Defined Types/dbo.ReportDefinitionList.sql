﻿CREATE TYPE [dbo].[ReportDefinitionList] AS TABLE
(
	[Index]								INT PRIMARY KEY,
	[Id]								NVARCHAR(50)		NOT NULL,
	[Title]								NVARCHAR (255),
	[Title2]							NVARCHAR (255),
	[Title3]							NVARCHAR (255),
	[Description]						NVARCHAR (1024),
	[Description2]						NVARCHAR (1024),
	[Description3]						NVARCHAR (1024),
	[Type]								NVARCHAR (10)		NOT NULL,	-- N'Summary' or N'Details'
	[Chart]								NVARCHAR (50),					-- N'BarsVertical', N'Pie', etc...
	[DefaultsToChart]					BIT,							-- Whether the report opens in chart view by default
	[Collection]						NVARCHAR (50)		NOT NULL,	-- aka. The fact table
	[DefinitionId]						NVARCHAR (50),
	[Filter]							NVARCHAR (1024),
	[OrderBy]							NVARCHAR (1024),
	[Top]								INT,
	[ShowColumnsTotal]					BIT,
	[ShowRowsTotal]						BIT,
	[ShowInMainMenu]					BIT,
	[MainMenuSection]					NVARCHAR (50),	-- IF Null, appears in the "Miscellaneous" section
	[MainMenuIcon]						NVARCHAR (50),
	[MainMenuSortKey]					DECIMAL (9,4)
)
