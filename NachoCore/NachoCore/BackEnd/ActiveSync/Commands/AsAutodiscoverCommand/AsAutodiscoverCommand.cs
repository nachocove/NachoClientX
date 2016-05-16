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
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

/* MUST-READs (Besides the ActiveSync specs):
 * http://msdn.microsoft.com/en-us/library/hh352638(v=exchg.140).aspx
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
            TestW1,
            TestW2,
            Peek404,
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
                // Pool of to-dos is empty, but default server can be tested.
                TestDefaultServer,
                Cancel,
            };
        };

        public const string RequestSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/requestschema/2006";
        public const string ResponseSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006";
        public const int TestTimeoutSecs = 30;
        private List<StepRobot> Robots;
        private object RobotsLockObj = new object ();
        private Queue<StepRobot> AskingRobotQ;
        private Queue<StepRobot> SuccessfulRobotQ;
        private ConcurrentQueue<Event> RobotEventsQ;
        private AsCommand TestCmd;
        private ConcurrentBag<object> DisposedJunk;
        private string Domain;
        private string BaseDomain;
        private McServer ServerCandidate;
        private bool AutoDSucceeded;
        private volatile bool SubdomainComplete;
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
                        Invalid = new [] {
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.TempFail, 
                            (uint)SmEvt.E.HardFail, 
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, 
                            (uint)AsProtoControl.AsEvt.E.ReProv, 
                            (uint)AsProtoControl.AsEvt.E.ReSync, 
                            (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, 
                            (uint)TlEvt.E.Empty, 
                            (uint)TlEvt.E.TestDefaultServer,
                        },
                        On = new[] {
                            // Start robots and wait.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // App just set the server so, go test it (skip robots).
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, ActSetsState = true },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Robots ARE running in this state.
                    new Node {State = (uint)Lst.RobotW,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, 
                            (uint)AsProtoControl.AsEvt.E.ReProv, 
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.SrvCertN, 
                            (uint)SharedEvt.E.SrvCertY,
                        },
                        On = new[] {
                            // Start robots and wait.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Stop robots, test, and wait for test results.
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoTestFromRobot, ActSetsState = true },
                            // Remove robot, post "Empty" if none left running.
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoReapRobot, State = (uint)Lst.RobotW },
                            // Stop robots and ask for creds, then wait.
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredW1
                            },
                            // Stop robots and start over.
                            new Trans {
                                Event = (uint)SharedEvt.E.ReStart,
                                Act = DoStepsPllRestart,
                                State = (uint)Lst.RobotW
                            },
                            // Stop and re-start robots, then wait.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Stop robots, test, and wait for test results.
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                ActSetsState = true
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
                            // The last robot failed, but test against Google before giving up.
                            new Trans {
                                Event = (uint)TlEvt.E.TestDefaultServer,
                                Act = DoTestDefaultServer,
                                ActSetsState = true
                            },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    new Node {State = (uint)Lst.AskW,
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, 
                            (uint)AsProtoControl.AsEvt.E.ReProv,
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.TestDefaultServer,
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
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, ActSetsState = true },
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

                    new Node {State = (uint)Lst.TestW1,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk,
                            (uint)TlEvt.E.Empty,
                            (uint)TlEvt.E.TestDefaultServer,
                        },
                        On = new[] {
                            // Test the new server config.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoTest, ActSetsState = true },
                            // It worked! We're done.
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = DoAcceptServerConf,
                                State = (uint)St.Stop
                            },
                            // It failed. Ask app for server config again.
                            new Trans {
                                Event = (uint)SmEvt.E.TempFail,
                                Act = DoUiGetServerTempFail,
                                State = (uint)Lst.SrvConfW
                            },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoPeek404,
                                State = (uint)Lst.Peek404
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
                                Act = DoUiGetCredOrGetServer,
                                ActSetsState = true
                            },
                            // Re-try test because app set creds.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoTest, ActSetsState = true },
                            // Re-try test because app set server config.
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                ActSetsState = true
                            },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    new Node {State = (uint)Lst.TestW2,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, 
                            (uint)TlEvt.E.Empty,
                            (uint)TlEvt.E.TestDefaultServer,
                        },
                        On = new[] {
                            // Test the new server config.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoTest, ActSetsState = true },
                            // Options worked, not test creds with Settings.
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = Do2ndTest,
                                State = (uint)Lst.TestW1
                            },
                            // It failed. Ask app for server config again.
                            new Trans {
                                Event = (uint)SmEvt.E.TempFail,
                                Act = DoUiGetServerTempFail,
                                State = (uint)Lst.SrvConfW
                            },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoPeek404,
                                State = (uint)Lst.Peek404
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
                                Act = DoUiGetCredOrGetServer,
                                ActSetsState = true
                            },
                            // Re-try test because app set creds.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoTest, ActSetsState = true },
                            // Re-try test because app set server config.
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                ActSetsState = true
                            },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Treat a 404 differently - as an auth-fail due to username.
                    new Node {State = (uint)Lst.Peek404,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {
                            (uint)SmEvt.E.Success, 
                            (uint)SmEvt.E.TempFail, 
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, 
                            (uint)AsProtoControl.AsEvt.E.ReProv,
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, 
                            (uint)TlEvt.E.Empty, 
                            (uint)TlEvt.E.TestDefaultServer,
                        },
                        On = new [] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoTest, ActSetsState = true },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoUiGetServer,
                                State = (uint)Lst.SrvConfW
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredW2
                            },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoTest, ActSetsState = true },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                ActSetsState = true
                            },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Waiting for new creds before server config set or robot success.
                    new Node {State = (uint)Lst.CredW1,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.Success,
                            (uint)SmEvt.E.HardFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, 
                            (uint)AsProtoControl.AsEvt.E.ReProv, 
                            (uint)AsProtoControl.AsEvt.E.ReSync, 
                            (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk,
                            (uint)TlEvt.E.Empty,
                            (uint)TlEvt.E.TestDefaultServer,
                        },
                        On = new[] {
                            // Ask app for creds.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiGetCred, State = (uint)Lst.CredW1 },
                            // Got creds, re-start all robots and try again.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoStepsPll, State = (uint)Lst.RobotW },
                            // Got new server value. run-with it and test it.
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, ActSetsState = true },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Waiting for new creds during server config testing.
                    new Node {State = (uint)Lst.CredW2,
                        Drop = new [] { (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] {
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.Success, 
                            (uint)SmEvt.E.HardFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc,
                            (uint)AsProtoControl.AsEvt.E.ReProv, 
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk,
                            (uint)TlEvt.E.Empty, 
                            (uint)TlEvt.E.TestDefaultServer,
                        },
                        On = new [] {
                            // Ask app for creds.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiGetCred, State = (uint)Lst.CredW1 },
                            // Got creds, re-test & wait.
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoTest, ActSetsState = true },
                            // Got new server value. run-with it and test it.
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, ActSetsState = true },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },

                    // Asked the app for a server config, waiting now...
                    new Node {State = (uint)Lst.SrvConfW, 
                        Drop = new [] { (uint)TlEvt.E.CredSet, (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY },
                        Invalid = new [] { 
                            (uint)SmEvt.E.Success, 
                            (uint)SmEvt.E.TempFail,
                            (uint)SmEvt.E.HardFail,
                            (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                            (uint)NcProtoControl.PcEvt.E.PendQHot,
                            (uint)NcProtoControl.PcEvt.E.Park,
                            (uint)AsProtoControl.AsEvt.E.ReDisc,
                            (uint)AsProtoControl.AsEvt.E.ReProv, 
                            (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)AsProtoControl.AsEvt.E.AuthFail, 
                            (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk, 
                            (uint)TlEvt.E.Empty,
                            (uint)TlEvt.E.TestDefaultServer,
                        },
                        On = new[] {
                            // Ask again and wait.
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiGetServer, State = (uint)Lst.SrvConfW },
                            // Got server config, now test & wait.
                            new Trans { Event = (uint)TlEvt.E.ServerSet, Act = DoTestFromUi, ActSetsState = true },
                            new Trans { Event = (uint)TlEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                        }
                    },
                }
            };
            Sm.Validate ();
        }

        private string KnownServer (string domain)
        {
            if (new NcServiceHelper.HotmailOutLookVerifier().validSuffixes.Contains (domain)) {
                return McServer.AS_HotMail_Host;
            } else {
                return null;
            }
        }

        public override void Execute (NcStateMachine ownerSm)
        {
            OwnerSm = ownerSm;
            Sm.Name = OwnerSm.Name + ":AUTOD";
            Domain = DomainFromEmailAddr (BEContext.Account.EmailAddr);
            BaseDomain = NachoPlatform.RegDom.Instance.RegDomFromFqdn (Domain);
            var known = KnownServer (Domain);
            if (null == BEContext.Server && null != known) {
                var server = new McServer () {
                    AccountId = AccountId,
                    Capabilities = McAccount.ActiveSyncCapabilities,
                    IsHardWired = true,
                    Host = known,
                };
                server.Insert ();
            }
            if (null == BEContext.Server || true == BEContext.Server.UsedBefore) {
                Sm.Start ();
            } else {
                ServerCandidate = BEContext.Server;
                // Okay to start at TestW1 because DoTest will evaluate Host.
                Sm.Start ((uint)Lst.TestW1);
            }
        }
        // UTILITY METHODS.
        private void AddAndStartRobot (StepRobot.Steps step, string domain, bool isUserSpecifiedDomain)
        {
            Log.Info (Log.LOG_AS, "AUTOD:{0}:BEGIN:Starting discovery for {1}/step {2}", step, domain, step);
            var robot = new StepRobot (this, step, BEContext.Account.EmailAddr, domain, isUserSpecifiedDomain, RobotEventsQ);
            Robots.Add (robot);
            robot.Execute ();
        }

        private void KillAllRobots ()
        {
            Log.Info (Log.LOG_AS, "AUTOD::END:Stopping all robots.");
            if (null != Robots) {
                lock (RobotsLockObj) {
                    foreach (var robot in Robots) {
                        robot.Cancel ();
                        DisposedJunk.Add (robot);
                    }
                }
                Robots = null;
            }
        }

        public override void Cancel ()
        {
            Sm.PostEvent ((uint)TlEvt.E.Cancel, "AUTODCANCEL");
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

        /// <summary>
        /// When authorization fails, we normally go to the get credentials view.
        /// But if the user selected Google Apps for Work and auto-d failed, then
        /// we don't really know that we have the correct server and we want to go
        /// to the advanced login view instead.
        /// </summary>
        private void DoUiGetCredOrGetServer ()
        {
            if (!AutoDSucceeded && McAccount.AccountServiceEnum.GoogleExchange == Account.AccountService) {
                Sm.State = (uint)Lst.SrvConfW;
                DoUiGetServer ();
            } else {
                Sm.State = (uint)Lst.CredW2;
                DoUiGetCred ();
            }
        }

        private void DoCancel ()
        {
            KillAllRobots ();
            AskingRobotQ = null;
            SuccessfulRobotQ = null;
            RobotEventsQ = null;
            SubdomainComplete = false;
            if (null != TestCmd) {
                TestCmd.Cancel ();
                DisposedJunk.Add (TestCmd);
                TestCmd = null;
            }
        }

        private void UpdateEmailAddressToAccount (string newEmailAddr)
        {
            var cred = BEContext.Cred;
            var account = BEContext.Account;
            if (cred.Username.Equals (account.EmailAddr, StringComparison.Ordinal)) {
                // cred.Username is the same as the current account.EmailAddr
                cred.Username = newEmailAddr;
                cred.Update ();
            }
            account.EmailAddr = newEmailAddr;
            account.Update ();
        }

        private void DoStepsPllRestart ()
        {
            if (Sm.Arg != null) {
                StepRobot robot = (StepRobot)Sm.Arg;
                if (!robot.SrEmailAddr.Equals (BEContext.Account.EmailAddr, StringComparison.Ordinal)) {
                    Log.Info (Log.LOG_AS, "AUTOD::Will restart auto discovery with new email address/domain {0}", robot.SrDomain);                   
                    UpdateEmailAddressToAccount (robot.SrEmailAddr);
                    Domain = DomainFromEmailAddr (BEContext.Account.EmailAddr);
                    BaseDomain = NachoPlatform.RegDom.Instance.RegDomFromFqdn (Domain);
                }
                DoStepsPll ();
            } else {
                Log.Error (Log.LOG_AS, "AUTOD::Restart event doesn't have Sm.Arg value.");  
                // Didn't do a hard fail since it doesn't report an error back to user. Posted a cannot connect to server. 
                // Sm.PostEvent ((uint)SmEvt.E.HardFail, "AUTODRESTARTFAIL");
                OwnerSm.PostEvent ((uint)AsProtoControl.CtlEvt.E.GetServConf, "AUTODRESTARTFAIL", BackEnd.AutoDFailureReasonEnum.CannotConnectToServer);

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
            RobotEventsQ = new ConcurrentQueue<Event> ();
            SubdomainComplete = false;
            Log.Info (Log.LOG_AS, "AUTOD::BEGIN:Starting all robots for domain {0}...", Domain);
            AddAndStartRobot (StepRobot.Steps.S1, Domain, true);
            AddAndStartRobot (StepRobot.Steps.S2, Domain, true);
            AddAndStartRobot (StepRobot.Steps.S3, Domain, true);
            AddAndStartRobot (StepRobot.Steps.S4, Domain, true);
            AddAndStartRobot (StepRobot.Steps.S5, Domain, true);
            if (BaseDomain != Domain) {
                AddAndStartRobot (StepRobot.Steps.S1, BaseDomain, false);
                AddAndStartRobot (StepRobot.Steps.S2, BaseDomain, false);
                AddAndStartRobot (StepRobot.Steps.S3, BaseDomain, false);
                AddAndStartRobot (StepRobot.Steps.S4, BaseDomain, false);
                AddAndStartRobot (StepRobot.Steps.S5, BaseDomain, false);
            }
        }

        private void DoReapRobot ()
        {
            StepRobot robot = (StepRobot)Sm.Arg;
            // Robot can't be on either ask or success queue, or it would not be reporting failure.
            Robots.Remove (robot);
            lock (RobotsLockObj) {
                if (ShouldDeQueueRobotEvents ()) {
                    SubdomainComplete = true;
                }
            }
            if (SubdomainComplete) {
                DeQueueRobotEvents ();
            }
            if (0 == Robots.Count) {
                // This can never happen in AskW - there is still the asking robot in the list.
                if (McAccount.AccountServiceEnum.GoogleExchange == Account.AccountService) {
                    // Auto-d failed, but the user earlier selected Google Apps for Work.  Test
                    // against the Google server before giving up.
                    Sm.PostEvent ((uint)TlEvt.E.TestDefaultServer, "AUTODTRYG");
                } else {
                    Sm.PostEvent ((uint)TlEvt.E.Empty, "AUTODDRR");
                }
            }
        }

        private bool ShouldDeQueueRobotEvents ()
        {
            // if base domain is same as domain, no events should have queued up. remove this check if events can be queued up for different reasons
            if (BaseDomain.Equals (Domain, StringComparison.Ordinal)) {
                return false;
            }
            // if subdomain robots are done, flush robot event Q
            return AreSubDomainRobotsDone ();
        }

        // check if robots are still doing subdomain discovery
        private bool AreSubDomainRobotsDone ()
        {
            return(!Robots.Any (x => x.IsUserSpecifiedDomain));
        }

        // DeQueue all queued events that the base domain robots may have sent
        private void DeQueueRobotEvents ()
        {
            Event Event;
            while (RobotEventsQ.TryDequeue (out Event)) {
                Log.Info (Log.LOG_AS, "AUTOD::Sending queued Event to SM for base domain {0}", BaseDomain);
                Sm.PostEvent (Event);
            }
        }

        // handle event from Robot
        private void ProcessEventFromRobot (Event Event, StepRobot Robot, ConcurrentQueue<Event> robotEventsQ)
        {
            lock (RobotsLockObj) {
                if (ShouldEnQueueRobotEvent (Event, Robot)) {
                    Log.Info (Log.LOG_AS, "AUTOD:{0}:Enqueuing Event for base domain {1}", Robot.Step, Robot.SrDomain);
                    robotEventsQ.Enqueue (Event);
                    return;
                }
            }
            Sm.PostEvent (Event);
        }

        private bool ShouldEnQueueRobotEvent (Event Event, StepRobot Robot)
        {
            // if robot domain is not the user specified domain, the robot reporting is running discovery for base domain
            // enqueue base domain robot events only if subdomain robots are not done 
            return !Robot.IsUserSpecifiedDomain && !SubdomainComplete;
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

        private void Do2ndTest ()
        {
            // TODO: enhance the top-level state machine so that these operations
            // OPTIONS, Settings/Provision aren't repeated unnecessarily.
            DoCancel ();
            if (ProtocolState.DisableProvisionCommand) {
                TestCmd = new AsSettingsCommand (this) {
                    DontReportCommResult = true,
                    MaxTries = 2,
                    OmitDeviceInformation = true,
                };
            } else {
                TestCmd = new AsProvisionCommand (this) {
                    DontReportCommResult = true,
                    MaxTries = 2,
                };
            }
            TestCmd.Execute (Sm);
        }

        private void DoTest ()
        {
            DoCancel ();
            TestCmd = new AsOptionsCommand (this) {
                DontReportCommResult = true,
                MaxTries = 2,
            };
            // HotMail/GMail doesn't WWW-Authenticate on OPTIONS.
            Sm.State = (ServerCandidate.HostIsAsGMail () || ServerCandidate.HostIsAsHotMail ())
                ? (uint)Lst.TestW2 : (uint)Lst.TestW1;

            TestCmd.Execute (Sm);
        }

        private void DoTestFromUi ()
        {
            AutoDSucceeded = false;
            ServerCandidate = BEContext.Server;
            DoTest ();
        }

        private void DoTestFromRobot ()
        {
            AutoDSucceeded = true;
            Log.Info (Log.LOG_AS, "AUTOD::END: Auto discovery succeeded.");
            var robot = (StepRobot)Sm.Arg;
            NcAssert.NotNull (robot);
            ServerCandidate = McServer.Create (AccountId, McAccount.ActiveSyncCapabilities, robot.SrServerUri);
            if (ServerCandidate.HostIsAsGMail ()) {
                // Robot can do this because of MX record. Not that if the user had entered this value, we would 
                // not want IsHardWired to be true.
                ServerCandidate.IsHardWired = true;
            }
            // Must shut down any remaining robots so they don't post events to TL SM.
            KillAllRobots ();
            // Must clear event Q for TL SM of anything a robot may have posted (threads).
            Sm.ClearEventQueue ();
            DoTest ();
        }

        /// <summary>
        /// Auto-d failed, but the user selected Google Apps for Work.  Do what the user
        /// told us to do and test the Google server.
        /// </summary>
        private void DoTestDefaultServer ()
        {
            AutoDSucceeded = false;
            ServerCandidate = McServer.Create (AccountId, McAccount.ActiveSyncCapabilities, 
                McServer.BaseUriForHost (McServer.AS_GMail_Host));
            ServerCandidate.IsHardWired = true;
            DoTest ();
        }

        private void DoPeek404 ()
        {
            if (((int?)HttpStatusCode.NotFound) == Sm.Arg as int?) {
                Sm.PostEvent ((uint)AsProtoControl.AsEvt.E.AuthFail, "AUTODP404USER");
            } else {
                Sm.PostEvent ((uint)SmEvt.E.HardFail, "AUTODP404HF");
            }
        }

        private void DoUiGetServer ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.CtlEvt.E.GetServConf, "AUTODDUGS", BackEnd.AutoDFailureReasonEnum.CannotFindServer);
        }

        private void DoUiGetServerTempFail ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.CtlEvt.E.GetServConf, "AUTODDUGSTF", BackEnd.AutoDFailureReasonEnum.CannotConnectToServer);
        }

        private void DoUiServerCertAsk ()
        {
            var robot = EnqAskAndGimmieRobot ();
            NcAssert.NotNull (robot.ServerCertificate);
            OwnerSm.PostEvent (Event.Create ((uint)AsProtoControl.CtlEvt.E.GetCertOk, "AUTODCERTASK", robot.ServerCertificate));
        }

        private void DoAcceptServerConf ()
        {
            var protocolState = BEContext.ProtocolState;
            protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                var target = (McProtocolState)record;
                target.LastAutoDSucceeded = AutoDSucceeded;
                return true;
            });

            // Save validated server config in DB.
            NcModel.Instance.RunInTransaction (() => {
                var serverRecord = BEContext.Server;
                if (null != serverRecord) {
                    serverRecord.CopyFrom (ServerCandidate);
                    serverRecord.UsedBefore = true;
                    serverRecord.Update ();
                } else {
                    var account = BEContext.Account;
                    ServerCandidate.Insert ();
                    account.Update ();
                }
            });
            // Signal that we are done and that we have a server config.
            // Success is the only way we finish - either by UI setting or autodiscovery.
            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
            OwnerSm.PostEvent ((uint)SmEvt.E.Success, "AUTODDASC");
        }
        // IBEContext proxying.
        public INcProtoControlOwner Owner {
            get { return BEContext.Owner; }
            set { BEContext.Owner = value; }
        }

        public NcProtoControl ProtoControl {
            get { return BEContext.ProtoControl; }
            set { BEContext.ProtoControl = value; }
        }

        public McProtocolState ProtocolState {
            get { return BEContext.ProtocolState; }
        }

        public McServer Server {
            get { return ServerCandidate; }
            // OPTIONS should not need to set the server, but Settings might. So we allow it.
            set { 
                Log.Info (Log.LOG_AS, "Server changed to {0} during test.", value.Host);
                ServerCandidate = value;
            }
        }

        public McAccount Account {
            get { return BEContext.Account; }
        }

        public McCred Cred {
            get { return BEContext.Cred; }
        }
    }
}
