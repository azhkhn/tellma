﻿CREATE TABLE [dbo].[DocumentLines] (
--	These are for transactions only. If there are Lines from requests or inquiries, etc=> other tables
	[Id]						INT					PRIMARY KEY IDENTITY,
	[DocumentId]				INT					NOT NULL CONSTRAINT [FK_DocumentLines__DocumentId]	FOREIGN KEY ([DocumentId])	REFERENCES [dbo].[Documents] ([Id]) ON DELETE CASCADE,
	[LineDefinitionId]			NVARCHAR (50)		NOT NULL CONSTRAINT [FK_DocumentLines__LineDefinitionId]	FOREIGN KEY ([LineDefinitionId])	REFERENCES [dbo].[LineDefinitions] ([Id]),
	[State]						NVARCHAR (30)		NOT NULL DEFAULT N'Draft' CONSTRAINT [CK_DocumentLines__State] CHECK ([State] IN (N'Draft', N'Void', N'Requested', N'Rejected', N'Authorized', N'Failed', N'Completed', N'Invalid', N'Reviewed')),
	[RequestedAt]				DATETIMEOFFSET(7),
	[AuthorizeddAt]				DATETIMEOFFSET(7),
	[CompletedAt]				DATETIMEOFFSET(7),
	[ReviewedAt]				DATETIMEOFFSET(7),

	[CurrencyId]				NCHAR (3)			CONSTRAINT [FK_DocumentLines__CurrencyId] REFERENCES dbo.Currencies([Id]),
	[AgentRelationDefinitionId] NVARCHAR (50)		REFERENCES dbo.AgentRelationDefinitions([Id]),
	[AgentId]					INT					REFERENCES dbo.Agents([Id]), -- useful for storing the conversion agent in conversion transactions
	[ResourceId]				INT					REFERENCES dbo.Resources([Id]),
	[Amount]					MONEY,
-- Additional information to satisfy reporting requirements
	[Memo]						NVARCHAR (255), -- a textual description for statements and reports
	[ExternalReference]		NVARCHAR (50),
	[AdditionalReference]	NVARCHAR (50),
-- While Voucher Number referes to the source document, this refers to any other identifying string 
-- for support documents, such as deposit slip reference, invoice number, etc...

	[SortKey]					INT				NOT NULL,
-- for auditing
	[CreatedAt]				DATETIMEOFFSET(7)	NOT NULL DEFAULT SYSDATETIMEOFFSET() CONSTRAINT [FK_DocumentLines__CreatedById]	FOREIGN KEY ([CreatedById])	REFERENCES [dbo].[Users] ([Id]),
	[CreatedById]			INT	NOT NULL DEFAULT CONVERT(INT, SESSION_CONTEXT(N'UserId')),
	[ModifiedAt]			DATETIMEOFFSET(7)	NOT NULL DEFAULT SYSDATETIMEOFFSET(),
	[ModifiedById]			INT	NOT NULL DEFAULT CONVERT(INT, SESSION_CONTEXT(N'UserId')) CONSTRAINT [FK_DocumentLines__ModifiedById]	FOREIGN KEY ([ModifiedById])REFERENCES [dbo].[Users] ([Id]),
);
GO
CREATE INDEX [IX_DocumentLines__DocumentId] ON [dbo].[DocumentLines]([DocumentId]);
GO