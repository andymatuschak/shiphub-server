﻿#pragma warning disable 1591
//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace RealArtists.ShipHub.Mail.Views
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    
    #line 2 "..\..\Views\PurchaseOrganizationHtml.cshtml"
    using RealArtists.ShipHub.Mail;
    
    #line default
    #line hidden
    
    #line 3 "..\..\Views\PurchaseOrganizationHtml.cshtml"
    using RealArtists.ShipHub.Mail.Models;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    public partial class PurchaseOrganizationHtml : ShipHubTemplateBase<PurchaseOrganizationMailMessage>
    {
#line hidden

        public override void Execute()
        {


WriteLiteral("\r\n");





            
            #line 5 "..\..\Views\PurchaseOrganizationHtml.cshtml"
  
  Layout = new RealArtists.ShipHub.Mail.Views.LayoutHtml() { Model = Model };


            
            #line default
            #line hidden
WriteLiteral("<p>\r\n    Thanks for purchasing a subscription to Ship - we hope your\r\n    team en" +
"joys using it.\r\n</p>\r\n<p>\r\n    <a href=\"");


            
            #line 13 "..\..\Views\PurchaseOrganizationHtml.cshtml"
        Write(Model.InvoicePdfUrl);

            
            #line default
            #line hidden
WriteLiteral(@""">Download a PDF receipt</a> for your records.
</p>

<h4>How to manage your account:</h4>
<p class=""last"">
    If you need to change billing or payment info, or need to cancel your account, you can do so
    from within the Ship application. From the <em>Ship</em> menu,
    choose <em>Manage Subscription</em>.  Then click <em>Manage</em> for
    your account.
</p>");


        }
    }
}
#pragma warning restore 1591
