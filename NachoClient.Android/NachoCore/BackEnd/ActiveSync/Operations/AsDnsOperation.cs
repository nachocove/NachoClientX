// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Net.Sockets;
using System.Threading;
using DnDns.Query;
using DnDns.Enums;
using NachoCore.Utils;
using NachoCore.Model;

namespace NachoCore.ActiveSync
{
    public class AsDnsOperation : IAsOperation
    {
        private const string KDefaultTimeoutSeconds = "10";

        public TimeSpan Timeout { set; get; }

        public Type DnsQueryRequestType { set; get; }

        private bool wasKilledByTimer;
        private bool wasCancelled;
        private IAsDnsOperationOwner m_owner;
        private NcTimer TimeoutTimer;
        private IDnsQueryRequest Request;
        private object LockObj;

        public AsDnsOperation (IAsDnsOperationOwner owner)
        {
            LockObj = new object ();
            var timeoutSeconds = McMutables.GetOrCreate ("DNSOP", "TimeoutSeconds", KDefaultTimeoutSeconds);
            Timeout = new TimeSpan (0, 0, timeoutSeconds.ToInt ());
            DnsQueryRequestType = typeof(MockableDnsQueryRequest);
            m_owner = owner;
        }

        public async void Execute (NcStateMachine sm)
        {
            DnsQueryResponse response;
            Request = (IDnsQueryRequest)Activator.CreateInstance (DnsQueryRequestType);
            TimeoutTimer = new NcTimer (TimerCallback, null, Convert.ToInt32 (Timeout.TotalSeconds),
                System.Threading.Timeout.Infinite);
            try {
                try {
                    response = await Request.ResolveAsync (m_owner.DnsHost (this),
                        m_owner.DnsType (this),
                        m_owner.DnsClass (this), ProtocolType.Udp).ConfigureAwait (false);
                } catch (AggregateException aex) {
                    Log.Error(Log.LOG_HTTP, "Received AggregateException in await ... ResolveAsync");
                    throw aex.InnerException;
                }
                CleanupTimeoutTimer ();
                if (!wasCancelled) {
                    var Event = m_owner.ProcessResponse (this, response);
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

