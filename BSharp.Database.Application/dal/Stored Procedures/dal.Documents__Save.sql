﻿CREATE PROCEDURE [dal].[Documents__Save]
	@DocumentTypeId NVARCHAR(50),
	@Documents [dbo].[DocumentList] READONLY,
	@Lines [dbo].[DocumentLineList] READONLY, 
	@Entries [dbo].[DocumentLineEntryList] READONLY,
	@ReturnIds BIT = 0
AS
BEGIN
	DECLARE @DocumentsIndexedIds [dbo].[IndexedIdList], @LinesIndexedIds [dbo].[IndexedIdList], @EntriesIndexedIds [dbo].[IndexedIdList];

	DECLARE @Now DATETIMEOFFSET(7) = SYSDATETIMEOFFSET();
	DECLARE @UserId INT = CONVERT(INT, SESSION_CONTEXT(N'UserId'));

	INSERT INTO @DocumentsIndexedIds([Index], [Id])
	SELECT x.[Index], x.[Id]
	FROM
	(
		MERGE INTO [dbo].[Documents] AS t
		USING (
			SELECT 
				[Index], [Id], [DocumentDate], [VoucherNumericReference], [Memo], [EvidenceTypeId], [Frequency], [Repetitions],
				ROW_Number() OVER (PARTITION BY [Id] ORDER BY [Index]) + (
					-- max(SerialNumber) per document type.
					SELECT ISNULL(MAX([SerialNumber]), 0) FROM dbo.Documents WHERE [DocumentTypeId] = @DocumentTypeId
				) As [SerialNumber]
			FROM @Documents D
		) AS s ON (t.Id = s.Id)
		WHEN MATCHED
		THEN
			UPDATE SET
				t.[DocumentDate]			= s.[DocumentDate],
				t.[VoucherNumericReference]	= s.[VoucherNumericReference],
				t.[Memo]					= s.[Memo],
				t.[EvidenceTypeId]			= s.[EvidenceTypeId],
				t.[Frequency]				= s.[Frequency],
				t.[Repetitions]				= s.[Repetitions],

				t.[ModifiedAt]				= @Now,
				t.[ModifiedById]			= @UserId
		WHEN NOT MATCHED THEN
			INSERT (
				[DocumentTypeId], [SerialNumber], [DocumentDate], [VoucherNumericReference], [Memo], [EvidenceTypeId], [Frequency], [Repetitions]
			)
			VALUES (
				@DocumentTypeId, s.[SerialNumber], s.[DocumentDate], s.[VoucherNumericReference], s.[Memo], s.[EvidenceTypeId], s.[Frequency], s.[Repetitions]
			)
		OUTPUT s.[Index], inserted.[Id] 
	) As x;
	
	---- Assign the new ones to self
	INSERT INTO dbo.DocumentAssignments(DocumentId, AssigneeId)
	SELECT Id, @UserId
	FROM @DocumentsIndexedIds
	WHERE [Index] IN (SELECT [Index] FROM @Documents WHERE [Id] = 0);

	WITH BL AS (
		SELECT * FROM dbo.[DocumentLines]
		WHERE DocumentId IN (SELECT [Id] FROM @DocumentsIndexedIds)
	)
	INSERT INTO @LinesIndexedIds([Index], [Id])
	SELECT x.[Index], x.[Id]
	FROM
	(
		MERGE INTO BL AS t
		USING (
			SELECT L.[Index], L.[Id], DI.Id AS DocumentId, L.[LineTypeId], L.[TemplateLineId], L.[ScalingFactor], L.[SortKey]
			FROM @Lines L
			JOIN @DocumentsIndexedIds DI ON L.[DocumentIndex] = DI.[Index]
		) AS s ON (t.Id = s.Id)
		WHEN MATCHED THEN
			UPDATE SET
				t.[LineTypeId]		= s.[LineTypeId],
				t.[TemplateLineId]	= s.[TemplateLineId], 
				t.[ScalingFactor]	= s.[ScalingFactor],
				t.[SortKey]			= s.[SortKey],
				t.[ModifiedAt]		= @Now,
				t.[ModifiedById]	= @UserId
		WHEN NOT MATCHED THEN
			INSERT ([DocumentId], [LineTypeId], [TemplateLineId], [ScalingFactor], [SortKey])
			VALUES (s.[DocumentId], s.[LineTypeId], s.[TemplateLineId], s.[ScalingFactor], s.[SortKey])
		WHEN NOT MATCHED BY SOURCE THEN
			DELETE
		OUTPUT s.[Index], inserted.[Id] 
	) AS x
	WHERE [Index] IS NOT NULL;

	WITH BE AS (
		SELECT * FROM dbo.[DocumentLineEntries]
		WHERE [DocumentLineId] IN (SELECT [Id] FROM @LinesIndexedIds)
	)
	MERGE INTO BE AS t
	USING (
		SELECT
			E.[Index], E.[Id], LI.Id AS [DocumentLineId], [EntryNumber], [Direction], [AccountId], [IfrsEntryClassificationId],
				[AgentId], [ResponsibilityCenterId], [ResourceId], [ResourcePickId], [BatchCode], [DueDate], [Quantity],
				[MonetaryValue], E.[Mass], E.[Volume], E.[Area], E.[Length], E.[Time], E.[Count], E.[Value], E.[Memo],
				[ExternalReference], [AdditionalReference], 
				[RelatedResourceId], [RelatedAgentId], [RelatedMoneyAmount]
		FROM @Entries E
		JOIN @DocumentsIndexedIds DI ON E.[DocumentIndex] = DI.[Index]
		JOIN @LinesIndexedIds LI ON E.[DocumentLineIndex] = LI.[Index]
	) AS s ON (t.Id = s.Id)
	WHEN MATCHED THEN
		UPDATE SET
			t.[Direction]				= s.[Direction],	
			t.[AccountId]				= s.[AccountId],
			t.[IfrsEntryClassificationId]= s.[IfrsEntryClassificationId],
			t.[AgentId]					= s.[AgentId],
			t.[ResponsibilityCenterId]	= s.[ResponsibilityCenterId],
			t.[ResourceId]				= s.[ResourceId],
			t.[ResourcePickId]			= s.[ResourcePickId],
			t.[BatchCode]				= s.[BatchCode],
			t.[Quantity]				= s.[Quantity],
			t.[MonetaryValue]			= s.[MonetaryValue],
			t.[Mass]					= s.[Mass],
			t.[Volume]					= s.[Volume],
			t.[Area]					= s.[Area],
			t.[Length]					= s.[Length],
			t.[Time]					= s.[Time],
			t.[Count]					= s.[Count],
			t.[Value]					= s.[Value],
			t.[Memo]					= s.[Memo],
			t.[ExternalReference]		= s.[ExternalReference],
			t.[AdditionalReference]		= s.[AdditionalReference],
			t.[RelatedResourceId]		= s.[RelatedResourceId],
			t.[RelatedAccountId]		= s.[RelatedAgentId],
			t.[RelatedMoneyAmount]		= s.[RelatedMoneyAmount],
			t.[ModifiedAt]				= @Now,
			t.[ModifiedById]			= @UserId
	WHEN NOT MATCHED THEN
		INSERT ([DocumentLineId], [EntryNumber], [Direction], [AccountId], [IfrsEntryClassificationId],
				[AgentId], [ResponsibilityCenterId], [ResourceId], [ResourcePickId], [BatchCode], [Quantity],
				[MonetaryValue], [Mass], [Volume], [Area], [Length], [Time], [Count],  [Value], [Memo],
				[ExternalReference], [AdditionalReference], [RelatedResourceId], [RelatedAccountId], [RelatedMoneyAmount])
		VALUES (s.[DocumentLineId], s.[EntryNumber], s.[Direction], s.[AccountId], s.[IfrsEntryClassificationId],
				s.[AgentId], s.[ResponsibilityCenterId], s.[ResourceId], s.[ResourcePickId], s.[BatchCode], s.[Quantity],
				s.[MonetaryValue], s.[Mass], s.[Volume], s.[Area], s.[Length], s.[Time], s.[Count], s.[Value], s.[Memo],
				s.[ExternalReference], s.[AdditionalReference], s.[RelatedResourceId], s.[RelatedAgentId], s.[RelatedMoneyAmount])
	WHEN NOT MATCHED BY SOURCE THEN
		DELETE;

	IF (@ReturnIds = 1)
		SELECT * FROM @DocumentsIndexedIds;
END;