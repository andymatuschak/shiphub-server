﻿namespace RealArtists.ShipHub.Common.DataModel {
  using System.Collections.Generic;
  using System.Diagnostics.CodeAnalysis;

  public class Organization : Account {
    [SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
    public virtual ICollection<OrganizationAccount> OrganizationAccounts { get; set; } = new HashSet<OrganizationAccount>();
  }
}
