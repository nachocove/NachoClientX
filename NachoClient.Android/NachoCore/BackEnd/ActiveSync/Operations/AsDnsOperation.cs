// # Copyright (C) 2013, 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using DnDns.Enums;
using DnDns.Query;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsDnsOperation : IAsOperation
    {
        private IAsDnsOperationOwner owner;
        private TimeSpan timeout;
        private NcStateMachine stateMachine;

        private object lockObject = new object ();
        private bool complete = false;
        private NcTimer timer;

        // Allow the unit tests to mock up the call to res_query().
        public delegate DnsQueryResponse CallResQueryDelegate (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType);

        static public CallResQueryDelegate CallResQuery = ResQuery;

        private const int kDefaultTimeoutSeconds = 10;

        // The res_query() error code for TRY_AGAIN.  This is the only error code
        // that we care about.
        private const int TRY_AGAIN_ERROR = 2;

        public AsDnsOperation (IAsDnsOperationOwner owner)
        {
            this.owner = owner;
            timeout = new TimeSpan (0, 0, McMutables.GetOrCreateInt (
                McAccount.GetDeviceAccount ().Id, "DNSOP", "TimeoutSeconds", kDefaultTimeoutSeconds));
        }

        public AsDnsOperation (IAsDnsOperationOwner owner, TimeSpan timeout)
        {
            this.owner = owner;
            this.timeout = timeout;
        }

        public void Execute (NcStateMachine stateMachine)
        {
            this.stateMachine = stateMachine;

            // Start the timer that will prevent the operation from running too long.
            timer = new NcTimer ("AsDnsOperation", TimerCallback, null,
                (int)timeout.TotalMilliseconds, Timeout.Infinite);

            // Start the task on another thread. DoExecuteWithRetries takes care of
            // reporting its results, so there is no need to wait for the task to
            // complete or to check its value.
            Task.Run ((Action)DoExecuteWithRetries);
        }

        public void Cancel ()
        {
            lock (lockObject) {
                if (!complete) {
                    owner.CancelCleanup (this);
                    StopTimer ();
                }
                complete = true;
            }
        }

        // TODO The declaration for nacho_res_query needs to be moved to platform-specific code.
#if __IOS__
        // res_query() stores its error code in h_errno, not errno.  The usual technique
        // for getting the error code, SetLastError=true, won't work.  So we have created
        // a wrapper function around res_query that gets the error code from h_errno and
        // returns it through a ref parameter.
        private static object resQueryLock = new object ();
        [DllImport("__Internal")]
        private static extern int nacho_res_query (
            string host, int queryClass, int queryType, byte[] answer, int anslen, ref int errorCode);

        private static DnsQueryResponse ResQuery (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType)
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

#elif __ANDROID__
        // We'd prefer to move the low-level DNS query capability to IPlatform.
        private static DnsQueryResponse ResQuery (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType)
        {
            var DnsQuery = new DnsQueryRequest ();
            DnsQueryResponse response = DnsQuery.Resolve (host, dnsType, dnsClass, System.Net.Sockets.ProtocolType.Udp);
            if (null == response || response.Answers.Length == 0) {
                return null;
            }
            return response;
        }
#else
#error
#endif
        private void DoExecuteWithRetries ()
        {
            DnsQueryResponse response = CallResQuery (this, owner.DnsHost (this), owner.DnsClass (this), owner.DnsType (this));

            lock (lockObject) {
                if (complete) {
                    // Operation was canceled or timed out.
                    return;
                }
                complete = true;
                StopTimer ();
                if (null != response) {
                    stateMachine.PostEvent (owner.ProcessResponse (this, response));
                } else {
                    stateMachine.PostEvent ((uint)SmEvt.E.HardFail, "DNSHARDFAIL");
                }
            }
        }

        private void TimerCallback (object state)
        {
            lock (lockObject) {
                if (!complete) {
                    stateMachine.PostEvent ((uint)SmEvt.E.TempFail, "DNSOPTO");
                }
                complete = true;
            }
        }

        private void StopTimer ()
        {
            if (null != timer) {
                timer.Dispose ();
                timer = null;
            }
        }
    }
}

