﻿CREATE TABLE [dbo].[Lines] (
--	These are for transactions only. If there are Lines from requests or inquiries, etc=> other tables
	[Id]						INT					CONSTRAINT [PK_Lines] PRIMARY KEY IDENTITY,
	[DocumentId]				INT					NOT NULL CONSTRAINT [FK_Lines__DocumentId] REFERENCES [dbo].[Documents] ([Id]) ON DELETE CASCADE,
	[DefinitionId]				NVARCHAR (50)		NOT NULL CONSTRAINT [FK_Lines__DefinitionId] REFERENCES [dbo].[LineDefinitions] ([Id]),
	[State]						NVARCHAR (30)		NOT NULL DEFAULT N'Draft' CONSTRAINT [CK_Lines__State] CHECK ([State] IN (N'Draft', N'Void', N'Requested', N'Rejected', N'Authorized', N'Failed', N'Completed', N'Invalid', N'Reviewed')),
	[RequestedAt]				DATETIMEOFFSET(7),
	[AuthorizeddAt]				DATETIMEOFFSET(7),
	[CompletedAt]				DATETIMEOFFSET(7),
	[ReviewedAt]				DATETIMEOFFSET(7),

	[CurrencyId]				NCHAR (3)			CONSTRAINT [FK_Lines__CurrencyId] REFERENCES dbo.Currencies([Id]),
	[AgentDefinitionId]			NVARCHAR (50)		CONSTRAINT [FK_Lines__AgentDefinitionId] REFERENCES dbo.AgentDefinitions([Id]),
	[AgentId]					INT					CONSTRAINT [FK_Lines__AgentId] REFERENCES dbo.Agents([Id]), -- useful for storing the conversion agent in conversion transactions
	[ResourceId]				INT					CONSTRAINT [FK_Lines__ResourceId] REFERENCES dbo.Resources([Id]),
	[Amount]					MONEY,
-- Additional information to satisfy reporting requirements
	[Memo]						NVARCHAR (255), -- a textual description for statements and reports
	[ExternalReference]			NVARCHAR (50),
	[AdditionalReference]		NVARCHAR (50),
-- While Voucher Number referes to the source document, this refers to any other identifying string 
-- for support documents, such as deposit slip reference, invoice number, etc...

	[SortKey]					INT				NOT NULL,
-- for auditing
	[CreatedAt]				DATETIMEOFFSET(7)	NOT NULL DEFAULT SYSDATETIMEOFFSET() CONSTRAINT [FK_Lines__CreatedById]	FOREIGN KEY ([CreatedById])	REFERENCES [dbo].[Users] ([Id]),
	[CreatedById]			INT	NOT NULL DEFAULT CONVERT(INT, SESSION_CONTEXT(N'UserId')),
	[ModifiedAt]			DATETIMEOFFSET(7)	NOT NULL DEFAULT SYSDATETIMEOFFSET(),
	[ModifiedById]			INT	NOT NULL DEFAULT CONVERT(INT, SESSION_CONTEXT(N'UserId')) CONSTRAINT [FK_Lines__ModifiedById]	FOREIGN KEY ([ModifiedById]) REFERENCES [dbo].[Users] ([Id]),
);
GO
CREATE INDEX [IX_Lines__DocumentId] ON [dbo].[Lines]([DocumentId]);
GO