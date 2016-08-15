﻿namespace RealArtists.ShipHub.Api.Tests {
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using System.Net;
  using System.Net.Http;
  using System.Runtime.Remoting.Metadata.W3cXsd2001;
  using System.Security.Cryptography;
  using System.Text;
  using System.Threading.Tasks;
  using System.Web.Http;
  using System.Web.Http.Controllers;
  using System.Web.Http.Hosting;
  using System.Web.Http.Results;
  using System.Web.Http.Routing;
  using AutoMapper;
  using Moq;
  using Newtonsoft.Json;
  using Newtonsoft.Json.Linq;
  using NUnit.Framework;
  using RealArtists.ShipHub.Api.Controllers;
  using RealArtists.ShipHub.Common.DataModel.Types;
  using RealArtists.ShipHub.Common.GitHub;
  using RealArtists.ShipHub.Common.GitHub.Models;
  using RealArtists.ShipHub.QueueClient;

  [TestFixture]
  [AutoRollback]
  public class WebhookTests {

    private static string SignatureForPayload(string key, string payload) {
      var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(key));
      byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
      return "sha1=" + new SoapHexBinary(hash).ToString();
    }

    private static IMapper AutoMapper() {
      var config = new MapperConfiguration(cfg => {
        cfg.AddProfile<Common.DataModel.GitHubToDataModelProfile>();
      });
      var mapper = config.CreateMapper();
      return mapper;
    }

    private static void ConfigureController(ApiController controller, string eventName, JObject body, string secretKey) {
      var json = JsonConvert.SerializeObject(body, GitHubClient.JsonSettings);
      var signature = SignatureForPayload(secretKey, json);

      var config = new HttpConfiguration();
      var request = new HttpRequestMessage(HttpMethod.Post, "http://localhost/webhook");
      request.Headers.Add("User-Agent", GitHubWebhookController.GitHubUserAgent);
      request.Headers.Add(GitHubWebhookController.EventHeaderName, eventName);
      request.Headers.Add(GitHubWebhookController.SignatureHeaderName, signature);
      request.Headers.Add(GitHubWebhookController.DeliveryIdHeaderName, Guid.NewGuid().ToString());
      request.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(json));
      var routeData = new HttpRouteData(config.Routes.MapHttpRoute("Webhook", "webhook"));

      controller.ControllerContext = new HttpControllerContext(config, routeData, request);
      controller.Request = request;
      controller.Request.Properties[HttpPropertyKeys.HttpConfigurationKey] = config;
    }

    private static async Task<IChangeSummary> ChangeSummaryFromIssuesHook(JObject obj, string repoOrOrg, long repoOrOrgId, string secret) {
      IChangeSummary changeSummary = null;

      var mockBusClient = new Mock<IShipHubBusClient>();
      mockBusClient.Setup(x => x.NotifyChanges(It.IsAny<IChangeSummary>()))
        .Returns(Task.CompletedTask)
        .Callback((IChangeSummary arg) => { changeSummary = arg; });

      var controller = new GitHubWebhookController(mockBusClient.Object);
      ConfigureController(controller, "issues", obj, secret);

      IHttpActionResult result = await controller.HandleHook(repoOrOrg, repoOrOrgId);
      Assert.IsInstanceOf(typeof(StatusCodeResult), result);
      Assert.AreEqual(HttpStatusCode.Accepted, (result as StatusCodeResult).StatusCode);

      return changeSummary;
    }

    private static JObject IssueChange(string action, Issue issue, long repositoryId) {
      var obj = new {
        action = "opened",
        issue = issue,
        repository = new {
          id = repositoryId,
        },
      };
      return JObject.FromObject(obj, JsonSerializer.CreateDefault(GitHubClient.JsonSettings));
    }

    private static Common.DataModel.Organization MakeTestOrg(Common.DataModel.ShipHubContext context) {
      return (Common.DataModel.Organization)context.Accounts.Add(new Common.DataModel.Organization() {
        Id = 6001,
        Login = "myorg",
        Date = DateTimeOffset.Now,
      });
    }

    private static Common.DataModel.Issue MakeTestIssue(Common.DataModel.ShipHubContext context, long accountId, long repoId) {
      var issue = new Common.DataModel.Issue() {
        Id = 1001,
        UserId = accountId,
        RepositoryId = repoId,
        Number = 5,
        State = "open",
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
      };
      context.Issues.Add(issue);
      return issue;
    }

    private Common.DataModel.Hook MakeTestRepoHook(Common.DataModel.ShipHubContext context, long creatorId, long repoId) {
      return context.Hooks.Add(new Common.DataModel.Hook() {
        Secret = Guid.NewGuid(),
        Active = true,
        Events = "event1,event2",
        RepositoryId = repoId,
      });
    }

    private Common.DataModel.Hook MakeTestOrgHook(Common.DataModel.ShipHubContext context, long creatorId, long orgId) {
      return context.Hooks.Add(new Common.DataModel.Hook() {
        Secret = Guid.NewGuid(),
        Active = true,
        Events = "event1,event2",
        OrganizationId = orgId,
      });
    }

    [Test]
    public async Task TestPingSucceedsIfSignatureMatchesRepoHook() {
      using (var context = new Common.DataModel.ShipHubContext()) {
        var user = TestUtil.MakeTestUser(context);
        var repo = TestUtil.MakeTestRepo(context, user.Id);
        var hook = MakeTestRepoHook(context, user.Id, repo.Id);

        await context.SaveChangesAsync();

        var obj = JObject.FromObject(new {
          hook_id = 1234,
          repository = new {
            id = repo.Id,
          },
        }, JsonSerializer.CreateDefault(GitHubClient.JsonSettings));

        var controller = new GitHubWebhookController();
        ConfigureController(controller, "ping", obj, hook.Secret.ToString());
        var result = await controller.HandleHook("repo", repo.Id);
        Assert.IsInstanceOf(typeof(StatusCodeResult), result);
        Assert.AreEqual(HttpStatusCode.Accepted, ((StatusCodeResult)result).StatusCode);
      }
    }

    [Test]
    public async Task TestPingSucceedsIfSignatureMatchesOrgHook() {
      using (var context = new Common.DataModel.ShipHubContext()) {
        var user = TestUtil.MakeTestUser(context);
        var org = MakeTestOrg(context);
        var repo = TestUtil.MakeTestRepo(context, user.Id);
        var hook = MakeTestOrgHook(context, user.Id, org.Id);
        org.Members.Add(user);
        await context.SaveChangesAsync();

        var obj = JObject.FromObject(new {
          hook_id = 1234,
          repository = new {
            id = repo.Id,
          },
          organization = new {
            id = org.Id,
          },
        }, JsonSerializer.CreateDefault(GitHubClient.JsonSettings));

        var controller = new GitHubWebhookController();
        ConfigureController(controller, "ping", obj, hook.Secret.ToString());
        var result = await controller.HandleHook("org", org.Id);
        Assert.IsInstanceOf(typeof(StatusCodeResult), result);
        Assert.AreEqual(HttpStatusCode.Accepted, ((StatusCodeResult)result).StatusCode);
      }
    }

    [Test]
    public async Task TestPingFailsWithInvalidSignature() {
      using (var context = new Common.DataModel.ShipHubContext()) {
        var user = TestUtil.MakeTestUser(context);
        var repo = TestUtil.MakeTestRepo(context, user.Id);

        var hook = context.Hooks.Add(new Common.DataModel.Hook() {
          Secret = Guid.NewGuid(),
          Active = true,
          Events = "some events",
          RepositoryId = repo.Id,
        });

        await context.SaveChangesAsync();

        var obj = JObject.FromObject(new {
          hook_id = 1234,
          repository = new {
            id = repo.Id,
          },
        }, JsonSerializer.CreateDefault(GitHubClient.JsonSettings));

        var controller = new GitHubWebhookController();
        ConfigureController(controller, "ping", obj, "someIncorrectSignature");
        var result = await controller.HandleHook("repo", repo.Id);
        Assert.IsInstanceOf(typeof(BadRequestErrorMessageResult), result);
        Assert.AreEqual("Invalid signature.", ((BadRequestErrorMessageResult)result).Message);
      }
    }

    [Test]
    public async Task TestWebhookCallUpdatesLastSeen() {
      using (var context = new Common.DataModel.ShipHubContext()) {
        var user = TestUtil.MakeTestUser(context);
        var repo = TestUtil.MakeTestRepo(context, user.Id);
        var hook = MakeTestRepoHook(context, user.Id, repo.Id);

        await context.SaveChangesAsync();

        Assert.Null(hook.LastSeen);

        var obj = new JObject(
        new JProperty("zen", "It's not fully shipped until it's fast."),
        new JProperty("hook_id", 1234),
        new JProperty("hook", null),
        new JProperty("sender", null),
        new JProperty("repository", new JObject(
          new JProperty("id", repo.Id)
          )));

        var controller = new GitHubWebhookController();
        ConfigureController(controller, "ping", obj, hook.Secret.ToString());
        var result = await controller.HandleHook("repo", repo.Id);
        Assert.IsInstanceOf(typeof(StatusCodeResult), result);
        Assert.AreEqual(HttpStatusCode.Accepted, ((StatusCodeResult)result).StatusCode);

        context.Entry(hook).Reload();
        Assert.NotNull(hook.LastSeen);
      }
    }

    [Test]
    public async Task TestIssueOpened() {
      Common.DataModel.User user;
      Common.DataModel.Repository repo;
      Common.DataModel.Hook hook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        user = TestUtil.MakeTestUser(context);
        repo = TestUtil.MakeTestRepo(context, user.Id);
        hook = MakeTestRepoHook(context, user.Id, repo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 1,
        Labels = new List<Label>(),
        User = new Account() {
          Id = user.Id,
          Login = user.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("opened", issue, repo.Id), "repo", repo.Id, hook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { 2001 }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var newIssue = context.Issues.First();
        Assert.AreEqual(1001, newIssue.Id);
        Assert.AreEqual(1, newIssue.Number);
        Assert.AreEqual("Some Title", newIssue.Title);
        Assert.AreEqual("Some Body", newIssue.Body);
        Assert.AreEqual("open", newIssue.State);
        Assert.AreEqual(2001, newIssue.RepositoryId);
        Assert.AreEqual(3001, newIssue.UserId);
      }
    }

    [Test]
    public async Task TestIssueClosed() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;
      Common.DataModel.Hook testHook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = TestUtil.MakeTestUser(context);
        testRepo = TestUtil.MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        testHook = MakeTestRepoHook(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "closed",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("closed", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.AreEqual("closed", updatedIssue.State);
      };
    }

    [Test]
    public async Task TestIssueReopened() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;
      Common.DataModel.Hook testHook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = TestUtil.MakeTestUser(context);
        testRepo = TestUtil.MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        testHook = MakeTestRepoHook(context, testUser.Id, testRepo.Id);

        testIssue.State = "closed";

        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("reopened", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.AreEqual("open", updatedIssue.State);
      }
    }

    [Test]
    public async Task TestIssueEdited() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;
      Common.DataModel.Hook testHook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = TestUtil.MakeTestUser(context);
        testRepo = TestUtil.MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        testHook = MakeTestRepoHook(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "A New Title",
        Body = "A New Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label> {
          new Label() {
            Color = "ff0000",
            Name = "Red",
          },
          new Label() {
            Color = "0000ff",
            Name = "Blue",
          },
        },
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("edited", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.AreEqual("A New Title", updatedIssue.Title);
        Assert.AreEqual("A New Body", updatedIssue.Body);
      };
    }

    [Test]
    public async Task TestIssueAssigned() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;
      Common.DataModel.Hook testHook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = TestUtil.MakeTestUser(context);
        testRepo = TestUtil.MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        testHook = MakeTestRepoHook(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "closed",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
        Assignees = new[] {
          new Account() {
            Id = testUser.Id,
            Login = testUser.Login,
            Type = GitHubAccountType.User,
          }
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("assigned", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.AreEqual(testUser.Id, updatedIssue.Assignees.First().Id);
      }
    }

    [Test]
    public async Task TestIssueUnassigned() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;
      Common.DataModel.Hook testHook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = TestUtil.MakeTestUser(context);
        testRepo = TestUtil.MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        testHook = MakeTestRepoHook(context, testUser.Id, testRepo.Id);

        testIssue.Assignees = new[] { testUser };

        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "closed",
        Number = 5,
        Labels = new List<Label>(),
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
        Assignees = new Account[0],
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("unassigned", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        Assert.AreEqual(0, updatedIssue.Assignees.Count);
      }
    }

    [Test]
    public async Task TestIssueLabeled() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;
      Common.DataModel.Hook testHook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = TestUtil.MakeTestUser(context);
        testRepo = TestUtil.MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        testHook = MakeTestRepoHook(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label> {
          new Label() {
            Color = "ff0000",
            Name = "Red",
          },
          new Label() {
            Color = "0000ff",
            Name = "Blue",
          },
        },
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("labeled", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        var labels = updatedIssue.Labels.OrderBy(x => x.Name).ToArray();
        Assert.AreEqual(2, labels.Count());
        Assert.AreEqual("Blue", labels[0].Name);
        Assert.AreEqual("0000ff", labels[0].Color);
        Assert.AreEqual("Red", labels[1].Name);
        Assert.AreEqual("ff0000", labels[1].Color);
      };
    }

    [Test]
    public async Task TestIssueUnlabeled() {
      Common.DataModel.User testUser;
      Common.DataModel.Repository testRepo;
      Common.DataModel.Issue testIssue;
      Common.DataModel.Hook testHook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        testUser = TestUtil.MakeTestUser(context);
        testRepo = TestUtil.MakeTestRepo(context, testUser.Id);
        testIssue = MakeTestIssue(context, testUser.Id, testRepo.Id);
        testHook = MakeTestRepoHook(context, testUser.Id, testRepo.Id);
        await context.SaveChangesAsync();
      }

      // First add the labels Red and Blue
      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 5,
        Labels = new List<Label> {
          new Label() {
            Color = "ff0000",
            Name = "Red",
          },
          new Label() {
            Color = "0000ff",
            Name = "Blue",
          },
        },
        User = new Account() {
          Id = testUser.Id,
          Login = testUser.Login,
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("edited", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { testRepo.Id }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        var labels = updatedIssue.Labels.OrderBy(x => x.Name).ToArray();
        Assert.AreEqual(2, labels.Count());
      };

      // Then remove the Red label.
      issue.Labels = issue.Labels.Where(x => !x.Name.Equals("Red"));
      changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("unlabeled", issue, testRepo.Id), "repo", testRepo.Id, testHook.Secret.ToString());

      // Expect null if there are no changes to notify about.
      Assert.Null(changeSummary);

      using (var context = new Common.DataModel.ShipHubContext()) {
        var updatedIssue = context.Issues.Where(x => x.Id == testIssue.Id).First();
        var labels = updatedIssue.Labels.OrderBy(x => x.Name).ToArray();
        Assert.AreEqual(1, labels.Count());
        Assert.AreEqual("Blue", labels[0].Name);
        Assert.AreEqual("0000ff", labels[0].Color);
      };
    }

    [Test]
    public async Task TestIssueHookCreatesMilestoneIfNeeded() {
      Common.DataModel.User user;
      Common.DataModel.Repository repo;
      Common.DataModel.Hook hook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        user = TestUtil.MakeTestUser(context);
        repo = TestUtil.MakeTestRepo(context, user.Id);
        hook = MakeTestRepoHook(context, user.Id, repo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 1,
        Labels = new List<Label>(),
        User = new Account() {
          Id = user.Id,
          Login = user.Login,
          Type = GitHubAccountType.User,
        },        
        Milestone = new Milestone() {
          Id = 5001,
          Number = 1234,
          State = "",
          Title = "some milestone",
          Description = "more info about some milestone",
          CreatedAt = DateTimeOffset.Parse("1/1/2016"),
          UpdatedAt = DateTimeOffset.Parse("1/2/2016"),
          DueOn = DateTimeOffset.Parse("2/1/2016"),
          ClosedAt = DateTimeOffset.Parse("3/1/2016"),
          Creator = new Account() {
            Id = user.Id,
            Login = user.Login,
            Type = GitHubAccountType.User,
          }
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("opened", issue, repo.Id), "repo", repo.Id, hook.Secret.ToString());

      Assert.AreEqual(0, changeSummary.Organizations.Count());
      Assert.AreEqual(new long[] { 2001 }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var milestone = context.Milestones.First(x => x.Id == 5001);
        Assert.AreEqual("some milestone", milestone.Title);
        Assert.AreEqual("more info about some milestone", milestone.Description);
        Assert.AreEqual(1234, milestone.Number);
        Assert.AreEqual(DateTimeOffset.Parse("1/1/2016"), milestone.CreatedAt);
        Assert.AreEqual(DateTimeOffset.Parse("1/2/2016"), milestone.UpdatedAt);
        Assert.AreEqual(DateTimeOffset.Parse("2/1/2016"), milestone.DueOn);
        Assert.AreEqual(DateTimeOffset.Parse("3/1/2016"), milestone.ClosedAt);
      }
    }

    [Test]
    public async Task TestIssueHookCreatesAssigneesIfNeeded() {
      Common.DataModel.User user;
      Common.DataModel.Repository repo;
      Common.DataModel.Hook hook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        user = TestUtil.MakeTestUser(context);
        repo = TestUtil.MakeTestRepo(context, user.Id);
        hook = MakeTestRepoHook(context, user.Id, repo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 1,
        Labels = new List<Label>(),
        User = new Account() {
          Id = user.Id,
          Login = user.Login,
          Type = GitHubAccountType.User,
        },
        Assignees = new Account[] {
          new Account() {
            Id = 11001,
            Login = "nobody1",
            Type = GitHubAccountType.User,
          },
          new Account() {
            Id = 11002,
            Login = "nobody2",
            Type = GitHubAccountType.User,
          },
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("opened", issue, repo.Id), "repo", repo.Id, hook.Secret.ToString());

      Assert.AreEqual(new long[] { 2001 }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var nobody1 = context.Accounts.Single(x => x.Id == 11001);
        var nobody2 = context.Accounts.Single(x => x.Id == 11002);

        Assert.AreEqual("nobody1", nobody1.Login);
        Assert.AreEqual("nobody2", nobody2.Login);
      }
    }

    [Test]
    public async Task TestIssueHookCreatesCreatorIfNeeded() {
      Common.DataModel.User user;
      Common.DataModel.Repository repo;
      Common.DataModel.Hook hook;

      using (var context = new Common.DataModel.ShipHubContext()) {
        user = TestUtil.MakeTestUser(context);
        repo = TestUtil.MakeTestRepo(context, user.Id);
        hook = MakeTestRepoHook(context, user.Id, repo.Id);
        await context.SaveChangesAsync();
      }

      var issue = new Issue() {
        Id = 1001,
        Title = "Some Title",
        Body = "Some Body",
        CreatedAt = DateTimeOffset.Now,
        UpdatedAt = DateTimeOffset.Now,
        State = "open",
        Number = 1,
        Labels = new List<Label>(),
        User = new Account() {
          Id = 12001,
          Login = "nobody",
          Type = GitHubAccountType.User,
        },
      };

      IChangeSummary changeSummary = await ChangeSummaryFromIssuesHook(IssueChange("opened", issue, repo.Id), "repo", repo.Id, hook.Secret.ToString());

      Assert.AreEqual(new long[] { 2001 }, changeSummary.Repositories.ToArray());

      using (var context = new Common.DataModel.ShipHubContext()) {
        var nobody1 = context.Accounts.Single(x => x.Id == 12001);
        Assert.AreEqual("nobody", nobody1.Login);
      }
    }

    [Test]
    public async Task TestRepoCreatedTriggersSyncAccountRepositories() {
      Common.DataModel.User user1;
      Common.DataModel.User user2;
      Common.DataModel.Hook hook;
      Common.DataModel.Organization org;

      using (var context = new Common.DataModel.ShipHubContext()) {
        user1 = TestUtil.MakeTestUser(context);
        user2 = (Common.DataModel.User)context.Accounts.Add(new Common.DataModel.User() {
          Id = 3002,
          Login = "alok",
          Date = DateTimeOffset.Now,
          Token = Guid.NewGuid().ToString(),
        });
        org = TestUtil.MakeTestOrg(context);
        org.Members.Add(user1);
        org.Members.Add(user2);
        hook = MakeTestOrgHook(context, user1.Id, org.Id);
        await context.SaveChangesAsync();
      }

      var obj = JObject.FromObject(new {
        action = "created",
        repository = new Repository() {
          Id = 555,
          Owner = new Account() {
            Id = org.Id,
            Login = "loopt",
            Type = GitHubAccountType.Organization,
          },
          Name = "mix",
          FullName = "loopt/mix",
          Private = true,
          HasIssues = true,
          UpdatedAt = DateTimeOffset.Parse("1/1/2016"),
        },
      }, JsonSerializer.CreateDefault(GitHubClient.JsonSettings));

      var syncAccountRepositoryCalls = new List<Tuple<long, string, string>>();

      var mockBusClient = new Mock<IShipHubBusClient>();
      mockBusClient
        .Setup(x => x.SyncAccountRepositories(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<string>()))
        .Returns(Task.CompletedTask)
        .Callback((long accountId, string login, string accessToken) => {
          syncAccountRepositoryCalls.Add(
            new Tuple<long, string, string>(accountId, login, accessToken));
        });

      var controller = new GitHubWebhookController(mockBusClient.Object);
      ConfigureController(controller, "repository", obj, hook.Secret.ToString());
      var result = await controller.HandleHook("org", org.Id);
      Assert.IsInstanceOf(typeof(StatusCodeResult), result);
      Assert.AreEqual(HttpStatusCode.Accepted, ((StatusCodeResult)result).StatusCode);

      Assert.AreEqual(
        new List<Tuple<long, string, string>> {
          Tuple.Create(user1.Id, user1.Login, user1.Token),
          Tuple.Create(user2.Id, user2.Login, user2.Token),
        },
        syncAccountRepositoryCalls);
    }
  }
}
