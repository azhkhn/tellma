﻿CREATE PROCEDURE [bll].[Documents_Validate__Close]
	@Ids [dbo].[IndexedIdList] READONLY,
	@Top INT = 10
AS
SET NOCOUNT ON;
	DECLARE @ValidationErrors [dbo].[ValidationErrorList], @UserId INT = CONVERT(INT, SESSION_CONTEXT(N'UserId'));

	-- Document Date not before last archive date (C#)
	-- Posting date must not be within Archived period (C#)

	-- Cannot file with no lines
	INSERT INTO @ValidationErrors([Key], [ErrorName])
	SELECT TOP (@Top)
		'[' + CAST([Index] AS NVARCHAR (255)) + ']',
		N'Error_TheDocumentHasNoLines'
	FROM @Ids 
	WHERE [Id] NOT IN (
		SELECT DISTINCT [DocumentId] 
		FROM dbo.[Lines]
	);

	-- All lines must be in their final states.
	WITH DocumentsLineDefinitions AS
	(
		SELECT DISTINCT DL.[DefinitionId] FROM 
		dbo.[Lines] DL
		JOIN @Ids D ON DL.DocumentId = D.[Id]
	),
	WorkflowsFinalStateIds AS
	(
		SELECT LineDefinitionId, MAX([dbo].[fn_State__StateId]([ToState])) AS FinalStateId
		FROM dbo.Workflows
		WHERE LineDefinitionId IN (SELECT [DefinitionId] FROM DocumentsLineDefinitions)
		GROUP BY LineDefinitionId
	),
	WorkflowsFinalStates AS
	(
		SELECT LineDefinitionId, [dbo].[fn_StateId__State](FinalStateId) AS FinalState
		FROM WorkflowsFinalStateIds
	)
	INSERT INTO @ValidationErrors([Key], [ErrorName], [Argument0])
	SELECT TOP (@Top)
		'[' + CAST([Index] AS NVARCHAR (255)) + '].Lines[' +
			CAST(DL.[Id] AS NVARCHAR (255)) + ']',
		N'Error_State0IsNotFinal',
		dbo.fn_StateId__State(DL.[State])
	FROM @Ids D
	JOIN dbo.[Lines] DL ON DL.[DocumentId] = D.[Id]
	JOIN WorkflowsFinalStates WFS ON DL.[DefinitionId] = WFS.[LineDefinitionId]
	--WHERE DL.[State] NOT IN (N'Void', N'Rejected', N'Failed', N'Invalid', WFS.FinalState)
	WHERE DL.[State] >= 0 AND DL.[State] <> WFS.FinalState

	-- Cannot close a document with non-balanced (Reviewed) lines
	INSERT INTO @ValidationErrors([Key], [ErrorName], [Argument0])
	SELECT TOP (@Top)
		'[' + ISNULL(CAST(FE.[Index] AS NVARCHAR (255)),'') + ']', 
		N'Error_TransactionHasDebitCreditDifference0',
		FORMAT(SUM(E.[Direction] * E.[Value]), '##,#;(##,#);-', 'en-us') AS NetDifference
	FROM @Ids FE
	JOIN dbo.[Lines] L ON FE.[Id] = L.[DocumentId]
	JOIN dbo.[Entries] E ON L.[Id] = E.[LineId]
	WHERE L.[State] = +4 -- N'Reviewed'
	GROUP BY FE.[Index]
	HAVING SUM(E.[Direction] * E.[Value]) <> 0;

	SELECT TOP (@Top) * FROM @ValidationErrors;