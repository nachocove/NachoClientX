//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using System.Threading;
using NachoPlatform;
using NachoCore.Utils;
using System.Net.Http;
using Newtonsoft;
using System.Text;
using System.Collections.Generic;
using System.Linq;

namespace NachoCore
{
    /// OAuth2 refresh has the following cases:
    /// 1) Oauth2RefreshTimer callback find all oauth2 credentials and initiates a refresh.
    ///    It creates an entry in CredReqActive to keep track of the refresh. This
    ///    is also needed so that we know we're already working on it, should the controller
    ///    initiate a CredReq. If the timer-initiated refresh finishes without the controller
    ///    sending a CredReq, we don't need to send a CredResp. If it does, we do.
    /// 2) Controller initiates a CredReq before the Oauth2RefreshTimer callback notices
    ///    it needs to refresh a token. This can happen when the Backend is first Start()'ed.
    ///    The timer will wait KOauth2RefreshIntervalSecs to run, but the Controller will
    ///    start immediately, so we'll start the refresh via the CredReq. When the timer callback
    ///    runs and notices it needs to refresh the token (assuming it hasn't finished already),
    ///    it will not restart the refresh, and simply keep track.
    /// 
    /// Alerting the UI:
    /// We should only alert the UI (and thus the user) if we can absolutely not wait any longer.
    /// We will retry refreshing the auth-token (using the refresh-token) until the auth-token times
    /// out, and even then we'll retry a few more times.
    /// 
    /// FIXME: Be smarter about network conditions
    /// FIXME: Be smarter about detecting when the server says the refresh-token is bad.
    /// 
    /// Success:
    /// 1) The refresh succeeds, and there was a CredReq: We need to send a CredResp. The CredResp 
    ///    deletes the CredReqActive record.
    /// 2) The refresh succeeds, and there was NO CredReq. We don't send a CredResp, and thus
    ///    need to delete the CredReqActive entry ourselves.
    /// 
    /// <remarks>
    /// refresh-tokens for google are valid forever (Other services may limit this to 2 weeks). The 
    /// refresh token can also be invalidated by the user. Currently we retry KOauth2RefreshMaxFailure
    /// times, but we can do better if we catch an invalid refresh token in the McCred.RefreshOAuth2()
    /// and immediately punt up the UI.
    /// </remarks>
    public class Oauth2Refresh
    {
        /// <summary>
        /// Interval in seconds after which we re-check the OAuth2 credentials.
        /// </summary>
        const int KOauth2RefreshIntervalSecs = 300;

        /// <summary>
        /// Initial delay after which we do a first pass at the oauth2 credentials.
        /// </summary>
        const int KOauth2RefreshDelaySecs = 1;

        /// <summary>
        /// The percentage of OAuth2-expiry after which we refresh the token.
        /// </summary>
        const int KOauth2RefreshPercent = 80;

        /// <summary>
        /// The OAuth2 Refresh NcTimer
        /// </summary>
        NcTimer RefreshTimer;

        /// <summary>
        /// Number of retries after which we call the attempts failed, and tell the UI
        /// to ask the user to log in anew. Not saved in the DB.
        /// </summary>
        /// <remarks>
        /// NOTE: The retries are only counted as failures AFTER the auth-token has expired.
        /// This means we'll retry as long and as much as necessary while the auth-token is
        /// still valid. There's no need to alert the UI (and thus the user) until we run out
        /// of options to refresh the auth-token.
        /// </remarks>
        public const uint KOauth2RefreshMaxFailure = 3;

        protected static object lockObj = new object ();

        protected static Oauth2Refresh _Instance;

        /// <summary>
        /// Gets or sets the refresh cancel source. 
        /// </summary>
        /// <value>The refresh cancel source.</value>
        /// 
        CancellationTokenSource Cts;

        protected virtual BackEnd Be {
            get {
                return BackEnd.Instance;
            }
        }

        public static Oauth2Refresh Instance {
            get {
                if (null == _Instance) {
                    lock (lockObj) {
                        if (null == _Instance) {
                            _Instance = new Oauth2Refresh ();
                        }
                    }
                }
                return _Instance;
            }
        }

        protected Oauth2Refresh ()
        {
            Cts = new CancellationTokenSource ();
        }

        object LockObj = new object ();

        bool Initted { get; set; }
        bool Started { get; set; }

        public void Start ()
        {
            if (!Started) {
                lock (LockObj) {
                    if (!Started) {
                        if (Cts.IsCancellationRequested) {
                            Cts.Dispose ();
                            Cts = new CancellationTokenSource ();
                        }
                        if (null != RefreshTimer) {
                            Log.Error (Log.LOG_SYS, "McCred:Oauth2RefreshTimer: Starting new timer without having stopped it.");
                            RefreshTimer.Dispose ();
                        }
                        RefreshTimer = new NcTimer ("McCred:Oauth2RefreshTimer", state => RefreshAllDueTokens (),
                            null, new TimeSpan (0, 0, KOauth2RefreshDelaySecs), new TimeSpan (0, 0, KOauth2RefreshIntervalSecs));
                        if (!Initted) {
                            NcCommStatus.Instance.CommStatusNetEvent += Oauth2NetStatusEventHandler;
                        }
                        Initted = true;
                        Started = true;
                    }
                }
            }
        }

        /// <summary>
        /// Stops the oauth refresh timer.
        /// </summary>
        public void Stop (bool cancel = true)
        {
            lock (LockObj) {
                if (null != RefreshTimer) {
                    RefreshTimer.Dispose ();
                    RefreshTimer = null;
                }
                if (cancel) {
                    Cts.Cancel ();
                }
                Started = false;
            }
        }

        protected virtual void Reset ()
        {
            Stop (false);
            Start ();
        }

        void Oauth2NetStatusEventHandler (Object sender, NetStatusEventArgs e)
        {
            switch (e.Status) {
            case NetStatusStatusEnum.Down:
                Stop ();
                break;

            case NetStatusStatusEnum.Up:
                // If we haven't started it (i.e. never ran through BackEnd.Start(int)),
                // then don't start it now, either. This is to prevent a network event to start
                // us sooner than we really want to. We initially delay a lot of services
                // for performance reasons.
                if (Initted) {
                    Start ();
                } else {
                    Log.Error (Log.LOG_SYS, "Oauth2Refresh not started because not Initted");
                }
                break;
            }
        }

        protected virtual void ChangeOauthRefreshTimer (int nextUpdate)
        {
            if (!Cts.IsCancellationRequested) {
                NcAssert.NotNull (RefreshTimer);
                RefreshTimer.Change (new TimeSpan (0, 0, nextUpdate), new TimeSpan (0, 0, KOauth2RefreshIntervalSecs));
            }
        }

        /// <summary>
        /// Find all the McCred's that are OAuth2, and which are within KOauth2RefreshPercent of expiring.
        /// Mark them as CredReqActive, so we have handle this properly if/when the Controller sends
        /// a CredReq for the same item (can happen if we timeout with no network connectivity, for example).
        /// </summary>
        protected void RefreshAllDueTokens ()
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                // why bother.. no one listens to me anyway...
                Log.Info (Log.LOG_SYS, "OAUTH2 RefreshAllDueTokens: Network down.");
                return;
            }
            var oauthCreds = McCred.QueryByCredType (McCred.CredTypeEnum.OAuth2);
            Log.Info (Log.LOG_SYS, "OAUTH2 RefreshAllDueTokens: checking {0} creds", oauthCreds.Count);
            foreach (var cred in oauthCreds) {
                if (Cts.IsCancellationRequested) {
                    Log.Info (Log.LOG_SYS, "OAUTH2 RefreshAllDueTokens: cancelled.");
                    return;
                }
                CredReqActiveState.CredReqActiveStatus status;
                var expiryFractionSecs = Math.Round ((double)(cred.ExpirySecs * (100 - KOauth2RefreshPercent)) / 100);
                if (Be.CredReqActive.TryGetStatus (cred.AccountId, out status) ||
                    cred.Expiry.AddSeconds (-expiryFractionSecs) <= DateTime.UtcNow) {
                    if (null != status && status.State == CredReqActiveState.State.NeedUI) {
                        Log.Info (Log.LOG_SYS, "OAUTH2 RefreshAllDueTokens({0}): token already needs UI. BackEndStates={1}", cred.AccountId, string.Join (",", Be.BackEndStates (cred.AccountId)));
                        // We've decided to give up on this one
                        continue;
                    }
                    Log.Info (Log.LOG_SYS, "OAUTH2 RefreshAllDueTokens({0}): token refresh needed.", cred.AccountId);
                    RefreshCredential (cred);
                } else {
                    Log.Info (Log.LOG_SYS, "OAUTH2 RefreshAllDueTokens({0}): token refresh not needed. due {1}, is {2}, expiryFractionSecs {3} ({4}), status {5}", 
                        cred.AccountId, cred.Expiry, DateTime.UtcNow, expiryFractionSecs, cred.Expiry.AddSeconds (-expiryFractionSecs),
                        status != null ? status.ToString () : "NULL");
                }
            }
        }

        /// <summary>
        /// Keep track of retries and do some error checking. Then Refresh the credential.
        /// If the credential isn't an oauth2 token, we can't refresh, so just pass up to the UI.
        /// </summary>
        /// <param name="cred">Cred.</param>
        public void RefreshCredential (McCred cred)
        {
            if (!cred.CanRefresh ()) {
                // it's not a token. Ask the UI.
                if (!Be.CredReqActive.Update (cred.AccountId, (status) => {
                    status.State = CredReqActiveState.State.NeedUI;
                })) {
                    Log.Error (Log.LOG_BACKEND, "Expecting a CredReqActiveState, but none found.");
                }
                Log.Info (Log.LOG_BACKEND, "RefreshCredential({0}): Sending indication to UI about CredReq.", cred.AccountId);
                alertUi (cred.AccountId, "NotCanRefresh");
                return;
            }

            bool needUi = false;
            if (Be.CredReqActive.Update (cred.AccountId, (status) => {
                if (status.State != CredReqActiveState.State.AwaitingRefresh) {
                    Log.Warn (Log.LOG_BACKEND, "RefreshCredential ({0}): State should be CredReqActive_AwaitingRefresh", cred.AccountId);
                }
                if (status.PostExpiryRefreshRetries >= KOauth2RefreshMaxFailure) {
                    // We've retried too many times. Guess we need the UI afterall.
                    status.State = CredReqActiveState.State.NeedUI;
                    needUi = true;
                }
            })) {
                if (needUi) {
                    alertUi (cred.AccountId, "RefreshRetries");
                }
            } else {
                Be.CredReqActive.TryAdd (cred.AccountId, CredReqActiveState.State.AwaitingRefresh, false);
            }
            RefreshOauth2 (cred);
        }

        /// <summary>
        /// Refreshs the McCred. Exists so we can override it in testing
        /// </summary>
        /// <param name="cred">Cred.</param>
        protected virtual void RefreshOauth2 (McCred cred)
        {
            NcAssert.NotNull (Cts, "_Oauth2RefreshCancelSource is null");
            cred.RefreshOAuth2 (TokenRefreshSuccess, TokenRefreshFailure, Cts.Token);
        }

        /// <summary>
        /// Callback called after a failure to refresh the oauth2 token
        /// </summary>
        /// <param name="cred">Cred.</param>
        /// <param name = "fatalError"></param>
        protected virtual void TokenRefreshFailure (McCred cred, bool fatalError)
        {
            bool isExpired = cred.IsExpired;
            bool needUi = false;
            if (Be.CredReqActive.Update (cred.AccountId, (status) => {
                if (isExpired || fatalError) {
                    status.PostExpiryRefreshRetries++;
                }

                // check if we need the UI (note other places can set this as well)
                if (status.PostExpiryRefreshRetries >= KOauth2RefreshMaxFailure) {
                    status.State = CredReqActiveState.State.NeedUI;
                }
                // We want to pass the request up to the UI in the following cases:
                // - the ProtoController asked for creds, and we failed.
                //   We should have refreshed long before this, so if we reach this point, pass it up.
                // - The state was set to CredReqActive_NeedUI somehow (password auth gets this)
                needUi |= status.State == CredReqActiveState.State.NeedUI;
            })) {
                if (needUi) {
                    alertUi (cred.AccountId, "TokenRefreshFailure1");
                }
            } else {
                Log.Warn (Log.LOG_BACKEND, "TokenRefreshFailure with no CredReqActiveState");
                alertUi (cred.AccountId, "TokenRefreshFailure2");
                return;
            }
            if (isExpired || fatalError) {
                // accelerate the update a bit, by restarting the timer.
                ChangeOauthRefreshTimer (KOauth2RefreshDelaySecs * 2);
            }
        }

        protected virtual void alertUi (int accountId, string message)
        {
            Log.Info (Log.LOG_BACKEND, "Alerting UI for CredReq({0}): {1}", accountId, message);
            InvokeOnUIThread.Instance.Invoke (() => Be.Owner.CredReq (accountId));
        }

        /// <summary>
        /// Callback called after a successful OAuth2 refresh.
        /// </summary>
        /// <param name="cred">Cred.</param>
        protected virtual void TokenRefreshSuccess (McCred cred)
        {
            CredReqActiveState.CredReqActiveStatus status;
            if (Be.CredReqActive.TryGetStatus (cred.AccountId, out status)) {
                if (status.NeedCredResp) {
                    CredResp (cred.AccountId);
                } else {
                    Be.CredReqActive.Remove (cred.AccountId);
                }
            }
            if (Be.CredReqActive.Count == 0) {
                // there's currently none active, and thus not a failed
                // attempt we need to re-check at a quicker pace, so reset
                // the timer.
                Reset ();
            }
        }

        protected virtual void CredResp (int accountId)
        {
            Be.CredResp (accountId);
        }
    }

    public class Oauth2TokenRefresh
    {
        const int KDefaultOauth2HttpTimeout = 30;

        protected McCred Cred;
        string ClientSecret;
        string ClientId;
        string RefreshUrl;
        int TimeoutSecs;

        public Oauth2TokenRefresh (McCred cred, string refreshUrl, string clientSecret, string clientId, int timeoutSecs = KDefaultOauth2HttpTimeout)
        {
            Cred = cred;
            RefreshUrl = refreshUrl;
            TimeoutSecs = timeoutSecs;
            ClientSecret = clientSecret;
            ClientId = clientId;
        }

        static public INcHttpClient TestHttpClient { get; set; }

        public INcHttpClient HttpClient {
            get {
                if (TestHttpClient != null) {
                    return TestHttpClient;
                } else {
                    return NcHttpClient.Instance;
                }
            }
        }

        public virtual void Refresh (Action<McCred> onSuccess, Action<McCred, bool> onFailure, CancellationToken Token)
        {
            var queryDict = new Dictionary<string, string> ();
            queryDict ["client_id"] = ClientId;
            queryDict ["client_secret"] = ClientSecret;
            queryDict ["grant_type"] = "refresh_token";
            queryDict ["refresh_token"] = Cred.GetRefreshToken ();

            var queryString = string.Join ("&", queryDict.Select (kv => string.Format ("{0}={1}", kv.Key, Uri.EscapeDataString (kv.Value))));
            var requestUri = new Uri (RefreshUrl + "?" + queryString);
            var request = new NcHttpRequest (HttpMethod.Post, requestUri);
            request.Headers.Add ("Accept", "application/json");
            request.SetContent (null, "application/x-www-form-urlencoded");

            Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): Attempting Refresh", Cred.AccountId);

            HttpClient.SendRequest (request, TimeoutSecs, ((response, token) => {
                if (response.StatusCode != System.Net.HttpStatusCode.OK) {
                    bool fatalError = false;
                    switch (response.StatusCode) {
                    case System.Net.HttpStatusCode.BadRequest:
                        // This probably means the refresh token is no longer valid. We don't immediately punch
                        // up to the UI, because we really don't fully trust HTTP response code. Treat it as 'fatal',
                        // and let the underlying code decide what to do with fatal responses.
                        Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): Refresh Status {1}", Cred.AccountId, response.StatusCode.ToString ());
                        fatalError = true;
                        break;
                    default:
                        string body = "";
                        if (response.HasBody) {
                            body = Encoding.UTF8.GetString (response.GetContent ());
                        }
                        Log.Warn (Log.LOG_SYS, "RefreshOAuth2({0}): HTTP Status {1}\n{2}", Cred.AccountId, response.StatusCode.ToString (), body);
                        break;
                    }
                    onFailure (Cred, fatalError);
                    return;
                }
                var jsonResponse = Encoding.UTF8.GetString (response.GetContent ());
                var decodedResponse = Newtonsoft.Json.Linq.JObject.Parse (jsonResponse);

#if DEBUG
                Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): : OAUTH2 response: {1}", Cred.AccountId, jsonResponse);
#endif
                Newtonsoft.Json.Linq.JToken tokenType;
                if (!decodedResponse.TryGetValue ("token_type", out tokenType) || (string)tokenType != "Bearer") {
                    Log.Error (Log.LOG_SYS, "RefreshOAuth2({0}): Unknown OAUTH2 token_type {1}", Cred.AccountId, tokenType);
                }

                Newtonsoft.Json.Linq.JToken accessToken;
                if (!decodedResponse.TryGetValue ("access_token", out accessToken)) {
                    Log.Error (Log.LOG_SYS, "RefreshOAuth2({0}): Missing OAUTH2 access_token {1}", Cred.AccountId, accessToken);
                    onFailure (Cred, true);
                }
                Newtonsoft.Json.Linq.JToken expiresIn;
                if (!decodedResponse.TryGetValue ("expires_in", out expiresIn)) {
                    Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): OAUTH2 Token refreshed.", Cred.AccountId);
                } else {
                    Log.Info (Log.LOG_SYS, "RefreshOAuth2({0}): OAUTH2 Token refreshed. expires_in={1}", Cred.AccountId, expiresIn);
                }

                Newtonsoft.Json.Linq.JToken refreshToken;
                if (!decodedResponse.TryGetValue ("refresh_token", out refreshToken)) {
                    Log.Warn (Log.LOG_SYS, "RefreshOAuth2({0}): No refresh-token in reply", Cred.AccountId);
                }

                int expires = null != expiresIn ? (int)expiresIn : 0;
                if (expires <= 0) {
                    expires = 3600;
                }
                // also there's an ID token: http://stackoverflow.com/questions/8311836/how-to-identify-a-google-oauth2-user/13016081#13016081
                Cred.UpdateOauth2 ((string)accessToken,
                    string.IsNullOrEmpty ((string)refreshToken) ? Cred.GetRefreshToken () : (string)refreshToken,
                    (uint)expires);
                onSuccess (Cred);
                RefreshAction (Cred, decodedResponse);
            }), ((ex, token) => {
                Log.Error (Log.LOG_SYS, "RefreshOAuth2({0}): Exception {1}", Cred.AccountId, ex.ToString ());
                onFailure (Cred, false);
            }), Token);
        }

        protected virtual void RefreshAction (McCred cred, Newtonsoft.Json.Linq.JObject jsonObject)
        {
        }
    }
}

