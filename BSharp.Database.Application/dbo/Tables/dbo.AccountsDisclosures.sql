﻿CREATE TABLE [dbo].[AccountsDisclosures] (
	-- For Trial balance reporting based on a custom classification
	[AccountId]		INT,
	[IfrsDisclosureId]				NVARCHAR (255), -- StatementOf__Abstract and disclosure notes from 800500
	[Concept]						NVARCHAR (255) NOT NULL, -- the taxonomy defines whether to use instant or period.
	CONSTRAINT [PK_AccountsDisclosures] PRIMARY KEY ([AccountId], [IfrsDisclosureId]),
);