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
using System.Xml.Linq;
using SQLite;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Net;
using NUnit.Framework;
using System.Collections.Generic;


namespace Test.iOS
{
    public enum MockSteps
    {
        S1,
        S2,
        S3,
        S4,
        Other,
    };

    // reusable request/response data
    public class CommonMockData
    {
        // utopiasystems is used because they have an Autodiscover SRV response
        public static Uri MockUri = new Uri ("https://utopiasystems.net");
        public static string Host = "utopiasystems.net";
        public static string SubHost = "foo.utopiasystems.net";

        // DNS tests depends on RedirectionUrl being exactly this because the certificate contains this url
        public static string RedirectionUrl = "https://mail.utopiasystems.net./autodiscover/autodiscover.xml"; 
        public static string InvalidRedirUrl = "http://invalid.utopiasystems.net/autodiscover/autodiscover.xml";
        public static string PhonyAbsolutePath = "/Microsoft-Server-ActiveSync";

        public static XDocument MockRequestXml = XDocument.Parse (BasicPhonyPingRequestXml);
        public static XDocument MockResponseXml = XDocument.Parse (BasicPhonyPingResponseXml);
        public static byte[] Wbxml = MockResponseXml.ToWbxml ();

        // These responses cannot be used for Auto-d
        public const string BasicPhonyPingRequestXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"no\"?>\n<Ping xmlns=\"Ping\">\n  <HeartbeatInterval>600</HeartbeatInterval>\n  <Folders>\n    <Folder>\n      <Id>1</Id>\n      <Class>Calendar</Class>\n    </Folder>\n    <Folder>\n      <Id>3</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>4</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>5</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>7</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>9</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>10</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>2</Id>\n      <Class>Contacts</Class>\n    </Folder>\n  </Folders>\n</Ping>";
        public const string BasicPhonyPingResponseXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"yes\"?>\n<Ping xmlns=\"Ping\">\n  <Status>2</Status>\n  <Folders>\n    <Folder>3</Folder>\n  </Folders>\n</Ping>";

        // This response is from http://msdn.microsoft.com/en-us/library/hh352638(v=exchg.140).aspx
        public const string AutodPhonyPingResponseXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\"><autodiscover:Response><autodiscover:Culture>en:us</autodiscover:Culture><autodiscover:User><autodiscover:DisplayName>John Doe</autodiscover:DisplayName><autodiscover:EMailAddress>johnd@utopiasystems.net</autodiscover:EMailAddress></autodiscover:User><autodiscover:Action><autodiscover:Settings><autodiscover:Server><autodiscover:Type>MobileSync</autodiscover:Type><autodiscover:Url>https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync</autodiscover:Url><autodiscover:Name>https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync</autodiscover:Name></autodiscover:Server><autodiscover:Server><autodiscover:Type>CertEnroll</autodiscover:Type><autodiscover:Url>https://cert.woodgrovebank.com/CertEnroll</autodiscover:Url><autodiscover:Name /><autodiscover:ServerData>CertEnrollTemplate</autodiscover:ServerData></autodiscover:Server></autodiscover:Settings></autodiscover:Action></autodiscover:Response></Autodiscover>";
        // This response is a real response from the nachocove.com domain
        public const string AutodOffice365ResponseXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Autodiscover xmlns=\"http://schemas.microsoft.com/exchange/autodiscover/responseschema/2006\"><Response xmlns=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\"><Culture>en:us</Culture><User><DisplayName>John Doe</DisplayName><EMailAddress>johnd@utopiasystems.net</EMailAddress></User><Action><Settings><Server><Type>MobileSync</Type><Url>https://outlook.office365.com/Microsoft-Server-ActiveSync</Url><Name>https://outlook.office365.com/Microsoft-Server-ActiveSync</Name></Server></Settings></Action></Response></Autodiscover>";

        public const string AutodPhonyErrorResponse = "<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n    <autodiscover:Response>\n        <autodiscover:Culture>en:us</autodiscover:Culture>\n        <autodiscover:User>\n           <autodiscover:EMailAddress>johnd@utopiasystems.net</autodiscover:EMailAddress>\n       </autodiscover:User>\n       <autodiscover:Action>\n           <autodiscover:Error>\n               <Status>1</Status>\n               <Message>The directory service could not be reached</Message>\n               <DebugData>MailUser</DebugData>\n           </autodiscover:Error>\n       </autodiscover:Action>\n    </autodiscover:Response>\n</Autodiscover>";
        public const string AutodPhonyRedirectResponse = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n    <autodiscover:Response>\n        <autodiscover:Culture>en:us</autodiscover:Culture>\n        <autodiscover:User>\n           <autodiscover:DisplayName>John Doe</autodiscover:DisplayName>\n           <autodiscover:EMailAddress>johnd@utopiasystems.net</autodiscover:EMailAddress>\n        </autodiscover:User>\n        <autodiscover:Action>\n           <autodiscover:Redirect>johnd@redir.utopiasystems.net </autodiscover:Redirect>\n        </autodiscover:Action>\n    </autodiscover:Response>\n</Autodiscover>";

        // Invalid Request
        public const string AutodPhony600Response = "<?xml version=\"1.0\" encoding=\"utf-8\" ?>  \n<Autodiscover xmlns=\"http://schemas.microsoft.com/exchange/autodiscover/responseschema/2006\">\n  <Response>\n    <Error Time=\"09:28:57.8739220\" Id=\"2715916636\">\n      <ErrorCode>600</ErrorCode>\n      <Message>Invalid Request</Message>\n      <DebugData />\n    </Error>\n  </Response>\n</Autodiscover>";
        // Schema version not supported by server
        public const string AutodPhony601Response = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Autodiscover\nxmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n   <autodiscover:Response>\n      <autodiscover:Error Time=\"16:56:32.6164027\" Id=\"1054084152\">\n          <autodiscover:ErrorCode>601</autodiscover:ErrorCode>\n          <autodiscover:Message>Requested schema version not supported</autodiscover:Message>\n          <autodiscover:DebugData />\n      </autodiscover:Error>\n   </autodiscover:Response>\n</Autodiscover>";
    }

    public class MockHttpClient : IHttpClient
    {
        // TODO: do we need to go the factory route and get rid of the statics?
        public delegate void ExamineHttpRequestMessageDelegate (HttpRequestMessage request);
        public static ExamineHttpRequestMessageDelegate ExamineHttpRequestMessage { set; get; }

        // Provide the request message so that the type of auto-d can be checked
        public delegate HttpResponseMessage ProvideHttpResponseMessageDelegate (HttpRequestMessage request);
        public static ProvideHttpResponseMessageDelegate ProvideHttpResponseMessage { set; get; }

        // Turn on/off server certificate validation callback
        public delegate bool HasServerCertificateDelegate ();
        public static HasServerCertificateDelegate HasServerCertificate { set; get; }

        public static uint AsyncCalledCount { set; get; }

        public TimeSpan Timeout { get; set; }

        public MockHttpClient (HttpClientHandler handler)
        {
        }

        public Task<HttpResponseMessage> GetAsync (Uri uri)
        {
            // provide validated certificate
            var webRequest = WebRequest.Create (CommonMockData.RedirectionUrl);

            var hasCert = true;
            if (null != HasServerCertificate) {
                hasCert = HasServerCertificate ();
            }

            // cert is under resources in Test.iOS and Test.Android
            X509Certificate mockCert = new X509Certificate ("utopiasystems.cer");

            if (hasCert) {
                ServerCertificatePeek.CertificateValidationCallback (webRequest, mockCert, new X509Chain (), new SslPolicyErrors ());
            }

            // Create and return a mock response
            var mockResponse = new HttpResponseMessage () {};
            return Task.Run<HttpResponseMessage> (delegate {
                return mockResponse;
            });
        }

        public Task<HttpResponseMessage> SendAsync (HttpRequestMessage request, 
            HttpCompletionOption completionOption,
            CancellationToken cancellationToken)
        {
            AsyncCalledCount++;

            if (null != ExamineHttpRequestMessage) {
                ExamineHttpRequestMessage (request);
            }

            return Task.Run<HttpResponseMessage> (delegate {
                return ProvideHttpResponseMessage (request);
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

        public MockContext (AsProtoControl protoControl = null, McServer server = null)
        {
            Owner = new MockOwner ();
            ProtoControl = protoControl;
            var protoState = new McProtocolState ();
            protoState.Insert ();
            ProtocolState = protoState;
            // READ AsPolicyKey
            // R/W AsProtocolVersion
            // READ InitialProvisionCompleted
            Server = server; 
            Account = new McAccount () {
                Id = 1,
                EmailAddr = "johnd@foo.utopiasystems.net",
                ServerId = 1,
            };

            Cred = new McCred () {
                Username = "dummy",
                Password = "password",
            };
        }
    }

    public class MockOwner : IProtoControlOwner
    {
        // Helper property added for test purposes only
        public static NcResult Status { get; set; }

        // Use these to check which error code was posted
        public void StatusInd (ProtoControl sender, NcResult status)
        {
            Status = status;
        }
        public void StatusInd (ProtoControl sender, NcResult status, string[] tokens)
        {
            Status = status;
        }

        public MockOwner ()
        {
            Status = null;
        }

        // we aren't interested in these
        public void CredReq (ProtoControl sender) {}
        public void ServConfReq (ProtoControl sender) {}
        public void CertAskReq (ProtoControl sender, X509Certificate2 certificate) {}
        public void SearchContactsResp (ProtoControl sender, string prefix, string token) {}
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

        public void ReportCommResult (int serverId, DateTime delayUntil)
        {
        }

        public void ReportCommResult (string host, DateTime delayUntil)
        {
            Host = host;
            DelayUntil = delayUntil;
        }

        public void ReportCommResult (int serverId, bool didFailGenerally)
        {
        }

        public void ReportCommResult (string host, bool didFailGenerally)
        {
            Host = host;
            DidFailGenerally = didFailGenerally;
        }

        public string Host { get; set; }
        public bool DidFailGenerally { get; set; }
        public DateTime DelayUntil;

        public void Reset (int serverId) {}
        public void Refresh () {}
    }

    public class MockStrategy : IAsStrategy
    {
        public McFolder Folder;
        public List<McPending> PendList;

        public MockStrategy ()
        {
            PendList = new List<McPending> ();
        }

        public MockStrategy (McFolder folder) : this ()
        {
            Folder = folder;
        }

        public void ReportSyncResult (System.Collections.Generic.List<McFolder> folders) {}

        public Tuple<uint, System.Collections.Generic.List<Tuple<McFolder, System.Collections.Generic.List<McPending>>>> SyncKit ()
        {
            uint windowSize = 1; // TODO determine a good default window size
            var retList = new List<Tuple<McFolder, List<McPending>>> ();
            retList.Add (Tuple.Create (Folder, PendList));

            return Tuple.Create (windowSize, retList);
        }

        public bool IsMoreSyncNeeded () { return false; }

        public System.Collections.Generic.IEnumerable<McFolder> PingKit () { throw new NotImplementedException (); }

        public bool RequestQuickFetch { get; set; }

        public bool IsMoreFetchingNeeded ()
        {
            return false;
        }

        public Tuple<IEnumerable<McPending>, IEnumerable<Tuple<McAbstrItem, string>>> FetchKit ()
        {
            return null;
        }
    }
}
