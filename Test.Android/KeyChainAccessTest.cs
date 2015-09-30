//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoPlatform;

namespace Test.iOS
{
    public class KeyChainAccessTest
    {
        public KeyChainAccessTest ()
        {
        }

        const int KKeyChainId = 1;
        [TearDown]
        public void TearDown ()
        {
            Keychain.Instance.DeleteLogSalt (KKeyChainId);
        }

        [Test]
        public void TestKeyChainLogSalt ()
        {
            Assert.IsTrue (Keychain.Instance.HasKeychain ());

            var logSalt = "Foo1234";
            Assert.IsTrue (Keychain.Instance.SetLogSalt (KKeyChainId, logSalt));
            Assert.AreEqual (logSalt, Keychain.Instance.GetLogSalt (KKeyChainId));
        }

        [Test]
        public void TestKeyChainLogSaltFail ()
        {
            Keychain.Instance.DeleteLogSalt (KKeyChainId);
            Assert.IsNull (Keychain.Instance.GetLogSalt (KKeyChainId));
        }

        [Test]
        [Ignore("Ignoring long running unit test.")]
        public void TestKeyChainLogSaltMulti ()
        {
            Assert.IsTrue (Keychain.Instance.HasKeychain ());

            var logSalt = "Foo1234";
            var retryCount = 1000000;
            var loopSalt = logSalt + "-0000";
            Assert.IsTrue (Keychain.Instance.SetLogSalt (KKeyChainId, loopSalt));
            for (var i = 0; i < retryCount; i++) {
                if (i % 1000 == 0) {
                    loopSalt = string.Format ("{0}-{1}", logSalt, i);
                    Assert.IsTrue (Keychain.Instance.SetLogSalt (KKeyChainId, loopSalt), string.Format ("loopSalt not set on iteration {0}", i));
                    Console.WriteLine ("Iteration {0}", i);
                }
                Assert.AreEqual (loopSalt, Keychain.Instance.GetLogSalt (KKeyChainId), string.Format ("loopSalt not found on iteration {0}", i));
            }
        }

    }
}

