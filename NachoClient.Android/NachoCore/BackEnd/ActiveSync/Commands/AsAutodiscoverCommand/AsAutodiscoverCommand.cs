// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

/* MUST-READs (Besides the ActiveSync specs):
 * http://msdn.microsoft.com/en-us/library/exchange/hh352638(v=exchg.140).aspx
 * http://msdn.microsoft.com/en-us/library/ee332364(EXCHG.140).aspx
 * http://support.microsoft.com/?kbid=940881
 *
 * How this works:
 * AsAutodiscoverCommand (aka autod) is used by the protocol controller just like any other command.
 * Autod has to run quickly for UX, and there are potentially a TON of network accesses involved. Therefore:
 * Autod has a serial-ish top level state machine that can be comprehended by humans.
 * Autod maintains a pool of robots that run in parallel, doing the network accesses for each "step" of the process. 
 * The robots either post results to the autod state machine, or they store their results - 
 *   depending on the state of the autod machine. If the results are stored, then the autod machine retrieves them
 *   when top level state machine is ready to consume them.
 * The robots are the owners of the DNS/HTTP operations - not the top level state machine - 
 *   this is atypical for subclasses of AsCommand.
 * 
 * TODO: Right now once a robot succeeds, all other robots are stopped. The resulting server config is tested.
 * If the test passes, then good. If not, we declare auto-d failed, and ask the user for the server config.
 * We can in the future push that test into the robot so that if more than one path yields server config, then
 * the first working path can be selected.
 */
namespace NachoCore.ActiveSync
{
    /* The only reason we implement & proxy IAsDataSource is so that we can source
     * candidate values for Server to AsHttpOperation when testing them.
     */
    public partial class AsAutodiscoverCommand : AsCommand, IBEContext
    {
        public enum Lst : uint
        {
            RobotW = (St.Last + 1),
            AskW,
            CredW1,
            CredW2,
            SrvConfW,
            TestW,
        };
        // Event codes shared between TL and Robot SMs.
        public class SharedEvt : AsProtoControl.AsEvt
        {
            new public enum E : uint
            {
                // UI response on server cert.
                SrvCertY = (AsProtoControl.AsEvt.E.Last + 1),
                // UI response on server cert.
                SrvCertN,
                // Protocol indicates that search must be restarted from step-1 (Robot or Top-Level).
                ReStart,
                Last = ReStart,
            };
        }

        public class TlEvt : SharedEvt
        {
            new public enum E : uint
            {
                // UI has updated the credentials for this account (Top-Level only).
                CredSet = (SharedEvt.E.Last + 1),
                // UI has updated the server information for this account (Top-Level only).
                ServerSet,
                // Robot says UI has to ask user if server cert is okay (Top-Level only).
                ServerCertAsk,
                // Pool of to-dos (asking about certs, waiting for robots, etc) is empty.
                Empty,
                Cancel,
            };
        };

        public const string RequestSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/requestschema/2006";
        public const string ResponseSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006";
        private List<StepRobot> Robots;
        private Queue<StepRobot> AskingRobotQ;
        private Queue<StepRobot> SuccessfulRobotQ;
        private AsOptionsCommand OptCmd;
        private ConcurrentBag<object> DisposedJunk;
        private string Domain;
        private string BaseDomain;
        private McServer ServerCandidate;
        public uint ReDirsLeft;

        public NcStateMachine Sm { get; set; }
        // CALLABLE BY THE OWNER.
        public AsAutodiscoverCommand (IBEContext dataSource) : base ("Autodiscover", 
                                                                     RequestSchema,
                                                                     dataSource)
        {
            ReDirsLeft = 10;
            DisposedJunk = new ConcurrentBag<object> ();
            Sm = new NcStateMachine ("AUTOD") {
                LocalEventType = typeof(TlEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    // Robots ARE NOT running in this state.
                    new Node {State = (uint)St.Start,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY,
                            (uint)TlEvt.E.CredSet
                        },
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail, 
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync, (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, (uint)TlEvt.E.Empty,
                        },
                        On = new[] {
                            // Start robots and wait.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // App just set the server so, go test it (skip robots).
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestW },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Robots ARE running in this state.
                    new Node {State = (uint)Lst.RobotW,
                        Invalid = new [] {(uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY
                        },
                        On = new[] {
                            // Start robots and wait.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Stop robots, test, and wait for test results.
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoTestFromRobot, State = (uint)Lst.TestW },
                            // Remove robot, post "Empty" if none left running.
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoReapRobot, State = (uint)Lst.RobotW },
                            // Stop robots and ask for creds, then wait.
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredW1
                            },
                            // Stop robots and start over.
                            new Trans { Event = (uint)SharedEvt.E.ReStart, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Stop and re-start robots, then wait.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Stop robots, test, and wait for test results.
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestW
                            },
                            // Keeping robots running, ask app if server cert is okay.
                            new Trans {
                                Event = (uint)TlEvt.E.ServerCertAsk,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.AskW
                            },
                            // The last robot failed. Ask app for server conf.
                            new Trans {
                                Event = (uint)TlEvt.E.Empty,
                                Act = DoUiGetServer,
                                State = (uint)Lst.SrvConfW
                            },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    new Node {State = (uint)Lst.AskW,
                        Invalid = new [] {(uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync, (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                        },
                        On = new[] {
                            // Start robots and wait.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // A robot succeeded. Queue it for after we're answered.
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoQueueSuccess, State = (uint)Lst.AskW },
                            // A robot failed. Discard it.
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoReapRobot, State = (uint)Lst.AskW },
                            // App said "yes". Post "Empty" if no queued asks.
                            new Trans {
                                Event = (uint)SharedEvt.E.SrvCertY,
                                Act = DoSxSrvCertY,
                                State = (uint)Lst.AskW
                            },
                            // App said "no". Post "Empty" if no queued asks.
                            new Trans {
                                Event = (uint)SharedEvt.E.SrvCertN,
                                Act = DoSxSrvCertN,
                                State = (uint)Lst.AskW
                            },
                            // Stop and re-start robots, then wait.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Stop robots, test, and wait for test results.
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestW },
                            // We're already asking, so queue this ask.
                            new Trans {
                                Event = (uint)TlEvt.E.ServerCertAsk,
                                Act = DoEnqAsk,
                                State = (uint)Lst.AskW
                            },
                            // No more asks queued, apply 1st queued success if any, and go back to waiting on robots.
                            new Trans { Event = (uint)TlEvt.E.Empty, Act = DoNop, State = (uint)Lst.RobotW },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },

                        }
                    },

                    new Node {State = (uint)Lst.TestW,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {(uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, (uint)TlEvt.E.Empty
                        },
                        On = new[] {
                            // Test the new server config.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoTest, State = (uint)Lst.TestW },
                            // It worked! We're done.
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = DoAcceptServerConf,
                                State = (uint)St.Stop
                            },
                            // It failed. Try again (FIXME - do we need this? TempFail now handled by AsHttpOp).
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoTest, State = (uint)Lst.TestW },
                            // It failed. Ask app for server config again.
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoUiGetServer,
                                State = (uint)Lst.SrvConfW
                            },
                            // We got told to re-do auto-d. But that won't work!
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                Act = DoUiGetServer,
                                State = (uint)Lst.SrvConfW
                            },
                            // We got told to re-prov. Should not happen.
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                Act = DoUiGetServer,
                                State = (uint)Lst.SrvConfW
                            },
                            // We got told to re-sync. Should not happen.
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                Act = DoUiGetServer,
                                State = (uint)Lst.SrvConfW
                            },
                            // Ask for creds again before re-test.
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredW2
                            },
                            // Re-try test because app set creds.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoTest, State = (uint)Lst.TestW },
                            // Re-try test because app set server config.
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestW
                            },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Waiting for new creds before server config set or robot success.
                    new Node {State = (uint)Lst.CredW1,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {(uint)SmEvt.E.TempFail, (uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync, (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, (uint)TlEvt.E.Empty
                        },
                        On = new[] {
                            // Ask app for creds.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiGetCred, State = (uint)Lst.CredW1 },
                            // Got creds, re-start all robots and try again.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Got new server value. run-with it and test it.
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestW },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Waiting for new creds during server config testing.
                    new Node {State = (uint)Lst.CredW2,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {(uint)SmEvt.E.TempFail, (uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync, (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, (uint)TlEvt.E.Empty
                        },
                        On = new [] {
                            // Ask app for creds.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiGetCred, State = (uint)Lst.CredW1 },
                            // Got creds, re-test & wait.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoTest, State = (uint)Lst.TestW },
                            // Got new server value. run-with it and test it.
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestW },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Asked the app for a server config, waiting now...
                    new Node {State = (uint)Lst.SrvConfW, 
                        Drop = new [] { (uint)TlEvt.E.CredSet, (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] { (uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync, (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, (uint)TlEvt.E.Empty
                        },
                        On = new[] {
                            // Ask again and wait.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiGetServer, State = (uint)Lst.SrvConfW },
                            // Got server config, now test & wait.
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, State = (uint)Lst.TestW },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },
                }
            };
            Sm.Validate ();
        }

        public override void Execute (NcStateMachine ownerSm)
        {
            OwnerSm = ownerSm;
            Sm.Name = OwnerSm.Name + ":AUTOD";
            Domain = DomainFromEmailAddr (BEContext.Account.EmailAddr);
            BaseDomain = NachoPlatform.RegDom.Instance.RegDomFromFqdn (Domain);
            if (null == BEContext.Server || true == BEContext.Server.UsedBefore ||
                string.Empty == BEContext.Server.Host) {
                Sm.Start ();
            } else {
                ServerCandidate = BEContext.Server;
                Sm.Start ((uint)Lst.TestW);
            }
        }
        // UTILITY METHODS.
        private void AddAndStartRobot (StepRobot.Steps step, string domain)
        {
            var robot = new StepRobot (this, step, BEContext.Account.EmailAddr, domain);
            Robots.Add (robot);
            robot.Execute ();
        }

        private void KillAllRobots ()
        {
            if (null != Robots) {
                foreach (var robot in Robots) {
                    robot.Cancel ();
                    DisposedJunk.Add (robot);
                }
                Robots = null;
            }
        }

        public override void Cancel ()
        {
            var cancelEvent = Event.Create ((uint)TlEvt.E.Cancel, "AUTODCANCEL");
            cancelEvent.DropIfStopped = true;
            Sm.PostEvent (cancelEvent);
        }

        private static string DomainFromEmailAddr (string EmailAddr)
        {
            return EmailAddr.Split ('@').Last ();
        }

        private StepRobot EnqAskAndGimmieRobot ()
        {
            StepRobot robot = (StepRobot)Sm.Arg;
            AskingRobotQ.Enqueue (robot);
            return robot;
        }

        private void SxServerCertX (uint eventCode, string mnemonic)
        {
            var robot = AskingRobotQ.Dequeue ();
            if (0 == AskingRobotQ.Count) {
                Sm.PostEvent ((uint)TlEvt.E.Empty, "AUTODECQ");
            } else {
                OwnerSm.PostEvent (Event.Create ((uint)AsProtoControl.CtlEvt.E.GetCertOk, "AUTODCERTASK1", robot.ServerCertificate));
            }
            robot.StepSm.PostEvent (eventCode, mnemonic);
        }
        // IMPLEMENTATION OF TOP-LEVEL STATE MACHINE.
        protected override void DoUiGetCred ()
        {
            DoCancel ();
            base.DoUiGetCred ();
        }

        private void DoCancel ()
        {
            KillAllRobots ();
            AskingRobotQ = null;
            SuccessfulRobotQ = null;
            if (null != OptCmd) {
                OptCmd.Cancel ();
                DisposedJunk.Add (OptCmd);
                OptCmd = null;
            }
        }

        private void DoStepsPll ()
        {
            // If we have a queued success, just try testing that 1st!
            if (null != SuccessfulRobotQ && 0 != SuccessfulRobotQ.Count) {
                var winner = SuccessfulRobotQ.Dequeue ();
                Sm.PostEvent ((uint)SmEvt.E.Success, "AUTODSREQ", winner, null);
                return;
            }
            DoCancel ();
            Robots = new List<StepRobot> ();
            AskingRobotQ = new Queue<StepRobot> ();
            SuccessfulRobotQ = new Queue<StepRobot> ();
            AddAndStartRobot (StepRobot.Steps.S1, Domain);
            AddAndStartRobot (StepRobot.Steps.S2, Domain);
            AddAndStartRobot (StepRobot.Steps.S3, Domain);
            AddAndStartRobot (StepRobot.Steps.S4, Domain);
            if (BaseDomain != Domain) {
                AddAndStartRobot (StepRobot.Steps.S1, BaseDomain);
                AddAndStartRobot (StepRobot.Steps.S2, BaseDomain);
                AddAndStartRobot (StepRobot.Steps.S3, BaseDomain);
                AddAndStartRobot (StepRobot.Steps.S4, BaseDomain);
            }
        }

        private void DoReapRobot ()
        {
            StepRobot robot = (StepRobot)Sm.Arg;
            // Robot can't be on either ask or success queue, or it would not be reporting failure.
            Robots.Remove (robot);
            if (0 == Robots.Count) {
                // This can never happen in AskW - there is still the asking robot in the list.
                Sm.PostEvent ((uint)TlEvt.E.Empty, "AUTODDRR");
            }
        }

        private void DoQueueSuccess ()
        {
            // We really should preserve the mnemonic here.
            StepRobot robot = (StepRobot)Sm.Arg;
            SuccessfulRobotQ.Enqueue (robot);
        }

        private void DoEnqAsk ()
        {
            EnqAskAndGimmieRobot ();
        }

        private void DoSxSrvCertY ()
        {
            SxServerCertX ((uint)SharedEvt.E.SrvCertY, "AUTODS1CY");
        }

        private void DoSxSrvCertN ()
        {
            SxServerCertX ((uint)SharedEvt.E.SrvCertN, "AUTODS1CN");
        }

        private void DoTest ()
        {
            DoCancel ();
            OptCmd = new AsOptionsCommand (this);
            OptCmd.Execute (Sm);
        }

        private void DoTestFromUi ()
        {
            ServerCandidate = BEContext.Server;
            DoTest ();
        }

        private void DoTestFromRobot ()
        {
            var robot = (StepRobot)Sm.Arg;
            ServerCandidate = McServer.Create (robot.SrServerUri);
            // Must shut down any remaining robots so they don't post events to TL SM.
            KillAllRobots ();
            // Must clear event Q for TL SM of anything a robot may have posted (threads).
            Sm.ClearEventQueue ();
            DoTest ();
        }

        private void DoUiGetServer ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.CtlEvt.E.GetServConf, "AUTODDUGS");
        }

        private void DoUiServerCertAsk ()
        {
            var robot = EnqAskAndGimmieRobot ();
            OwnerSm.PostEvent (Event.Create ((uint)AsProtoControl.CtlEvt.E.GetCertOk, "AUTODCERTASK", robot.ServerCertificate));
        }

        private void DoAcceptServerConf ()
        {
            // Save validated server config in DB.
            var serverRecord = BEContext.Server;
            serverRecord.CopyFrom (ServerCandidate);
            serverRecord.UsedBefore = true;
            serverRecord.Update ();
            // Signal that we are done and that we have a server config.
            // Success is the only way we finish - either by UI setting or autodiscovery.
            OwnerSm.PostEvent ((uint)SmEvt.E.Success, "AUTODDASC");
        }
        // IAsDataSource proxying.
        public IProtoControlOwner Owner {
            get { return BEContext.Owner; }
            set { BEContext.Owner = value; }
        }

        public AsProtoControl ProtoControl {
            get { return BEContext.ProtoControl; }
            set { BEContext.ProtoControl = value; }
        }

        public McProtocolState ProtocolState {
            get { return BEContext.ProtocolState; }
            set { BEContext.ProtocolState = value; }
        }

        public McServer Server {
            get { return ServerCandidate; }
            set { throw new Exception ("Illegal set of Server by AsOptionsCommand."); }
        }

        public McAccount Account {
            get { return BEContext.Account; }
        }

        public McCred Cred {
            get { return BEContext.Cred; }
        }
    }
}
