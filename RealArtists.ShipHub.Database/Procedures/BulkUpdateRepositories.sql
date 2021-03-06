﻿CREATE PROCEDURE [dbo].[BulkUpdateRepositories]
  @Date DATETIMEOFFSET,
  @Repositories RepositoryTableType READONLY
AS
BEGIN
  -- SET NOCOUNT ON added to prevent extra result sets from
  -- interfering with SELECT statements.
  SET NOCOUNT ON

  -- For tracking required updates to sync log
  DECLARE @Changes TABLE (
    [Id]        BIGINT NOT NULL PRIMARY KEY CLUSTERED,
    [AccountId] BIGINT NOT NULL INDEX IX_Account NONCLUSTERED
  )

  -- Check for FullName collisions and assume the existing repo has been deleted
  DECLARE @DeletedRepositories ItemListTableType
  
  BEGIN TRY
    BEGIN TRANSACTION

    INSERT INTO @DeletedRepositories (Item)
    SELECT r.Id
    FROM @Repositories as rvar
      INNER JOIN Repositories as r ON (r.FullName = rvar.FullName AND r.Id != rvar.Id)
  
    IF EXISTS (SELECT * FROM @DeletedRepositories)
    BEGIN
      EXEC [dbo].[DeleteRepositories] @Repositories = @DeletedRepositories
    END

    MERGE INTO Repositories WITH (SERIALIZABLE) as [Target]
    USING (
      SELECT Id, AccountId, [Private], [Name], FullName, Size, HasIssues, HasProjects, [Disabled], Archived, AllowMergeCommit, AllowRebaseMerge, AllowSquashMerge
      FROM @Repositories
    ) as [Source]
    ON ([Target].Id = [Source].Id)
    WHEN NOT MATCHED BY TARGET THEN
      INSERT (Id, AccountId, [Private], [Name], FullName, Size, [Date], HasIssues, HasProjects, [Disabled], Archived, AllowMergeCommit, AllowRebaseMerge, AllowSquashMerge)
      VALUES (Id, AccountId, [Private], [Name], FullName, Size, @Date,  HasIssues, HasProjects, ISNULL([Disabled], 0), Archived, ISNULL(AllowMergeCommit, 1), ISNULL(AllowRebaseMerge, 1), ISNULL(AllowSquashMerge, 1))
    WHEN MATCHED
      AND [Target].[Date] < @Date
      AND EXISTS (
        SELECT [Target].AccountId, [Target].[Private], [Target].[Name], [Target].FullName, [Target].Size, [Target].HasIssues, [Target].HasProjects, [Target].[Disabled], [Target].Archived, [Target].AllowMergeCommit, [Target].AllowRebaseMerge, [Target].AllowSquashMerge
        EXCEPT
        SELECT [Source].AccountId, [Source].[Private], [Source].[Name], [Source].FullName, [Source].Size, [Source].HasIssues, [Source].HasProjects, ISNULL([Source].[Disabled], [Target].[Disabled]), [Source].Archived, ISNULL([Source].AllowMergeCommit, [Target].AllowMergeCommit), ISNULL([Source].AllowRebaseMerge, [Target].AllowRebaseMerge), ISNULL([Source].AllowSquashMerge, [Target].AllowSquashMerge)
      ) THEN
      UPDATE SET
        AccountId = [Source].AccountId,
        [Private] = [Source].[Private],
        [Name] = [Source].[Name],
        FullName = [Source].FullName,
        Size = [Source].Size,
        [Date] = @Date,
        HasIssues = [Source].HasIssues,
        HasProjects = [Source].HasProjects,
        [Disabled] = ISNULL([Source].[Disabled], [Target].[Disabled]),
        Archived = [Source].Archived,
        AllowMergeCommit = ISNULL([Source].AllowMergeCommit, [Target].AllowMergeCommit),
        AllowRebaseMerge = ISNULL([Source].AllowRebaseMerge, [Target].AllowRebaseMerge),
        AllowSquashMerge = ISNULL([Source].AllowSquashMerge, [Target].AllowSquashMerge)
    OUTPUT INSERTED.Id, INSERTED.AccountId INTO @Changes
    OPTION (LOOP JOIN, FORCE ORDER);

    -- Bump existing repos
    UPDATE SyncLog SET
      [RowVersion] = DEFAULT
    WHERE ItemType = 'repository'
      AND ItemId IN (SELECT Id FROM @Changes)

    -- New repositories reference themselves
    INSERT INTO SyncLog (OwnerType, OwnerId, ItemType, ItemId, [Delete])
    SELECT 'repo', Id, 'repository', Id, 0
    FROM @Changes as c
    WHERE NOT EXISTS (
      SELECT * FROM SyncLog
      WHERE OwnerType = 'repo' AND OwnerId = c.Id AND ItemType = 'repository' AND ItemId = c.Id)

    -- Best to inline owners too
    INSERT INTO SyncLog (OwnerType, OwnerId, ItemType, ItemId, [Delete])
    SELECT 'repo', Id, 'account', AccountId, 0
    FROM @Changes as c
    WHERE NOT EXISTS (
      SELECT * FROM SyncLog
      WHERE OwnerType = 'repo' AND OwnerId = c.Id AND ItemType = 'account' AND ItemId = c.AccountId)

    COMMIT TRANSACTION
  END TRY
  BEGIN CATCH
    IF (XACT_STATE() != 0) ROLLBACK TRANSACTION;
    THROW;
  END CATCH

  -- Return sync notifications
  SELECT 'repo' as ItemType, Id as ItemId FROM @Changes
END
