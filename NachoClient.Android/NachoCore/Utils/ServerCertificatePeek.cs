// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.Utils
{
    public delegate void ServerCertificateEventHandler (IHttpWebRequest sender,
                                                       X509Certificate2 certificate,
                                                       X509Chain chain,
                                                       SslPolicyErrors sslPolicyErrors, 
                                                       EventArgs e);
    // Allow multiple modules to register for Validation of the server cert.
    // Right now, this is just to let the modules view the cert - they can't deny validation.
    public sealed class ServerCertificatePeek
    {
        private static volatile ServerCertificatePeek instance;
        private static object syncRoot = new Object ();

        private ServerCertificatePeek ()
        {
        }

        public event ServerCertificateEventHandler ValidationEvent;
        public ConcurrentDictionary<string, X509Certificate2> Cache;

        public static ServerCertificatePeek Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new ServerCertificatePeek ();
                            instance.Cache = new ConcurrentDictionary<string, X509Certificate2> ();
                            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
                        }
                    }
                }
                return instance;
            }
        }

        // Note: public only for test code!
        public static bool CertificateValidationCallback (Object sender,
                                                           X509Certificate certificate,
                                                           X509Chain chain,
                                                           SslPolicyErrors sslPolicyErrors)
        {
            IHttpWebRequest request = new MockableHttpWebRequest ((HttpWebRequest)sender);
            X509Certificate2 certificate2 = new X509Certificate2 (certificate);
            if (null != Instance.ValidationEvent) {
                Instance.ValidationEvent (request, certificate2, chain, sslPolicyErrors, EventArgs.Empty);
            }
            if (SslPolicyErrors.None == sslPolicyErrors) {
                var host = request.Address.Host;
                Instance.Cache.AddOrUpdate (host, certificate2, (k, v) => certificate2);
                return true;
            }
            return false;
        }
    }
}

