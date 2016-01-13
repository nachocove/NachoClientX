//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Model;
using System.Threading;

namespace NachoCore.Utils
{
    public class SFDCOAuth2Constants
    {
        public static string ClientId = "";
        public static string ClientSecret = "";
        public static string RefreshUrl = "https://login.salesforce.com/services/oauth2/authorize";
    }

    /// <summary>
    /// SFDC oauth2 refresh
    /// </summary>
    /// <remarks>
    /// Note that salesforce does not seem to respond with the same kind of data as google does. In particular no expiration date.
    public class SFDCOauth2Refresh : Oauth2Refresh
    {
        public SFDCOauth2Refresh (McCred cred) : base (cred, SFDCOAuth2Constants.RefreshUrl, SFDCOAuth2Constants.ClientSecret, SFDCOAuth2Constants.ClientId)
        {
        }
    }
}

