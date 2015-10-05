//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Model;
using MailKit;
using MimeKit;
using NachoCore.IMAP;
using NachoCore.Utils;
using System.Collections.Generic;

namespace Test.iOS
{
    public class ImapBodyDownloadTest
    {
        int someIndex = 1;

        McAccount Account;
        McFolder TestFolder;

        [SetUp]
        public void Setup ()
        {
            Account = new McAccount () {
            };
            Account.Insert ();

            TestFolder = new McFolder () {
                AccountId = 1,
                ServerId = "folder1",
                DisplayName = "FooFolder",
            };
            TestFolder.Insert ();
            NcModel.Instance.Db.DeleteAll<McEmailMessage> ();
        }

        [TearDown]
        public void Teardown ()
        {
            TestFolder.Delete ();
            Account.Delete ();
            NcModel.Instance.Db.DeleteAll<McEmailMessage> ();
        }

        [Test]
        public void TestFetchBodyFromEmail ()
        {
            List<Tuple<int?, string>> TestBodies = new List<Tuple<int?, string>> () {
                new Tuple<int?, string>(null, "(\"TEXT\" \"HTML\" (\"CHARSET\" \"UTF-8\") NIL NIL \"7BIT\" 133 NIL NIL NIL NIL 4)"),
                new Tuple<int?, string>(null, "(\"TEXT\" \"PLAIN\" (\"CHARSET\" \"utf-8\") NIL NIL \"7BIT\" 90 NIL NIL NIL NIL 7)"),
                new Tuple<int?, string>(null, "((\"TEXT\" \"HTML\" (\"CHARSET\" \"UTF-8\") NIL NIL \"QUOTED-PRINTABLE\" 37996 NIL NIL NIL NIL 489) \"ALTERNATIVE\" (\"BOUNDARY\" \"----=_Part_933832_197359920.1443126110090\") NIL NIL NIL)"),
                new Tuple<int?, string> (2, "((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"us-ascii\") NIL NIL \"QUOTED-PRINTABLE\" 0 NIL NIL NIL NIL 0) (\"APPLICATION\" \"VND.OPENXMLFORMATS-OFFICEDOCUMENT.WORDPROCESSINGML.DOCUMENT\" (\"NAME\" \"Jan 2.docx\") \"<7423F57C0F2F364E9CC652F41A75890F@prod.exchangelabs.com>\" \"Jan 2.docx\" \"BASE64\" 645060 NIL (\"ATTACHMENT\" (\"CREATION-DATE\" \"Wed, 30 Sep 2015 22:38:03 GMT\" \"FILENAME\" \"Jan 2.docx\" \"MODIFICATION-DATE\" \"Wed, 30 Sep 2015 22:38:03 GMT\" \"SIZE\" \"471390\")) NIL NIL) \"MIXED\" (\"BOUNDARY\" \"_002_88D46C5406914A07925CCADACC88D9E0nachocovecom_\") NIL NIL NIL)"),
                new Tuple<int?, string> (5, "(((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"us-ascii\") NIL NIL \"QUOTED-PRINTABLE\" 21 NIL NIL NIL NIL 2) (\"TEXT\" \"HTML\" (\"CHARSET\" \"us-ascii\") \"<E8BA53760FB0EE4F8A927B7D0E5076E2@prod.exchangelabs.com>\" NIL \"QUOTED-PRINTABLE\" 364 NIL NIL NIL NIL 13) \"ALTERNATIVE\" (\"BOUNDARY\" \"_000_1912C59D99EC4692A80D6748C8112C90nachocovecom_\") NIL NIL NIL) (\"APPLICATION\" \"VND.OPENXMLFORMATS-OFFICEDOCUMENT.WORDPROCESSINGML.DOCUMENT\" (\"NAME\" \"Jan 2.docx\") \"<B00850B3CAD8104ABC3D30ABAF2F511B@prod.exchangelabs.com>\" \"Jan 2.docx\" \"BASE64\" 645060 NIL (\"ATTACHMENT\" (\"CREATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"FILENAME\" \"Jan 2.docx\" \"MODIFICATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"SIZE\" \"471390\")) NIL NIL) (\"TEXT\" \"HTML\" (\"NAME\" \"ATT00001.htm\") \"<FD6E27C2C65D7740935A66326CAE001A@prod.exchangelabs.com>\" \"ATT00001.htm\" \"BASE64\" 478 NIL (\"ATTACHMENT\" (\"CREATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"FILENAME\" \"ATT00001.htm\" \"MODIFICATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"SIZE\" \"348\")) NIL NIL 7) (\"IMAGE\" \"PNG\" (\"NAME\" \"Screen Shot 2015-09-30 at 3.15.39 PM.png\") \"<51E0BCB766517B4AA478518A77690F02@prod.exchangelabs.com>\" \"Screen Shot 2015-09-30 at 3.15.39 PM.png\" \"BASE64\" 31156 NIL (\"ATTACHMENT\" (\"CREATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"FILENAME\" \"Screen Shot 2015-09-30 at 3.15.39 PM.png\" \"MODIFICATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"SIZE\" \"22766\")) NIL NIL) (\"TEXT\" \"HTML\" (\"NAME\" \"ATT00002.htm\") \"<F468CA4B4959DD4F842FCCF755DE6715@prod.exchangelabs.com>\" \"ATT00002.htm\" \"BASE64\" 420 NIL (\"ATTACHMENT\" (\"CREATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"FILENAME\" \"ATT00002.htm\" \"MODIFICATION-DATE\" \"Thu, 01 Oct 2015 21:52:23 GMT\" \"SIZE\" \"304\")) NIL NIL 6) \"MIXED\" (\"BOUNDARY\" \"_007_1912C59D99EC4692A80D6748C8112C90nachocovecom_\") NIL NIL NIL)"),
                new Tuple<int?, string> (null, "(((\"TEXT\" \"PLAIN\" (\"CHARSET\" \"UTF-8\") NIL NIL \"7BIT\" 1256 NIL NIL NIL NIL 39) (\"TEXT\" \"HTML\" (\"CHARSET\" \"UTF-8\") NIL NIL \"QUOTED-PRINTABLE\" 1804 NIL NIL NIL NIL 29) \"ALTERNATIVE\" (\"BOUNDARY\" \"----=_Part_5892_862749002.1443700429195\") NIL NIL NIL) \"MIXED\" (\"BOUNDARY\" \"----=_Part_5891_1127761857.1443700429195\") NIL NIL NIL)"),
            };
            foreach (var testInfo in TestBodies) {
                var email = new McEmailMessage () {
                    AccountId = 1,
                    Subject = "FooEmailSubject",
                    ImapBodyStructure = testInfo.Item2,
                    ServerId = Guid.NewGuid ().ToString (),
                };
                email.Insert ();
                TestFolder.Link (email);

                var fetchBody = ImapStrategy.FetchBodyFromEmail (email);
                Assert.NotNull (fetchBody);
                Assert.AreEqual (email.ServerId, fetchBody.ServerId);
                Assert.AreEqual (TestFolder.ServerId, fetchBody.ParentId);
                if (!testInfo.Item1.HasValue) {
                    Assert.Null (fetchBody.Parts, string.Format ("Body Structure: {0}", testInfo.Item2));
                } else {
                    Assert.NotNull (fetchBody.Parts, string.Format ("Body Structure: {0}", testInfo.Item2));
                    Assert.AreEqual (testInfo.Item1.Value, fetchBody.Parts.Count, string.Format ("Body Structure: {0}", testInfo.Item2));
                }
            }
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

    }
}
