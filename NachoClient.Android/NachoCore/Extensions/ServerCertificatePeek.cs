// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.Utils
{
    public delegate void ServerCertificateEventHandler (HttpWebRequest sender,
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

        public static ServerCertificatePeek Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new ServerCertificatePeek ();
                            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
                        }
                    }
                }
                return instance;
            }
        }

        private static bool CertificateValidationCallback (Object sender,
                                                           X509Certificate certificate,
                                                           X509Chain chain,
                                                           SslPolicyErrors sslPolicyErrors)
        {
            if (null != Instance.ValidationEvent) {
                // NOTE: the cast to HttpWebRequest is evil. We could use reflection.
                Instance.ValidationEvent ((HttpWebRequest)sender, 
                    new X509Certificate2 (certificate), 
                    chain, sslPolicyErrors, EventArgs.Empty);
            }
            return SslPolicyErrors.None == sslPolicyErrors;
        }
    }
}

