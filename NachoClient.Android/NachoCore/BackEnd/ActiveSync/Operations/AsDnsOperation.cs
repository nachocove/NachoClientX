// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Sockets;
using System.Threading;
using DnDns.Query;
using DnDns.Enums;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
    public class AsDnsOperation : IAsOperation
    {
        public TimeSpan Timeout { set; get; }
        public Type DnsQueryRequestType { set; get; }

        private bool wasKilledByTimer;
        private bool wasCancelled;
        private IAsDnsOperationOwner m_owner;
        private NcTimer TimeoutTimer;
        private IDnsQueryRequest Request;
        private object LockObj;

        public AsDnsOperation(IAsDnsOperationOwner owner) {
            LockObj = new object ();
            Timeout = TimeSpan.Zero;
            DnsQueryRequestType = typeof(MockableDnsQueryRequest);
            m_owner = owner;
        }

        public async void Execute (NcStateMachine sm) {
            Request = (IDnsQueryRequest)Activator.CreateInstance (DnsQueryRequestType);
            TimeoutTimer = new NcTimer (TimerCallback, null, Convert.ToInt32 (Timeout.TotalSeconds),
                System.Threading.Timeout.Infinite);
            try {
                var Response = await Request.ResolveAsync (m_owner.DnsHost (this),
                                   m_owner.DnsType (this),
                                   m_owner.DnsClass (this), ProtocolType.Udp);
                CleanupTimeoutTimer();
                if (! wasCancelled) {
                    var Event = m_owner.ProcessResponse (this, Response);
                    sm.PostEvent (Event);
                }
            } catch (Exception ex) {
                if (ex is ObjectDisposedException || ex is SocketException) {
                    if (wasKilledByTimer ||
                        (ex is SocketException && !wasCancelled)) {
                        sm.PostEvent ((uint)SmEvt.E.TempFail, "DNSOPTEMP0");
                    }
                } else {
                    throw;
                }
            }
        }

        public void Cancel ()
        {
            lock (LockObj) {
                wasCancelled = true;
                CleanupTimeoutTimer ();
                Close ();
            }
        }

        private void Close ()
        {
            if (null != Request && null != Request.UdpClient) {
                Request.UdpClient.Close ();
            }
        }

        private void CleanupTimeoutTimer ()
        {
            if (null != TimeoutTimer) {
                TimeoutTimer.Dispose ();
                TimeoutTimer = null;
            }
        }

        private void TimerCallback (object State)
        {
            wasKilledByTimer = true;
            CleanupTimeoutTimer ();
            Close ();
        }
    }
}

