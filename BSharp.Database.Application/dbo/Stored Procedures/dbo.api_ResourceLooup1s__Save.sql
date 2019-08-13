﻿CREATE PROCEDURE [dbo].[api_ResourceLooup1s__Save]
	@Entities [ResourceLookupList] READONLY,
	@ReturnIds BIT = 0,
	@ValidationErrorsJson NVARCHAR(MAX) OUTPUT
AS
BEGIN
SET NOCOUNT ON;
	DECLARE @ValidationErrors [dbo].[ValidationErrorList];

	--INSERT INTO @ValidationErrors
	EXEC [dbo].[bll_ResourceLookup1s_Validate__Save]
		@Entities = @Entities,
		@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

	SELECT @ValidationErrorsJson = 
	(
		SELECT *
		FROM @ValidationErrors
		FOR JSON PATH
	);

	IF @ValidationErrorsJson IS NOT NULL
		RETURN;

	EXEC [dbo].[dal_ResourceLookup1s__Save]
		@Entities = @Entities
END;