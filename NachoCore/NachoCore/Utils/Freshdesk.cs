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

    public class FreshdeskSession
    {
        public static readonly FreshdeskSession Shared = new FreshdeskSession (new Uri (BuildInfo.FreshdeskEndpoint), BuildInfo.FreshdeskApiKey);

        readonly NcHttpClient HttpClient;
        readonly Uri Endpoint;
        readonly string ApiKey;
        readonly CancellationTokenSource TokenSource = new CancellationTokenSource ();

        public FreshdeskSession (Uri endpoint, string apikey)
        {
            Endpoint = endpoint;
            HttpClient = NcHttpClient.Instance;
            ApiKey = apikey;
        }

        public void CreateTicket (CrashReport report, Action<Exception> complete)
        {
            var request = new NcHttpRequest (HttpMethod.Post, CreateTicketUri);
            request.Cred = new Model.McCred { Username = ApiKey, Password = "X" };
            var post = new JObject ();
            var ticket = new JObject ();
            ticket.Add ("subject", string.Format ("{0}: {1}", report.Exception, report.Message));
            ticket.Add ("description_html", @"<pre style=""font-family: monospace; word-wrap: normal; white-space: pre; overflow: auto; padding: 0 0 20px 0;"">" + report.ToString ().Replace ("&", "&amp;").Replace ("<", "&lt;") + "</pre>");
            ticket.Add ("email", "owens@nachocove.com");
            ticket.Add ("priority", 3); // 3 = High (see https://freshdesk.com/api#ticket)
            ticket.Add ("status", 2); // 2 = Open
            post.Add ("helpdesk_ticket", ticket);
            request.SetContent (System.Text.Encoding.UTF8.GetBytes (post.ToString ()), "application/json");
            HttpClient.SendRequest (request, 10, (response, token) => {
                complete (null);
            }, (exception, token) => {
                complete (exception);
            }, TokenSource.Token);
        }

        Uri CreateTicketUri {
            get {
                return new Uri (Endpoint, "/helpdesk/tickets.json");
            }
        }
    }

    public class FreshdeskRequest
    {
        NcHttpRequest HttpRequest;

        protected FreshdeskRequest (HttpMethod method, Uri uri)
        {
            HttpRequest = new NcHttpRequest (method, uri);
        }
    }
}
