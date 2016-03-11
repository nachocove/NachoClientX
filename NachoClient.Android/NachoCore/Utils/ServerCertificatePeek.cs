// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.Utils
{
    public delegate bool ServerCertificateValidator (IHttpWebRequest sender,
        X509Certificate2 certificate,
        X509Chain chain,
        bool result // the validation result of the default validation routine
    );

    /// <summary>
    /// A server identity is a pair of (hostname, port). Each unique server identity can
    /// have its own server validation policy.
    /// </summary>
    public class ServerIdentity
    {
        static int NextId = 1;

        public int Id { get; protected set; }

        public string Host { set; get; }

        public int Port { set; get; }

        public ServerIdentity ()
        {
            Id = NextId++;
        }

        public ServerIdentity (Uri uri)
        {
            Host = uri.Host;
            Port = uri.Port;
        }
    }

    /// <summary>
    /// Helper class for comparing ServerIdentity
    /// </summary>
    public class ServerIdentityComparer : IEqualityComparer<ServerIdentity>
    {

        public bool Equals (ServerIdentity a, ServerIdentity b)
        {
            return ((a.Host == b.Host) && (a.Port == b.Port));
        }

        public int GetHashCode (ServerIdentity a)
        {
            return a.Id;
        }
    }

    /// <summary>
    /// A validation policy for a server identity. One can install a pinned cert and his own
    /// validation routine. If no routine is given
    /// </summary>
    public class ServerValidationPolicy
    {
        public X509Certificate2 PinnedCert { set; get; }

        public X509Certificate2Collection PinnedSigningCerts { set; get; }

        public ServerCertificateValidator Validator { set; get; }
    }

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

        private ConcurrentDictionary<ServerIdentity, ServerValidationPolicy> Policies;

        public static ServerCertificatePeek Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new ServerCertificatePeek ();
                            instance.Cache = new ConcurrentDictionary<string, X509Certificate2> ();
                            var serverComparer = new ServerIdentityComparer ();
                            instance.Policies = new ConcurrentDictionary<ServerIdentity, ServerValidationPolicy> (serverComparer);
                            ServicePointManager.ServerCertificateValidationCallback = CertificateValidationCallback;
                        }
                    }
                }
                return instance;
            }
        }

        public static void Initialize ()
        {
            ServicePointManager.ServerCertificateValidationCallback = ServerCertificatePeek.CertificateValidationCallback;
        }

        // Note: public only for test code!
        public static bool CertificateValidationCallback (Object sender,
                                                          X509Certificate certificate,
                                                          X509Chain chain,
                                                          SslPolicyErrors sslPolicyErrors)
        {
            var maybeRequest = sender as HttpWebRequest;
            if (null == maybeRequest) {
                return SslPolicyErrors.None == sslPolicyErrors;
            }
            IHttpWebRequest request = new MockableHttpWebRequest (maybeRequest);
            X509Certificate2 certificate2 = null;
            if (null != certificate) {
                certificate2 = new X509Certificate2 (certificate);
            }
            if (null != Instance.ValidationEvent) {
                Instance.ValidationEvent (request, certificate2, chain, sslPolicyErrors, EventArgs.Empty);
            }

            // Look up a validation policy
            ServerIdentity ident = new ServerIdentity (request.RequestUri);
            ServerValidationPolicy policy;
            if (Instance.Policies.TryGetValue (ident, out policy)) {
                bool hasPinning = (null != policy.PinnedCert);
                if (hasPinning) {
                    if (!chain.ChainPolicy.ExtraStore.Contains (policy.PinnedCert)) {
                        chain.ChainPolicy.ExtraStore.Add (policy.PinnedCert);
                    }
                    foreach (var cert in policy.PinnedSigningCerts) {
                        if (!chain.ChainPolicy.ExtraStore.Contains (cert)) {
                            chain.ChainPolicy.ExtraStore.Add (cert);
                        }                            
                    }

                    CrlMonitor.Instance.Register (chain.ChainPolicy.ExtraStore);

                    // Pinned cert - Remove all self-signed certs in ExtraStore and inject the pinned cert
                    var selfSignedCerts = new X509Certificate2Collection ();
                    foreach (var cert in chain.ChainPolicy.ExtraStore) {
                        if (CompareCerts(cert, policy.PinnedCert) == false && cert.Issuer == cert.Subject) {
                            selfSignedCerts.Add (cert);
                        }
                    }
                    chain.ChainPolicy.ExtraStore.RemoveRange (selfSignedCerts);
                }

                // Remove all revoked certs
                var revokedCerts = new X509Certificate2Collection ();
                foreach (var cert in chain.ChainPolicy.ExtraStore) {
                    if (CrlMonitor.Instance.IsRevoked (cert)) {
                        revokedCerts.Add (cert);
                    }
                }
                chain.ChainPolicy.ExtraStore.RemoveRange (revokedCerts);
                bool ok;
                if (null == certificate2) {
                    ok = false;
                } else if (CrlMonitor.Instance.IsRevoked (certificate2)) {
                    ok = false;
                } else {
                    ok = chain.Build (certificate2);
                }
                if (ok && hasPinning) {
                    // We use our own cert for pinning so there should be at most one status of untrusted cert
                    if ((1 < chain.ChainStatus.Length) ||
                        (X509ChainStatusFlags.UntrustedRoot != chain.ChainStatus [0].Status)) {
                        ok = false;
                    }
                    if (!ok) {
                        // We change the result. Log the reason
                        foreach (var status in chain.ChainStatus) {
                            Log.Warn (Log.LOG_HTTP, "Cert chain status: {0}", status.Status);
                        }
                    }
                }
                if (null != policy.Validator) {
                    // Custom validation
                    ok = policy.Validator (request, certificate2, chain, ok);
                }

                if (!ok) {
                    Log.Warn (Log.LOG_HTTP, "Cert validation failure (uri={0})", request.RequestUri.AbsoluteUri);
                    return false;
                } else if (null != policy.Validator) {
                    // If there is a custom validator and it says yes, overwrite all previous validation results.
                    sslPolicyErrors = SslPolicyErrors.None;
                }
            }

            if (SslPolicyErrors.None == sslPolicyErrors) {
                var host = request.Address.Host;
                Instance.Cache.AddOrUpdate (host, certificate2, (k, v) => certificate2);
                return true;
            } else {
                Log.Info (Log.LOG_HTTP, "Certificate validation failed with: {0}", sslPolicyErrors);
            }
            return false;
        }

        static bool CompareCerts (X509Certificate2 cert1, X509Certificate2 cert2)
        {
            return cert1.SubjectName.Name == cert2.SubjectName.Name &&
            cert1.SerialNumber == cert2.SerialNumber &&
            cert1.IssuerName.Name == cert2.IssuerName.Name;
        }

        public static void TestOnlyFlushCache ()
        {
            instance.Cache = new ConcurrentDictionary<string, X509Certificate2> ();
        }

        public void AddPolicy (ServerIdentity server, ServerValidationPolicy policy)
        {
            Policies.AddOrUpdate (server, policy, (k, v) => policy);
        }

        public ServerValidationPolicy DeletePolicy (ServerIdentity server)
        {
            ServerValidationPolicy policy;
            if (!Policies.TryRemove (server, out policy)) {
                return null;
            }
            return policy;
        }
    }
}

