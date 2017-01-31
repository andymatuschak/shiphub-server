﻿namespace RealArtists.ShipHub.Api.Controllers {
  using System;
  using System.Collections.Generic;
  using System.Data.Entity;
  using System.Data.Entity.Infrastructure;
  using System.Diagnostics.CodeAnalysis;
  using System.IO;
  using System.Linq;
  using System.Net;
  using System.Net.Http;
  using System.Net.Http.Headers;
  using System.Security.Authentication;
  using System.Security.Cryptography;
  using System.Text;
  using System.Threading.Tasks;
  using System.Web;
  using System.Web.Http;
  using Common;
  using Common.GitHub;
  using Newtonsoft.Json;

  public class AnalyticsEvent {
    public string Event { get; set; }

    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public Dictionary<string, string> Properties { get; set; }
  }

  [RoutePrefix("analytics")]
  public class AnalyticsController : ApiController {
    private IShipHubConfiguration _configuration;

    public AnalyticsController(IShipHubConfiguration config) {
      _configuration = config;
    }

    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
    public static HttpClient CreateHttpClient() {
      var handler = new HttpClientHandler() {
        AllowAutoRedirect = false,
        AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
        UseCookies = false,
        UseDefaultCredentials = false,
        UseProxy = false,
      };

      return CreateHttpClient(handler, true);
    }

    [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope")]
    public static HttpClient CreateHttpClient(HttpMessageHandler handler, bool disposeHandler) {
      var httpClient = new HttpClient(handler, disposeHandler);

      var headers = httpClient.DefaultRequestHeaders;
      headers.AcceptEncoding.Clear();
      headers.AcceptEncoding.ParseAdd("gzip");
      headers.AcceptEncoding.ParseAdd("deflate");

      headers.Accept.Clear();
      headers.Accept.ParseAdd("application/json");

      headers.AcceptCharset.Clear();
      headers.AcceptCharset.ParseAdd(Encoding.UTF8.WebName);

      headers.UserAgent.Clear();
      headers.UserAgent.Add(new ProductInfoHeaderValue("RealArtists", "server"));

      return httpClient;
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("track")]
    public async Task<HttpResponseMessage> Track() {
      var payloadString = await Request.Content.ReadAsStringAsync();
      var payloadBytes = Encoding.UTF8.GetBytes(payloadString);
      var payloadEvents = JsonConvert.DeserializeObject<IEnumerable<AnalyticsEvent>>(payloadString, GitHubSerialization.JsonSerializerSettings);
      
      foreach (var payloadEvent in payloadEvents) {
        payloadEvent.Properties["token"] = _configuration.MixpanelToken;
        payloadEvent.Properties["ip"] = HttpContext.Current.Request.ServerVariables["HTTP_X_FORWARDED_FOR"];
      }
      
      var json = JsonConvert.SerializeObject(payloadEvents, GitHubSerialization.JsonSerializerSettings);
      var jsonBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json), Base64FormattingOptions.None);

      var uri = new Uri("https://api.mixpanel.com/track/?data=" + jsonBase64);
      var request = new HttpRequestMessage(HttpMethod.Post, uri);

      var client = CreateHttpClient();
      return await client.SendAsync(request);
    }
  }
}
