﻿namespace RealArtists.ShipHub.Api.Controllers {
  using System;
  using System.Data.Entity;
  using System.Linq;
  using System.Threading.Tasks;
  using System.Web.Http;
  using AutoMapper;
  using RealArtists.ShipHub.Api.Sync.Messages.Entries;
  using RealArtists.ShipHub.Common;
  using RealArtists.ShipHub.Common.DataModel;
  using RealArtists.ShipHub.QueueClient;

  public class ShortQueryResponse {
    public string Identifier { get; set; }
    public string Title { get; set; }
    public string Predicate { get; set; }
    public long Author { get; set; }
  }

  public class QueryBody {
    public string Title { get; set; }
    public string Predicate { get; set; }
  }

  [RoutePrefix("api/query")]
  public class QueryController : ShipHubApiController {
    private IMapper _mapper;
    private IShipHubQueueClient _queueClient;

    public QueryController(IMapper mapper, IShipHubQueueClient queueClient) {
      _mapper = mapper;
      _queueClient = queueClient;
    }

    private async Task<QueryEntry> LookupQuery(ShipHubContext context, Guid id) {
      var q = await context.Queries
        .AsNoTracking()
        .Include(x => x.Author)
        .Where(x => x.Id == id)
        .SingleOrDefaultAsync();
      if (q != null) {
        return new QueryEntry() {
          Id = q.Id,
          Title = q.Title,
          Predicate = q.Predicate,
          Author = new AccountEntry() {
            Identifier = q.Author.Id,
            Login = q.Author.Login,
            Name = q.Author.Name
          }
        };
      }
      return null;
    }

    [AllowAnonymous]
    [HttpGet]
    [Route("{queryId}")]
    public async Task<IHttpActionResult> QueryInfo(string queryId) {
      var id = Guid.Parse(queryId);

      QueryEntry entry = null;
      using (var context = new ShipHubContext()) {
        entry = await LookupQuery(context, id);
      }

      if (entry != null) {
        return Ok(entry);
      } else {
        return NotFound();
      }
    }

    [HttpPut]
    [Route("{queryId}")]
    public async Task<IHttpActionResult> SaveQuery(string queryId, [FromBody] QueryBody query) {
      var id = Guid.Parse(queryId);

      using (var context = new ShipHubContext()) {
        var updater = new DataUpdater(context, _mapper);
        await updater.UpdateQuery(id, ShipHubUser.UserId, query.Title, query.Predicate);
        // If there are no changes, then the author ID didn't match
        if (updater.Changes.IsEmpty) {
          return StatusCode(System.Net.HttpStatusCode.Conflict);
        }
        await _queueClient.NotifyChanges(updater.Changes);
      }

      var ret = new ShortQueryResponse() {
        Identifier = queryId,
        Title = query.Title,
        Predicate = query.Predicate,
        Author = ShipHubUser.UserId
      };

      return Ok(ret);
    }

    [HttpDelete]
    [Route("{queryId}")]
    public async Task<IHttpActionResult> UnwatchQuery(string queryId) {
      var id = Guid.Parse(queryId);

      using (var context = new ShipHubContext()) {
        var updater = new DataUpdater(context, _mapper);
        await updater.ToggleWatchQuery(id, ShipHubUser.UserId, false);
        await _queueClient.NotifyChanges(updater.Changes);
      }

      return StatusCode(System.Net.HttpStatusCode.NoContent);
    }

    [HttpPut]
    [Route("{queryId}/watch")]
    public async Task<IHttpActionResult> WatchQuery(string queryId) {
      var id = Guid.Parse(queryId);

      QueryEntry entry = null;
      using (var context = new ShipHubContext()) {
        entry = await LookupQuery(context, id);
        if (entry != null) {
          var updater = new DataUpdater(context, _mapper);
          await updater.ToggleWatchQuery(id, ShipHubUser.UserId, true);
          await _queueClient.NotifyChanges(updater.Changes);
        }
      }

      if (entry != null) {
        return Ok(entry);
      } else {
        return NotFound();
      }
    }
  }
}
