﻿CREATE PROCEDURE [dbo].[MarkRepositoryIssuesAsFullyImported]
	@RepositoryId BIGINT
AS
BEGIN
  -- SET NOCOUNT ON added to prevent extra result sets from
  -- interfering with SELECT statements.
  SET NOCOUNT ON

  UPDATE Repositories
    SET IssuesFullyImported = 1
  WHERE Id = @RepositoryId AND IssuesFullyImported = 0

  IF(@@ROWCOUNT > 0)
  BEGIN
    -- Update sync log. We have to do this as nothing else may have changed besides
    -- our realization that we're finished, so we have to update the client.
    UPDATE SyncLog SET
      [RowVersion] = DEFAULT
    WHERE ItemType = 'repository' AND ItemId = @RepositoryId

    SELECT 'repo' as ItemType, @RepositoryId as ItemId
  END
END
