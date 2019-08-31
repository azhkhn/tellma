﻿BEGIN -- Cleanup & Declarations

	DECLARE @LineType NVARCHAR (255), @DocsIdList dbo.[IdList];
END
-- get acceptable document types; and user permissions and general settings;
-- Journal Vouchers
DECLARE @VR1_2 VTYPE, @VRU_3 VTYPE, @Frequency NVARCHAR (255), @P1_2 int, @P1_U int, @PU_3 int, @P2_3 int,
		@date1 date = '2017.02.01', @date2 date = '2022.02.01', @dU datetime = '2018.02.01', @date3 datetime = '2023.02.01';
		:r .\11_Financing.sql
		--:r .\12_Purchasing.sql
		--:r .\21_Financing.sql
		--:r .\30_HRCycle.sql
		--:r .\40_PurchasingCycle.sql
		--:r .\13_ProductionCycle.sql
		--:r .\14_SalesCycle.sql
		--:r .\12_ManualMisc.sql
SELECT @fromDate = '2017.01.01', @toDate = '2017.01.31'

INSERT INTO dbo.ExchangeRatesHistory([BaseCurrency], [TargetCurrency], [ExchangeRate])
VALUES(N'USD', N'ETB', 30);
--SELECT * from [fi_Journal](@fromDate, @toDate) ORDER BY [Id], [EntryId];
--EXEC rpt_TrialBalance @fromDate = @fromDate, @toDate = @toDate, @PrintQuery=1;
--SELECT * FROM dbo.Documents;
--EXEC rpt_TrialBalance @fromDate = @fromDate, @toDate = @toDate, @ByCustody = 1, @ByResource = 1, @PrintQuery = 0;

--select * FROM Documents where  Id in (Select Id from @Docs);
--SElect * from lines where DocumentId in (Select Id from @Docs);
--select * from entries where lineid in (select id from lines where DocumentId in (Select Id from @Docs));