//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using MailKit;
using MimeKit;
using NachoCore.IMAP;
using NachoCore.Utils;
using System.IO;
using System.Linq;
using NachoCore;
using System.Security.Cryptography.X509Certificates;
using NachoCore.ActiveSync;
using System.Collections.Generic;

namespace Test.iOS
{
    public class ImapBodyDownloadTest : IBEContext
    {
        int someIndex = 1;
        McFolder TestFolder { get; set; }

        #region BEContext

        public INcProtoControlOwner Owner { set; get; }
        public NcProtoControl ProtoControl { set; get; }
        public McProtocolState ProtocolState { get; set; }
        public McServer Server { get; set; }
        public McAccount Account { get; set; }
        public McCred Cred { get; set; }

        #endregion

        class TestOwner : INcProtoControlOwner
        {
            #region INcProtoControlOwner implementation

            public void StatusInd (NcProtoControl sender, NcResult status)
            {
            }

            public void StatusInd (NcProtoControl sender, NcResult status, string[] tokens)
            {
            }

            public void CredReq (NcProtoControl sender)
            {
            }

            public void ServConfReq (NcProtoControl sender, object arg)
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

            #endregion
        }

        class TestProtoControl : NcProtoControl
        {
            public TestProtoControl (INcProtoControlOwner owner, int accountId) : base(owner, accountId)
            {
                
            }
        }

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount ();
            Account.Insert ();
            TestFolder = McFolder.Create (Account.Id, false, false, true, "0", "someServerId123", "MyFolder123", Xml.FolderHierarchy.TypeCode.DefaultInbox_2);
            TestFolder.Insert ();
            var p = new McProtocolState (){
                AccountId = Account.Id,
                ImapServerCapabilities = McProtocolState.NcImapCapabilities.None,
            };
            p.Insert ();
            ProtocolState = p;
            Owner = new TestOwner ();
            ProtoControl = new TestProtoControl (Owner, Account.Id);
        }

        [TearDown]
        public void Teardown ()
        {
            TestFolder.Delete ();
            Account.Delete ();
            ProtocolState.Delete ();
        }

        [Test]
        public void TestBodyTypeFromSummary ()
        {
            NcResult result;
            MessageSummary imapSummary = new MessageSummary (someIndex);

            // No headers and no body. Expect an error.
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("No headers nor body"));

            // Add headers, but not mime content type.
            imapSummary.Headers = new HeaderList ();
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("No Body Type found"));

            // Add a body part, but no content type
            imapSummary.Body = new BodyPartMultipart ();
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("No Body Type found"));

            // add a bogus Body content type
            imapSummary.Body.ContentType = new ContentType ("foo", "bar");
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("No Body Type found"));

            imapSummary.Body.ContentType = new ContentType ("text", "foo");
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("No Body Type found"));

            imapSummary.Body.ContentType = new ContentType ("multipart", "foo");
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isOK ());
            Assert.AreEqual (McAbstrFileDesc.BodyTypeEnum.MIME_4,
                result.GetValue<McAbstrFileDesc.BodyTypeEnum> ());

            imapSummary.Body.ContentType = new ContentType ("text", "html");
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isOK ());
            Assert.AreEqual (McAbstrFileDesc.BodyTypeEnum.HTML_2,
                result.GetValue<McAbstrFileDesc.BodyTypeEnum> ());

            imapSummary.Body.ContentType = new ContentType ("text", "plain");
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isOK ());
            Assert.AreEqual (McAbstrFileDesc.BodyTypeEnum.PlainText_1,
                result.GetValue<McAbstrFileDesc.BodyTypeEnum> ());

            // add a mime header. Header takes precedence over the bogus body content type.
            imapSummary.Body.ContentType = new ContentType ("foo", "bar");
            imapSummary.Headers.Add (new Header (HeaderId.MimeVersion, "XXX"));
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isOK ());
            Assert.AreEqual (McAbstrFileDesc.BodyTypeEnum.MIME_4,
                result.GetValue<McAbstrFileDesc.BodyTypeEnum> ());
        }

        const string imapStructureEmail1 = "(((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"utf-8\") NIL NIL \"7BIT\" 24 NIL NIL NIL NIL 2) (\"TEXT\" \"HTML\" (\"CHARSET\" \"utf-8\") \"<OYC4T8NOSVT4.P294HLXAOQ981@NachoPro.local>\" NIL \"7BIT\" 781 NIL NIL NIL NIL 19) \"ALTERNATIVE\" (\"BOUNDARY\" \"=-bTgg6onxZ7v48VEtC2ERiQ==\") NIL NIL NIL) (\"MESSAGE\" \"RFC822\" NIL NIL NIL \"7BIT\" 14347 NIL NIL NIL NIL (\"Thu, 06 Aug 2015 18:32:06 +0000\" \"Fwd: Your app status is Processing for App Store\" ((\"Jan Vilhuber\" NIL \"janv\" \"nachocove.com\")) ((\"Jan Vilhuber\" NIL \"janv\" \"nachocove.com\")) ((\"Jan Vilhuber\" NIL \"janv\" \"nachocove.com\")) ((\"jan.vilhuber@gmail.com\" NIL \"jan.vilhuber\" \"gmail.com\")) NIL NIL \"989468595.244035231437710042067.JavaMail.email@email.apple.com\" \"OSW8G3JNSVT4.K5DHTI9YJ7AL1@NachoPro.local\") ((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"iso-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 2136 NIL NIL NIL NIL 66) (\"TEXT\" \"HTML\" (\"CHARSET\" \"iso-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 7525 NIL NIL NIL NIL 225) \"ALTERNATIVE\" (\"BOUNDARY\" \"_000_OSW8G3JNSVT4K5DHTI9YJ7AL1NachoProlocal_\") NIL NIL NIL) 359) \"MIXED\" (\"BOUNDARY\" \"=-Tm3rrFfyFmoy7RAI9rnkMA==\") NIL NIL NIL)";
        const string imapStructureEmail2 = "(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"utf-8\") NIL NIL \"BASE64\" 1558 NIL NIL NIL NIL 25)";
        const string imapStructureEmail3 = "(((\"TEXT\" \"BAR\" (\"CHARSET\" \"utf-8\") NIL NIL \"7BIT\" 24 NIL NIL NIL NIL 2) (\"TEXT\" \"HTML\" (\"CHARSET\" \"utf-8\") \"<OYC4T8NOSVT4.P294HLXAOQ981@NachoPro.local>\" NIL \"7BIT\" 781 NIL NIL NIL NIL 19) \"ALTERNATIVE\" (\"BOUNDARY\" \"=-bTgg6onxZ7v48VEtC2ERiQ==\") NIL NIL NIL) (\"MESSAGE\" \"RFC822\" NIL NIL NIL \"7BIT\" 14347 NIL NIL NIL NIL (\"Thu, 06 Aug 2015 18:32:06 +0000\" \"Fwd: Your app status is Processing for App Store\" ((\"Jan Vilhuber\" NIL \"janv\" \"nachocove.com\")) ((\"Jan Vilhuber\" NIL \"janv\" \"nachocove.com\")) ((\"Jan Vilhuber\" NIL \"janv\" \"nachocove.com\")) ((\"jan.vilhuber@gmail.com\" NIL \"jan.vilhuber\" \"gmail.com\")) NIL NIL \"989468595.244035231437710042067.JavaMail.email@email.apple.com\" \"OSW8G3JNSVT4.K5DHTI9YJ7AL1@NachoPro.local\") ((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"iso-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 2136 NIL NIL NIL NIL 66) (\"TEXT\" \"HTML\" (\"CHARSET\" \"iso-8859-1\") NIL NIL \"QUOTED-PRINTABLE\" 7525 NIL NIL NIL NIL 225) \"ALTERNATIVE\" (\"BOUNDARY\" \"_000_OSW8G3JNSVT4K5DHTI9YJ7AL1NachoProlocal_\") NIL NIL NIL) 359) \"MIXED\" (\"BOUNDARY\" \"=-Tm3rrFfyFmoy7RAI9rnkMA==\") NIL NIL NIL)";
        const string imapStructureEmail4 = "(\"TEXT\" \"HTML\" (\"CHARSET\" \"utf-8\") NIL NIL \"BASE64\" 1558 NIL NIL NIL NIL 25)";

        [Test]
        public void TestBodyPreview ()
        {
            var protocolState = ProtocolState;
            var Strategy = new ImapStrategy (this);
            var syncKit = Strategy.GenSyncKit (ref protocolState, TestFolder, null, false);
            var imapClient = new NcImapClient ();
            var syncCmd = new ImapSyncCommand (this, imapClient, syncKit);

            BodyPart genericpart;
            List<BodyPartBasic> basicParts;
            BodyPart part;

            Assert.IsTrue (BodyPart.TryParse (imapStructureEmail1, out genericpart));
            basicParts = extractParts (genericpart);
            Assert.AreEqual (4, basicParts.Count);
            part = syncCmd.findPreviewablePart (basicParts);
            Assert.IsNotNull (part);
            Assert.IsTrue (part.ContentType.Matches ("text", "plain"));
            Assert.AreEqual ("1.1", part.PartSpecifier);

            Assert.IsTrue (BodyPart.TryParse (imapStructureEmail2, out genericpart));
            basicParts = extractParts (genericpart);
            Assert.AreEqual (1, basicParts.Count);
            part = syncCmd.findPreviewablePart (basicParts);
            Assert.IsNotNull (part);
            Assert.IsTrue (part.ContentType.Matches ("text", "plain"));
            // in this case, there is no part or subpart. There's just 'the message' which is an empty part.PartSpecifier
            Assert.AreEqual ("", part.PartSpecifier);

            Assert.IsTrue (BodyPart.TryParse (imapStructureEmail3, out genericpart));
            basicParts = extractParts (genericpart);
            Assert.AreEqual (4, basicParts.Count);
            part = syncCmd.findPreviewablePart (basicParts);
            Assert.IsNotNull (part);
            Assert.IsTrue (part.ContentType.Matches ("text", "html"));
            Assert.AreEqual ("1.2", part.PartSpecifier);

            Assert.IsTrue (BodyPart.TryParse (imapStructureEmail4, out genericpart));
            basicParts = extractParts (genericpart);
            Assert.AreEqual (1, basicParts.Count);
            part = syncCmd.findPreviewablePart (basicParts);
            Assert.IsNotNull (part);
            Assert.IsTrue (part.ContentType.Matches ("text", "html"));
            // in this case, there is no part or subpart. There's just 'the message' which is an empty part.PartSpecifier
            Assert.AreEqual ("", part.PartSpecifier);
        }

        List<BodyPartBasic> extractParts (BodyPart parts)
        {
            var p = new List<BodyPartBasic> ();
            if (parts is BodyPartText) {
                p.Add (parts as BodyPartBasic);
            } else if (parts is BodyPartMessage) {
                var mp = parts as BodyPartMessage;
                return extractParts (mp.Body);
            } else if (parts is BodyPartMultipart) {
                var mp = parts as BodyPartMultipart;
                foreach (var x in mp.BodyParts) {
                    p.AddRange (extractParts (x));
                }
            }
            return p;
        }
    }
}
