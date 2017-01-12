﻿namespace RealArtists.ShipHub.Actors {
  using System;
  using System.Collections.Generic;
  using System.Data.Entity;
  using System.Linq;
  using System.Net;
  using System.Threading.Tasks;
  using ActorInterfaces;
  using ActorInterfaces.GitHub;
  using AutoMapper;
  using Common;
  using Common.DataModel;
  using Common.DataModel.Types;
  using Common.GitHub;
  using Orleans;
  using QueueClient;

  public class UserActor : Grain, IUserActor {
    public static readonly TimeSpan SyncDelay = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan SyncIdle = TimeSpan.FromSeconds(SyncDelay.TotalSeconds * 3);

    private IMapper _mapper;
    private IGrainFactory _grainFactory;
    private IFactory<ShipHubContext> _contextFactory;
    private IShipHubQueueClient _queueClient;

    private long _userId;
    private IGitHubActor _github;

    // MetaData
    private GitHubMetadata _metadata;
    private GitHubMetadata _repoMetadata;
    private GitHubMetadata _orgMetadata;

    // Sync logic
    private DateTimeOffset _lastSyncInterest;
    private IDisposable _syncTimer;

    public UserActor(IMapper mapper, IGrainFactory grainFactory, IFactory<ShipHubContext> contextFactory, IShipHubQueueClient queueClient) {
      _mapper = mapper;
      _grainFactory = grainFactory;
      _contextFactory = contextFactory;
      _queueClient = queueClient;
    }

    public override async Task OnActivateAsync() {
      using (var context = _contextFactory.CreateInstance()) {
        _userId = this.GetPrimaryKeyLong();

        // Ensure this user actually exists, and lookup their token.
        var user = await context.Users.SingleOrDefaultAsync(x => x.Id == _userId);

        if (user == null) {
          throw new InvalidOperationException($"User {_userId} does not exist and cannot be activated.");
        }

        if (user.Token.IsNullOrWhiteSpace()) {
          throw new InvalidOperationException($"User {_userId} has an invalid token and cannot be activated.");
        }

        _metadata = user.Metadata;
        _repoMetadata = user.RepositoryMetadata;
        _orgMetadata = user.OrganizationMetadata;

        _github = _grainFactory.GetGrain<IGitHubActor>(user.Id);
      }

      await base.OnActivateAsync();
    }

    public override async Task OnDeactivateAsync() {
      _syncTimer?.Dispose();
      _syncTimer = null;

      using (var context = _contextFactory.CreateInstance()) {
        // I think all we need to persist is the metadata.
        await context.UpdateMetadata("Accounts", _userId, _metadata);
        await context.UpdateMetadata("Accounts", "RepoMetadataJson", _userId, _repoMetadata);
        await context.UpdateMetadata("Accounts", "OrgMetadataJson", _userId, _orgMetadata);
      }

      // TODO: Look into how agressively Orleans deactivates "inactive" grains.
      // We may need to delay deactivation based on sync interest.

      await base.OnDeactivateAsync();
    }

    public Task Sync() {
      // For now, calls to sync just indicate interest in syncing.
      // Rather than sync here, we just ensure that a timer is registered.
      _lastSyncInterest = DateTimeOffset.UtcNow;

      if (_syncTimer == null) {
        _syncTimer = RegisterTimer(SyncCallback, null, TimeSpan.Zero, SyncDelay);
      }

      return Task.CompletedTask;
    }

    public Task ForceSyncRepositories() {
      return TimerSync(forceRepos: true);
    }

    // Implementation methods

    private async Task SyncCallback(object state) {
      if (DateTimeOffset.UtcNow.Subtract(_lastSyncInterest) > SyncIdle) {
        DeactivateOnIdle();
        return;
      }

      await TimerSync(forceRepos: false);
    }

    private async Task TimerSync(bool forceRepos) {
      if (_github == null) {
        return;
      }

      var tasks = new List<Task>();
      var changes = new ChangeSummary();
      using (var context = _contextFactory.CreateInstance()) {

        // User
        if (_metadata == null || _metadata.Expires < DateTimeOffset.UtcNow) {
          var user = await _github.User(_metadata);

          if (user.IsOk) {
            changes.UnionWith(
              await context.UpdateAccount(user.Date, _mapper.Map<AccountTableType>(user.Result))
            );
          }

          // Don't update until saved.
          _metadata = GitHubMetadata.FromResponse(user);
        }

        // TODO: Don't think this should actually happen this often
        tasks.Add(_queueClient.BillingGetOrCreatePersonalSubscription(_userId));

        // Update this user's org memberships
        if (_orgMetadata == null || _orgMetadata.Expires < DateTimeOffset.UtcNow) {
          var orgs = await _github.OrganizationMemberships(cacheOptions: _orgMetadata);

          if (orgs.IsOk) {
            changes.UnionWith(
              await context.BulkUpdateAccounts(orgs.Date, _mapper.Map<IEnumerable<AccountTableType>>(orgs.Result.Select(x => x.Organization)))
            );

            var userOrgChanges = await context.SetUserOrganizations(_userId, orgs.Result.Select(x => x.Organization.Id));
            changes.UnionWith(userOrgChanges);

            if (!userOrgChanges.IsEmpty) {
              // When this user's org membership changes, re-evaluate whether or not they
              // should have a complimentary personal subscription.
              tasks.Add(_queueClient.BillingUpdateComplimentarySubscription(_userId));
            }
          }

          _orgMetadata = GitHubMetadata.FromResponse(orgs);
        }

        // TODO: Actually and load and maintain the list of orgs inside the object
        // Maintain the grain references too.
        var allOrgIds = await context.OrganizationAccounts
          .Where(x => x.UserId == _userId)
          .Select(x => x.OrganizationId)
          .ToArrayAsync();

        if (allOrgIds.Any()) {
          tasks.AddRange(allOrgIds.Select(x => _grainFactory.GetGrain<IOrganizationActor>(x).Sync(_userId)));
          tasks.AddRange(allOrgIds.Select(x => _queueClient.BillingSyncOrgSubscriptionState(x, _userId)));
        }

        // Update this user's repo memberships
        if (forceRepos || _repoMetadata == null || _repoMetadata.Expires < DateTimeOffset.UtcNow) {
          var repos = await _github.Repositories(_repoMetadata);

          if (repos.IsOk) {
            var keepRepos = repos.Result.Where(x => x.HasIssues && x.Permissions.Push);

            var owners = keepRepos
              .Select(x => x.Owner)
              .Distinct(x => x.Login);

            changes.UnionWith(
              await context.BulkUpdateAccounts(repos.Date, _mapper.Map<IEnumerable<AccountTableType>>(owners)),
              await context.BulkUpdateRepositories(repos.Date, _mapper.Map<IEnumerable<RepositoryTableType>>(keepRepos)),
              await context.SetAccountLinkedRepositories(_userId, keepRepos.Select(x => Tuple.Create(x.Id, x.Permissions.Admin)))
            );
          }

          // Don't update until saved.
          _repoMetadata = GitHubMetadata.FromResponse(repos);
        }

        // TODO: Save the repo list locally in this object
        var allRepos = await context.AccountRepositories
          .Where(x => x.AccountId == _userId)
          .ToArrayAsync();

        tasks.AddRange(allRepos.Select(x => _grainFactory.GetGrain<IRepositoryActor>(x.RepositoryId).Sync(_userId)));
        tasks.AddRange(allRepos
          .Where(x => x.Admin)
          .Select(x => _queueClient.AddOrUpdateRepoWebhooks(x.RepositoryId, _userId)));
      }

      // Send Changes.
      if (!changes.IsEmpty) {
        tasks.Add(_queueClient.NotifyChanges(changes));
      }

      // Await all outstanding operations.
      await Task.WhenAll(tasks);

      // Future Compatibility:

      // Tell the user's orgs and repos that we're interested
      // TODO: Sync orgs
      // TODO: Sync repos
    }
  }
}
