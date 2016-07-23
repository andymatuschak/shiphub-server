﻿namespace RealArtists.ShipHub.QueueProcessor {
  using System;
  using System.Diagnostics;
  using Microsoft.Azure;
  using Microsoft.Azure.WebJobs;
  using Microsoft.Azure.WebJobs.ServiceBus;
  using QueueClient;

  static class Program {
    static void Main() {
      var config = new JobHostConfiguration() {
        DashboardConnectionString = CloudConfigurationManager.GetSetting("AzureWebJobsDashboard"),
        StorageConnectionString = CloudConfigurationManager.GetSetting("AzureWebJobsStorage"),
      };

      var sbConfig = new ServiceBusConfiguration() {
        ConnectionString = CloudConfigurationManager.GetSetting("AzureWebJobsServiceBus"),
      };
      sbConfig.MessageOptions.MaxConcurrentCalls = 128;

      // Adjust this based on real performance data
      //sbConfig.MessageOptions.AutoRenewTimeout = 

      // TOOD: Override default messaging provider?
      //sbConfig.MessagingProvider = 

      // See https://github.com/Azure/azure-webjobs-sdk/wiki/Running-Locally
      if (config.IsDevelopment) {
        config.UseDevelopmentSettings();
        config.DashboardConnectionString = null;
        sbConfig.MessageOptions.AutoRenewTimeout = TimeSpan.FromSeconds(10); // Abandon locks quickly
        sbConfig.MessageOptions.MaxConcurrentCalls = 1;
      }

      // https://azure.microsoft.com/en-us/documentation/articles/service-bus-performance-improvements/ recommends
      // 20x the processing rate/sec
      var ratePerSecond = 1;
      sbConfig.PrefetchCount = sbConfig.MessageOptions.MaxConcurrentCalls * 20 * ratePerSecond;

      config.UseServiceBus(sbConfig);

#if DEBUG
      var timer = new Stopwatch();
      Console.WriteLine("Creating Missing Queues");
      timer.Start();
#endif

      ShipHubBusClient.EnsureQueues().Wait();

#if DEBUG
      timer.Stop();
      Console.WriteLine($"Done in {timer.Elapsed}\n");

      Console.WriteLine("Creating Missing Topics");
      timer.Restart();
#endif

      ShipHubBusClient.EnsureTopics().Wait();

#if DEBUG
      timer.Stop();
      Console.WriteLine($"Done in {timer.Elapsed}\n");

      //Console.Write("Send Sync Message? [y/N]: ");
      //var key = Console.Read();
      //if (key == (int)'y') {
      //  Console.WriteLine("Sending sync account message");
      //  timer.Restart();
      //  var qc = new ShipHubBusClient();
      //  qc.SyncAccount(CloudConfigurationManager.GetSetting("GitHubTestToken")).Wait();
      //  timer.Stop();
      //  Console.WriteLine($"Done in {timer.Elapsed}\n");
      //}

      // HACKS!

      //ShipHubBusClient.EnsureSubscription(ShipHubTopicNames.Changes, "DEBUGGING").Wait();
      //var changes = ShipHubBusClient.SubscriptionClientForName(ShipHubTopicNames.Changes, "DEBUGGING");
      //changes.OnMessage(m => {
      //  using (var body = m.GetBody<Stream>())
      //  using (var reader = new StreamReader(body, Encoding.UTF8)) {
      //    Console.WriteLine(reader.ReadToEnd());
      //  }
      //  m.Complete();
      //});

      // END HACKS!
#endif

      Console.WriteLine("Starting job host...\n\n");
      var host = new JobHost(config);
#if DEBUG
      host.Start();
      Console.WriteLine("Press Any Key to Exit.");
      Console.ReadKey();
      Console.WriteLine("Stopping job host...");
      host.Stop();
#else
      host.RunAndBlock();
#endif
    }
  }
}
