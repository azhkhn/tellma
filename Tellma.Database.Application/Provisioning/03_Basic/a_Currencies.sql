﻿DECLARE @Currencies CurrencyList;

IF @DB = N'100' -- ACME, USD, en/ar/zh
INSERT INTO @Currencies([Index],
	[Id],	[Name],			[Name2],			[Description],			[Description2],		[E]) VALUES
(0, N'USD', N'USD',			N'دولار',			N'United States Dollar',N'دولار أمريكي',		2),
(1, N'ETB', N'Birr',		N'بر',				N'Ethiopian Birr',		N'بر أثيوبي',		2),
(2, N'GBP', N'B.Pound',		N'جنيه ب',			N'Sterling Pound',		N'جنيه استرليني',	2),
(3, N'AED', N'Dirham',		N'درهم',			N'Emirates Dirham',		N'درهم إماراتي',	2),
(4, N'SAR', N'Riyal',		N'ريال',				N'Saudi Riyal',			N'ريال سعودي',			2),
(5, N'CNY', N'Yuan',		N'يوان',			N'Chinese Yuan',		N'يوان صيني',		2),
(6, N'SDG', N'S.Pound',		N'جنيه س',			N'Sudanese Pound',		N'جنيه سوداني',		2),
(7, N'EUR', N'EUR',			N'يورو',			N'Euro',				N'يورو',			2);

ELSE IF @DB = N'101' -- Banan SD, USD, en
INSERT INTO @Currencies([Index],
	[Id],	[Name],			[Name2],			[Description],			[Description2],		[E]) VALUES
(0, N'USD', N'USD',			N'دولار',			N'United States Dollar',N'دولار أمريكي',		2),
--(1, N'ETB', N'Birr',		N'بر',				N'Ethiopian Birr',		N'بر أثيوبي',		2),
--(2, N'GBP', N'Pound',		N'جنيه',			N'Sterling Pound',		N'جنيه استرليني',	2),
(3, N'AED', N'Dirham',		N'درهم',			N'Emirates Dirham',		N'درهم إماراتي',	2),
(4, N'SAR', N'Riyal',		N'ريال',				N'Saudi Riyal',			N'ريال سعودي',			2),
--(5, N'CNY', N'Yuan',		N'يوان',			N'Chinese Yuan',		N'يوان صيني',		2),
(6, N'SDG', N'Pound',		N'جنيه',			N'Sudanese Pound',		N'جنيه سوداني',		2);
--(7, N'EUR', N'EUR',		N'يورو',			N'Euro',				N'يورو',			2);

ELSE IF @DB = N'102' -- Banan ET, ETB, en
INSERT INTO @Currencies([Index],
	[Id],	[Name],			[Name2],			[Description],			[Description2],		[E]) VALUES
(0, N'USD', N'USD',			N'دولار',			N'United States Dollar',N'دولار أمريكي',		2),
(1, N'ETB', N'Birr',		N'بر',				N'Ethiopian Birr',		N'بر أثيوبي',		2),
(2, N'GBP', N'Pound',		N'جنيه',			N'Sterling Pound',		N'جنيه استرليني',	2),
(3, N'AED', N'Dirham',		N'درهم',			N'Emirates Dirham',		N'درهم إماراتي',	2),
(4, N'SAR', N'Riyal',		N'ريال',				N'Saudi Riyal',			N'ريال سعودي',			2);
--(5, N'CNY', N'Yuan',		N'يوان',			N'Chinese Yuan',		N'يوان صيني',		2),
--(6, N'SDG', N'Pound',		N'جنيه',			N'Sudanese Pound',		N'جنيه سوداني',		2),
--(7, N'EUR', N'EUR',		N'يورو',			N'Euro',				N'يورو',			2);

ELSE IF @DB = N'103' -- Lifan Cars, SAR, en/ar/zh
INSERT INTO @Currencies([Index],
	[Id],	[Name],			[Name2],			[Description],			[Description2],		[E]) VALUES
(0, N'USD', N'USD',			N'دولار',			N'United States Dollar',N'دولار أمريكي',		2),
(1, N'ETB', N'Birr',		N'بر',				N'Ethiopian Birr',		N'بر أثيوبي',		2),
--(2, N'GBP', N'Pound',		N'جنيه',			N'Sterling Pound',		N'جنيه استرليني',	2),
--(3, N'AED', N'Dirham',		N'درهم',			N'Emirates Dirham',		N'درهم إماراتي',	2),
(4, N'SAR', N'Riyal',		N'ريال',				N'Saudi Riyal',			N'ريال سعودي',			2),
(5, N'CNY', N'Yuan',		N'يوان',			N'Chinese Yuan',		N'يوان صيني',		2);
--(6, N'SDG', N'Pound',		N'جنيه',			N'Sudanese Pound',		N'جنيه سوداني',		2),
--(7, N'EUR', N'EUR',			N'يورو',			N'Euro',				N'يورو',			2);

ELSE IF @DB = N'104' -- Walia Steel, ETB, en/am
INSERT INTO @Currencies([Index],
	[Id],	[Name],			[Name2],			[Description],			[Description2],		[E]) VALUES
(0, N'USD', N'USD',			N'دولار',			N'United States Dollar',N'دولار أمريكي',		2),
(1, N'ETB', N'Birr',		N'بر',				N'Ethiopian Birr',		N'بر أثيوبي',		2),
--(2, N'GBP', N'Pound',		N'جنيه',			N'Sterling Pound',		N'جنيه استرليني',	2),
--(3, N'AED', N'Dirham',		N'درهم',			N'Emirates Dirham',		N'درهم إماراتي',	2),
--(4, N'SAR', N'Riyal',		N'ريال',				N'Saudi Riyal',			N'ريال سعودي',			2),
--(5, N'CNY', N'Yuan',		N'يوان',			N'Chinese Yuan',		N'يوان صيني',		2),
--(6, N'SDG', N'Pound',		N'جنيه',			N'Sudanese Pound',		N'جنيه سوداني',		2),
(7, N'EUR', N'EUR',			N'يورو',			N'Euro',				N'يورو',			2);

ELSE IF @DB = N'105' -- Simpex, SAR, en/ar
INSERT INTO @Currencies([Index],
	[Id],	[Name],			[Name2],			[Description],			[Description2],		[E]) VALUES
(0, N'USD', N'USD',			N'دولار',			N'United States Dollar',N'دولار أمريكي',		2),
--(1, N'ETB', N'Birr',		N'بر',				N'Ethiopian Birr',		N'بر أثيوبي',		2),
--(2, N'GBP', N'Pound',		N'جنيه',			N'Sterling Pound',		N'جنيه استرليني',	2),
(3, N'AED', N'Dirham',		N'درهم',			N'Emirates Dirham',		N'درهم إماراتي',	2),
(4, N'SAR', N'Riyal',		N'ريال',				N'Saudi Riyal',			N'ريال سعودي',			2),
--(5, N'CNY', N'Yuan',		N'يوان',			N'Chinese Yuan',		N'يوان صيني',		2),
--(6, N'SDG', N'Pound',		N'جنيه',			N'Sudanese Pound',		N'جنيه سوداني',		2),
(7, N'EUR', N'EUR',			N'يورو',			N'Euro',				N'يورو',			2);

EXEC [api].Currencies__Save
	@Entities = @Currencies,
	@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

IF @ValidationErrorsJson IS NOT NULL 
BEGIN
	Print 'Currencies: Inserting: ' + @ValidationErrorsJson
	GOTO Err_Label;
END;						
EXEC master.sys.sp_set_session_context 'FunctionalCurrencyId', @FunctionalCurrencyId;

--DECLARE @ActiveCurrencies IndexedStringList;
--INSERT INTO @ActiveCurrencies([Index], [Id]) VALUES 
--(0,N'GBP'),
--(1,N'AED'),
--(2,N'SAR');

--EXEC api.Currencies__Activate
--	@IndexedIds = @ActiveCurrencies,
--	@IsActive = 0,
--	@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

--DECLARE @DeletedCurrencies IndexedStringList;
--INSERT INTO @DeletedCurrencies
--([Index],	[Id]) VALUES 
--(0,			N'GBP'), 
--(1,			N'SAR');

--EXEC [api].Currencies__Delete
--	@IndexedIds = @DeletedCurrencies,
--	@ValidationErrorsJson = @ValidationErrorsJson OUTPUT;

IF @DebugCurrencies = 1
BEGIN
	SELECT * FROM map.Currencies();
	SELECT * FROM map.Resources();
END
