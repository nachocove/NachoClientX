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
            Assert.IsNull (r.GetObject ());
            Assert.IsNull (r.GetString ());

            // OK with an object
            r = NcResult.OK ("object");
            Assert.True (r.isOK ());
            Assert.False (r.isError ());
            Assert.IsNull (r.GetMessage ());
            Assert.IsNotNull (r.GetObject ());
            Assert.IsNotNull (r.GetString ());
            Assert.AreEqual (r.GetString (), "object");
            Assert.AreEqual (r.GetObject (), "object");
            Assert.AreNotEqual (r.GetString (), "foo");
            Assert.AreNotEqual (r.GetObject (), "foo");

            // Error with a message
            r = NcResult.Error ("failure");
            Assert.False (r.isOK ());
            Assert.True (r.isError ());
            Assert.IsNull (r.GetObject ());
            Assert.IsNull (r.GetString ());
            Assert.IsNotNull (r.GetMessage ());
            Assert.AreEqual (r.GetMessage(), "failure");

        }
    }
}