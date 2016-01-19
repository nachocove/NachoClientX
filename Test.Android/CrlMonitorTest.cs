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

        [Test]
        public void CrlGetRevoked ()
        {
            string nachocove_crl = "-----BEGIN X509 CRL-----\nMIICzTCBtgIBATANBgkqhkiG9w0BAQsFADBKMQswCQYDVQQGEwJVUzEZMBcGA1UE\nCgwQTmFjaG8gQ292ZSwgSW5jLjEgMB4GA1UEAwwXTmFjaG8gQ292ZSBDUkwgRGVs\nZWdhdGUXDTE1MTIxODAzNTkyMloXDTE2MDExNzAzNTkyMlowKDASAgEIFw0xNTEy\nMTgwMzU4NTRaMBICAQkXDTE1MTIxODAzNTkyMlqgDjAMMAoGA1UdFAQDAgEDMA0G\nCSqGSIb3DQEBCwUAA4ICAQBAvOO9g+06s6W0ZXvasEbD+JWU93epOWTcvJ3qmnP1\ntN8mQygp9RdMJlJLd4y7pwilIS/WVKUuR3/PlIzBsOe+xIfIcU/Vc6a4yY2elwyy\nLkuUGTJt+/brl80FTH0se4x1tpTfXuBeBOQsPF+JuAAAe0OVmx2YD8IRu0EpIXRv\nhhQV2rnINiTRdU/RIv429MTyrGBgwHfnZX2kY2OxGG6g4KqGa9sp6XEBrBbm3bJb\nNzt7eC/phVScb27pMhHSuZ92dcMVNAKIFZtt0RvdAOcTKQwHbX0rcJO7zRyucEdZ\ntEUCRp3U32BN/fgM2pPYJ5+DSZD+71efLH7hf8W+1hjiOKptJPHvzt69X/EJxDMt\nJFprCek99Wg2Vr9pUK9y+3KRoy2Tz4Lt473V8cVa+S+oVCRNmOp944RRdl5lqrdm\n/H9TPRVrhpYMMoRrH1P4C3HVQniiTBCSz2+pB/sRqQgM1mbJyCj+VPGTVZG4zpsG\npmE1JvLExgyh7PfqfqZkOqAfh7Esweq3IIRMRruMMCnL4UoWzwhfgI3kObRbMHhP\nMkXOhtay7Xm5xtNZihpR8o1G3+ST5cp8G8S1eJCTPaHkJoSqoqDSmGUSPBCVlybH\neTcY+/+Rp0CUaAXAa6s8nb9nCPN7lmwwj6Fz7XLKFHu2rTndhmmJgkIaW4OcdmZn\n6A==\n-----END X509 CRL-----";
            string nachocove_ca_cert = "-----BEGIN CERTIFICATE-----\n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\n3WaO7R8wBlUKOTpN4gqwtTA=\n-----END CERTIFICATE-----";
            string nachocove_crl_cert = "-----BEGIN CERTIFICATE-----\nMIIFsDCCA5igAwIBAgIBAjANBgkqhkiG9w0BAQsFADCBkDELMAkGA1UEBhMCVVMx\nCzAJBgNVBAgMAkNBMRUwEwYDVQQHDAxTb2xhbmEgQmVhY2gxGDAWBgNVBAoMD05h\nY2hvIENvdmUsIEluYzEdMBsGA1UEAwwUTmFjaG8gQ292ZSBSb290IENBIDExJDAi\nBgkqhkiG9w0BCQEWFXN1cHBvcnRAbmFjaG9jb3ZlLmNvbTAeFw0xNTAzMTgxNzQx\nMzRaFw0zNTAzMTMxNzQxMzRaMEoxCzAJBgNVBAYTAlVTMRkwFwYDVQQKDBBOYWNo\nbyBDb3ZlLCBJbmMuMSAwHgYDVQQDDBdOYWNobyBDb3ZlIENSTCBEZWxlZ2F0ZTCC\nAiIwDQYJKoZIhvcNAQEBBQADggIPADCCAgoCggIBALIXb7vP3A9Q8R3c2/f3fylC\nSj61TATI0dkJ1lbPAciCdH7nIaMVWxM73gt+xOi+wmoLc1NmeBxckZ3uz2r2tvDx\nvxsIKCawhQBREHfxQ5LI5Am7s+OHf6ZSY0L6rJDh5C6KAFth7IHRBZiljbdpEBxu\njYdLxF2Dw/KtlOP0CU6v0ZMCc1AfL3bqx1rIThfxtM8FIsNT3OKed+JlK3sOG9Sx\nibtmZmD4J9JLDqyOf3QUY40BfpzTweAQJRpD9KR6sYkcfueNCL3Mymvj1lmtCJqs\nBylsZEu1cR3Qq5lQNeNikOHpTwVPwXmcWS5FzQdILYp6lU08RqONNsNRuo650Tfp\nz8HFcOcuHh+fqrHyKWcy43B0sQbyp8AKoPAPyVXGrQoTocvKG1khpe81iZBjYCk/\nEcvP13Z7gzuWZoXyXC4Hvbs20e7eLyIqnudtOJLLmeNVziPvUdR7pSOtJGDA6VBj\nOjrEQ/xJ+MV6cMh/HnyPylt3RVv/xmqrpDEhVgQjqRTb0RXFWbzPdyJfW7DrNvMS\ny+cRBynaLhI1mgAoQ5Xi2sOzWLOinMPGbXNO3fOoPzh1JvZh2t95KnPddpk47yrt\nKujOllhLwi7BvpqD9loPiz4mEP44XImnGIgiZgBc7N2atrc06CoD1yZrv8dfZmrV\nWqEKkQK1YsqJsEJ8q5mZAgMBAAGjWjBYMAkGA1UdEwQCMAAwCwYDVR0PBAQDAgEC\nMB0GA1UdDgQWBBQk25SdCLd7FrkrBZoiOKq5Xj31jzAfBgNVHSMEGDAWgBThFLCv\nvfNAsfYBAcy63LKn3SvyQzANBgkqhkiG9w0BAQsFAAOCAgEAemmWIn0SKlwV3zZr\n7ipLS7pKK3lN4OkNAJ44DxPRF+sLSOClgU5bnvPoeytnK8ppdfRB+wkuDUB14V4v\n03GEVd+0WmVA/aEBQNxPRF5uXOITvuncbWahD++pEQVbGXMMj/kIr0u7sj7zF2yl\nbdMUUv4VCIw0o+8bdpJUWatLP5mlFbFYnRq6JldLlQZ/mdQyfo7X4l9rZvBddGNx\nREZptUF8ZAE0/Y8cVo3wbb/LX8NR0AFiUgaNprxhWtXduXUZwWtBabX04Wjw+cG0\nwyI0kIGTylOHIRRCpaXnH2k7dFIikR0ZBiTufQxa1JGeFwAFkVUZG+Oz2NRf4gUY\nU+hVr/PLqX71tunPmHJWD3fO5MAnO0TOg9V3F9kN/keEJhoP5x4MATdoY2Jx+vxk\nr8M+vgHJqfJziDcxLcgj3l6JwTHFTlBl8RAWogww1hilrKVPvK8LxA5nRX1OlkP0\nhQe8mrRuk/3/oXgvfFtSEsAz1MepX97ULzdP7ajqkHWiITfzUCCsf9tLAjaOB0Eg\n1eVfssBtuzxQkRzElDcryIzL2gFr07xZYD+anlj0AeCWoI9livQVvkdCnitcvD47\nYJo97btfI7gaobyfjJSH7fVehzXjA8qjvEx8kqdGeywUZ286j0ziie2OFsY8cZJr\n/clRp/TBoYj034oBjvcOFPg045c=\n-----END CERTIFICATE-----";

            var cert = new X509Certificate2 (Encoding.ASCII.GetBytes (nachocove_ca_cert));
            var crl_cert = new X509Certificate2 (Encoding.ASCII.GetBytes (nachocove_crl_cert));
            var signers = new X509Certificate2Collection ();
            signers.Add (cert);
            signers.Add (crl_cert);

            var urls = CrlMonitor.CDPUrls (cert);
            Assert.AreEqual (1, urls.Count);
            Assert.AreEqual (0, CrlMonitor.CDPUrls (crl_cert).Count);

            var monitor = new WrappedCrlMonitor (1, cert, urls, signers);
            Assert.NotNull (monitor);
            Assert.IsTrue (monitor.ExtractCrl (nachocove_crl));
            monitor.CrlGetRevoked ();
            var snTable = monitor.getRevoked ();
            Assert.AreEqual (2, snTable.Count);
            Assert.IsTrue (snTable.Contains ("9"));
            Assert.IsTrue (snTable.Contains ("8"));
        }

        [Test]
        public void CrlGetRevoked2 ()
        {
            string digicert_root_cert = "-----BEGIN CERTIFICATE-----\nMIIDrzCCApegAwIBAgIQCDvgVpBCRrGhdWrJWZHHSjANBgkqhkiG9w0BAQUFADBh\nMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3\nd3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBD\nQTAeFw0wNjExMTAwMDAwMDBaFw0zMTExMTAwMDAwMDBaMGExCzAJBgNVBAYTAlVT\nMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5j\nb20xIDAeBgNVBAMTF0RpZ2lDZXJ0IEdsb2JhbCBSb290IENBMIIBIjANBgkqhkiG\n9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4jvhEXLeqKTTo1eqUKKPC3eQyaKl7hLOllsB\nCSDMAZOnTjC3U/dDxGkAV53ijSLdhwZAAIEJzs4bg7/fzTtxRuLWZscFs3YnFo97\nnh6Vfe63SKMI2tavegw5BmV/Sl0fvBf4q77uKNd0f3p4mVmFaG5cIzJLv07A6Fpt\n43C/dxC//AH2hdmoRBBYMql1GNXRor5H4idq9Joz+EkIYIvUX7Q6hL+hqkpMfT7P\nT19sdl6gSzeRntwi5m3OFBqOasv+zbMUZBfHWymeMr/y7vrTC0LUq7dBMtoM1O/4\ngdW7jVg/tRvoSSiicNoxBN33shbyTApOB6jtSj1etX+jkMOvJwIDAQABo2MwYTAO\nBgNVHQ8BAf8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4EFgQUA95QNVbR\nTLtm8KPiGxvDl7I90VUwHwYDVR0jBBgwFoAUA95QNVbRTLtm8KPiGxvDl7I90VUw\nDQYJKoZIhvcNAQEFBQADggEBAMucN6pIExIK+t1EnE9SsPTfrgT1eXkIoyQY/Esr\nhMAtudXH/vTBH1jLuG2cenTnmCmrEbXjcKChzUyImZOMkXDiqw8cvpOp/2PV5Adg\n06O/nVsJ8dWO41P0jmP6P6fbtGbfYmbW0W5BjfIttep3Sp+dWOIrWcBAI+0tKIJF\nPnlUkiaY4IBIqDfv8NZ5YBberOgOzW6sRBc4L0na4UU+Krk2U886UAb3LujEV0ls\nYSEY1QSteDwsOoBrp+uvFRTp2InBuThs4pFsiv9kuXclVzDAGySj4dzp30d8tbQk\nCAUw7C29C79Fv1C5qfPrmAESrciIxpg0X40KPMbp1ZWVbd4=\n-----END CERTIFICATE-----";
            string digicert_d2_cert = "-----BEGIN CERTIFICATE-----\nMIIElDCCA3ygAwIBAgIQAf2j627KdciIQ4tyS8+8kTANBgkqhkiG9w0BAQsFADBh\nMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3\nd3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBD\nQTAeFw0xMzAzMDgxMjAwMDBaFw0yMzAzMDgxMjAwMDBaME0xCzAJBgNVBAYTAlVT\nMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxJzAlBgNVBAMTHkRpZ2lDZXJ0IFNIQTIg\nU2VjdXJlIFNlcnZlciBDQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEB\nANyuWJBNwcQwFZA1W248ghX1LFy949v/cUP6ZCWA1O4Yok3wZtAKc24RmDYXZK83\nnf36QYSvx6+M/hpzTc8zl5CilodTgyu5pnVILR1WN3vaMTIa16yrBvSqXUu3R0bd\nKpPDkC55gIDvEwRqFDu1m5K+wgdlTvza/P96rtxcflUxDOg5B6TXvi/TC2rSsd9f\n/ld0Uzs1gN2ujkSYs58O09rg1/RrKatEp0tYhG2SS4HD2nOLEpdIkARFdRrdNzGX\nkujNVA075ME/OV4uuPNcfhCOhkEAjUVmR7ChZc6gqikJTvOX6+guqw9ypzAO+sf0\n/RR3w6RbKFfCs/mC/bdFWJsCAwEAAaOCAVowggFWMBIGA1UdEwEB/wQIMAYBAf8C\nAQAwDgYDVR0PAQH/BAQDAgGGMDQGCCsGAQUFBwEBBCgwJjAkBggrBgEFBQcwAYYY\naHR0cDovL29jc3AuZGlnaWNlcnQuY29tMHsGA1UdHwR0MHIwN6A1oDOGMWh0dHA6\nLy9jcmwzLmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEdsb2JhbFJvb3RDQS5jcmwwN6A1\noDOGMWh0dHA6Ly9jcmw0LmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEdsb2JhbFJvb3RD\nQS5jcmwwPQYDVR0gBDYwNDAyBgRVHSAAMCowKAYIKwYBBQUHAgEWHGh0dHBzOi8v\nd3d3LmRpZ2ljZXJ0LmNvbS9DUFMwHQYDVR0OBBYEFA+AYRyCMWHVLyjnjUY4tCzh\nxtniMB8GA1UdIwQYMBaAFAPeUDVW0Uy7ZvCj4hsbw5eyPdFVMA0GCSqGSIb3DQEB\nCwUAA4IBAQAjPt9L0jFCpbZ+QlwaRMxp0Wi0XUvgBCFsS+JtzLHgl4+mUwnNqipl\n5TlPHoOlblyYoiQm5vuh7ZPHLgLGTUq/sELfeNqzqPlt/yGFUzZgTHbO7Djc1lGA\n8MXW5dRNJ2Srm8c+cftIl7gzbckTB+6WohsYFfZcTEDts8Ls/3HB40f/1LkAtDdC\n2iDJ6m6K7hQGrn2iWZiIqBtvLfTyyRRfJs8sjX7tN8Cp1Tm5gr8ZDOo0rwAhaPit\nc+LJMto4JQtV05od8GiG7S5BNO98pVAdvzr508EIDObtHopYJeS4d60tbvVS3bR0\nj6tJLp07kzQoH3jOlOrHvdPJbRzeXDLz\n-----END CERTIFICATE-----";
            string digicert_d2_crl3 = "-----BEGIN X509 CRL-----\nMIICDzCB+AIBATANBgkqhkiG9w0BAQUFADBhMQswCQYDVQQGEwJVUzEVMBMGA1UE\nChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3d3cuZGlnaWNlcnQuY29tMSAwHgYD\nVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBDQRcNMTUxMjE2MjEwMDAwWhcNMTYw\nMTA2MjEwMDAwWjAxMC8CEAEAAAAAAAAAAAAAAAAAAAEXDTA2MTExMDAwMDEwMFow\nDDAKBgNVHRUEAwoBAKAwMC4wHwYDVR0jBBgwFoAUA95QNVbRTLtm8KPiGxvDl7I9\n0VUwCwYDVR0UBAQCAgESMA0GCSqGSIb3DQEBBQUAA4IBAQCaZ5SlQ8VpOungKIl2\n/6SpGcjKBaQF0lSWcIjTHjPIfZ1N/Eec8qdGoAi6gURFe/9F5x2GWQReUJeFMSlL\nZiZQ6Xh0TzPo2iO9KAREPr4/bwJJnilgR8NXgnPXz4BWAEHZIzgHgRy+k8gVmNzv\nEAvs0mwuji50x/SZFmQdZbP77kg6YsuEF2pCW8CmrKoPwbbmg7M0AW4J5+PCiuo2\nXO+HArXLtjvUXCfmz9C1q84NryfEv2O0IfuVwUzJnDhxhM52DZICBjtnanylu5sU\nHv1zr++YW4RY6c4XVyUnJmyHIgigfu54H1hhsvQrbW1HK7mqhGarbivEMBQ2+J/w\n0EJI\n-----END X509 CRL-----";

            var cert = new X509Certificate2 (Encoding.ASCII.GetBytes (digicert_d2_cert));
            var cacert = new X509Certificate2 (Encoding.ASCII.GetBytes (digicert_root_cert));
            var signers = new X509Certificate2Collection ();
            signers.Add (cert);
            signers.Add (cacert);

            var urls = CrlMonitor.CDPUrls (cert);
            Assert.AreEqual (2, urls.Count);
            Assert.AreEqual (0, CrlMonitor.CDPUrls (cacert).Count);


            var monitor = new WrappedCrlMonitor (1, cert, urls, signers);
            Assert.IsTrue (monitor.ExtractCrl (digicert_d2_crl3));
            monitor.CrlGetRevoked ();

            var snTable = monitor.getRevoked ();
            Assert.AreEqual (1, snTable.Count);
            Assert.IsTrue (snTable.Contains ("1"));
        }
    }

    public class WrappedCrlMonitor : CrlMonitorItem
    {
        public WrappedCrlMonitor (int id, X509Certificate2 cert, List<string> urls, X509Certificate2Collection signerCerts) : base (id, cert, urls, signerCerts)
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

