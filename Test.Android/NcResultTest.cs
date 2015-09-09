using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;

namespace Test.Common
{
    public class NcResultTest : NcTestBase
    {
        [Test]
        public void Result ()
        {
            NcResult r;

            // OK
            r = NcResult.OK ();
            Assert.True (r.isOK ());
            Assert.False (r.isError ());
            Assert.IsNull (r.GetMessage ());
            Assert.IsNull (r.GetValue<string> ());

            // OK with an object
            r = NcResult.OK ("object");
            Assert.True (r.isOK ());
            Assert.False (r.isError ());
            Assert.IsNull (r.GetMessage ());
            Assert.IsNotNull (r.GetValue<string> ());
            Assert.IsNotNull (r.GetValue<object> ());
            Assert.AreEqual (r.GetValue<string> (), "object");
            Assert.AreEqual (r.GetValue<object> (), "object");
            Assert.AreNotEqual (r.GetValue<string> (), "foo");
            Assert.AreNotEqual (r.GetValue<object> (), "foo");

            // Error with a message
            r = NcResult.Error ("failure");
            Assert.False (r.isOK ());
            Assert.True (r.isError ());
            Assert.IsNull (r.GetValue<object> ());
            Assert.IsNull (r.GetValue<string> ());
            Assert.IsNotNull (r.GetMessage ());
            Assert.AreEqual (r.GetMessage (), "failure");
        }

        [Test]
        public void ResultToString ()
        {
            Assert.AreEqual ("NcResult(OK)", NcResult.OK ().ToString ());
            string bar = "bar";
            Assert.AreEqual ("NcResult(OK): Value=bar", NcResult.OK (bar).ToString ());

            Assert.AreEqual ("NcResult(Info): Message=Foo", NcResult.Info ("Foo").ToString ());
            Assert.AreEqual ("NcResult(Info): SubKind=Error_AccountDoesNotExist", NcResult.Info (NcResult.SubKindEnum.Error_AccountDoesNotExist).ToString ());

            Assert.AreEqual ("NcResult(Error): Message=Foo", NcResult.Error ("Foo").ToString ());
            Assert.AreEqual ("NcResult(Error): SubKind=Error_AccountDoesNotExist", NcResult.Error (NcResult.SubKindEnum.Error_AccountDoesNotExist).ToString ());
            Assert.AreEqual ("NcResult(Error): SubKind=Error_AccountDoesNotExist, Why=UnresolvedRecipient", NcResult.Error (NcResult.SubKindEnum.Error_AccountDoesNotExist, NcResult.WhyEnum.UnresolvedRecipient).ToString ());
            var err = NcResult.Error (NcResult.SubKindEnum.Error_AccountDoesNotExist, NcResult.WhyEnum.UnresolvedRecipient);
            err.Value = bar;
            Assert.AreEqual ("NcResult(Error): SubKind=Error_AccountDoesNotExist, Why=UnresolvedRecipient, Value=bar", err.ToString ());
            err.Value = new McEmailMessage ();
            Assert.AreEqual ("NcResult(Error): SubKind=Error_AccountDoesNotExist, Why=UnresolvedRecipient, Value=NachoCore.Model.McEmailMessage", err.ToString ());
        }

    }
}