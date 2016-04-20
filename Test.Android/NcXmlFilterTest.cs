//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Xml.Linq;
using System.Threading;
using NUnit.Framework;
using NachoCore.Utils;
using NachoCore.Wbxml;

namespace Test.Common
{
    public class NcXmlFilterTest
    {
        public NcXmlFilterTest ()
        {
        }

        public class Filter1 : NcXmlFilter
        {
            public Filter1 () : base ("Filter1")
            {
                NcXmlFilterNode node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
                node0.Add (new NcXmlFilterNode ("full", RedactionType.FULL, RedactionType.FULL));
                node0.Add (new NcXmlFilterNode ("partial", RedactionType.LENGTH, RedactionType.LENGTH));
                node0.Add (new NcXmlFilterNode ("none", RedactionType.NONE, RedactionType.NONE));

                Root = node0;
            }
        }

        [Test]
        public void NcXmlFilterTest1 ()
        {
            string[] xml = {
                // verify full redaction of both elements and attributes
                "<full xmlns=\"Filter1\" name1=\"Bob\" name2=\"John\">Hello, world</full>",
                // verify partial redaction of elements and attributes
                "<partial xmlns=\"Filter1\" name1=\"Mark\" name2=\"Jim\">Hey, world</partial>",
                // verify no redaction of elements and attributes
                "<none xmlns=\"Filter1\" name1=\"Kim\" name2=\"Mary\">Hi, world</none>",
            };

            string[] expected = {
                "<full xmlns=\"Filter1\" />",
                "<partial xmlns=\"Filter1\" name1=\"[4 redacted bytes]\" name2=\"[3 redacted bytes]\">[10 redacted bytes]</partial>",
                "<none xmlns=\"Filter1\" name1=\"Kim\" name2=\"Mary\">Hi, world</none>",
            };

            NcXmlFilterSet filterSet = new NcXmlFilterSet ();
            filterSet.Add (new Filter1 ());
            for (int n = 0; n < xml.Length; n++) {
                XDocument docIn = XDocument.Parse (xml [n]);
                XDocument docOut = filterSet.Filter (docIn, new CancellationToken ());
                var outStr = docOut.ToString ();
                Console.Write ("Docout: {0}", outStr);
                Assert.AreEqual (expected [n], outStr);
            }
        }

        public class Filter2 : NcXmlFilter
        {
            public Filter2 () : base ("Filter2")
            {
                NcXmlFilterNode node0, node1, node2;

                node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);
                node1 = new NcXmlFilterNode ("employee", RedactionType.NONE, RedactionType.NONE);
                node0.Add (node1);
                node1.Add (new NcXmlFilterNode ("name", RedactionType.NONE, RedactionType.NONE));
                node1.Add (new NcXmlFilterNode ("salary", RedactionType.FULL, RedactionType.FULL));
                node1.Add (new NcXmlFilterNode ("title", RedactionType.LENGTH, RedactionType.LENGTH));
                node2 = new NcXmlFilterNode ("team", RedactionType.NONE, RedactionType.NONE);
                node1.Add (node2);
                node2.Add (new NcXmlFilterNode ("secret_member", RedactionType.LENGTH, RedactionType.LENGTH));
                node2.Add (new NcXmlFilterNode ("member", RedactionType.NONE, RedactionType.NONE));
                Root = node0;
            }
        }

        [Test]
        public void NcXmlFilterTest2 ()
        {
            string[] xml = {
                "<employee xmlns=\"Filter2\" status=\"active\">\n" +
                "  <name>bob</name>\n" +
                "  <title>Accountant</title>\n" +
                "  <team>\n" +
                "    <member>robert</member>\n" +
                "    <member>robbie</member>\n" +
                "    <secret_member>rob</secret_member>\n" +
                "  </team>\n" +
                "</employee>",
                "<employee xmlns:filter2=\"Filter2\" status=\"active\">\n" +
                "  <filter2:name>bob</filter2:name>\n" +
                "  <filter2:title>Accountant</filter2:title>\n" +
                "  <filter2:team>\n" +
                "    <filter2:member>robert</filter2:member>\n" +
                "    <filter2:member>robbie</filter2:member>\n" +
                "    <filter2:secret_member>rob</filter2:secret_member>\n" +
                "  </filter2:team>\n" +
                "</employee>"

            };

            string[] expected = {
                "<employee xmlns=\"Filter2\" status=\"active\">\n" +
                "  <name>bob</name>\n" +
                "  <title>[10 redacted bytes]</title>\n" +
                "  <team>\n" +
                "    <member>robert</member>\n" +
                "    <member>robbie</member>\n" +
                "    <secret_member>[3 redacted bytes]</secret_member>\n" +
                "  </team>\n" +
                "</employee>",
                "<employee xmlns:filter2=\"Filter2\" status=\"active\">\n" +
                "  <filter2:name>bob</filter2:name>\n" +
                "  <filter2:title>[10 redacted bytes]</filter2:title>\n" +
                "  <filter2:team>\n" +
                "    <filter2:member>robert</filter2:member>\n" +
                "    <filter2:member>robbie</filter2:member>\n" +
                "    <filter2:secret_member>[3 redacted bytes]</filter2:secret_member>\n" +
                "  </filter2:team>\n" +
                "</employee>"
            };

            NcXmlFilterSet filterSet = new NcXmlFilterSet ();
            filterSet.Add (new Filter2 ());
            for (int n = 0; n < xml.Length; n++) {
                XDocument docIn = XDocument.Parse (xml [n]);
                XDocument docOut = filterSet.Filter (docIn, new CancellationToken ());
                var outStr = docOut.ToString ();
                Assert.AreEqual (expected [n], outStr);
            }
        }

        [Test]
        public void AutodiscoverFilterTest ()
        {
            // example output from https://msdn.microsoft.com/en-us/library/hh352638(v=exchg.140).aspx
            string autoDxml = "<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n    <autodiscover:Response>\n        <autodiscover:Culture>en:us</autodiscover:Culture>\n        <autodiscover:User>\n           <autodiscover:EMailAddress>chris@woodgrovebank.com</autodiscover:EMailAddress>\n       </autodiscover:User>\n       <autodiscover:Action>\n           <autodiscover:Error>\n               <Status>1</Status>\n               <Message>The directory service could not be reached</Message>\n               <DebugData>MailUser</DebugData>\n           </autodiscover:Error>\n       </autodiscover:Action>\n    </autodiscover:Response>\n</Autodiscover>\n";
            string expectedData = "<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n  <autodiscover:Response>\n    <autodiscover:Culture>en:us</autodiscover:Culture>\n    <autodiscover:User>\n      <autodiscover:EMailAddress />\n    </autodiscover:User>\n    <autodiscover:Action>\n      <autodiscover:Error>\n        <Status>1</Status>\n        <Message>The directory service could not be reached</Message>\n        <DebugData>MailUser</DebugData>\n      </autodiscover:Error>\n    </autodiscover:Action>\n  </autodiscover:Response>\n</Autodiscover>";
            var docIn = XDocument.Parse (autoDxml);
            NcXmlFilterSet filterSet = new NcXmlFilterSet ();
            filterSet.Add (new AutoDiscoverXmlFilter ());
            var docOut = filterSet.Filter (docIn, default(CancellationToken));
            Assert.AreEqual (expectedData, docOut.ToString ());

            autoDxml = "<Autodiscover\nxmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n    <autodiscover:Response>\n        <autodiscover:Culture>en:us</autodiscover:Culture>\n        <autodiscover:User>\n            <autodiscover:DisplayName>Chris Gray</autodiscover:DisplayName>\n            <autodiscover:EMailAddress>chris@woodgrovebank.com</autodiscover:EMailAddress>\n        </autodiscover:User>\n        <autodiscover:Action>\n            <autodiscover:Settings>\n                <autodiscover:Server>\n                    <autodiscover:Type>MobileSync</autodiscover:Type>\n                    <autodiscover:Url>\n                        https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync\n                    </autodiscover:Url>\n                    <autodiscover:Name>\n                 https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync\n             </autodiscover:Name>\n                </autodiscover:Server>\n                <autodiscover:Server>\n                    <autodiscover:Type>CertEnroll</autodiscover:Type>\n                    <autodiscover:Url>https://cert.woodgrovebank.com/CertEnroll</autodiscover:Url>\n                    <autodiscover:Name />\n                   <autodiscover:ServerData>CertEnrollTemplate</autodiscover:ServerData>\n                </autodiscover:Server>\n            </autodiscover:Settings>\n        </autodiscover:Action>\n    </autodiscover:Response>\n</Autodiscover>";
            expectedData = "<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n  <autodiscover:Response>\n    <autodiscover:Culture>en:us</autodiscover:Culture>\n    <autodiscover:User>\n      <autodiscover:DisplayName />\n      <autodiscover:EMailAddress />\n    </autodiscover:User>\n    <autodiscover:Action>\n      <autodiscover:Settings>\n        <autodiscover:Server>\n          <autodiscover:Type>MobileSync</autodiscover:Type>\n          <autodiscover:Url>\n                        https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync\n                    </autodiscover:Url>\n          <autodiscover:Name>\n                 https://loandept.woodgrovebank.com/Microsoft-Server-ActiveSync\n             </autodiscover:Name>\n        </autodiscover:Server>\n        <autodiscover:Server>\n          <autodiscover:Type>CertEnroll</autodiscover:Type>\n          <autodiscover:Url>https://cert.woodgrovebank.com/CertEnroll</autodiscover:Url>\n          <autodiscover:Name />\n          <autodiscover:ServerData>CertEnrollTemplate</autodiscover:ServerData>\n        </autodiscover:Server>\n      </autodiscover:Settings>\n    </autodiscover:Action>\n  </autodiscover:Response>\n</Autodiscover>";
            docIn = XDocument.Parse (autoDxml);
            filterSet = new NcXmlFilterSet ();
            filterSet.Add (new AutoDiscoverXmlFilter ());
            docOut = filterSet.Filter (docIn, default(CancellationToken));
            Assert.AreEqual (expectedData, docOut.ToString ());

            autoDxml = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n    <autodiscover:Response>\n        <autodiscover:Culture>en:us</autodiscover:Culture>\n        <autodiscover:User>\n           <autodiscover:DisplayName>Chris Gray</autodiscover:DisplayName>\n           <autodiscover:EMailAddress>chris@woodgrovebank.com</autodiscover:EMailAddress>\n        </autodiscover:User>\n        <autodiscover:Action>\n           <autodiscover:Redirect>chris@loandept.woodgrovebank.com </autodiscover:Redirect>\n        </autodiscover:Action>\n    </autodiscover:Response>\n</Autodiscover>\n";
            expectedData = "<Autodiscover xmlns:autodiscover=\"http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006\">\n  <autodiscover:Response>\n    <autodiscover:Culture>en:us</autodiscover:Culture>\n    <autodiscover:User>\n      <autodiscover:DisplayName />\n      <autodiscover:EMailAddress />\n    </autodiscover:User>\n    <autodiscover:Action>\n      <autodiscover:Redirect />\n    </autodiscover:Action>\n  </autodiscover:Response>\n</Autodiscover>";
            docIn = XDocument.Parse (autoDxml);
            filterSet = new NcXmlFilterSet ();
            filterSet.Add (new AutoDiscoverXmlFilter ());
            docOut = filterSet.Filter (docIn, default(CancellationToken));
            Assert.AreEqual (expectedData, docOut.ToString ());
        }

        [Test]
        public void AsXmlFilterSettingsResponseTest ()
        {
            string Settings = "<Settings>\n    <Status>1</Status>\n    <UserInformation>\n        <Status>1</Status>\n        <Get>\n            <EmailAddresses>\n                <SMTPAddress>janv@d3.officeburrito.com</SMTPAddress>\n            </EmailAddresses>\n        </Get>\n    </UserInformation>\n    <DeviceInformation>\n        <Status>1</Status>\n    </DeviceInformation>\n</Settings>";
            string expectedData = "<Settings>\n  <Status>1</Status>\n  <UserInformation>\n    <Status>1</Status>\n    <Get>\n      <EmailAddresses>\n        <SMTPAddress />\n      </EmailAddresses>\n    </Get>\n  </UserInformation>\n  <DeviceInformation>\n    <Status>1</Status>\n  </DeviceInformation>\n</Settings>";
            var docIn = XDocument.Parse (Settings);
            var filterSet = new NcXmlFilterSet ();
            var filter = new AsXmlFilterSettingsResponse ();
            filterSet.Add (filter);
            var docOut = filterSet.Filter (docIn, default(CancellationToken));
            Assert.AreEqual (expectedData, docOut.ToString ());
        }

    }
}

