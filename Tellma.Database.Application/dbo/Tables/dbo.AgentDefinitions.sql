﻿CREATE TABLE [dbo].[AgentDefinitions]
(
	[Id]								NVARCHAR(50)		CONSTRAINT [PK_AgentDefinitions] PRIMARY KEY,
	[TitleSingular]						NVARCHAR (255),
	[TitleSingular2]					NVARCHAR (255),
	[TitleSingular3]					NVARCHAR (255),
	[TitlePlural]						NVARCHAR (255),
	[TitlePlural2]						NVARCHAR (255),
	[TitlePlural3]						NVARCHAR (255),
	[TaxIdentificationNumberVisibility] NVARCHAR (50),
	[StartDateVisibility]				NVARCHAR (50),
	[StartDateLabel]					NVARCHAR (50),
	[StartDateLabel2]					NVARCHAR (50),
	[StartDateLabel3]					NVARCHAR (50),

	--[Prefix]							NVARCHAR (30)	DEFAULT (N''),
	--[CodeWidth]							TINYINT			DEFAULT (3), -- For presentation purposes
	[IsActive]							BIT				NOT NULL DEFAULT 1,

	[JobVisibility]						NVARCHAR (50), -- None, Visible, Required
	[BasicSalaryVisibility]				NVARCHAR (50),
	[TransportationAllowanceVisibility]	NVARCHAR (50),
--	[HardshipAllowanceVisibility]		NVARCHAR (50),
	[OvertimeRateVisibility]			NVARCHAR (50),
	[BankAccountNumberVisibility]		NVARCHAR (50),
	
	[State]							NVARCHAR (50)	DEFAULT N'Draft',	-- Deployed, Archived (Phased Out)
	[MainMenuIcon]					NVARCHAR (50),
	[MainMenuSection]				NVARCHAR (50),			-- IF Null, it does not show on the main menu
	[MainMenuSortKey]				DECIMAL (9,4),

	[SavedById]			INT				NOT NULL DEFAULT CONVERT(INT, SESSION_CONTEXT(N'UserId')) CONSTRAINT [FK_AgentDefinitions__SavedById] REFERENCES [dbo].[Users] ([Id]),
	[ValidFrom]			DATETIME2		GENERATED ALWAYS AS ROW START NOT NULL,
	[ValidTo]			DATETIME2		GENERATED ALWAYS AS ROW END HIDDEN NOT NULL,
	PERIOD FOR SYSTEM_TIME ([ValidFrom], [ValidTo])
)
WITH (SYSTEM_VERSIONING = ON (HISTORY_TABLE = dbo.[AgentDefinitionsHistory]));
GO;