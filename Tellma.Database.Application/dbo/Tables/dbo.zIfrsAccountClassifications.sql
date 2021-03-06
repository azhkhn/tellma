﻿CREATE TABLE [dbo].[IfrsAccountClassifications] ( -- managed by Tellma
	[Id]						NVARCHAR (255)			CONSTRAINT [PK_IfrsAccountClassifications] PRIMARY KEY NONCLUSTERED CONSTRAINT [FK_IfrsAccountClassifications__Id] REFERENCES [dbo].[IfrsConcepts] ([Id]) ON DELETE CASCADE,
	[Node]						HIERARCHYID				NOT NULL CONSTRAINT [CK_IfrsClassifications__Node] UNIQUE INDEX [IX_IfrsAccountClassifications__Node] CLUSTERED,
	[ParentNode]				AS [Node].GetAncestor(1),
	[IsLeaf]					BIT						NOT NULL DEFAULT 1, -- update to 0 those who do appear as ancestors
	-- classifications of childen of same parent can all be aggregated to the parent,
	-- or can some be combined into catchall "other", like Other Inventories, Other property plant and equipment, etc.
	[StatementClassificationId]			NVARCHAR (255), -- financial position, comprehensive income
	-- sum the debit movement, and map it to the debit cash flow classification. Repeat for credit movement.
	[DebitCashFlowClassificationId]		NVARCHAR (255),
	[CreditCashFlowClassificationId]	NVARCHAR (255),
	-- Inactive means, the subtree - whose root is this - does not appear to the user when classifying an account
	[IsActive]					BIT						NOT NULL DEFAULT 1, -- when off, the customer does not use this classification
	[Label]						NVARCHAR (1024)			NOT NULL,
	[Label2]					NVARCHAR (1024),
	[Label3]					NVARCHAR (1024),
	[Documentation]				NVARCHAR (MAX),
	[Documentation2]			NVARCHAR (MAX),
	[Documentation3]			NVARCHAR (MAX),
	[EffectiveDate]				DATETIME2(7)			NOT NULL DEFAULT('0001-01-01 00:00:00'),
	[ExpiryDate]				DATETIME2(7)			NOT NULL DEFAULT('9999-12-31 23:59:59'),

--	The settings below apply to any account whose type is this Ifrs
	[IfrsNoteSetting]			NVARCHAR (255)			NOT NULL DEFAULT 'N/A', -- N/A, Optional, Required
);
GO
