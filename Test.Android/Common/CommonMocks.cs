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


namespace Test.iOS
{
    // reusable request/response data
    public class CommonMockData
    {
        public static Uri MockUri = new Uri ("https://gmail.com");
        public static string Host = "gmail.com";

        public static XDocument MockRequestXml = XDocument.Parse (BasicPhonyPingRequestXml);
        public static XDocument MockResponseXml = XDocument.Parse (BasicPhonyPingResponseXml);
        public static byte[] Wbxml = MockResponseXml.ToWbxml ();

        public static XDocument MockAutodResponseXml = XDocument.Parse (AutodPhonyPingResponseXml);
        public static XDocument MockAutodResponseXml_otherdoc = XDocument.Parse (AutodPhonyPingResponseXml_otherdoc);

        public const string BasicPhonyPingRequestXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"no\"?>\n<Ping xmlns=\"Ping\">\n  <HeartbeatInterval>600</HeartbeatInterval>\n  <Folders>\n    <Folder>\n      <Id>1</Id>\n      <Class>Calendar</Class>\n    </Folder>\n    <Folder>\n      <Id>3</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>4</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>5</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>7</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>9</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>10</Id>\n      <Class>Email</Class>\n    </Folder>\n    <Folder>\n      <Id>2</Id>\n      <Class>Contacts</Class>\n    </Folder>\n  </Folders>\n</Ping>";
        public const string BasicPhonyPingResponseXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"yes\"?>\n<Ping xmlns=\"Ping\">\n  <Status>2</Status>\n  <Folders>\n    <Folder>3</Folder>\n  </Folders>\n</Ping>";
//        public const string AutodPhonyPingResponseXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"yes\"?>\n<Ping xmlns=\"Ping\">\n  <Response xmlns=\"Ping\">\n    <User>\n      <DisplayName>First Last</DisplayName>\n    </User>\n    <Account>\n      <AccountType>email</AccountType>\n      <Action>settings</Action>\n      <Protocol>\n        <Type>EXCH</Type>\n        <Server>MBX-SERVER.mail.internal.contoso.com</Server>\n        <ServerDN>(abbreviated for clarity)</ServerDN>\n        <ServerVersion>72008287</ServerVersion>\n        <MdbDN>(abbreviated for clarity)</MdbDN>\n        <ASUrl>https://mail.contoso.com/ews/exchange.asmx</ASUrl>\n        <OOFUrl>https://mail.contoso.com/ews/exchange.asmx</OOFUrl>\n        <UMUrl>https://mail.contoso.com/unifiedmessaging/service.asmx</UMUrl>\n        <OABUrl>https://mail.contoso.com/OAB/d29844a9-724e-468c-8820-0f7b345b767b/</OABUrl>\n      </Protocol>\n      <Protocol>\n        <Type>EXPR</Type>\n        <Server>Exchange.contoso.com</Server>\n        <ASUrl>https://mail.contoso.com/ews/exchange.asmx</ASUrl>\n        <OOFUrl>https://mail.contoso.com/ews/exchange.asmx</OOFUrl>\n        <UMUrl>https://mail.contoso.com/unifiedmessaging/service.asmx</UMUrl>\n        <OABUrl>https://mail.contoso.com/OAB/d29844a9-724e-468c-8820-0f7b345b767b/</OABUrl>\n      </Protocol>\n      <Protocol>\n        <Type>WEB</Type>\n        <Internal>\n          <OWAUrl AuthenticationMethod=\"Ntlm, WindowsIntegrated\">https://cas-01-server.mail.internal.contoso.com/owa</OWAUrl>\n          <OWAUrl AuthenticationMethod=\"Ntlm, WindowsIntegrated\">https://cas-02-server.mail.internal.contoso.com/owa</OWAUrl>\n          <OWAUrl AuthenticationMethod=\"Basic\">https://cas-04-server.mail.internal.contoso.com/owa</OWAUrl>\n          <OWAUrl AuthenticationMethod=\"Ntlm, WindowsIntegrated\">https://cas-05-server.mail.internal.contoso.com/owa</OWAUrl>\n        </Internal>\n      </Protocol>\n    </Account>\n  </Response>\n</Ping>\n";
        public const string AutodPhonyPingResponseXml = "<?xml version=\"1.0\" encoding=\"utf-16\" standalone=\"yes\"?>\n<Autodiscover xmlns=\"http://schemas.microsoft.com/exchange/autodiscover/responseschema/2006\"><Response xmlns=\"http://schemas.microsoft.com/exchange/autodiscover/outlook/responseschema/2006a\"><User><DisplayName>First Last</DisplayName><EMailAddress>john_doe@gmail.com</EMailAddress><LegacyDN>/o=contoso/ou=First Administrative Group/cn=Recipients/cn=iuser885646</LegacyDN><DeploymentId>644560b8-a1ce-429c-8ace-23395843f701</DeploymentId></User><Account><AccountType>email</AccountType><Action>settings</Action><Protocol><Type>EXCH</Type><Server>MBX-SERVER.mail.internal.contoso.com</Server><ServerDN>(abbreviated for clarity)</ServerDN><ServerVersion>72008287</ServerVersion><MdbDN>(abbreviated for clarity)</MdbDN><ASUrl>https://mail.contoso.com/ews/exchange.asmx</ASUrl><OOFUrl>https://mail.contoso.com/ews/exchange.asmx</OOFUrl><UMUrl>https://mail.contoso.com/unifiedmessaging/service.asmx</UMUrl><OABUrl>https://mail.contoso.com/OAB/d29844a9-724e-468c-8820-0f7b345b767b/</OABUrl></Protocol><Protocol><Type>EXPR</Type><Server>Exchange.contoso.com</Server><ASUrl>https://mail.contoso.com/ews/exchange.asmx</ASUrl><OOFUrl>https://mail.contoso.com/ews/exchange.asmx</OOFUrl><UMUrl>https://mail.contoso.com/unifiedmessaging/service.asmx</UMUrl><OABUrl>https://mail.contoso.com/OAB/d29844a9-724e-468c-8820-0f7b345b767b/</OABUrl></Protocol><Protocol><Type>WEB</Type><Internal><OWAUrl AuthenticationMethod=\"Ntlm, WindowsIntegrated\">https://cas-01-server.mail.internal.contoso.com/owa</OWAUrl><OWAUrl AuthenticationMethod=\"Ntlm, WindowsIntegrated\">https://cas-02-server.mail.internal.contoso.com/owa</OWAUrl><OWAUrl AuthenticationMethod=\"Basic\">https://cas-04-server.mail.internal.contoso.com/owa</OWAUrl><OWAUrl AuthenticationMethod=\"Ntlm, WindowsIntegrated\">https://cas-05-server.mail.internal.contoso.com/owa</OWAUrl></Internal></Protocol></Account></Response></Autodiscover>";
        public const string AutodPhonyPingResponseXml_otherdoc = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\"><autodiscover:Response><autodiscover:Culture>en:us</autodiscover:Culture><autodiscover:User><autodiscover:DisplayName>Chris Gray</autodiscover:DisplayName><autodiscover:EMailAddress>chris@woodgrovebank.com</autodiscover:EMailAddress></autodiscover:User><autodiscover:Action><autodiscover:Settings><autodiscover:Server><autodiscover:Type>MobileSync</autodiscover:Type><autodiscover:Url>https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync</autodiscover:Url><autodiscover:Name>https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync</autodiscover:Name></autodiscover:Server><autodiscover:Server><autodiscover:Type>CertEnroll</autodiscover:Type><autodiscover:Url>https://cert.woodgrovebank.com/CertEnroll</autodiscover:Url><autodiscover:Name /><autodiscover:ServerData>CertEnrollTemplate</autodiscover:ServerData></autodiscover:Server></autodiscover:Settings></autodiscover:Action></autodiscover:Response></Autodiscover>";

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
                EmailAddr = "john_doe@gmail.com",
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