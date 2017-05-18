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
    
    #line 2 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
    using RealArtists.ShipHub.Mail;
    
    #line default
    #line hidden
    
    #line 3 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
    using RealArtists.ShipHub.Mail.Models;
    
    #line default
    #line hidden
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("RazorGenerator", "2.0.0.0")]
    public partial class PaymentSucceededOrganizationPlain : ShipHubTemplateBase<PaymentSucceededOrganizationMailMessage>
    {
#line hidden

        public override void Execute()
        {


WriteLiteral("\r\n");





            
            #line 5 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
  
  Layout = new RealArtists.ShipHub.Mail.Views.LayoutPlain() { Model = Model };


            
            #line default
            #line hidden
WriteLiteral("We received payment for your organization Ship subscription.\r\n\r\n");


            
            #line 10 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
Write(string.Format("{0:C}", Model.AmountPaid));

            
            #line default
            #line hidden
WriteLiteral(" was charged to your ");


            
            #line 10 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                         Write(PaymentMethodSummaryPlain(Model.PaymentMethodSummary));

            
            #line default
            #line hidden
WriteLiteral(" and covers service through ");


            
            #line 10 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                                                                                           Write(Model.ServiceThroughDate.ToString("MMM d, yyyy"));

            
            #line default
            #line hidden
WriteLiteral(".\r\n\r\nDownload a PDF receipt for your records:\r\n");


            
            #line 13 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
Write(Model.InvoicePdfUrl);

            
            #line default
            #line hidden
WriteLiteral("\r\n\r\nIn the prior month beginning on ");


            
            #line 15 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                           Write(Model.PreviousMonthStart.ToString("MMM d, yyyy"));

            
            #line default
            #line hidden
WriteLiteral(", your organization had ");


            
            #line 15 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                                                    Write(Model.PreviousMonthActiveUsersCount);

            
            #line default
            #line hidden
WriteLiteral(" active Ship user");


            
            #line 15 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                                                                                                          Write(Model.PreviousMonthActiveUsersCount == 1 ? "" : "s");

            
            #line default
            #line hidden
WriteLiteral(".\r\n\r\n");


            
            #line 17 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
 if (Model.PreviousMonthActiveUsersCount > 1) {

            
            #line default
            #line hidden
WriteLiteral("The base monthly fee (paid as part of your last invoice) covers the first active " +
"Ship user, so you were billed for ");


            
            #line 18 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                                                                     Write(Model.PreviousMonthActiveUsersCount - 1);

            
            #line default
            #line hidden
WriteLiteral(" additional active user");


            
            #line 18 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                                                                                                                                      Write((Model.PreviousMonthActiveUsersCount - 1) == 1 ? "" : "s");

            
            #line default
            #line hidden
WriteLiteral(" on this invoice.\r\n");

WriteLiteral("\r\n");


            
            #line 20 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
}

            
            #line default
            #line hidden

            
            #line 21 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
 if (Model.PreviousMonthActiveUsersCount > 0) {
    
            
            #line default
            #line hidden
            
            #line 22 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
     if (Model.PreviousMonthActiveUsersCount == Model.PreviousMonthActiveUsersSample.Count()) {

            
            #line default
            #line hidden
WriteLiteral("Active Ship users in your organization in the prior month were: ");


            
            #line 23 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                  Write(string.Join(", ", Model.PreviousMonthActiveUsersSample));

            
            #line default
            #line hidden
WriteLiteral(".\r\n");

WriteLiteral("\r\n");


            
            #line 25 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
    } else {

            
            #line default
            #line hidden
WriteLiteral("Active Ship users in your organization in the prior month included: ");


            
            #line 26 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                      Write(string.Join(", ", Model.PreviousMonthActiveUsersSample));

            
            #line default
            #line hidden
WriteLiteral(", and ");


            
            #line 26 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
                                                                                                                                      Write(Model.PreviousMonthActiveUsersCount - Model.PreviousMonthActiveUsersSample.Count());

            
            #line default
            #line hidden
WriteLiteral(" others.\r\n");

WriteLiteral("\r\n");



            
            #line 28 "..\..\Views\PaymentSucceededOrganizationPlain.cshtml"
    }
}

            
            #line default
            #line hidden
WriteLiteral("We appreciate your business!\r\n");


        }
    }
}
#pragma warning restore 1591
