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
        private bool wasKilledByTimer;
        private IAsDnsOperationOwner m_owner;
        public TimeSpan Timeout;
        private Timer Timer;

        public AsDnsOperation(IAsDnsOperationOwner owner) {
            Timeout = TimeSpan.Zero;
            m_owner = owner;
        }

        private DnsQueryRequest Request;

        public async void Execute (StateMachine sm) {
            Request = new DnsQueryRequest ();
            Timer = new Timer (TimerCallback, null, Convert.ToInt32 (Timeout.TotalSeconds),
                               System.Threading.Timeout.Infinite);
            try {
                var Response = await Request.ResolveAsync(m_owner.DnsHost (this),
                                                          m_owner.DnsType (this),
                                                          m_owner.DnsClass (this), ProtocolType.Udp);
                Timer.Dispose();
                Timer = null;
                var Event = m_owner.ProcessResponse(this, Response);
                sm.PostEvent(Event);
            } catch (ObjectDisposedException) {
                if (wasKilledByTimer) {
                    sm.PostEvent ((uint)SmEvt.E.TempFail);
                } else {
                    // Do nothing - this is a cancellation.
                    Timer.Dispose ();
                    Timer = null;
                }
            } catch (SocketException) {
                if (! wasKilledByTimer) {
                    Timer.Dispose ();
                    Timer = null;
                }
                sm.PostEvent ((uint)SmEvt.E.TempFail);
            }
        }

        public void Cancel () {
            if (null != Request && null != Request.UdpClient) {
                Request.UdpClient.Close ();
            }
        }

        public void TimerCallback (object State) {
            wasKilledByTimer = true;
            Cancel ();
            Timer = null;
        }
    }
}

