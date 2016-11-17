﻿namespace RealArtists.ShipHub.ActorInterfaces {
  using System.Threading.Tasks;
  using Orleans;

  public interface IRepositoryActor : IGrainWithIntegerKey {
    /// <summary>
    /// Right now this simply refreshes a timer. If no sync interest
    /// is observed for a period of time, the grain will deactivate.
    /// 
    /// TODO: Track and return some kind of status
    /// TODO: Publish event streams for sync status and data changes.
    /// </summary>
    Task Sync(long forUserId);
  }
}