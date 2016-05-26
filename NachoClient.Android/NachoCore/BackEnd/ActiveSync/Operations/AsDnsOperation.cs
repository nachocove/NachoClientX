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
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public class AsDnsOperation : IAsOperation, IDnsLockObject
    {
        private IAsDnsOperationOwner owner;
        private TimeSpan timeout;
        private NcStateMachine stateMachine;

        private object _lockObject = new object ();
        public object lockObject { 
            get {
                return _lockObject;
            }
        }

        private bool _complete = false;
        public bool complete {
            get {
                return _complete;
            }
            protected set {
                _complete = value;
            }
        }

        private NcTimer timer;

        // Allow the unit tests to mock up the call to res_query().
        public delegate DnsQueryResponse CallResQueryDelegate (AsDnsOperation op, string host, NsClass dnsClass, NsType dnsType);

        static public CallResQueryDelegate CallResQuery = ResQuery;

        private const int kDefaultTimeoutSeconds = 10;

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
            NcTask.Run (DoExecuteWithRetries, "DnsDoExecuteWithRetries");
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

        private static DnsQueryResponse ResQuery (IDnsLockObject op, string host, NsClass dnsClass, NsType dnsType)
        {
            var platformDns = new PlatformDns ();
            return platformDns.ResQuery (op, host, dnsClass, dnsType);
        }

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

