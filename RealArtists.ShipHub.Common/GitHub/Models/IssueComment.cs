﻿namespace RealArtists.ShipHub.Common.GitHub.Models {
  using System;
  using Newtonsoft.Json;

  public class IssueComment {
    public long Id { get; set; }
    public string Body { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Account User { get; set; }
    public ReactionSummary Reactions { get; set; }

    // Undocumented. Of course.
    private string _issueUrl;
    public string IssueUrl {
      get => _issueUrl;
      set {
        _issueUrl = value;
        IssueNumber = null;

        if (!_issueUrl.IsNullOrWhiteSpace()) {
          var parts = _issueUrl.Split('/');
          IssueNumber = int.Parse(parts[parts.Length - 1]);
        }
      }
    }

    [JsonIgnore]
    public int? IssueNumber { get; set; }
  }
}
