﻿CREATE TABLE [dbo].[AccountDefinitions] (
	[Id]									NVARCHAR (50) PRIMARY KEY,
	[Description]							NVARCHAR (255),
	[Description2]							NVARCHAR (255),
	[Description3]							NVARCHAR (255),
	[TitleSingular]							NVARCHAR (255) NOT NULL,
	[TitleSingular2]						NVARCHAR (255),
	[TitleSingular3]						NVARCHAR (255),
	[TitlePlural]							NVARCHAR (255) NOT NULL,
	[TitlePlural2]							NVARCHAR (255),
	[TitlePlural3]							NVARCHAR (255),
	-- If defined, then any	account with the said definition would be mapped to this GL Account.
	-- if null, then the account needs to be mapped manually.
	[GLAccountId]							NVARCHAR (50),
	-- If defined and leaf, then any entry using an account with the said definition would have this IfrsEntryClassification
	-- If defined and parent, then in the entry IfrsEntryClassification will be limited to the leaf children
	-- If null, then IfrsEntryClassification field will be hidden
	[IfrsEntryClassificationId]				NVARCHAR (255),
	-- When optional, it is used only for detailed reports such as account statement.
	-- When required, it is also used for summary report and to define the account statement parameters (will disappear from report)
	-- If required in Account, the field will be read/only in the statement parameters header.
	-- If required in Account Entries, the field will be editable in the statement parameters header
	[ResponsibleVisibility]					NVARCHAR (50) CHECK ([ResponsibleVisibility] IN (N'None', N'RequiredInAccounts', N'RequiredInEntries', N'OptionalInEntries')),
	[ResponsibleLabel1]						NVARCHAR (50),
	[ResponsibleLabel2]						NVARCHAR (50),
	[ResponsibleLabel3]						NVARCHAR (50),
	[ResponsibleRelationDefinitionList]		NVARCHAR (255),
	[CustodianVisibility]					NVARCHAR (50) CHECK ([CustodianVisibility] IN (N'None', N'RequiredInAccounts', N'RequiredInEntries', N'OptionalInEntries')),
	[CustodianLabel1]						NVARCHAR (50),
	[CustodianLabel2]						NVARCHAR (50),
	[CustodianLabel3]						NVARCHAR (50),
	[CustodianRelationDefinitionList]		NVARCHAR (255),
	[ResourceVisibility]					NVARCHAR (50) CHECK ([ResourceVisibility] IN (N'None', N'RequiredInAccounts', N'RequiredInEntries', N'OptionalInEntries')),
	[ResourceLabel1]						NVARCHAR (50),
	[ResourceLabel2]						NVARCHAR (50),
	[ResourceLabel3]						NVARCHAR (50),
	[ResourceDefinitionList]				NVARCHAR (255),
	[LocationVisibility]					NVARCHAR (50) CHECK ([LocationVisibility] IN (N'None', N'RequiredInAccounts', N'RequiredInEntries', N'OptionalInEntries')),
	[LocationLabel1]						NVARCHAR (50),
	[LocationLabel2]						NVARCHAR (50),
	[LocationLabel3]						NVARCHAR (50),
	[LocationDefinitionList]				NVARCHAR (255),
	[DueDateVisibility]						NVARCHAR (50) CHECK ([DueDateVisibility] IN (N'None', N'RequiredInEntries', N'OptionalInEntries')),
	[DueDateLabel1]							NVARCHAR (50),
	[DueDateLabel2]							NVARCHAR (50),
	[DueDateLabel3]							NVARCHAR (50),
	[RelatedAgentVisibility]				NVARCHAR (50) CHECK ([RelatedAgentVisibility] IN (N'None', N'RequiredInEntries', N'OptionalInEntries')),
	[RelatedAgentLabel1]					NVARCHAR (50),
	[RelatedAgentLabel2]					NVARCHAR (50),
	[RelatedAgentLabel3]					NVARCHAR (50),
	[RelatedAgentRelationDefinitionList]	NVARCHAR (255),
	[RelatedMonetaryAmountVisibility]		NVARCHAR (50) CHECK ([RelatedMonetaryAmountVisibility] IN (N'None', N'RequiredInEntries', N'OptionalInEntries')),
	[RelatedMonetaryAmountLabel1]			NVARCHAR (50),
	[RelatedMonetaryAmountLabel2]			NVARCHAR (50),
	[RelatedMonetaryAmountLabel3]			NVARCHAR (50),
	[ExternalReferenceVisibility]			NVARCHAR (50) CHECK ([ExternalReferenceVisibility] IN (N'None', N'RequiredInEntries', N'OptionalInEntries')),
	[ExternalReferenceLabel1]				NVARCHAR (50),
	[ExternalReferenceLabel2]				NVARCHAR (50),
	[ExternalReferenceLabel3]				NVARCHAR (50),
	[AdditionalReferenceVisibility]			NVARCHAR (50) CHECK ([AdditionalReferenceVisibility] IN (N'None', N'RequiredInEntries', N'OptionalInEntries')),
	[AdditionalReferenceLabel1]				NVARCHAR (50),
	[AdditionalReferenceLabel2]				NVARCHAR (50),
	[AdditionalReferenceLabel3]				NVARCHAR (50)
);