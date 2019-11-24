﻿CREATE PROCEDURE [dbo].[rpt_ERCA__VAT_SalesDeclaration] -- used for manual declaration
	@fromDate Date = '01.01.2000', 
	@toDate Date = '01.01.2100',
	@AccountId INT
AS
BEGIN
	SELECT 
		A.[Name] As [Customer], 
		A.TaxIdentificationNumber As TIN, 
		J.ExternalReference As [Invoice #], J.[AdditionalReference] As [Cash M/C #],
		SUM(J.[MonetaryValue]) AS VAT, SUM(J.[RelatedAmount]) AS [Taxable Amount],
		J.DocumentDate As [Invoice Date], J.[DocumentLineId]
	FROM dbo.[fi_Journal](@fromDate, @toDate) J
	LEFT JOIN dbo.Agents A ON J.[RelatedAgentId] = A.Id
	WHERE J.[AccountId]  = @AccountId
	AND J.Direction = -1
	GROUP BY
		A.[Name],
		A.TaxIdentificationNumber,
		J.ExternalReference, J.[AdditionalReference],
		J.DocumentDate,	J.[DocumentLineId]
END;
GO;