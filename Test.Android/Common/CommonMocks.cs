//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Net.Http;
using NachoCore.Utils;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.ActiveSync;
using NachoCore;
using NachoCore.Model;
using NachoPlatform;


namespace Test.iOS
{
    // reusable request/response data
    public class CommonMockData
    {
        public static Uri MockUri = new Uri ("https://contoso.com");
    }

    public class MockHttpClient : IHttpClient
    {
        // TODO: do we need to go the factory route and get rid of the statics?
        public delegate void ExamineHttpRequestMessageDelegate (HttpRequestMessage request);
        public static ExamineHttpRequestMessageDelegate ExamineHttpRequestMessage { set; get; }

        public delegate HttpResponseMessage ProvideHttpResponseMessageDelegate ();
        public static ProvideHttpResponseMessageDelegate ProvideHttpResponseMessage { set; get; }

        public TimeSpan Timeout { get; set; }

        public MockHttpClient (HttpClientHandler handler)
        {
        }

        public Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, 
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            if (null != ExamineHttpRequestMessage) {
                ExamineHttpRequestMessage (request);
            }

            return Task.Run<HttpResponseMessage> (delegate {
                return ProvideHttpResponseMessage ();
            });
        }
    }

    public class MockContext : IBEContext
    {
        public IProtoControlOwner Owner { set; get; }
        public AsProtoControl ProtoControl { set; get; }
        public McProtocolState ProtocolState { get; set; }
        public McServer Server { get; set; }
        public McAccount Account { set; get; }
        public McCred Cred { set; get; }

        public MockContext ()
        {
            Owner = null; // Should not be accessed.
            ProtoControl = null; // Should not be accessed.
            ProtocolState = new McProtocolState ();
            // READ AsPolicyKey
            // R/W AsProtocolVersion
            // READ InitialProvisionCompleted
            Server = null; // Should not be accessed.
            Account = new McAccount () {
                Id = 1,
                EmailAddr = "john_doe@example.com",
            };

            Cred = new McCred () {
                Username = "dummy",
                Password = "password",
            };
        }
    }

    public class MockNcCommStatus : INcCommStatus
    {
        private static volatile MockNcCommStatus instance;

        private MockNcCommStatus () {}

        public static MockNcCommStatus Instance { 
            get {
                if (instance == null) {
                    instance = new MockNcCommStatus ();
                }
                return instance;
            } set {
                // allow MockNcCommStatus to be reset to null between tests
                instance = value;
            }
        }

        public void NetStatusEventHandler (Object sender, NetStatusEventArgs e) {}

        public event NcCommStatusServerEventHandler CommStatusServerEvent;
        public event NetStatusEventHandler CommStatusNetEvent;

        public void ReportCommResult (int serverId, bool didFailGenerally) {}
        public void ReportCommResult (string host, bool didFailGenerally)
        {
            Host = host;
            DidFailGenerally = didFailGenerally;
        }

        public string Host { get; set; }
        public bool DidFailGenerally { get; set; }

        public void Reset (int serverId) {}
        public void Refresh () {}
    }
}