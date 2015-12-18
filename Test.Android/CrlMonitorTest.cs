//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using NachoCore.Utils;
using NachoPlatform;
using Test.iOS;
using System.Security.Cryptography.X509Certificates;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace Test.Common
{
    [TestFixture]
    public class CrlMonitorTest
    {
        string nachocove_crl = "-----BEGIN X509 CRL-----\nMIICzTCBtgIBATANBgkqhkiG9w0BAQsFADBKMQswCQYDVQQGEwJVUzEZMBcGA1UE\nCgwQTmFjaG8gQ292ZSwgSW5jLjEgMB4GA1UEAwwXTmFjaG8gQ292ZSBDUkwgRGVs\nZWdhdGUXDTE1MTIxODAzNTkyMloXDTE2MDExNzAzNTkyMlowKDASAgEIFw0xNTEy\nMTgwMzU4NTRaMBICAQkXDTE1MTIxODAzNTkyMlqgDjAMMAoGA1UdFAQDAgEDMA0G\nCSqGSIb3DQEBCwUAA4ICAQBAvOO9g+06s6W0ZXvasEbD+JWU93epOWTcvJ3qmnP1\ntN8mQygp9RdMJlJLd4y7pwilIS/WVKUuR3/PlIzBsOe+xIfIcU/Vc6a4yY2elwyy\nLkuUGTJt+/brl80FTH0se4x1tpTfXuBeBOQsPF+JuAAAe0OVmx2YD8IRu0EpIXRv\nhhQV2rnINiTRdU/RIv429MTyrGBgwHfnZX2kY2OxGG6g4KqGa9sp6XEBrBbm3bJb\nNzt7eC/phVScb27pMhHSuZ92dcMVNAKIFZtt0RvdAOcTKQwHbX0rcJO7zRyucEdZ\ntEUCRp3U32BN/fgM2pPYJ5+DSZD+71efLH7hf8W+1hjiOKptJPHvzt69X/EJxDMt\nJFprCek99Wg2Vr9pUK9y+3KRoy2Tz4Lt473V8cVa+S+oVCRNmOp944RRdl5lqrdm\n/H9TPRVrhpYMMoRrH1P4C3HVQniiTBCSz2+pB/sRqQgM1mbJyCj+VPGTVZG4zpsG\npmE1JvLExgyh7PfqfqZkOqAfh7Esweq3IIRMRruMMCnL4UoWzwhfgI3kObRbMHhP\nMkXOhtay7Xm5xtNZihpR8o1G3+ST5cp8G8S1eJCTPaHkJoSqoqDSmGUSPBCVlybH\neTcY+/+Rp0CUaAXAa6s8nb9nCPN7lmwwj6Fz7XLKFHu2rTndhmmJgkIaW4OcdmZn\n6A==\n-----END X509 CRL-----";
        string nachocove_ca_cert = "-----BEGIN CERTIFICATE-----\n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\n3WaO7R8wBlUKOTpN4gqwtTA=\n-----END CERTIFICATE-----";
        string nachocove_crl_cert = "-----BEGIN CERTIFICATE-----\nMIIFsDCCA5igAwIBAgIBAjANBgkqhkiG9w0BAQsFADCBkDELMAkGA1UEBhMCVVMx\nCzAJBgNVBAgMAkNBMRUwEwYDVQQHDAxTb2xhbmEgQmVhY2gxGDAWBgNVBAoMD05h\nY2hvIENvdmUsIEluYzEdMBsGA1UEAwwUTmFjaG8gQ292ZSBSb290IENBIDExJDAi\nBgkqhkiG9w0BCQEWFXN1cHBvcnRAbmFjaG9jb3ZlLmNvbTAeFw0xNTAzMTgxNzQx\nMzRaFw0zNTAzMTMxNzQxMzRaMEoxCzAJBgNVBAYTAlVTMRkwFwYDVQQKDBBOYWNo\nbyBDb3ZlLCBJbmMuMSAwHgYDVQQDDBdOYWNobyBDb3ZlIENSTCBEZWxlZ2F0ZTCC\nAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBALIXb7vP3A9Q8R3c2/f3fylC\nSj61TATI0dkJ1lbPAciCdH7nIaMVWxM73gt+xOi+wmoLc1NmeBxckZ3uz2r2tvDx\nvxsIKCawhQBREHfxQ5LI5Am7s+OHf6ZSY0L6rJDh5C6KAFth7IHRBZiljbdpEBxu\njYdLxF2Dw/KtlOP0CU6v0ZMCc1AfL3bqx1rIThfxtM8FIsNT3OKed+JlK3sOG9Sx\nibtmZmD4J9JLDqyOf3QUY40BfpzTweAQJRpD9KR6sYkcfueNCL3Mymvj1lmtCJqs\nBylsZEu1cR3Qq5lQNeNikOHpTwVPwXmcWS5FzQdILYp6lU08RqONNsNRuo650Tfp\nz8HFcOcuHh+fqrHyKWcy43B0sQbyp8AKoPAPyVXGrQoTocvKG1khpe81iZBjYCk/\nEcvP13Z7gzuWZoXyXC4Hvbs20e7eLyIqnudtOJLLmeNVziPvUdR7pSOtJGDA6VBj\nOjrEQ/xJ+MV6cMh/HnyPylt3RVv/xmqrpDEhVgQjqRTb0RXFWbzPdyJfW7DrNvMS\ny+cRBynaLhI1mgAoQ5Xi2sOzWLOinMPGbXNO3fOoPzh1JvZh2t95KnPddpk47yrt\nKujOllhLwi7BvpqD9loPiz4mEP44XImnGIgiZgBc7N2atrc06CoD1yZrv8dfZmrV\nWqEKkQK1YsqJsEJ8q5mZAgMBAAGjWjBYMAkGA1UdEwQCMAAwCwYDVR0PBAQDAgEC\nMB0GA1UdDgQWBBQk25SdCLd7FrkrBZoiOKq5Xj31jzAfBgNVHSMEGDAWgBThFLCv\nvfNAsfYBAcy63LKn3SvyQzANBgkqhkiG9w0BAQsFAAOCAgEAemmWIn0SKlwV3zZr\n7ipLS7pKK3lN4OkNAJ44DxPRF+sLSOClgU5bnvPoeytnK8ppdfRB+wkuDUB14V4v\n03GEVd+0WmVA/aEBQNxPRF5uXOITvuncbWahD++pEQVbGXMMj/kIr0u7sj7zF2yl\nbdMUUv4VCIw0o+8bdpJUWatLP5mlFbFYnRq6JldLlQZ/mdQyfo7X4l9rZvBddGNx\nREZptUF8ZAE0/Y8cVo3wbb/LX8NR0AFiUgaNprxhWtXduXUZwWtBabX04Wjw+cG0\nwyI0kIGTylOHIRRCpaXnH2k7dFIikR0ZBiTufQxa1JGeFwAFkVUZG+Oz2NRf4gUY\nU+hVr/PLqX71tunPmHJWD3fO5MAnO0TOg9V3F9kN/keEJhoP5x4MATdoY2Jx+vxk\nr8M+vgHJqfJziDcxLcgj3l6JwTHFTlBl8RAWogww1hilrKVPvK8LxA5nRX1OlkP0\nhQe8mrRuk/3/oXgvfFtSEsAz1MepX97ULzdP7ajqkHWiITfzUCCsf9tLAjaOB0Eg\n1eVfssBtuzxQkRzElDcryIzL2gFr07xZYD+anlj0AeCWoI9livQVvkdCnitcvD47\nYJo97btfI7gaobyfjJSH7fVehzXjA8qjvEx8kqdGeywUZ286j0ziie2OFsY8cZJr\n/clRp/TBoYj034oBjvcOFPg045c=\n-----END CERTIFICATE-----";

        [Test]
        public void CrlGetRevoked ()
        {
            var monitor = new WrappedCrlMonitor (1, "http://foo.bar.com",
                              new X509Certificate2 (Encoding.ASCII.GetBytes (nachocove_ca_cert)),
                              new X509Certificate2 (Encoding.ASCII.GetBytes (nachocove_crl_cert)));
            Assert.IsTrue (monitor.ExtractCrl (nachocove_crl));
            monitor.CrlGetRevoked ();
            var snTable = monitor.getRevoked ();
            Assert.AreEqual (2, snTable.Count);
            Assert.IsTrue (snTable.Contains ("9"));
            Assert.IsTrue (snTable.Contains ("8"));
        }

        [Test]
        public void CrlGetRevokedBadSigner ()
        {
            var monitor = new WrappedCrlMonitor (1, "http://foo.bar.com",
                              new X509Certificate2 (Encoding.ASCII.GetBytes (nachocove_ca_cert)));
            Assert.IsFalse (monitor.ExtractCrl (nachocove_crl));
        }
    }

    public class WrappedCrlMonitor : CrlMonitorItem
    {
        public WrappedCrlMonitor (int id, string url, X509Certificate2 cacert, X509Certificate2 delegateSigner = null) : base (id, url, cacert, delegateSigner)
        {
        }

        public override INcHttpClient HttpClient {
            get {
                return MockHttpClient.Instance;
            }
        }

        public bool ExtractCrl (string crl)
        {
            return base.ExtractCrl (new MemoryStream (Encoding.ASCII.GetBytes (crl)), failOnExpired: false);
        }

        public void CrlGetRevoked ()
        {
            base.CrlGetRevoked ();
        }

        public HashSet<string> getRevoked ()
        {
            return base.Revoked;
        }
    }
}

