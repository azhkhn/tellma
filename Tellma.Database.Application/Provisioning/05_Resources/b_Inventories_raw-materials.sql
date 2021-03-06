﻿IF @DB = N'104' -- Walia Steel, ETB, en/am
BEGIN
	DELETE FROM @ResourceDefinitions;
	INSERT INTO @ResourceDefinitions (
		[Id],			[TitlePlural],		[TitleSingular],	[IdentifierLabel], [IdentifierVisibility]) VALUES
	( N'raw-materials',	N'Raw Materials',	N'Raw Material',	N'Roll #',			N'Optional');

	EXEC [api].[ResourceDefinitions__Save]
	@Entities = @ResourceDefinitions,
	@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

	IF @ValidationErrorsJson IS NOT NULL 
	BEGIN
		Print 'Resource Definitions: Inserting: ' + @ValidationErrorsJson
		GOTO Err_Label;
	END;		

	DECLARE @RawMaterialsDescendantsTemp TABLE ([Code] NVARCHAR(255), [Name] NVARCHAR(255), [Node] HIERARCHYID, [IsAssignable] BIT DEFAULT 1, [Index] INT, [ResourceDefinitionId] NVARCHAR (50))
	INSERT INTO @RawMaterialsDescendantsTemp ([Index],
		[Code],					[Name],			[Node],			[IsAssignable]) VALUES
--		(N'RawMaterials',					N'Raw materials',								N'/1/11/1/1/',	1,29),
	(0, N'HotRollExtension',	N'Hot Roll',	N'/1/11/1/1/1/',	1),
	(1, N'ColdRollExtension',	N'Cold Roll',	N'/1/11/1/1/2/',	1);
	
	DECLARE @RawMaterialsDescendants AccountTypeList;
	SET @PId = (SELECT [Id] FROM dbo.[AccountTypes] WHERE [Code] = N'RawMaterials');
	INSERT INTO @RawMaterialsDescendants ([ParentId],[Code], [Name], [ParentIndex], [IsAssignable], [Index])
	SELECT @PId, [Code], [Name], (SELECT [Index] FROM @RawMaterialsDescendantsTemp WHERE [Node] = RC.[Node].GetAncestor(1)) AS ParentIndex, [IsAssignable], [Index]
	FROM @RawMaterialsDescendantsTemp RC

	EXEC [api].[AccountTypes__Save]
		@Entities = @RawMaterialsDescendants,
		@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

	IF @ValidationErrorsJson IS NOT NULL 
	BEGIN
		Print 'Raw Materials: Inserting: ' + @ValidationErrorsJson
		GOTO Err_Label;
	END;		
	
	DECLARE @RawMaterials dbo.ResourceList;
	INSERT INTO @RawMaterials ([Index],
		[AccountTypeId],						[Name],					[Code],			[Identifier], [MassUnitId],			[CountUnitId],				[Lookup1Id]) VALUES
	(0, dbo.fn_ATCode__Id(N'HotRollExtension'),N'HR 1000MMx1.9MM',	N'HR1000x1.9',	N'1001',	dbo.fn_UnitName__Id(N'Kg'),	dbo.fn_UnitName__Id(N'pcs'),dbo.fn_Lookup(N'steel-thicknesses', N'1.9')),
	(1, dbo.fn_ATCode__Id(N'ColdRollExtension'),N'CR 1000MMx1.4MM',	N'CR1000x1.4',	N'1002',	dbo.fn_UnitName__Id(N'Kg'),	dbo.fn_UnitName__Id(N'pcs'),dbo.fn_Lookup(N'steel-thicknesses', N'1.4'));
	-- For RM, we use the descriptor - if any - in Entries

	EXEC [api].[Resources__Save]
		@DefinitionId = N'raw-materials',
		@Entities = @RawMaterials,
		@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

	IF @ValidationErrorsJson IS NOT NULL 
	BEGIN
		Print 'Inserting raw materials: ' + @ValidationErrorsJson
		GOTO Err_Label;
	END;

	IF @DebugResources = 1 
	BEGIN
		SELECT N'raw-materials' AS [Resource Definition]
		DECLARE @RawMaterialsIds dbo.IdList;
		INSERT INTO @RawMaterialsIds SELECT [Id] FROM dbo.Resources WHERE [DefinitionId] = N'raw-materials';

		SELECT [Classification], [Name] AS 'Raw Material', [MassUnit] AS 'Weight In', [CountUnit] AS 'Count In', [Lookup1] As N'Thickness'
		FROM rpt.Resources(@RawMaterialsIds);
	END
END