﻿namespace OrleansTester {
  using System;
  using System.Threading.Tasks;
  using Newtonsoft.Json;
  using Orleans;
  using RealArtists.ShipHub.ActorInterfaces;
  using RealArtists.ShipHub.ActorInterfaces.GitHub;
  using RealArtists.ShipHub.Common;

  class Program {
    static void Main(string[] args) {
      DoIt().Wait();
      //DoIt2();
      //DoIt3().Wait();
      Console.WriteLine("Done");
      Console.ReadKey();
    }

    static async Task DoIt3() {
      var gc = new OrleansAzureClient(ShipHubCloudConfiguration.Instance.DeploymentId, ShipHubCloudConfiguration.Instance.DataConnectionString);

      var github = await gc.GetGrain<IGitHubActor>(87309); // kogir
      var repoFullName = "realartists/shiphub-server";
      var prs = new[] { 147, 164, 166 };

      Console.WriteLine("[q]: Quit, [g]: Make GraphQL request");
      ConsoleKeyInfo keyInfo;
      while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Q) {
        switch (keyInfo.Key) {
          case ConsoleKey.G:
            Console.WriteLine($"[{DateTimeOffset.Now}]: Making query on {repoFullName}.");
            var resp = await github.PullRequestReviews(repoFullName, prs);
            if (resp.IsOk) {
              Console.WriteLine(resp.Result.SerializeObject(Formatting.Indented));
            }
            break;
        }
      }
    }

    static void DoIt2() {
      string counterName = null;
      var counterValue = 1L;
      string valueName = null;
      double valueValue = 0;

      Console.WriteLine("[q]: Quit, [c]: Count [v]: Value");
      ConsoleKeyInfo keyInfo;
      while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Q) {
        switch (keyInfo.Key) {
          case ConsoleKey.C:
            do {
              Console.Write($"Counter Name [{counterName}]: ");
              var cni = Console.ReadLine();
              counterName = cni.IsNullOrWhiteSpace() ? counterName : cni;
            } while (counterName.IsNullOrWhiteSpace());
            Console.Write($"Counter Value [{counterValue}]: ");
            var ci = Console.ReadLine();
            if (!ci.IsNullOrWhiteSpace() && long.TryParse(ci, out var counterInput)) {
              counterValue = counterInput;
            }
            StatHat.Count(counterName, counterValue);
            break;
          case ConsoleKey.V:
            do {
              Console.Write($"Value Name [{valueName}]: ");
              var vni = Console.ReadLine();
              valueName = vni.IsNullOrWhiteSpace() ? valueName : vni;
            } while (valueName.IsNullOrWhiteSpace());
            Console.Write($"Value Value [{valueValue}]: ");
            var civ = Console.ReadLine();
            if (!civ.IsNullOrWhiteSpace() && double.TryParse(civ, out var valueInput)) {
              valueValue = valueInput;
            }
            StatHat.Value(valueName, valueValue);
            break;
        }
      }
    }

    static async Task DoIt() {
      var gc = new OrleansAzureClient(ShipHubCloudConfiguration.Instance.DeploymentId, ShipHubCloudConfiguration.Instance.DataConnectionString);

      var user = await gc.GetGrain<IUserActor>(87309); // kogir

      //var repo = await gc.GetGrain<IRepositoryActor>(59613425); // realartists/test
      var repo = await gc.GetGrain<IRepositoryActor>(28232663); // dotnet/orleans
      
      //var issue = gc.GetGrain<IIssueActor>(139, "realartists/test", grainClassNamePrefix: null); // realartists/test#139
      var issue = await gc.GetGrain<IIssueActor>(423, "realartists/shiphub-server", grainClassNamePrefix: null); // realartists/shiphub-server#423

      Console.WriteLine("[q]: Quit, [u]: Sync user [r]: Sync repo [i]: Sync issue");
      ConsoleKeyInfo keyInfo;
      while ((keyInfo = Console.ReadKey(true)).Key != ConsoleKey.Q) {
        switch (keyInfo.Key) {
          case ConsoleKey.U:
            Console.WriteLine($"[{DateTimeOffset.Now}]: Syncing user {user.GetPrimaryKeyLong()}.");
            user.Sync().LogFailure();
            break;
          case ConsoleKey.R:
            Console.WriteLine($"[{DateTimeOffset.Now}]: Syncing repository {repo.GetPrimaryKeyLong()}.");
            repo.Sync().LogFailure();
            break;
          case ConsoleKey.I:
            Console.WriteLine($"[{DateTimeOffset.Now}]: Syncing issue {issue.GetPrimaryKeyLong(out string repoName)} in {repoName}.");
            issue.SyncTimeline(user.GetPrimaryKeyLong(), RealArtists.ShipHub.Common.GitHub.RequestPriority.Interactive).LogFailure();
            break;
        }
      }
    }
  }
}
