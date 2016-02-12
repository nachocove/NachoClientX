//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore;
using NachoCore.Utils;
using NachoCore.IMAP;
using System.Collections.Generic;
using System.Linq;
using NachoPlatform;

namespace NachoCore.IMAP
{
    public class ImapSideChannels
    {
        public object SideChannelLockObj = new object ();
        public int MaxConcurrentExtraRequests = 4;
        static List<ImapSideProtoControl> SideChannels = new List<ImapSideProtoControl> ();
        ImapProtoControl ImapOwner;

        public ImapSideChannels (ImapProtoControl imapOwner)
        {
            ImapOwner = imapOwner;
        }

        public int SideChannelCount {
            get {
                return SideChannels.Count;
            }
        }

        public void SetAndRun (ImapProtoControl imapProtoControl, ImapCommand cmd)
        {
            lock (SideChannelLockObj) {
                var sideChannel = SideChannels.SingleOrDefault (x => x.IsIdle);
                if (null == sideChannel) {
                    sideChannel = new ImapSideProtoControl (imapProtoControl);
                    SideChannels.Add (sideChannel);
                }
                sideChannel.SetCmd (cmd);
                sideChannel.Start ();
            }
        }

        public bool CanStartAnother ()
        {
            return (NcCommStatus.CommQualityEnum.OK == NcCommStatus.Instance.Quality (ImapOwner.Server.Id) &&
                NetStatusSpeedEnum.CellSlow_2 != NcCommStatus.Instance.Speed &&
                SideChannelCount < MaxConcurrentExtraRequests);
        }

        public bool CanHandle (PickActionEnum action)
        {
            switch (action) {
            case PickActionEnum.Fetch:
            case PickActionEnum.QOop:
            case PickActionEnum.HotQOp:
                return true;

            default:
                return false;
            }
        }

        public void StopAll ()
        {
            lock (SideChannelLockObj) {
                foreach (var side in SideChannels) {
                    side.Stop ();
                }
            }
        }
    }

    public class ImapSideProtoControl : NcProtoControl
    {
        ImapProtoControl ImapOwner;

        public enum Lst : uint
        {
            CmdW = (St.Last + 1),
        }

        public class ImapSideEvt : SmEvt
        {
            new public enum E : uint
            {
                Park = (PcEvt.E.Last + 1),
                AuthFail,
                Last = AuthFail,
            };
        }

        public bool IsIdle {
            get {
                return (Sm.State == (uint)St.Stop || Sm.State == (uint)St.Start);
            }
        }

        public ImapSideProtoControl (ImapProtoControl imapOwner) : base (imapOwner.Owner, imapOwner.AccountId)
        {
            ImapOwner = imapOwner;

            Sm = new NcStateMachine ("IMAPPC:SIDE", new ImapStateMachineContext ()) { 
                Name = string.Format ("IMAPPC:SIDE({0})", AccountId),
                LocalEventType = typeof(ImapSideEvt),
                TransTable = new[] {
                    new Node {
                        State = (uint)St.Start,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapSideEvt.E.AuthFail,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoRun, State = (uint)Lst.CmdW },
                            new Trans { Event = (uint)ImapSideEvt.E.Park, Act = DoCancel, State = (uint)St.Stop },
                        },
                    },
                    new Node {
                        State = (uint)Lst.CmdW,
                        Invalid = new [] {
                            (uint)SmEvt.E.Launch,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoDone, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoDone, State = (uint)St.Stop },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoDone, State = (uint)St.Stop },
                            new Trans { Event = (uint)ImapSideEvt.E.AuthFail, Act = DoOwnerAuthFail, State = (uint)St.Stop },
                            new Trans { Event = (uint)ImapSideEvt.E.Park, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },
                    new Node {
                        State = (uint)St.Stop,
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)SmEvt.E.TempFail,
                            (uint)ImapSideEvt.E.AuthFail,
                        },
                        Drop = new [] {
                            (uint)ImapSideEvt.E.Park,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoRun, State = (uint)Lst.CmdW },
                        },
                    },
                }
            };
            Sm.Validate ();
        }

        public ImapCommand Cmd { get; protected set; }

        public void SetCmd (ImapCommand cmd)
        {
            CancelCmd ();
            Cmd = cmd;
        }

        void CancelCmd ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
                Cmd = null;
            }
        }

        void ExecuteCmd ()
        {
            Cmd.Execute (Sm);
        }

        void DoRun ()
        {
            NcAssert.NotNull (Cmd);
            Cmd.Execute (Sm);
        }

        protected override bool Execute ()
        {
            if (!base.Execute ()) {
                return false;
            }
            if (null == Cmd) {
                return false;
            }
            NcTask.Run (() => Sm.PostEvent ((uint)SmEvt.E.Launch, "IMAPPCSIDEEXE"), "ImapSideExecute");
            return true;
        }

        protected override void ForceStop ()
        {
            Cts.Cancel ();
            Sm.PostEvent ((uint)PcEvt.E.Park, "PCFORCESTOP");
        }


        void DoDone ()
        {
            // Send the PendQHot so that the ProtoControl SM looks to see if there is another hot op
            // to run in parallel.
            if (!Cts.IsCancellationRequested) {
                ImapOwner.Sm.PostEvent ((uint)PcEvt.E.PendQHot, "IMAPPCSIDEDONE");
            }
        }

        void DoOwnerAuthFail ()
        {
            if (!Cts.IsCancellationRequested) {
                ImapOwner.Sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPPCSIDEAUTHFAIL");
            }
        }

        void DoCancel ()
        {
            CancelCmd ();
        }
    }
}

