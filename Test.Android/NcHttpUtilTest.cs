//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NUnit.Framework;
using Test.Common;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using NachoCore.Utils;
using System.Net;
using NachoPlatform;
using System.Net.Http;
using System.Threading;

namespace Test.iOS
{
    public class NcHttpUtilTest : NcTestBase
    {
        string digicert_sha2 = "-----BEGIN CERTIFICATE-----\nMIIElDCCA3ygAwIBAgIQAf2j627KdciIQ4tyS8+8kTANBgkqhkiG9w0BAQsFADBh\nMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3\nd3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBD\nQTAeFw0xMzAzMDgxMjAwMDBaFw0yMzAzMDgxMjAwMDBaME0xCzAJBgNVBAYTAlVT\nMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxJzAlBgNVBAMTHkRpZ2lDZXJ0IFNIQTIg\nU2VjdXJlIFNlcnZlciBDQTCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEB\nANyuWJBNwcQwFZA1W248ghX1LFy949v/cUP6ZCWA1O4Yok3wZtAKc24RmDYXZK83\nnf36QYSvx6+M/hpzTc8zl5CilodTgyu5pnVILR1WN3vaMTIa16yrBvSqXUu3R0bd\nKpPDkC55gIDvEwRqFDu1m5K+wgdlTvza/P96rtxcflUxDOg5B6TXvi/TC2rSsd9f\n/ld0Uzs1gN2ujkSYs58O09rg1/RrKatEp0tYhG2SS4HD2nOLEpdIkARFdRrdNzGX\nkujNVA075ME/OV4uuPNcfhCOhkEAjUVmR7ChZc6gqikJTvOX6+guqw9ypzAO+sf0\n/RR3w6RbKFfCs/mC/bdFWJsCAwEAAaOCAVowggFWMBIGA1UdEwEB/wQIMAYBAf8C\nAQAwDgYDVR0PAQH/BAQDAgGGMDQGCCsGAQUFBwEBBCgwJjAkBggrBgEFBQcwAYYY\naHR0cDovL29jc3AuZGlnaWNlcnQuY29tMHsGA1UdHwR0MHIwN6A1oDOGMWh0dHA6\nLy9jcmwzLmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEdsb2JhbFJvb3RDQS5jcmwwN6A1\noDOGMWh0dHA6Ly9jcmw0LmRpZ2ljZXJ0LmNvbS9EaWdpQ2VydEdsb2JhbFJvb3RD\nQS5jcmwwPQYDVR0gBDYwNDAyBgRVHSAAMCowKAYIKwYBBQUHAgEWHGh0dHBzOi8v\nd3d3LmRpZ2ljZXJ0LmNvbS9DUFMwHQYDVR0OBBYEFA+AYRyCMWHVLyjnjUY4tCzh\nxtniMB8GA1UdIwQYMBaAFAPeUDVW0Uy7ZvCj4hsbw5eyPdFVMA0GCSqGSIb3DQEB\nCwUAA4IBAQAjPt9L0jFCpbZ+QlwaRMxp0Wi0XUvgBCFsS+JtzLHgl4+mUwnNqipl\n5TlPHoOlblyYoiQm5vuh7ZPHLgLGTUq/sELfeNqzqPlt/yGFUzZgTHbO7Djc1lGA\n8MXW5dRNJ2Srm8c+cftIl7gzbckTB+6WohsYFfZcTEDts8Ls/3HB40f/1LkAtDdC\n2iDJ6m6K7hQGrn2iWZiIqBtvLfTyyRRfJs8sjX7tN8Cp1Tm5gr8ZDOo0rwAhaPit\nc+LJMto4JQtV05od8GiG7S5BNO98pVAdvzr508EIDObtHopYJeS4d60tbvVS3bR0\nj6tJLp07kzQoH3jOlOrHvdPJbRzeXDLz\n-----END CERTIFICATE-----";

        string digicert_global_root = "-----BEGIN CERTIFICATE-----\nMIIDrzCCApegAwIBAgIQCDvgVpBCRrGhdWrJWZHHSjANBgkqhkiG9w0BAQUFADBh\nMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMRkwFwYDVQQLExB3\nd3cuZGlnaWNlcnQuY29tMSAwHgYDVQQDExdEaWdpQ2VydCBHbG9iYWwgUm9vdCBD\nQTAeFw0wNjExMTAwMDAwMDBaFw0zMTExMTAwMDAwMDBaMGExCzAJBgNVBAYTAlVT\nMRUwEwYDVQQKEwxEaWdpQ2VydCBJbmMxGTAXBgNVBAsTEHd3dy5kaWdpY2VydC5j\nb20xIDAeBgNVBAMTF0RpZ2lDZXJ0IEdsb2JhbCBSb290IENBMIIBIjANBgkqhkiG\n9w0BAQEFAAOCAQ8AMIIBCgKCAQEA4jvhEXLeqKTTo1eqUKKPC3eQyaKl7hLOllsB\nCSDMAZOnTjC3U/dDxGkAV53ijSLdhwZAAIEJzs4bg7/fzTtxRuLWZscFs3YnFo97\nnh6Vfe63SKMI2tavegw5BmV/Sl0fvBf4q77uKNd0f3p4mVmFaG5cIzJLv07A6Fpt\n43C/dxC//AH2hdmoRBBYMql1GNXRor5H4idq9Joz+EkIYIvUX7Q6hL+hqkpMfT7P\nT19sdl6gSzeRntwi5m3OFBqOasv+zbMUZBfHWymeMr/y7vrTC0LUq7dBMtoM1O/4\ngdW7jVg/tRvoSSiicNoxBN33shbyTApOB6jtSj1etX+jkMOvJwIDAQABo2MwYTAO\nBgNVHQ8BAf8EBAMCAYYwDwYDVR0TAQH/BAUwAwEB/zAdBgNVHQ4EFgQUA95QNVbR\nTLtm8KPiGxvDl7I90VUwHwYDVR0jBBgwFoAUA95QNVbRTLtm8KPiGxvDl7I90VUw\nDQYJKoZIhvcNAQEFBQADggEBAMucN6pIExIK+t1EnE9SsPTfrgT1eXkIoyQY/Esr\nhMAtudXH/vTBH1jLuG2cenTnmCmrEbXjcKChzUyImZOMkXDiqw8cvpOp/2PV5Adg\n06O/nVsJ8dWO41P0jmP6P6fbtGbfYmbW0W5BjfIttep3Sp+dWOIrWcBAI+0tKIJF\nPnlUkiaY4IBIqDfv8NZ5YBberOgOzW6sRBc4L0na4UU+Krk2U886UAb3LujEV0ls\nYSEY1QSteDwsOoBrp+uvFRTp2InBuThs4pFsiv9kuXclVzDAGySj4dzp30d8tbQk\nCAUw7C29C79Fv1C5qfPrmAESrciIxpg0X40KPMbp1ZWVbd4=\n-----END CERTIFICATE-----";

        string officeburrito_com = "-----BEGIN CERTIFICATE-----\nMIIGFzCCBP+gAwIBAgIQCcCiO9Bk/u2hhI3yIbjEQzANBgkqhkiG9w0BAQsFADBN\nMQswCQYDVQQGEwJVUzEVMBMGA1UEChMMRGlnaUNlcnQgSW5jMScwJQYDVQQDEx5E\naWdpQ2VydCBTSEEyIFNlY3VyZSBTZXJ2ZXIgQ0EwHhcNMTUwOTE5MDAwMDAwWhcN\nMTcwMTE4MTIwMDAwWjBuMQswCQYDVQQGEwJVUzETMBEGA1UECBMKQ2FsaWZvcm5p\nYTEVMBMGA1UEBxMMU29sYW5hIEJlYWNoMRcwFQYDVQQKEw5OYWNobyBDb3ZlIElu\nYzEaMBgGA1UEAxMRb2ZmaWNlYnVycml0by5jb20wggEiMA0GCSqGSIb3DQEBAQUA\nA4IBDwAwggEKAoIBAQDQyzG4zSvm1zNqHvmNOEt68Q/wo1vn07BIYeEvX5f3GhvC\nFziaaZxER8nyC7OJM1DI07UYP8wgQTcFgLMIK2l3b3CXKmi9bsoQfspGzLFOA0C5\nM/EB6bqLM8sZ8/ApqAeHqPuFAhf480UNeP6eLTwcJfN8G3gcbmamykuxIp6H3BhQ\ncrUUJUYpIe2oRXkfvppqRXf7Jzc/bH4fkuWjJ5Fpr+9othxsS3UkA9Ac4l+5Fd66\nHCBvw/mAOx3B8spGPer+f+2TfcCRbaXlUIPjBQY+gTWbEbnCiK9VQvRqbTfbY6w/\nn5ocxncRgy675JhwNTt8Jx7X0rFURYqj8aouqPv1AgMBAAGjggLQMIICzDAfBgNV\nHSMEGDAWgBQPgGEcgjFh1S8o541GOLQs4cbZ4jAdBgNVHQ4EFgQU+fFn92Nvkk0a\nlhtFXlWl6x36fjEwggEcBgNVHREEggETMIIBD4IRb2ZmaWNlYnVycml0by5jb22C\nFGQxLm9mZmljZWJ1cnJpdG8uY29tghRkMi5vZmZpY2VidXJyaXRvLmNvbYIUZDMu\nb2ZmaWNlYnVycml0by5jb22CGW1haWwuZDEub2ZmaWNlYnVycml0by5jb22CGW1h\naWwuZDIub2ZmaWNlYnVycml0by5jb22CGW1haWwuZDMub2ZmaWNlYnVycml0by5j\nb22CIWF1dG9kaXNjb3Zlci5kMS5vZmZpY2VidXJyaXRvLmNvbYIhYXV0b2Rpc2Nv\ndmVyLmQyLm9mZmljZWJ1cnJpdG8uY29tgiFhdXRvZGlzY292ZXIuZDMub2ZmaWNl\nYnVycml0by5jb20wDgYDVR0PAQH/BAQDAgWgMB0GA1UdJQQWMBQGCCsGAQUFBwMB\nBggrBgEFBQcDAjBrBgNVHR8EZDBiMC+gLaArhilodHRwOi8vY3JsMy5kaWdpY2Vy\ndC5jb20vc3NjYS1zaGEyLWc0LmNybDAvoC2gK4YpaHR0cDovL2NybDQuZGlnaWNl\ncnQuY29tL3NzY2Etc2hhMi1nNC5jcmwwQgYDVR0gBDswOTA3BglghkgBhv1sAQEw\nKjAoBggrBgEFBQcCARYcaHR0cHM6Ly93d3cuZGlnaWNlcnQuY29tL0NQUzB8Bggr\nBgEFBQcBAQRwMG4wJAYIKwYBBQUHMAGGGGh0dHA6Ly9vY3NwLmRpZ2ljZXJ0LmNv\nbTBGBggrBgEFBQcwAoY6aHR0cDovL2NhY2VydHMuZGlnaWNlcnQuY29tL0RpZ2lD\nZXJ0U0hBMlNlY3VyZVNlcnZlckNBLmNydDAMBgNVHRMBAf8EAjAAMA0GCSqGSIb3\nDQEBCwUAA4IBAQC1GW7jq1+qLwdOpAXizpGK/SIC7DOYrbiNrF2Nx46h2cNw6H7p\neTDchlBE9g72GghqWdd1C4gpvJTdC92IKxxt8HXvF2URP4+h4M+SLj5ESYXp7kiZ\ngOWQgi8KmBKiwzUYubUcr0MFy1gZk90QJsFitzpgl3ZImdxstt+jic1hOyxFlOC4\n5zQpnrIsEA6XarilANkePh1BtbWeSjTGQMYTghv28lUARaPnhwP63gDozidn1Gly\nxKRJbRrbKAICNCQ9nvmPhY8TO7RRHOvo9FoLHRbSjCpHQX5Yf+3SLzrlYjxtJagt\n8glHm8b5f/9m2LVJMKQdMHAk2wa5bubWbb5Y\n-----END CERTIFICATE-----";

        X509Certificate2[] ChainCerts;
        X509Certificate2 Cert;

        public NcHttpUtilTest ()
        {
            ChainCerts = new X509Certificate2[] {
                new X509Certificate2 (Encoding.ASCII.GetBytes (digicert_sha2)),
                new X509Certificate2 (Encoding.ASCII.GetBytes (digicert_global_root)),
            };
            Cert = new X509Certificate2 (Encoding.ASCII.GetBytes (officeburrito_com));

            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
        }

        static SslPolicyErrors expectedError;

        X509Chain getChain ()
        {
            var chain = new X509Chain ();
            foreach (var cert in ChainCerts) {
                chain.ChainPolicy.ExtraStore.Add (cert);
            }
            return chain;
        }

        public static bool CertificateValidationCallback (Object sender,
                                                          X509Certificate certificate,
                                                          X509Chain chain,
                                                          SslPolicyErrors sslPolicyErrors)
        {
            Assert.AreEqual (expectedError, sslPolicyErrors);
            expectedError = SslPolicyErrors.None; // set to none after check.
            return true;
        }

        [Test]
        public void TestCertValidationWrongUrl ()
        {
            var url = new Uri ("http://www.google.com");
            var errors = SslPolicyErrors.None;
            expectedError = SslPolicyErrors.RemoteCertificateNameMismatch;
            NcHttpCertificateValidation.CertValidation (url, Cert, getChain(), errors);
        }

        //[Test]
        // this test was originally intended to catch a 'broken' chain, i.e. we add the top cert,
        // to the list, but not the intermediary cert. This results in a partial chain. It turns out, though,
        // that in iOS we are called with the full chain that IOS assembled for us, and in android we are called
        // with ONLY the certs sent by the server, which in most cases includes ONLY the server cert and the
        // intermediary cert(s), but NOT the top cert. This results in us ALWAYS getting a partial chain match,
        // so this test is no longer really useful.
        //
        // In general our cert validation isn't very useful, since cert validation is already done by the udnerlying
        // implementation (OkHttp and NSUrlSession respectively).
        public void TestCertValidationBadChain ()
        {
            var url = new Uri ("http://www.google.com");
            var errors = SslPolicyErrors.None;
            expectedError = SslPolicyErrors.RemoteCertificateChainErrors; // since we allow partial chains, this error will not be seen, making this test useless
            var badChain = new X509Chain ();
            badChain.ChainPolicy.ExtraStore.Add (new X509Certificate2 (Encoding.ASCII.GetBytes (digicert_global_root)));

            NcHttpCertificateValidation.CertValidation (url, Cert, badChain, errors);
        }

        [Test]
        public void TestCertValidationCorrectUrl ()
        {
            var url = new Uri ("http://mail.d1.officeburrito.com");
            var errors = SslPolicyErrors.None;
            expectedError = SslPolicyErrors.None;
            NcHttpCertificateValidation.CertValidation (url, Cert, getChain(), errors);

            url = new Uri ("http://d1.officeburrito.com");
            errors = SslPolicyErrors.None;
            expectedError = SslPolicyErrors.None;
            NcHttpCertificateValidation.CertValidation (url, Cert, getChain(), errors);
        }
    }

    /// <summary>
    /// Use these tests to test the underlying http transport implementation (for ios NSUrlSession, for android OkHttp)
    /// 
    /// NOTE: These tests need to be disabled by default, as they make requests to external servers, which is a no-no for 
    /// regular unit test runs.
    /// </summary>
    public class NcHttpCertValidationLiveTests : NcTestBase
    {
        int DefaultTimeoutSecs = 5;
        CancellationTokenSource Cts;
        SemaphoreSlim Sem;

        [SetUp]
        public void Setup ()
        {
            Cts = new CancellationTokenSource ();
            Sem = new SemaphoreSlim (0);
        }

        [TearDown]
        public void TearDown ()
        {
            Cts.Dispose ();
            Sem.Dispose ();
            Sem = null;
        }

        INcHttpClient HttpClient {
            get {
                return NcHttpClient.Instance;
            }
        }

        void TestInit ()
        {
            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
            CertificateValidationCallbackCalled = false;
            TestError = null;
        }

        [Test]
        [Ignore]
        public void TestValidUrl ()
        {
            TestInit ();
            var request = new NcHttpRequest (HttpMethod.Get, "https://mail.d2.officeburrito.com/");
            HttpClient.SendRequest (request, DefaultTimeoutSecs, DownloadSuccess, DownloadError, Cts.Token);
            Wait (1);
            Assert.IsTrue (null == TestError, TestError);
        }

        [Test]
        [Ignore]
        public void TestInValidUrl ()
        {
            TestInit ();
            var request = new NcHttpRequest (HttpMethod.Get, "https://pinger.officetaco.com/");
            HttpClient.SendRequest (request, DefaultTimeoutSecs, DownloadSuccess, DownloadError, Cts.Token);
            Wait (1);
            Assert.IsTrue (null != TestError, TestError);
        }

        private void Wait (int num_count)
        {
            for (int n = 0; n < num_count; n++) {
                Assert.NotNull (Sem);
                bool got = Sem.Wait (new TimeSpan (0, 0, DefaultTimeoutSecs + 1));
                Assert.True (got);
            }
        }

        string TestError = "Unknown Error";

        void DownloadSuccess (NcHttpResponse response, CancellationToken token)
        {
            try {
                if (null == response) {
                    TestError = "Response is null";
                    return;
                }

                if (HttpStatusCode.OK != response.StatusCode) {
                    TestError = "Status is not 200: " + response.StatusCode.ToString ();
                    return;
                }
                TestError = null;
            } finally {
                if (null != Sem) {
                    Sem.Release ();
                }
            }
        }

        void DownloadError (Exception ex, CancellationToken cToken)
        {
            TestError = "DownloadError called: " + ex.ToString ();
            if (null != Sem) {
                Sem.Release ();
            }
        }

        bool CertificateValidationCallbackCalled;
        public bool CertificateValidationCallback (Object sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            CertificateValidationCallbackCalled = true;
            return true;
        }
    }
}

