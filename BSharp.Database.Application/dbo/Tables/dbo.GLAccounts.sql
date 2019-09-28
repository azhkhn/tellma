﻿-- This table can be used to migrate the historic chart of accounts.
-- then the same list is migrated to Accounts, using 'GL-Accounts' account definition
CREATE TABLE [dbo].[GLAccounts] (
	[Id]								INT					CONSTRAINT [PK_Accounts] PRIMARY KEY NONCLUSTERED IDENTITY,
	-- Needed to generate the basic statements when operating in "simple" mode
	[AccountType]						NVARCHAR (50)		NOT NULL,
	-- This one is not needed, and must be replaces with AccountDefinition
	[Name]								NVARCHAR (255),
	[Name2]								NVARCHAR (255),
	[Name3]								NVARCHAR (255),
	[Code]								NVARCHAR (50)		NOT NULL CONSTRAINT [CK_Accounts__Code] UNIQUE CLUSTERED,
	-- Inactive means, it does not appear to the use when classifying an entry
	[IsDeprecated]						BIT					NOT NULL DEFAULT 0,
	-- Audit details
	[CreatedAt]							DATETIMEOFFSET(7)	NOT NULL DEFAULT SYSDATETIMEOFFSET(),
	[CreatedById]						INT					NOT NULL DEFAULT CONVERT(INT, SESSION_CONTEXT(N'UserId')) CONSTRAINT [FK_GLAccounts__CreatedById] FOREIGN KEY ([CreatedById]) REFERENCES [dbo].[Users] ([Id]),
	[ModifiedAt]						DATETIMEOFFSET(7)	NOT NULL DEFAULT SYSDATETIMEOFFSET(),
	[ModifiedById]						INT					NOT NULL DEFAULT CONVERT(INT, SESSION_CONTEXT(N'UserId')) CONSTRAINT [FK_GLAccounts__ModifiedById] FOREIGN KEY ([ModifiedById]) REFERENCES [dbo].[Users] ([Id]),
	-- Pure SQL properties and computed properties
	[Node]								HIERARCHYID				NOT NULL,
	[ParentNode]						AS [Node].GetAncestor(1)
);
GO

-- The allowable values are the lowest level of the calculation trees in Ifrs Taxonomies: (financial position, comprehensive income, by function)
	-- To generate the above financial statements , classifications of childen of same parent can all be aggregated to the parent,
	-- or can some be combined into catchall "other", like Other Inventories, Other property plant and equipment, etc.
	-- To generate additional disclosures, the user must design disclosures using appropriate Ifrs concepts, and then each account 
	-- could be mapped to any concept from that disclosure.