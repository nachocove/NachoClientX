//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Linq;
using NachoCore.Utils;
using System.Threading;
using NachoCore.ActiveSync;
using NachoCore;
using NachoCore.Model;
using NachoPlatform;
using System.Xml.Linq;
using System.Security.Cryptography.X509Certificates;
using NUnit.Framework;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.IO;
using System.Text;



namespace Test.iOS
{
    public enum MockSteps
    {
        S1,
        S2,
        S3,
        S4,
        S5,
        Other,
    };

    // reusable request/response data
    public static class CommonMockData
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

    public class MockHttpClient : INcHttpClient
    {
        // TODO: do we need to go the factory route and get rid of the statics?
        public delegate void ExamineHttpRequestMessageDelegate (NcHttpRequest request);

        public static ExamineHttpRequestMessageDelegate ExamineHttpRequestMessage { set; get; }

        // Provide the request message so that the type of auto-d can be checked
        public delegate NcHttpResponse ProvideHttpResponseMessageDelegate (NcHttpRequest request);

        public static ProvideHttpResponseMessageDelegate ProvideHttpResponseMessage { set; get; }

        // Turn on/off server certificate validation callback
        public delegate bool HasServerCertificateDelegate ();

        public static HasServerCertificateDelegate HasServerCertificate { set; get; }

        public static uint AsyncCalledCount { set; get; }

        public TimeSpan Timeout { get; set; }

        private MockHttpClient ()
        {
            
        }

        private static object LockObj = new object ();
        static MockHttpClient _Instance { get; set; }
        public static MockHttpClient Instance {
            get {
                if (_Instance == null) {
                    lock (LockObj) {
                        if (_Instance == null) {
                            _Instance = new MockHttpClient ();
                        }
                    }
                }
                return _Instance;
            }
        }

        void doRequest (NcHttpRequest request, NcHttpResponse response, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            if (response != null) {
                success (response, cancellationToken);
            } else {
                error (new Exception ("FOO"), cancellationToken);
            }
        }

        #region INcHttpClient implementation

        public void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            GetRequest (request, timeout, success, error, null, cancellationToken);
        }

        string utopiasystems_cer = "-----BEGIN CERTIFICATE-----\nMIICCzCCAXQCCQDoS8XUL9bSozANBgkqhkiG9w0BAQUFADBKMQswCQYDVQQGEwJV\nUzELMAkGA1UECBMCQ0ExFTATBgNVBAcTDFNvbGFuYSBCZWFjaDEXMBUGA1UEChMO\nVXRvcGlhIFN5c3RlbXMwHhcNMTQwNTIyMTkwOTM1WhcNMTUwNTIyMTkwOTM1WjBK\nMQswCQYDVQQGEwJVUzELMAkGA1UECBMCQ0ExFTATBgNVBAcTDFNvbGFuYSBCZWFj\naDEXMBUGA1UEChMOVXRvcGlhIFN5c3RlbXMwgZ8wDQYJKoZIhvcNAQEBBQADgY0A\nMIGJAoGBAMhoMLiT0Vk11Q4piLMnDfR5CyssqMKDxyBkQwNbmDboneJhXpxASDpT\nRjASXUWrbIJ0pcvIFcpSr/Cxa1jNy1dhPiQQHfkocWzrIu1mloHAIqQBKSsMapJV\nN+9PcmfZXdkU6VAhcXpb/WPsIP08/2tT+r134ss4KWniY48kV1IvAgMBAAEwDQYJ\nKoZIhvcNAQEFBQADgYEAjfcBGi1A5FAqDmBzhIfqkfHblnQNo7ehlDUGx0yAzzcw\nmBL3+awMzcYgSa3X1z6GQ8DzQ1Jy6eCiGjWal0bWR+gJSQAo9q/IwuHFe1Sa6fEa\nfhWL+g9yNN9EFPwWzWUXRO6lrM3jCMGLX/pDC2mqyMCSBGniT8BX5bmQaFo1ZQU=\n-----END CERTIFICATE-----";
        public void GetRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            // provide validated certificate
            var webRequest = WebRequest.Create (CommonMockData.RedirectionUrl);

            var hasCert = true;
            if (null != HasServerCertificate) {
                hasCert = HasServerCertificate ();
            }

            X509Certificate mockCert = new X509Certificate (Encoding.ASCII.GetBytes(utopiasystems_cer));

            if (hasCert) {
                ServerCertificatePeek.CertificateValidationCallback (webRequest, mockCert, new X509Chain (), new SslPolicyErrors ());
            }

            var response = new NcHttpResponse (request.Method, HttpStatusCode.OK);
            NcTask.Run (() => doRequest (request, response, timeout, success, error, progress, cancellationToken), "MockHttpClient.GetRequest");
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, CancellationToken cancellationToken)
        {
            SendRequest (request, timeout, success, error, null, cancellationToken);
        }

        public void SendRequest (NcHttpRequest request, int timeout, SuccessDelegate success, ErrorDelegate error, ProgressDelegate progress, CancellationToken cancellationToken)
        {
            AsyncCalledCount++;

            if (null != ExamineHttpRequestMessage) {
                ExamineHttpRequestMessage (request);
            }
            NcTask.Run (() => {
                try {
                    using (var response = ProvideHttpResponseMessage (request)) {
                        doRequest (request, response, timeout, success, error, progress, cancellationToken);
                    }
                } catch (Exception ex) {
                    error (ex, cancellationToken);
                }
            }, "MockHttpClient.SendRequest");
        }

        #endregion
    }

    public class MockContext : IBEContext
    {
        public INcProtoControlOwner Owner { set; get; }

        public NcProtoControl ProtoControl { set; get; }

        public McProtocolState ProtocolState { get; set; }

        public McServer Server { get; set; }

        public McAccount Account { set; get; }

        public McCred Cred { set; get; }

        public MockContext (McAccount account, AsProtoControl protoControl = null, McServer server = null)
        {
            Owner = new MockOwner ();
           
            // READ AsPolicyKey
            // R/W AsProtocolVersion
            // READ InitialProvisionCompleted
            Server = server; 

            if (null == account) {
                Account = new McAccount () {
                    EmailAddr = "johnd@foo.utopiasystems.net",
                };
                Account.Insert ();
            } else {
                Account = account;
            }
            var protoState = McProtocolState.QueryByAccountId<McProtocolState> (Account.Id).SingleOrDefault ();
            if (null == protoState) {
                protoState = new McProtocolState () {
                    AccountId = Account.Id,
                };
                protoState.Insert ();
            }
            ProtocolState = protoState;
            if (null == protoControl) {
                ProtoControl = new AsProtoControl (Owner, Account.Id);
            } else {
                ProtoControl = protoControl;
            }
            Cred = new McCred () {
                CredType = McCred.CredTypeEnum.Password,
                AccountId = Account.Id,
                Username = "dummy",
            };
            Cred.Insert ();
            Cred.UpdatePassword ("DummyPassword");
        }
    }

    public class MockOwner : INcProtoControlOwner
    {
        // register a callback in order to track StatusInd notifications
        public delegate void ViewStatusIndMessageDelegate (NcResult result);

        public static event ViewStatusIndMessageDelegate StatusIndCallback;

        // Helper property added for test purposes only
        public static NcResult Status { get; set; }

        // Use these to check which error code was posted
        public void StatusInd (NcProtoControl sender, NcResult status)
        {
            if (StatusIndCallback != null) {
                StatusIndCallback (status);
            }
            Status = status;
        }

        public void StatusInd (NcProtoControl sender, NcResult status, string[] tokens)
        {
            if (StatusIndCallback != null) {
                StatusIndCallback (status);
            }
            Status = status;
        }

        public MockOwner ()
        {
            Status = null;
        }

        // we aren't interested in these
        public void CredReq (NcProtoControl sender)
        {
        }

        public void ServConfReq (NcProtoControl sender, NachoCore.BackEnd.AutoDFailureReasonEnum arg)
        {
        }

        public void CertAskReq (NcProtoControl sender, X509Certificate2 certificate)
        {
        }

        public void SearchContactsResp (NcProtoControl sender, string prefix, string token)
        {
        }

        public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend)
        {
        }

        public void BackendAbateStart ()
        {
        }


        public void BackendAbateStop ()
        {
        }

    }

    public class MockNcCommStatus : INcCommStatus
    {
        private static volatile MockNcCommStatus instance;

        private MockNcCommStatus ()
        {
        }

        public NetStatusStatusEnum Status { get; set; }

        public NetStatusSpeedEnum Speed { get; set; }

        public static MockNcCommStatus Instance { 
            get {
                if (instance == null) {
                    instance = new MockNcCommStatus ();
                }
                return instance;
            }
            set {
                // allow MockNcCommStatus to be reset to null between tests
                instance = value;
            }
        }

        public bool IsRateLimited (int serverId)
        {
            return false;
        }

        public void NetStatusEventHandler (Object sender, NetStatusEventArgs e)
        {
        }

        #pragma warning disable 067
        public event NcCommStatusServerEventHandler CommStatusServerEvent;
        public event NetStatusEventHandler CommStatusNetEvent;
        #pragma warning restore 067

        public void ReportCommResult (int serverId, DateTime delayUntil)
        {
        }

        public void ReportCommResult (int accountId, string host, DateTime delayUntil)
        {
            AccountId = accountId;
            Host = host;
            DelayUntil = delayUntil;
        }

        public void ReportCommResult (int serverId, bool didFailGenerally)
        {
        }

        public void ReportCommResult (int accountId, McAccount.AccountCapabilityEnum capabilities, bool didFailGenerally)
        {
            AccountId = accountId;
            Capabilities = capabilities;
            DidFailGenerally = didFailGenerally;
        }

        public void ReportCommResult (int accountId, string host, bool didFailGenerally)
        {
            AccountId = accountId;
            Host = host;
            DidFailGenerally = didFailGenerally;
        }

        public int AccountId { get; set; }

        public string Host { get; set; }

        public McAccount.AccountCapabilityEnum Capabilities { get; set; }

        public bool DidFailGenerally { get; set; }

        public DateTime DelayUntil;

        public void Reset (int serverId)
        {
        }

        public void Refresh (string tag)
        {
        }
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

        public MoveKit GenMoveKit ()
        {
            return null;
        }

        public FetchKit GenFetchKit ()
        {
            return null;
        }

        public FetchKit GenFetchKitHints ()
        {
            return null;
        }

        public SyncKit GenSyncKit (McProtocolState protocolState)
        {
            return new NachoCore.ActiveSync.SyncKit () {
                OverallWindowSize = 1,
                PerFolders = new List<NachoCore.ActiveSync.SyncKit.PerFolder> () { 
                    new NachoCore.ActiveSync.SyncKit.PerFolder () {
                        Folder = Folder,
                        Commands = PendList,
                        WindowSize = 1,
                        FilterCode = Xml.Provision.MaxAgeFilterCode.TwoWeeks_4,
                        GetChanges = true,
                    }
                },
            };
        }

        public PingKit GenPingKit (McProtocolState protocolState, bool isNarrow, bool stillHaveUnsyncedFolders, bool ignoreToClientExpected)
        {
            return new NachoCore.ActiveSync.PingKit () {
                MaxHeartbeatInterval = 600,
                Folders = new List<McFolder> (),
            };
        }

        public SyncKit GenSyncKitFromPingKit (McProtocolState protocolState, PingKit pingKit)
        {
            throw new NotImplementedException ();
        }

        public Tuple<PickActionEnum, AsCommand> PickUserDemand ()
        {
            return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait, null);
        }

        public Tuple<PickActionEnum, AsCommand> Pick ()
        {
            return Tuple.Create<PickActionEnum, AsCommand> (PickActionEnum.Wait, null);
        }

        public int UploadTimeoutSecs (long length)
        {
            return DefaultTimeoutSecs;
        }

        public int DownloadTimeoutSecs (long length)
        {
            return DefaultTimeoutSecs;
        }

        public int DefaultTimeoutSecs { get { return 30; } }
    }
}
