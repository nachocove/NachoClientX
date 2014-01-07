using System;
using NUnit.Framework;
using NachoCore;
using NachoCore.Utils;

namespace Test.iOS
{
    [TestFixture]
    public class NcResultTest
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
    }
}