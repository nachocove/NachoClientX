//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Runtime.InteropServices;
using DnDns.Query;
using DnDns.Enums;
using NachoCore.Utils;

namespace NachoPlatform
{
    public class PlatformDns : IPlatformDns
    {
        // The res_query() error code for TRY_AGAIN.  This is the only error code
        // that we care about.
        private const int TRY_AGAIN_ERROR = 2;

        // res_query() stores its error code in h_errno, not errno.  The usual technique
        // for getting the error code, SetLastError=true, won't work.  So we have created
        // a wrapper function around res_query that gets the error code from h_errno and
        // returns it through a ref parameter.
        private static object resQueryLock = new object ();

        [DllImport("__Internal")]
        private static extern int nacho_res_query (
            string host, int queryClass, int queryType, byte[] answer, int anslen, ref int errorCode);

        public DnsQueryResponse ResQuery (IDnsLockObject op, string host, NsClass dnsClass, NsType dnsType)
        {
            // See https://www.dns-oarc.net/oarc/services/replysizetest - 4k is enough to hold the response.
            byte[] answer = new byte[4096];

            // Since the res_query call will be retried only if the error code is TRY_AGAIN,
            // set the maximum number of retries to a rediculously high number.  It is
            // unlikely that this limit will ever be reached.
            int retries = 200;
            while (0 < --retries) {

                // res_query() stores its error code in a global variable, h_errno.  Not a
                // thread-specific variable like errno, but a truly global variable.  To
                // prevent one call from overwriting the error code of another before it is
                // read, use a lock to prevent simultaneous calls to res_query.  (This won't
                // prevent some other thread that we don't control from calling res_query()
                // or some other function that uses h_errno, but there is no feasible way to
                // protect against that.)
                int errorCode = 0;
                int rc;
                lock (resQueryLock) {
                    rc = nacho_res_query (host, (int)dnsClass, (int)dnsType, answer, answer.Length, ref errorCode);
                }

                lock (op.lockObject) {
                    if (op.complete || (0 > rc && TRY_AGAIN_ERROR != errorCode)) {
                        // The operation timed out or was canceled, or res_query() failed.
                        return null;
                    }
                    if (0 <= rc) {
                        // res_query() succeeded.
                        try {
                            DnsQueryResponse response = new DnsQueryResponse ();
                            response.ParseResponse (answer, rc);
                            return response;
                        } catch (Exception e) {
                            Log.Error (Log.LOG_DNS, "DNS response parsing failed with an exception, likely because the response is malformed: {0}", e.ToString ());
                            return null;
                        }
                    }
                }
            }
            // Too many retries. Give up.
            return null;
        }
    }
}

