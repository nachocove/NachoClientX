//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Net.Http;
using NachoCore.Model;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Globalization;
using System.Collections;

namespace NachoCore.Utils
{
    public class NcHttpHeaders : HttpHeaders
    {
    }

    public class NcHttpRequest : IDisposable
    {
        public NcHttpHeaders Headers { get; protected set; }

        public long? ContentLength {
            get {
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Length", out values)) {
                    long len;
                    var lenStr = values.First ();
                    if (long.TryParse (lenStr, out len)) {
                        return len;
                    } else {
                        Log.Info (Log.LOG_HTTP, "Could not parse Content-Length header value {0}", lenStr);
                    }
                }
                return null;
            }
        }

        public string ContentType {
            get {
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Type", out values)) {
                    return values.First ();
                }
                return null;
            }
        }

        public Uri RequestUri { get; protected set; }

        public HttpMethod Method { get; protected set; }

        protected FileStream ContentStream { get; set; }

        protected bool DeleteStreamFile { get; set; }

        protected byte[] ContentData { get; set; }

        protected McCred _Cred;

        public McCred Cred { 
            get { return _Cred; }
            set {
                if (value != null) {
                    NcAssert.True (RequestUri.IsHttps ());
                }
                _Cred = value;
            }
        }

        public string guid { get; protected set; }

        public NcHttpRequest (HttpMethod method, Uri uri)
        {
            RequestUri = uri;
            Method = method;
            Headers = new NcHttpHeaders ();
            guid = Guid.NewGuid ().ToString ();
        }

        public NcHttpRequest (HttpMethod method, string url) : this (method, new Uri (url))
        {
        }

        public NcHttpRequest (HttpMethod method, string url, McCred cred) : this (method, new Uri (url), cred)
        {
        }

        public NcHttpRequest (HttpMethod method, Uri uri, McCred cred) : this (method, uri)
        {
            Cred = cred;
        }

        public object Content {
            get {
                if (ContentStream != null && ContentStream.Length > 0) {
                    return ContentStream;
                }
                if (ContentData != null && ContentData.Length > 0) {
                    return ContentData;
                }
                return null;
            }
        }

        public bool HasContent ()
        {
            return (ContentStream != null && ContentStream.Length > 0) || (ContentData != null && ContentData.Length > 0);
        }

        public void SetContent (FileStream stream, string contentType, bool deleteFile)
        {
            SetContent (stream, null, null, contentType, deleteFile);
        }

        public void SetContent (byte[] data, string contentType)
        {
            SetContent (null, data, null != data ? data.Length : 0, contentType, false);
        }

        protected void SetContent (FileStream stream, byte[] data, long? contentLength, string contentType, bool deleteFile)
        {
            ContentStream = stream;
            DeleteStreamFile = deleteFile;
            ContentData = data;
            if (!Headers.Contains ("Content-Type")) {
                Headers.Add ("Content-Type", contentType);
            }
            if (!Headers.Contains ("Content-Length")) {
                long? len = null;
                if (contentLength.HasValue) {
                    len = contentLength.Value;
                } else if (ContentData != null) {
                    len = ContentData.Length;
                } else if (ContentStream != null) {
                    Log.Info (Log.LOG_HTTP, "NcHttpRequest.SetContent({0}): set file {1}", guid, ContentStream.Name);
                    len = ContentStream.Length;
                }
                if (len.HasValue) {
                    Headers.Add ("Content-Length", len.Value.ToString ());
                }
            }
        }

        #region IDisposable implementation

        public void Dispose ()
        {
            if (null != ContentStream) {
                Log.Info (Log.LOG_HTTP, "NcHttpRequest.Dispose({0}): Disposing filestream with file {1}", guid, ContentStream.Name);
                try {
                    if (DeleteStreamFile) {
                        if (!File.Exists (ContentStream.Name)) {
                            Log.Error (Log.LOG_HTTP, "NcHttpRequest.Dispose({0}): Can not delete file since it no longer exists: {1}", guid, ContentStream.Name);
                        } else {
                            File.Delete (ContentStream.Name);
                        }
                    }
                } catch (Exception ex) {
                    Log.Error (Log.LOG_HTTP, "Could not delete stream file {0}: {1}", ContentStream.Name, ex);
                } finally {
                    ContentStream.Dispose ();
                }
            }
        }

        #endregion

        public byte[] GetContent ()
        {
            var content = Content;
            if (content == null) {
                return null;
            }

            if (content is FileStream) {
                var fs = content as FileStream;
                var buf = new byte[fs.Length];
                fs.Read (buf, 0, buf.Length);
                fs.Seek (0, SeekOrigin.Begin);
                return buf;
            }
            if (content is byte[]) {
                return content as byte[];
            }
            NcAssert.CaseError ("unknown type of content");
            return null;
        }
    }

    public class NcHttpResponse : IDisposable
    {
        public HttpMethod Method { get; protected set; }

        public NcHttpHeaders Headers { get; protected set; }

        public FileStream Content { get; protected set; }

        public string ContentType {
            get {
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Type", out values)) {
                    string value = values.First ();
                    MediaTypeHeaderValue cType;
                    if (MediaTypeHeaderValue.TryParse (value, out cType)) {
                        return cType.ToString ();
                    } else {
                        return null;
                    }
                }
                return null;
            }
            protected set {
                if (!string.IsNullOrEmpty (value)) {
                    MediaTypeHeaderValue cType;
                    if (MediaTypeHeaderValue.TryParse (value, out cType)) {
                        if (!Headers.Contains ("Content-Type")) {
                            Headers.Add ("Content-Type", value);
                        }
                    } else {
                        Log.Error (Log.LOG_HTTP, "Bad media-type value {0}", value);
                    }
                }
            }
        }

        public HttpStatusCode StatusCode { get; protected set; }

        public long ContentLength {
            get {
                // need to check the headers first, so that unit tests can set bogus values.
                IEnumerable<string> values;
                if (Headers.TryGetValues ("Content-Length", out values)) {
                    long len;
                    var lenStr = values.First ();
                    if (long.TryParse (lenStr, out len)) {
                        return len;
                    } else {
                        Log.Info (Log.LOG_HTTP, "Could not parse Content-Length header value {0}", lenStr);
                    }
                }
                if (null != Content) {
                    return Content.Length;
                }
                return -1;
            }
        }

        public bool HasBody {
            get {
                if (Method.Method == "HEAD") {
                    return false;
                }
                if ((StatusCode < HttpStatusCode.Continue || StatusCode >= HttpStatusCode.OK) &&
                    StatusCode != HttpStatusCode.NoContent &&
                    StatusCode != HttpStatusCode.NotModified) {
                    return true;
                }

                IEnumerable<string> values;
                bool chunked = false;
                if (Headers.TryGetValues ("Transfer-Encoding", out values)) {
                    chunked = values.First ().ToLowerInvariant () == "chunked";
                }
                if (ContentLength != -1 || chunked) {
                    return true;
                }
                return false;
            }
        }

        public NcHttpResponse (HttpMethod method, HttpStatusCode status, byte[] data, string contentType, NcHttpHeaders headers = null)
        {
            initMe (method, status, StreamFromByte (data), contentType, headers);
        }


        public NcHttpResponse (HttpMethod method, HttpStatusCode status, FileStream stream, string contentType, NcHttpHeaders headers = null)
        {
            initMe (method, status, stream, contentType, headers);
        }

        public NcHttpResponse (string method, HttpStatusCode status, FileStream stream, string contentType, NcHttpHeaders headers = null)
        {
            initMe (method, status, stream, contentType, headers);
        }

        string tempFileName { get; set; }

        public NcHttpResponse (string method, HttpStatusCode status, byte[] data, string contentType, NcHttpHeaders headers = null)
        {
            initMe (method, status, StreamFromByte (data), contentType, headers);
        }

        void initMe (string method, HttpStatusCode status, FileStream stream, string contentType, NcHttpHeaders headers = null)
        {
            initMe (new HttpMethod (method), status, stream, contentType, headers);
        }

        void initMe (HttpMethod method, HttpStatusCode status, FileStream stream, string contentType, NcHttpHeaders headers = null)
        {
            Method = method;
            Headers = headers ?? new NcHttpHeaders ();
            StatusCode = status;
            Content = stream;
            ContentType = contentType;
        }

        FileStream StreamFromByte (byte[] data)
        {
            tempFileName = Path.GetTempFileName ();
            Log.Info (Log.LOG_HTTP, "Created tempfile {0}", tempFileName);
            using (var fs = new FileStream (tempFileName, FileMode.OpenOrCreate, FileAccess.Write)) {
                fs.Write (data, 0, data.Length);
            }
            return new FileStream (tempFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public void Dispose ()
        {
            if (!string.IsNullOrEmpty (tempFileName)) {
                File.Delete (tempFileName);
                Log.Info (Log.LOG_HTTP, "Deleted tempfile {0}", tempFileName);
            }
        }

        public NcHttpResponse (HttpMethod method, HttpStatusCode status)
        {
            StatusCode = status;
            Headers = new NcHttpHeaders ();
        }

        public byte[] GetContent ()
        {
            var content = Content;
            if (content == null) {
                return null;
            }

            if (content is FileStream) {
                var fs = content as FileStream;
                var buf = new byte[fs.Length];
                fs.Read (buf, 0, buf.Length);
                fs.Seek (0, SeekOrigin.Begin);
                return buf;
            }
            NcAssert.CaseError ("unknown type of content");
            return null;
        }
    }

    public static class NcHttpCertificateValidation
    {
        static string SubjectAltNameOid = "2.5.29.17";

        static readonly Regex cnRegex = new Regex (@"CN\s*=\s*([^,]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public static bool CertValidation (Uri Url, X509Certificate2 cert, X509Chain chain, SslPolicyErrors errors)
        {
            if (errors != SslPolicyErrors.None) {
                goto sslErrorVerify;
            }

            chain.ChainPolicy.RevocationFlag = X509RevocationFlag.EntireChain;
            chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan (0, 1, 0);
            chain.ChainPolicy.VerificationFlags = X509VerificationFlags.AllowUnknownCertificateAuthority;

            try {
                if (!chain.Build (cert)) {
                    errors = SslPolicyErrors.RemoteCertificateChainErrors;
                    goto sslErrorVerify;
                }

                // see 'Remarks' section here https://msdn.microsoft.com/en-us/library/System.Security.Cryptography.X509Certificates.X509Chain(v=vs.110).aspx
                foreach (var status in chain.ChainStatus) {
                    switch (status.Status) {
                    case X509ChainStatusFlags.UntrustedRoot: // the chain validated up to a root, but we don't have it in our trust store.
                    case X509ChainStatusFlags.PartialChain:  // the chain validated up to a non-root cert
                    case X509ChainStatusFlags.NoError:       // the chain validated
                        break;

                    default:
                        Log.Info (Log.LOG_HTTP, "Cert not validated: {0} (subject={1} issuer={2})", status.StatusInformation, cert.Subject, cert.Issuer);
                        errors = SslPolicyErrors.RemoteCertificateChainErrors;
                        goto sslErrorVerify;
                    }
                }
            } catch (System.Security.Cryptography.CryptographicException) {
                // As best we can tell, a XAMMIT (spurious).
                errors = SslPolicyErrors.RemoteCertificateChainErrors;
                goto sslErrorVerify;
            }

            var subject = cert.Subject;
            var subjectCn = cnRegex.Match (subject).Groups [1].Value;

            if (String.IsNullOrWhiteSpace (subjectCn) || !MatchHostnameToPattern (Url.Host, subjectCn)) {
                bool found = false;
                foreach (var ext in cert.Extensions) {
                    if (ext.Oid.Value == SubjectAltNameOid) {
                        // TODO Quite the hack. Need to figure out how to get the raw data rather than string splitting
                        foreach (var line in ext.Format (true).Split (new [] {'\n'})) {
                            var parts = line.Split (new []{ '=' });
                            if (parts [0] == "DNS Name") {
                                if (MatchHostnameToPattern (Url.Host, parts [1])) {
                                    found = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (found)
                        break;
                }
                if (!found) {
                    errors = SslPolicyErrors.RemoteCertificateNameMismatch;
                    goto sslErrorVerify;
                }
            }

            sslErrorVerify:
            return ServicePointManager.ServerCertificateValidationCallback (new HttpWebRequest (Url), cert, chain, errors);
        }

        static bool MatchHostnameToPattern (string hostname, string pattern)
        {
            // check if this is a pattern
            int index = pattern.IndexOf ('*');
            if (index == -1) {
                // not a pattern, do a direct case-insensitive comparison
                return (String.Compare (hostname, pattern, true, CultureInfo.InvariantCulture) == 0);
            }

            // check pattern validity
            // A "*" wildcard character MAY be used as the left-most name component in the certificate.

            // unless this is the last char (valid)
            if (index != pattern.Length - 1) {
                // then the next char must be a dot .'.
                if (pattern [index + 1] != '.') {
                    return false;
                }
            }

            // only one (A) wildcard is supported
            int i2 = pattern.IndexOf ('*', index + 1);
            if (i2 != -1)
                return false;

            // match the end of the pattern
            string end = pattern.Substring (index + 1);
            int length = hostname.Length - end.Length;
            // no point to check a pattern that is longer than the hostname
            if (length <= 0)
                return false;

            if (String.Compare (hostname, length, end, 0, end.Length, true, CultureInfo.InvariantCulture) != 0) {
                return false;
            }

            // special case, we start with the wildcard
            if (index == 0) {
                // ensure we hostname non-matched part (start) doesn't contain a dot
                int i3 = hostname.IndexOf ('.');
                return ((i3 == -1) || (i3 >= (hostname.Length - end.Length)));
            }

            // match the start of the pattern
            string start = pattern.Substring (0, index);
            return (String.Compare (hostname, 0, start, 0, start.Length, true, CultureInfo.InvariantCulture) == 0);
        }

        /// <summary>
        /// Verifies client ciphers and is only available in Mono and Xamarin products.
        /// </summary>
        /// <returns><c>true</c>, if client ciphers was verifyed, <c>false</c> otherwise.</returns>
        /// <param name="uri"></param>
        /// <param name="protocol"></param>
        /// <param name="cipherSuite"></param>
        public static bool verifyClientCiphers (Uri uri, string protocol, string cipherSuite)
        {
            var callback = ServicePointManager.ClientCipherSuitesCallback;
            if (callback == null)
                return true;

            var SSLProto = protocol.StartsWith ("SSL", StringComparison.InvariantCulture) ? SecurityProtocolType.Ssl3 : SecurityProtocolType.Tls;
            var acceptedCiphers = callback (SSLProto, new[] { cipherSuite });

            return acceptedCiphers.Contains (cipherSuite);
        }
    }
}
