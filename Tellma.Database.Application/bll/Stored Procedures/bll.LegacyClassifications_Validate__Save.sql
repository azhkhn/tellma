﻿CREATE PROCEDURE [bll].[LegacyClassifications_Validate__Save]
	@Entities [LegacyClassificationList] READONLY,
	@Top INT = 10
AS
	DECLARE @ValidationErrors [dbo].[ValidationErrorList];

		-- Code must not be already in the back end
	INSERT INTO @ValidationErrors([Key], [ErrorName], [Argument0])
	SELECT
		'[' + CAST(FE.[Index] AS NVARCHAR (255)) + '].Code',
		N'Error_TheCode0IsUsed',
		FE.Code AS Argument0
	FROM @Entities FE 
	JOIN [dbo].[LegacyClassifications] BE ON FE.Code = BE.Code
	WHERE FE.Id <> BE.Id;

	-- Code must not be duplicated in the uploaded list
	INSERT INTO @ValidationErrors([Key], [ErrorName], [Argument0])
	SELECT
		'[' + CAST([Index] AS NVARCHAR (255)) + '].Code',
		N'Error_TheCode0IsDuplicated',
		[Code]
	FROM @Entities
	WHERE [Code] IN (
		SELECT [Code]
		FROM @Entities
		WHERE [Code] IS NOT NULL
		GROUP BY [Code]
		HAVING COUNT(*) > 1
	) OPTION (HASH JOIN);

	SELECT TOP(@Top) * FROM @ValidationErrors;
;