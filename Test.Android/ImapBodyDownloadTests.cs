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
using System.Threading;
using System.Text;

namespace Test.iOS
{
    public class ImapBodyDownloadTest
    {
        int someIndex = 1;

        [SetUp]
        public void Setup ()
        {
        }

        [TearDown]
        public void Teardown ()
        {
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

        class TestImapFolder : IMailFolder
        {
            public Stream DataStream { get; set; }
            public UniqueId uidUsed { get; set; }
            public string SectionUsed { get; set; }

            public override Stream GetStream (UniqueId uid, string section, CancellationToken cancellationToken = default (CancellationToken), ITransferProgress progress = null)
            {
                return DataStream;
            }
        }

        [Test]
        public void TestBodyPreview ()
        {
            var stream = new MemoryStream (Encoding.UTF8.GetBytes ("83838383"));
            private string getPreviewFromBodyPart (UniqueId uid, BodyPartBasic part, IMailFolder mailKitFolder)

        }
    }
}
