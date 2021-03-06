﻿CREATE PROCEDURE [api].[LineDefinitions__Save]
	@Entities [LineDefinitionList] READONLY,
	@LineDefinitionColumns [LineDefinitionColumnList] READONLY,
	@LineDefinitionEntries [LineDefinitionEntryList] READONLY,
	@LineDefinitionStateReasons [LineDefinitionStateReasonList] READONLY,
	@ReturnIds BIT = 0,
	@ValidationErrorsJson NVARCHAR(MAX) OUTPUT
AS
BEGIN
SET NOCOUNT ON;
	DECLARE @ValidationErrors [dbo].[ValidationErrorList];

	INSERT INTO @ValidationErrors
	EXEC [bll].[LineDefinitions_Validate__Save]
		@Entities = @Entities,
		@LineDefinitionColumns = @LineDefinitionColumns,
		@LineDefinitionEntries = @LineDefinitionEntries,
		@LineDefinitionStateReasons = @LineDefinitionStateReasons;

	SELECT @ValidationErrorsJson = 
	(
		SELECT *
		FROM @ValidationErrors
		FOR JSON PATH
	);

	IF @ValidationErrorsJson IS NOT NULL
		RETURN;

	EXEC [dal].[LineDefinitions__Save]
		@Entities = @Entities,
		@LineDefinitionColumns = @LineDefinitionColumns,
		@LineDefinitionEntries = @LineDefinitionEntries,
		@LineDefinitionStateReasons = @LineDefinitionStateReasons;
END;