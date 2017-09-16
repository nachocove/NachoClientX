//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Net.Http;
using NachoPlatform;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NachoClient.Build;

namespace NachoCore.Utils
{

    public class GithubSession
    {
        public static readonly GithubSession Shared = new GithubSession (new Uri (BuildInfo.GitHubEndpoint), BuildInfo.GitHubUsername, BuildInfo.GitHubApiKey);

        readonly NcHttpClient HttpClient;
        readonly Uri Endpoint;
        readonly string Username;
        readonly string ApiKey;
        readonly CancellationTokenSource TokenSource = new CancellationTokenSource ();

        public GithubSession (Uri endpoint, string username, string apikey)
        {
            Endpoint = endpoint;
            HttpClient = NcHttpClient.Instance;
            Username = username;
            ApiKey = apikey;
        }

        public void CreateIssue (CrashReport report, Action<Exception> complete)
        {
            var request = new NcHttpRequest (HttpMethod.Post, CreateIssueUri);
            request.Cred = new Model.McCred { Username = Username, Password = ApiKey };
            var issue = new JObject ();
            issue.Add ("title", string.Format ("{0}: {1}", report.Exception, report.Message));
            issue.Add ("body", "```\n" + report.ToString () + "\n```");
            var labels = new JArray ();
            labels.Add ("CRASH");
            issue.Add ("labels", labels);
            request.SetContent (System.Text.Encoding.UTF8.GetBytes (issue.ToString ()), "application/vnd.github.v3.raw+json");
            request.Headers.Add ("Accept", "application/vnd.github.v3.raw+json");
            HttpClient.SendRequest (request, 10, (response, token) => {
                if (response.StatusCode == System.Net.HttpStatusCode.Created) {
                    complete (null);
                } else {
                    complete (new GithubException (response));
                }
            }, (exception, token) => {
                complete (exception);
            }, TokenSource.Token);
        }

        Uri CreateIssueUri {
            get {
                return new Uri (Endpoint, "/repos/nachocove/NachoClientX/issues");
            }
        }
    }

    public class GithubException : Exception
    {
        public GithubException (NcHttpResponse response) : base (string.Format ("Error response: {0}", response.StatusCode))
        {
        }
    }
}
