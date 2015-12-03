//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;
using NachoPlatform;
using Test.iOS;
using System.Linq;

namespace Test.Common
{
    [TestFixture]
    public class CrlMonitorTest
    {
        public CrlMonitorTest ()
        {
        }

        [Test]
        public void CrlGetRevoked ()
        {
            // Sample CRL lifed from: http://www.edulib.com/keystores-manager/resources/doc/html/CERTivity/ch04s08.html
            string crl = @"-----BEGIN X509 CRL-----
MIIDFDCCAfwCAQEwDQYJKoZIhvcNAQEFBQAwXzEjMCEGA1UEChMaU2FtcGxlIFNp
Z25lciBPcmdhbml6YXRpb24xGzAZBgNVBAsTElNhbXBsZSBTaWduZXIgVW5pdDEb
MBkGA1UEAxMSU2FtcGxlIFNpZ25lciBDZXJ0Fw0xMzAyMTgxMDMyMDBaFw0xMzAy
MTgxMDQyMDBaMIIBNjA8AgMUeUcXDTEzMDIxODEwMjIxMlowJjAKBgNVHRUEAwoB
AzAYBgNVHRgEERgPMjAxMzAyMTgxMDIyMDBaMDwCAxR5SBcNMTMwMjE4MTAyMjIy
WjAmMAoGA1UdFQQDCgEGMBgGA1UdGAQRGA8yMDEzMDIxODEwMjIwMFowPAIDFHlJ
Fw0xMzAyMTgxMDIyMzJaMCYwCgYDVR0VBAMKAQQwGAYDVR0YBBEYDzIwMTMwMjE4
MTAyMjAwWjA8AgMUeUoXDTEzMDIxODEwMjI0MlowJjAKBgNVHRUEAwoBATAYBgNV
HRgEERgPMjAxMzAyMTgxMDIyMDBaMDwCAxR5SxcNMTMwMjE4MTAyMjUxWjAmMAoG
A1UdFQQDCgEFMBgGA1UdGAQRGA8yMDEzMDIxODEwMjIwMFqgLzAtMB8GA1UdIwQY
MBaAFL4SAcyq6hGA2i6tsurHtfuf+a00MAoGA1UdFAQDAgEDMA0GCSqGSIb3DQEB
BQUAA4IBAQBCIb6B8cN5dmZbziETimiotDy+FsOvS93LeDWSkNjXTG/+bGgnrm3a
QpgB7heT8L2o7s2QtjX2DaTOSYL3nZ/Ibn/R8S0g+EbNQxdk5/la6CERxiRp+E2T
UG8LDb14YVMhRGKvCguSIyUG0MwGW6waqVtd6K71u7vhIU/Tidf6ZSdsTMhpPPFu
PUid4j29U3q10SGFF6cCt1DzjvUcCwHGhHA02Men70EgZFADPLWmLg0HglKUh1iZ
WcBGtev/8VsUijyjsM072C6Ut5TwNyrrthb952+eKlmxLNgT0o5hVYxjXhtwLQsL
7QZhrypAM1DLYqQjkiDI7hlvt7QuDGTJ
-----END X509 CRL-----
";
            
            var snList = WrappedCrlMonitor.CrlGetRevoked (crl).ToArray ();
            Assert.AreEqual (5, snList.Length);
            Console.WriteLine ("SN List: {0}", string.Join (", ", snList));
            Assert.AreEqual ("147947", snList [0]);
            Assert.AreEqual ("147948", snList [1]);
            Assert.AreEqual ("147949", snList [2]);
            Assert.AreEqual ("14794A", snList [3]);
            Assert.AreEqual ("14794B", snList [4]);
        }
    }

    public class WrappedCrlMonitor : CrlMonitor
    {
        public WrappedCrlMonitor (string url) : base (url)
        {}

        public override INcHttpClient HttpClient {
            get {
                return MockHttpClient.Instance;
            }
        }

    }
}

