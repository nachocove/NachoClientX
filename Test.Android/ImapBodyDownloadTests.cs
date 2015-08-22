//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using MailKit;
using MimeKit;
using NachoCore.IMAP;
using NachoCore.Utils;

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
            Assert.True (result.GetMessage ().Contains ("No Body found"));

            // Add a body part, but no content type
            imapSummary.Body = new BodyPartMultipart ();
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("No ContentType found in body."));

            // add a bogus Body content type
            imapSummary.Body.ContentType = new ContentType ("foo", "bar");
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("Unhandled contenttype"));

            imapSummary.Body.ContentType = new ContentType ("text", "foo");
            result = ImapFetchCommand.BodyTypeFromSummary (imapSummary);
            Assert.NotNull (result);
            Assert.True (result.isError ());
            Assert.True (result.GetMessage ().Contains ("Unhandled text subtype"));

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

    }
}
