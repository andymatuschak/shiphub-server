﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RealArtists.ShipHub.Common {
  public static class ChargeBeeUtilities {
    private static Regex CustomerIdRegex { get; } = new Regex(@"^(user|org)-(\d+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(200));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "1#")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1021:AvoidOutParameters", MessageId = "2#")]
    public static void ParseCustomerId(string customerId, out string type, out long id) {
      var match = CustomerIdRegex.Match(customerId);
      if (!match.Success) {
        throw new ArgumentException("Invalid ChargeBee customer id: " + customerId);
      }

      type = match.Groups[1].ToString();
      id = long.Parse(match.Groups[2].ToString());
    }

    public static long AccountIdFromCustomerId(string customerId) {
      string type;
      long id;
      ParseCustomerId(customerId, out type, out id);
      return id;
    }
  }
}