// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Linq;
using NachoCore.Model;

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

        public class ServerCertificateError
        {
            public X509Chain Chain { get; protected set; }
            public X509Certificate2 Cert { get; protected set; }
            public SslPolicyErrors SslPolicyError { get; protected set; }

            public ServerCertificateError (X509Chain chain, X509Certificate2 cert, SslPolicyErrors sslPolicyError)
            {
                Chain = chain;
                Cert = cert;
                SslPolicyError = sslPolicyError;
            }
        }

        public ConcurrentDictionary<string, ServerCertificateError> FailedCertificates;

        public static ServerCertificatePeek Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new ServerCertificatePeek ();
                            instance.Cache = new ConcurrentDictionary<string, X509Certificate2> ();
                            instance.FailedCertificates = new ConcurrentDictionary<string, ServerCertificateError> ();
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
            if (null != maybeRequest) {
                return HttpWebRequestCertificateValidationCallback (maybeRequest, certificate, chain, sslPolicyErrors);
            }

            string hostname = sender as string;
            if (null != hostname) {
                return StringCertificateValidationCallback (hostname, certificate, chain, sslPolicyErrors);
            }

            return SslPolicyErrors.None == sslPolicyErrors;
        }

        static bool StringCertificateValidationCallback (string hostname,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            if (SslPolicyErrors.None == sslPolicyErrors) {
                return true;
            }
            var cert2 = new X509Certificate2 (certificate);
            var ok = chain.Build (cert2);
            if (!ok) {
                Instance.FailedCertificates [hostname] = new ServerCertificateError (chain, cert2, sslPolicyErrors);
            }
            return ok;
        }

        static bool HttpWebRequestCertificateValidationCallback (HttpWebRequest sender,
            X509Certificate certificate,
            X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            IHttpWebRequest request = new MockableHttpWebRequest (sender);
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
                    // Extract all CRL DP in intermediary certs
                    foreach (var cert in chain.ChainPolicy.ExtraStore) {
                        var crlUrls = CertificateHelper.CrlDistributionPoint (cert);
                        CrlMonitor.Register (crlUrls);
                    }
                    // Pinned cert - Remove all self-signed certs in ExtraStore and inject the pinned cert
                    var selfSignedCerts = new X509Certificate2Collection ();
                    foreach (var cert in chain.ChainPolicy.ExtraStore) {
                        if (cert.Issuer == cert.Subject) {
                            selfSignedCerts.Add (cert);
                        }
                    }
                    chain.ChainPolicy.ExtraStore.RemoveRange (selfSignedCerts);
                    chain.ChainPolicy.ExtraStore.Add (policy.PinnedCert);
                }

                // Remove all revoked certs
                var revokedCerts = new X509Certificate2Collection ();
                foreach (var cert in chain.ChainPolicy.ExtraStore) {
                    if (CrlMonitor.IsRevoked (cert.SerialNumber)) {
                        revokedCerts.Add (cert);
                    }
                }
                chain.ChainPolicy.ExtraStore.RemoveRange (revokedCerts);
                bool ok;
                if (null == certificate2) {
                    ok = false;
                } else if (CrlMonitor.IsRevoked (certificate2.SerialNumber)) {
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
                if (chain.ChainElements.Count == 0 && null != certificate2) {
                    chain.Build (certificate2);
                }
                Instance.FailedCertificates [request.Address.Host] = new ServerCertificateError (chain, certificate2, sslPolicyErrors);
            }
            return false;
        }

        public static void LogCertificateChainErrors (ServerCertificateError failedInfo, string tag)
        {
            List<string> errors = new List<string> ();
            if (null != failedInfo.Chain.ChainElements) {
                foreach (var certEl in failedInfo.Chain.ChainElements) {
                    errors.Add (string.Format ("Certificate(status={0}):\n{1}", string.Join (",", certEl.ChainElementStatus.Select (x => x.StatusInformation).ToList ()), certEl.Certificate));
                }
            }
            Log.Info (Log.LOG_HTTP, "{0} sslPolicyErrors={1}, chain-errors: {2}\n{3}", tag, failedInfo.SslPolicyError, string.Join (",", failedInfo.Chain.ChainStatus.Select (x => x.StatusInformation)), string.Join ("\n", errors));
        }

        public string GetServerErrors (int accountId)
        {
            string serverErrors = "";
            foreach (var server in McServer.QueryByAccountId<McServer> (accountId)) {
                ServerCertificateError failedInfo;
                if (FailedCertificates.TryRemove (server.Host, out failedInfo)) {
                    serverErrors += string.Format ("{0}: {1}", server.Host, failedInfo.SslPolicyError);
                }
            }
            return serverErrors;
        }

        public static Dictionary<string, ServerCertificatePeek.ServerCertificateError> ServerErrors (int accountId)
        {
            var serverErrors = new Dictionary<string, ServerCertificatePeek.ServerCertificateError> ();
            foreach (var server in McServer.QueryByAccountId<McServer> (accountId)) {
                ServerCertificatePeek.ServerCertificateError failedInfo;
                if (ServerCertificatePeek.Instance.FailedCertificates.TryRemove (server.Host, out failedInfo)) {
                    serverErrors [server.Host] = failedInfo;
                }
            }
            return serverErrors;
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

