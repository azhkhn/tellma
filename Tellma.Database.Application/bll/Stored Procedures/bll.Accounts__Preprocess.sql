﻿CREATE PROCEDURE [bll].[Accounts__Preprocess]
	@Entities [dbo].[AccountList] READONLY
AS
SET NOCOUNT ON;

DECLARE @ProcessedEntities [dbo].[AccountList];
INSERT INTO @ProcessedEntities SELECT * FROM @Entities;

-- If there is only ONE active responsibility center set the account to it
IF (SELECT COUNT(*) FROM [dbo].[ResponsibilityCenters] WHERE [IsActive] = 1 AND [IsLeaf] = 1) = 1
UPDATE @ProcessedEntities
SET [ResponsibilityCenterId] = (SELECT [Id] FROM [dbo].[ResponsibilityCenters] WHERE [IsActive] = 1 AND [IsLeaf] = 1);

-- If AccountType.IsPersonal = 0, set IsRelated and HasAgent to 0
UPDATE A
SET A.[IsRelated] = 0, [HasAgent] = 0
FROM @ProcessedEntities A JOIN [AccountTypes] T ON A.[AccountTypeId] = T.Id
WHERE T.[IsPersonal] = 0;

-- If HasAgent = 0, Set AgentId and AgentDefinitionId to NULL (This depends on the previous step)
UPDATE @ProcessedEntities
SET [AgentId] = NULL, [AgentDefinitionId] = NULL
WHERE [HasAgent] = 0;

-- If AgentId is set, then AgentDefinitionId is auto determined
UPDATE A
SET A.[AgentDefinitionId] = AG.[DefinitionId]
FROM @ProcessedEntities A JOIN [dbo].[Agents] AG ON A.[AgentId] = AG.[Id]
WHERE A.[AgentId] IS NOT NULL;

-- If AccountType.IsReal = 0, set HasResource to 0
UPDATE A
SET [HasResource] = 0
FROM @ProcessedEntities A JOIN [AccountTypes] T ON A.[AccountTypeId] = T.Id
WHERE T.[IsReal] = 0;

-- If HasResource = 0, Set AgentId and AgentDefinitionId to NULL (This depends on the previous step)
UPDATE @ProcessedEntities
SET [ResourceId] = NULL
WHERE [HasResource] = 0;

-- If IsCurrent is set in Account Type, then copy it to Account
UPDATE A
SET A.[IsCurrent] = T.[IsCurrent]
FROM @ProcessedEntities A JOIN [AccountTypes] T ON A.[AccountTypeId] = T.Id
WHERE T.[IsCurrent] IS NOT NULL;

-- If Resource has a CurrencyId, copy it to Account
UPDATE A
SET A.[CurrencyId] = R.[CurrencyId]
FROM @ProcessedEntities A JOIN [Resources] R ON A.[ResourceId] = R.Id
WHERE R.[CurrencyId] IS NOT NULL;

-- Return the result
SELECT * FROM @ProcessedEntities;
