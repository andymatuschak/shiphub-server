﻿CREATE PROCEDURE [dbo].[BulkUpdateIssues]
  @RepositoryId BIGINT,
  @Issues IssueTableType READONLY,
  @Labels LabelTableType READONLY
AS
BEGIN
  -- SET NOCOUNT ON added to prevent extra result sets from
  -- interfering with SELECT statements.
  SET NOCOUNT ON;

  DECLARE @Changes TABLE (
    [IssueId] BIGINT NOT NULL PRIMARY KEY CLUSTERED
  );

  MERGE INTO Issues as [Target]
  USING (
    SELECT [Id], [UserId], [Number], [State], [Title], [Body], [AssigneeId], [MilestoneId], [Locked], [CreatedAt], [UpdatedAt], [ClosedAt], [ClosedById], [Reactions]
    FROM @Issues
  ) as [Source]
  ON ([Target].[Id] = [Source].[Id])
  -- Add
  WHEN NOT MATCHED BY TARGET THEN
    INSERT ([Id], [UserId], [RepositoryId], [Number], [State], [Title], [Body], [AssigneeId], [MilestoneId], [Locked], [CreatedAt], [UpdatedAt], [ClosedAt], [ClosedById], [Reactions])
    VALUES ([Id], [UserId], @RepositoryId, [Number], [State], [Title], [Body], [AssigneeId], [MilestoneId], [Locked], [CreatedAt], [UpdatedAt], [ClosedAt], [ClosedById], [Reactions])
  -- Update
  WHEN MATCHED AND [Target].[UpdatedAt] < [Source].[UpdatedAt] THEN
    UPDATE SET
      [UserId] = [Source].[UserId], -- This can change to ghost
      [State] = [Source].[State],
      [Title] = [Source].[Title],
      [Body] = [Source].[Body],
      [AssigneeId] = [Source].[AssigneeId],
      [MilestoneId] = [Source].[MilestoneId],
      [Locked] = [Source].[Locked],
      [UpdatedAt] = [Source].[UpdatedAt],
      [ClosedAt] = [Source].[ClosedAt],
      [ClosedById] = [Source].[ClosedById],
      [Reactions] = [Source].[Reactions]
  OUTPUT INSERTED.Id INTO @Changes (IssueId);

  EXEC [dbo].[BulkCreateLabels] @Labels = @Labels

  MERGE INTO IssueLabels as [Target]
  USING (SELECT L1.Id as LabelId, L2.ItemId as IssueId
    FROM Labels as L1
      INNER JOIN @Labels as L2 ON (L1.Color = L2.Color AND L1.Name = L2.Name)
  ) as [Source]
  ON ([Target].LabelId = [Source].LabelId AND [Target].IssueId = [Source].IssueId)
  -- Add
  WHEN NOT MATCHED BY TARGET THEN
    INSERT (IssueId, LabelId)
    VALUES (IssueId, LabelId)
  -- Delete
  WHEN NOT MATCHED BY SOURCE
    AND [Target].IssueId IN (SELECT DISTINCT(ItemId) FROM @Labels)
    THEN DELETE;

  -- Add issue changes to log
  MERGE INTO RepositoryLog as [Target]
  USING (SELECT IssueId as Id FROM @Changes) as [Source]
  ON ([Target].RepositoryId = @RepositoryId
    AND [Target].[Type] = 'issue'
    AND [Target].ItemId = [Source].Id)
  -- Insert
  WHEN NOT MATCHED BY TARGET THEN
    INSERT (RepositoryId, [Type], ItemId, [Delete])
    VALUES (@RepositoryId, 'issue', Id, 0)
  -- Update/Delete
  WHEN MATCHED THEN
    UPDATE SET [RowVersion] = NULL; -- Causes new ID to be assigned by trigger

  -- Add new account references to log
  -- Removed account references are leaked or GC'd later by another process.
  MERGE INTO RepositoryLog as [Target]
  USING (
    SELECT Distinct(UPUserId) as UserId
    FROM Issues as c
        INNER JOIN @Changes as ch ON (c.Id = ch.IssueId)
      UNPIVOT (UPUserId FOR [Role] IN (UserId, AssigneeId, ClosedById)) as [Ignored]
  ) as [Source]
  ON ([Target].RepositoryId = @RepositoryId
    AND [Target].[Type] = 'account'
    AND [Target].ItemId = [Source].UserId)
  -- Insert
  WHEN NOT MATCHED BY TARGET THEN
    INSERT (RepositoryId, [Type], ItemId, [Delete])
    VALUES (@RepositoryId, 'account', [Source].UserId, 0);
END
