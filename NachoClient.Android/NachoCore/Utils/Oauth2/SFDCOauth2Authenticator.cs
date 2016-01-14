//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Xamarin.Auth;
using NachoCore;
using NachoCore.Model;
using System.Threading;

namespace NachoCore.Utils
{
    public class SFDCOAuth2Constants
    {
        public static string ClientId = "3MVG9A2kN3Bn17huFKGUr4DK_fibJkZoULYNvjpRnEYo5C86mVzgPwZiKyHw0STBXZCf5ItwbmzhQwX_ud7i6";
        public static string ClientSecret = "2617653871112860670";
        public static string RefreshUrl = "https://login.salesforce.com/services/oauth2/authorize";
    }


    public class SFDCOAuth2Authenticator : OAuth2Authenticator
    {

        public SFDCOAuth2Authenticator (string clientId, string clientSecret, string scope, Uri authorizeUrl, Uri redirectUrl, Uri accessTokenUrl, string loginHint, GetUsernameAsyncFunc getUsernameAsync = null)
            : base (clientId, clientSecret, scope, authorizeUrl, redirectUrl, accessTokenUrl, getUsernameAsync)
        {
        }
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

