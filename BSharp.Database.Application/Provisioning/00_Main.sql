﻿BEGIN -- Setup Configuration
	DECLARE @DeployEmail NVARCHAR(255)					= '$(DeployEmail)';-- N'support@banan-it.com';
	DECLARE @ShortCompanyName NVARCHAR(255)				= '$(ShortCompanyName)'; --N'ACME International';
	DECLARE @PrimaryLanguageId NVARCHAR(255)			= '$(PrimaryLanguageId)'; --N'en';
	DECLARE @FunctionalCurrency NCHAR(3)				= '$(FunctionalCurrency)'; --N'ETB'
	DECLARE @ViewsAndSpecsVersion UNIQUEIDENTIFIER		= NEWID();
	DECLARE @SettingsVersion UNIQUEIDENTIFIER			= NEWID();
END
-- Local Variables
DECLARE @UserId INT, @Now DATETIMEOFFSET(7) = SYSDATETIMEOFFSET(), @FunctionalCurrencyId INT, @ValidationErrorsJson NVARCHAR(MAX);
-- Add the support account
IF NOT EXISTS(SELECT * FROM [dbo].[Users] WHERE [Email] = @DeployEmail)
BEGIN
	INSERT INTO dbo.Agents([Name],[AgentType], CreatedById, ModifiedById)
	VALUES (N'Banan IT', N'Organization', NULL, NULL)
	SET @UserId= SCOPE_IDENTITY();
	INSERT INTO [dbo].[Users]([Id], [Email], CreatedById, ModifiedById)
	VALUES (@UserId, @DeployEmail, @UserId, @UserId);
END
-- Set the user session context
SELECT @UserId = [Id] FROM dbo.[Users] WHERE [Email] = @DeployEmail;
EXEC sp_set_session_context 'UserId', @UserId;
--
EXEC [dal].[Settings__Save]
	@ShortCompanyName = @ShortCompanyName,
	@PrimaryLanguageId = @PrimaryLanguageId,
	@ViewsAndSpecsVersion = @ViewsAndSpecsVersion,
	@SettingsVersion = @SettingsVersion,
	@FunctionalCurrency = @FunctionalCurrency;

:r .\01_IfrsConcepts.sql
:r .\011_IfrsDisclosures.sql
:r .\012_IfrsEntryClassifications.sql
:r .\013_IfrsAccountClassifications.sql
:r .\02_MeasurementUnits.sql
--:r .\02_Accounts.sql
--EXEC [dbo].[adm_Accounts_Notes__Update];
--:r .\04_AccountsNotes.sql
:r .\06_DocumentTypes.sql
--:r .\05_LineTypeSpecifications.sql
--:r .\07_AccountSpecifications.sql