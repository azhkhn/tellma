﻿-- This file is executed before any test is run

DECLARE @UserId INT, @RoleId INT;
SELECT @UserId = [Id] FROM [dbo].[Users] WHERE [Email] = @Email;
SELECT @RoleId = [Id] FROM [dbo].[Roles] WHERE [Name] = 'Administrator';

EXEC sp_set_session_context 'UserId', @UserId;

-- Cleanup, Central records before lookup records

DELETE FROM [dbo].[ReportDefinitions];
DELETE FROM [dbo].[Documents]
DELETE FROM [dbo].[Permissions];
DELETE FROM [dbo].[RoleMemberships];

DELETE FROM [dbo].[Accounts];

DELETE FROM [dbo].[Roles] WHERE [Id] <> @RoleId;
DELETE FROM [dbo].[Users] WHERE [Id] <> @UserId;
DELETE FROM [dbo].[Agents];
DELETE FROM [dbo].[Resources];
DELETE FROM [dbo].[Currencies] WHERE Id NOT IN (Select FunctionalCurrencyId FROM [dbo].[Settings]);

DELETE FROM [dbo].[Lookups];
DELETE FROM [dbo].[MeasurementUnits];
DELETE FROM [dbo].[LegacyClassifications];
DELETE FROM [dbo].[ResourceDefinitions];
DELETE FROM [dbo].[LookupDefinitions];
DELETE FROM [dbo].[ResponsibilityCenters];
DELETE FROM [dbo].[AccountTypes];
DELETE FROM [dbo].[EntryTypes];

-- Populate


DECLARE @PTAccountTypes dbo.[AccountTypeList];
INSERT INTO @PTAccountTypes (
	[Index], [Code],					[Name],						[Description]) VALUES
(0, N'AccountsPayable',		N'Accounts Payable',		N'This represents balances owed to vendors for goods, supplies, and services purchased on an open account. Accounts payable balances are used in accrual-based accounting, are generally due in 30 or 60 days, and do not bear interest.'),
(1, N'AccountsReceivable',		N'Accounts Receivable',		N'This represents amounts owed by customers for items or services sold to them when cash is not received at the time of sale. Typically, accounts receivable balances are recorded on sales invoices that include terms of payment. Accounts receivable are used in accrual-based accounting.');

EXEC dal.AccountTypes__Save @Entities = @PTAccountTypes;
-- INSERT INTO dbo.AccountTypes ([Code], [Name], [Description]) SELECT [Code], [Name], [Description] FROM @PTAccountTypes;

INSERT INTO [dbo].[Permissions] ([RoleId], [View], [Action])
VALUES
(@RoleId, N'users', N'All'),
(@RoleId, N'roles', N'All')


INSERT INTO [dbo].[RoleMemberships] ([UserId], [RoleId])
VALUES (@UserId, @RoleId)

--IF NOT EXISTS(SELECT * FROM [dbo].[DocumentDefinitions] WHERE [Id] = N'manual-journal-vouchers')
--	INSERT INTO [dbo].[DocumentDefinitions]	([Id], [TitleSingular], [TitlePlural], [Prefix]) VALUES
--	(N'manual-journal-vouchers', N'Manual Journal Voucher',	N'Manual Journal Vouchers',	N'JV');
	
--IF NOT EXISTS(SELECT * FROM [dbo].[LineDefinitions] WHERE [Id] = N'ManualLine')
--	INSERT INTO [dbo].[LineDefinitions]([Id], [TitleSingular], [TitlePlural]) VALUES
--	(N'ManualLine', N'Adjustment', N'Adjustments');

IF NOT EXISTS(SELECT * FROM [dbo].[LookupDefinitions] WHERE [Id] = N'colors')
	INSERT INTO [dbo].[LookupDefinitions]([Id])
	VALUES(N'colors');	

IF NOT EXISTS(SELECT * FROM [dbo].[AgentDefinitions] WHERE [Id] = N'customers')
	INSERT INTO [dbo].[AgentDefinitions]([Id])
	VALUES(N'customers');

IF NOT EXISTS(SELECT * FROM [dbo].[ResourceDefinitions] WHERE [Id] = N'currencies')
	INSERT INTO [dbo].[ResourceDefinitions]([Id])
	VALUES(N'currencies');

UPDATE Settings SET DefinitionsVersion = NEWID(), SettingsVersion = NEWID();

DECLARE @ValidationErrorsJson nvarchar(max);

DECLARE @EntryTypes dbo.EntryTypeList;
INSERT INTO @EntryTypes([IsAssignable], [Index], [ForDebit], [ForCredit], [ParentIndex], [Code], [Name]) VALUES
 (0, 0, 1, 1, NULL, 'ChangesInPropertyPlantAndEquipment', 'Increase (decrease) in property, plant and equipment')
,(1, 1, 1, 0, 0, 'AdditionsOtherThanThroughBusinessCombinationsPropertyPlantAndEquipment', 'Additions other than through business combinations, property, plant and equipment')
,(1, 2, 1, 0, 0, 'AcquisitionsThroughBusinessCombinationsPropertyPlantAndEquipment', 'Acquisitions through business combinations, property, plant and equipment')

EXEC [api].[EntryTypes__Save]
	@Entities = @EntryTypes,
	@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

-- Currencies
DECLARE @Currencies CurrencyList;
INSERT INTO @Currencies([Index], [Id], [Name], [Name2], [E]) VALUES
(0, N'USD', N'US Dollar',N'دولار أمريكي', 2),
(1, N'ETB', N'ET Birr', N'بر أثيوبي', 2);

EXEC dal.Currencies__Save
	@Entities = @Currencies;
