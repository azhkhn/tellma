﻿BEGIN -- Cleanup & Declarations
	DECLARE @MU1 [dbo].MeasurementUnitList, @MU2 [dbo].MeasurementUnitList, @MU3 [dbo].MeasurementUnitList,
			@MUIndexedIds dbo.[IndexedIdList];
	DECLARE @ETBUnit NCHAR (3) = N'ETB', @USDUnit NCHAR (3) = N'USD'
	DECLARE @eaUnit INT, @pcsUnit INT, @shareUnit INT, @kgUnit INT, @LiterUnit INT, @KmUnit INT,
			@wmoUnit INT, @hrUnit INT, @yrUnit INT, @dayUnit INT, @moUnit INT;
END
BEGIN -- Inserting
	INSERT INTO @MU1 ([Index],
		[Name], [UnitType], [Description], [UnitAmount], [BaseAmount], [Code]) VALUES
	(0, N'pack-6', N'Count', N'Pack of 6', 1, 6, NULL),
	(1, N'dozen', N'Count', N'Dozen', 1, 12, NULL);


	EXEC [api].[MeasurementUnits__Save]
		@Entities = @MU1,
		@ValidationErrorsJson = @ValidationErrorsJson OUTPUT

	--SELECT * FROM @MU1;

	IF @ValidationErrorsJson IS NOT NULL 
	BEGIN
		Print 'MeasurementUnits: Inserting: ' + @ValidationErrorsJson
		GOTO Err_Label;
	END
END

-- Display units whose code starts with m
INSERT INTO @MU2 ([Index], [Id], [Code], [UnitType], [Name], [Description], [UnitAmount], [BaseAmount])
SELECT ROW_NUMBER() OVER(ORDER BY [Id]),
	[Id], [Code], [UnitType], [Name], [Description], [UnitAmount], [BaseAmount]
FROM [dbo].MeasurementUnits
WHERE [Name] Like 'm%';
SET @RowCount = @@ROWCOUNT;

-- Inserting
DECLARE @TestingValidation bit = 0;
IF (@TestingValidation = 1)
INSERT INTO @MU2 ( [Index],
				[Name], [UnitType], [Description], [UnitAmount], [BaseAmount], [Code]) Values
	--(@RowCount+1, N'AED', N'MonetaryValue', N'AE Dirhams', 3.67, 1, N'AED'),
	(@RowCount+2, N'c', N'Time', N'Century', 1, 3110400000, NULL),
	(@RowCount+3, N'dozen', N'Count', N'Dazzina', 1, 12, NULL);
-- Updating
UPDATE @MU2 
SET 
--	[Name] = N'pcs',
	[Description] = N'Metric Ton' -- Capitalizing the letter T
WHERE [Name] = N'mt';

-- SELECT * FROM @MU2;
DELETE FROM @MU2 WHERE [Name] Like 'm%' AND [Name] <> N'mt';
-- Calling Save API
EXEC [api].[MeasurementUnits__Save]
	@Entities = @MU2,
	@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

IF @ValidationErrorsJson IS NOT NULL
BEGIN
	Print 'MeasurementUnits: Updating: ' + @ValidationErrorsJson
	GOTO Err_Label;
END

INSERT INTO @MU3 ([Index], [Id], [Code], [UnitType], [Name], [Description], [UnitAmount], [BaseAmount])
SELECT ROW_NUMBER() OVER(ORDER BY [Id]),
	[Id], [Code], [UnitType], [Name], [Description], [UnitAmount], [BaseAmount]
FROM [dbo].MeasurementUnits
WHERE [UnitType] = N'Distance' AND [Name] <> N'Km';

-- Calling Delete API
INSERT INTO @MUIndexedIds([Index], [Id]) SELECT [Index], [Id] FROM @MU3;
EXEC [api].[MeasurementUnits__Delete]
	@IndexedIds = @MUIndexedIds,
	@ValidationErrorsJson = @ValidationErrorsJson OUTPUT

--SELECT * FROM [dbo].[fs_MeasurementUnits]();

IF @ValidationErrorsJson IS NOT NULL
BEGIN
	Print 'MeasurementUnits: Deleting: ' + @ValidationErrorsJson
	GOTO Err_Label;
END

SELECT
	@KgUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'Kg'),
	@LiterUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'ltr'),
	@KmUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'Km'),
	@pcsUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'pcs'),
	@eaUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'ea'),
	@shareUnit = (SELECT [Id] FROM [dbo].MeasurementUnits WHERE [Name] = N'share'),
	@wmoUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'wmo'),
	@hrUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'hr'),
	@yrUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'yr'),
	@dayUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'd'),
	@moUnit = (SELECT [Id] FROM [dbo].MeasurementUnits	WHERE [Name] = N'mo');