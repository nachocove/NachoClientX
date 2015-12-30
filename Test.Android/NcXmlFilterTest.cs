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
                "<partial xmlns=\"Filter1\">[10 redacted bytes]</partial>",
                "<none xmlns=\"Filter1\">Hi, world</none>",
            };

            NcXmlFilterSet filterSet = new NcXmlFilterSet ();
            filterSet.Add (new Filter1 ());
            for (int n = 0; n < xml.Length; n++) {
                XDocument docIn = XDocument.Parse (xml [n]);
                XDocument docOut = filterSet.Filter (docIn, new CancellationToken ());
                var outStr = docOut.ToString ();
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
                "<employee status=\"active\">" +
                "  <name>bob</name>" +
                "  <title>Accountant</title>" +
                "  <team>" +
                "    <member>robert</member>" +
                "    <member>robbie</member>" +
                "    <secret_member>rob</secret_member>" +
                "  </team>" +
                "</employee>"
            };

            string[] expected = {
                "<employee status=\"active\">\r\n" +
                "  <name>bob</name>\r\n" +
                "  <title>[10 redacted bytes]</title>\r\n" +
                "  <team>\r\n" +
                "    <member>robert</member>\r\n" +
                "    <member>robbie</member>\r\n" +
                "    <secret_member>[3 redacted bytes]</secret_member>\r\n" +
                "  </team>\r\n" +
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
    }

 


}

