// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
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
 * Autod has a serial top level state machine that can be comprehended by humans.
 * Autod maintains a pool of robots that run in parallel, doing the network accesses for each "step" of the process. 
 * The robots either post results to the autod state machine, or they store their results - 
 *   depending on the state of the autod machine. If the results are stored, then the autod machine retrieves them
 *   when top level state machine is ready to consume them.
 * The robots are the owners of the DNS/HTTP operations - not the top level state machine - 
 *   this is atypical for subclasses of AsCommand.
 */
namespace NachoCore.ActiveSync
{
    /* The only reason we implement & proxy IAsDataSource is so that we can source
     * candidate values for Server to AsHttpOperation when testing them.
     */
    public partial class AsAutodiscoverCommand : AsCommand, IAsDataSource
    {
        public enum Lst : uint
        {
            S1Wait = (St.Last + 1),
            S1AskWait,
            S2Wait,
            S2AskWait,
            S3Wait,
            S3AskWait,
            S4Wait,
            S4AskWait,
            BaseWait,
            CredWait,
            ServerWait,
            TestWait}
        ;
        // Event codes shared between TL and Robot SMs.
        public class SharedEvt : AsProtoControl.AsEvt
        {
            new public enum E : uint
            {
                AuthFail = (AsProtoControl.AsEvt.E.Last + 1),
                // 401.
                ServerCertYes,
                // UI response on server cert.
                ServerCertNo,
                // UI response on server cert.
                ReStart,
                // Protocol indicates that search must be restarted from step-1 (Robot or Top-Level).
                Last = ReStart}
            ;
        }

        public class TlEvt : SharedEvt
        {
            new public enum E : uint
            {
                CredSet = (SharedEvt.E.Last + 1),
                // UI has updated the credentials for this account (Top-Level only).
                ServerSet,
                // UI has updated the server information for this account (Top-Level only).
                ServerCertAsk,
                // Robot says UI has to ask user if server cert is okay (Top-Level only).
            };
        };

        public const string RequestSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/requestschema/2006";
        public const string ResponseSchema = "http://schemas.microsoft.com/exchange/autodiscover/mobilesync/responseschema/2006";
        private List<StepRobot> Robots;
        private AsOptionsCommand OptCmd;
        private string Domain;
        private string BaseDomain;
        private bool IsTryingBaseDomain;
        private NcServer ServerCandidate;
        public uint ReDirsLeft;

        public StateMachine Sm { get; set; }
        // CALLABLE BY THE OWNER.
        public AsAutodiscoverCommand (IAsDataSource dataSource) : base ("Autodiscover", 
                                                                        RequestSchema,
                                                                        dataSource)
        {
            ReDirsLeft = 10;
            Sm = new StateMachine () {
                Name = "as:autodiscover", 
                LocalEventType = typeof(TlEvt),
                LocalStateType = typeof(Lst),
                TransTable = new[] {
                    new Node {State = (uint)St.Start, 
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail, 
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        Drop = new [] { (uint)TlEvt.E.CredSet },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S1Wait,
                        Invalid = new [] {(uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = DoTestFromRobot,
                                State = (uint)Lst.TestWait
                            },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoS2, State = (uint)Lst.S2Wait },
                            new Trans {
                                Event = (uint)SharedEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredWait
                            },
                            new Trans { Event = (uint)SharedEvt.E.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerCertAsk,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S1AskWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S1AskWait,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)TlEvt.E.ServerCertAsk
                        },
                        On = new[] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S1AskWait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertYes,
                                Act = DoS1ServerCertYes,
                                State = (uint)Lst.S1Wait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertNo,
                                Act = DoS1ServerCertNo,
                                State = (uint)Lst.S1Wait
                            },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S2Wait,
                        Invalid = new [] {(uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoS2, State = (uint)Lst.S2Wait },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = DoTestFromRobot,
                                State = (uint)Lst.TestWait
                            },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoS3, State = (uint)Lst.S3Wait },
                            new Trans {
                                Event = (uint)SharedEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredWait
                            },
                            new Trans { Event = (uint)SharedEvt.E.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerCertAsk,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S2AskWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S2AskWait,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        On = new[] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S2AskWait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertYes,
                                Act = DoS2ServerCertYes,
                                State = (uint)Lst.S2Wait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertNo,
                                Act = DoS2ServerCertNo,
                                State = (uint)Lst.S2Wait
                            },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S3Wait,
                        Invalid = new [] {(uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoS3, State = (uint)Lst.S3Wait },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = DoTestFromRobot,
                                State = (uint)Lst.TestWait
                            },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoS4, State = (uint)Lst.S4Wait },
                            new Trans {
                                Event = (uint)SharedEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredWait
                            },
                            new Trans { Event = (uint)SharedEvt.E.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerCertAsk,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S3AskWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S3AskWait,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        On = new[] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S3AskWait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertYes,
                                Act = DoS3ServerCertYes,
                                State = (uint)Lst.S3Wait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertNo,
                                Act = DoS3ServerCertNo,
                                State = (uint)Lst.S3Wait
                            },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S4Wait,
                        Invalid = new [] {(uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoS4, State = (uint)Lst.S4Wait },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = DoTestFromRobot,
                                State = (uint)Lst.TestWait
                            },
                            new Trans { Event = (uint)SmEvt.E.HardFail, Act = DoBaseMaybe, State = (uint)Lst.BaseWait },
                            new Trans {
                                Event = (uint)SharedEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredWait
                            },
                            new Trans { Event = (uint)SharedEvt.E.ReStart, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerCertAsk,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S4AskWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.S4AskWait,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        On = new[] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoUiServerCertAsk,
                                State = (uint)Lst.S4AskWait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertYes,
                                Act = DoS4ServerCertYes,
                                State = (uint)Lst.S4Wait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.ServerCertNo,
                                Act = DoS4ServerCertNo,
                                State = (uint)Lst.S4Wait
                            },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.BaseWait,
                        Invalid = new [] {(uint)SmEvt.E.TempFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        Drop = new [] { (uint)TlEvt.E.CredSet },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoBaseMaybe, State = (uint)Lst.BaseWait },
                            new Trans { Event = (uint)SmEvt.E.Success, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoUiGetServer,
                                State = (uint)Lst.ServerWait
                            },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.TestWait,
                        Invalid = new [] {(uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoTest, State = (uint)Lst.TestWait },
                            new Trans {
                                Event = (uint)SmEvt.E.Success,
                                Act = DoAcceptServerConf,
                                State = (uint)St.Stop
                            },
                            new Trans { Event = (uint)SmEvt.E.TempFail, Act = DoTest, State = (uint)Lst.TestWait },
                            new Trans {
                                Event = (uint)SmEvt.E.HardFail,
                                Act = DoUiGetServer,
                                State = (uint)Lst.ServerWait
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                Act = DoUiGetServer,
                                State = (uint)Lst.ServerWait
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                Act = DoUiGetServer,
                                State = (uint)Lst.ServerWait
                            },
                            new Trans {
                                Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                Act = DoUiGetServer,
                                State = (uint)Lst.ServerWait
                            },
                            new Trans {
                                Event = (uint)SharedEvt.E.AuthFail,
                                Act = DoUiGetCred,
                                State = (uint)Lst.CredWait
                            },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoTest, State = (uint)Lst.TestWait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.CredWait,
                        Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        On = new[] {
                            new Trans { Event = (uint)SmEvt.E.Launch, Act = DoUiGetCred, State = (uint)Lst.CredWait },
                            new Trans { Event = (uint)TlEvt.E.CredSet, Act = DoS14Pll, State = (uint)Lst.S1Wait },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },

                    new Node {State = (uint)Lst.ServerWait, 
                        Invalid = new [] { (uint)SmEvt.E.Success, (uint)SmEvt.E.TempFail, (uint)SmEvt.E.HardFail,
                            (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                            (uint)SharedEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.ServerCertNo, (uint)SharedEvt.E.ServerCertYes,
                            (uint)TlEvt.E.ServerCertAsk
                        },
                        Drop = new [] { (uint)TlEvt.E.CredSet },
                        On = new[] {
                            new Trans {
                                Event = (uint)SmEvt.E.Launch,
                                Act = DoUiGetServer,
                                State = (uint)Lst.ServerWait
                            },
                            new Trans {
                                Event = (uint)TlEvt.E.ServerSet,
                                Act = DoTestFromUi,
                                State = (uint)Lst.TestWait
                            },
                        }
                    },
                }
            };
            Sm.Validate ();
        }

        public override void Execute (StateMachine ownerSm)
        {
            OwnerSm = ownerSm;
            Domain = DomainFromEmailAddr (DataSource.Account.EmailAddr);
            BaseDomain = NachoPlatform.RegDom.Instance.RegDomFromFqdn (Domain);
            Sm.PostEvent ((uint)SmEvt.E.Launch);
        }
        // UTILITY METHODS.
        private uint WaitStateFromStep (StepRobot.Steps Step)
        {
            switch (Step) {
            case StepRobot.Steps.S1:
                return (uint)Lst.S1Wait;
            case StepRobot.Steps.S2:
                return (uint)Lst.S2Wait;
            case StepRobot.Steps.S3:
                return (uint)Lst.S3Wait;
            case StepRobot.Steps.S4:
                return (uint)Lst.S4Wait;
            default:
                throw new Exception ("Unknown Step value");
            }
        }

        private bool MatchesState (StepRobot.Steps Step, bool IsBaseDomain)
        {
            if (IsBaseDomain != IsTryingBaseDomain) {
                return false;
            }
            uint waitState = WaitStateFromStep (Step);
            return waitState == Sm.State;
        }

        private StepRobot RobotFromOp (AsHttpOperation Op)
        {
            return Robots.Where (elem => Op == elem.HttpOp).Single ();
        }

        private void AddStartRobot (StepRobot.Steps step, bool isBaseDomain, string domain)
        {
            var robot = new StepRobot (this, step, DataSource.Account.EmailAddr, isBaseDomain, domain);
            Robots.Add (robot);
            robot.Execute ();
        }

        public override void Cancel ()
        {
            if (null != Robots) {
                foreach (var robot in Robots) {
                    robot.Cancel ();
                }
            }
        }

        private static string DomainFromEmailAddr (string EmailAddr)
        {
            return EmailAddr.Split ('@').Last ();
        }

        private StepRobot FindRobot (StepRobot.Steps step)
        {
            return Robots.Where (x => x.Step == step &&
            x.IsBaseDomain == IsTryingBaseDomain).Single ();
        }
        // IMPLEMENTATION OF TOP-LEVEL STATE MACHINE.
        private void DoS14Pll ()
        {
            Cancel ();
            Robots = new List<StepRobot> ();
            // Try to perform steps 1-4 in parallel.
            AddStartRobot (StepRobot.Steps.S1, false, Domain);
            AddStartRobot (StepRobot.Steps.S2, false, Domain);
            AddStartRobot (StepRobot.Steps.S3, false, Domain);
            AddStartRobot (StepRobot.Steps.S4, false, Domain);
            // If there is a base domain we might end up searching, then start that in parallel too.
            if (Domain != BaseDomain) {
                AddStartRobot (StepRobot.Steps.S1, true, BaseDomain);
                AddStartRobot (StepRobot.Steps.S2, true, BaseDomain);
                AddStartRobot (StepRobot.Steps.S3, true, BaseDomain);
                AddStartRobot (StepRobot.Steps.S4, true, BaseDomain);
            }
        }

        private void DoSx (StepRobot.Steps step)
        {
            var robot = FindRobot (step);
            if ((uint)StepRobot.RobotEvt.E.NullCode != robot.ResultingEvent.EventCode) {
                Sm.PostEvent (robot.ResultingEvent);
            }
        }

        private void DoS2 ()
        {
            DoSx (StepRobot.Steps.S2);
        }

        private void DoS3 ()
        {
            DoSx (StepRobot.Steps.S3);
        }

        private void DoS4 ()
        {
            DoSx (StepRobot.Steps.S4);
        }

        private void DoSxServerCertX (StepRobot.Steps step, uint eventCode)
        {
            var robot = FindRobot (step);
            robot.StepSm.PostEvent (eventCode);
        }

        private void DoS1ServerCertYes ()
        {
            DoSxServerCertX (StepRobot.Steps.S1, (uint)SharedEvt.E.ServerCertYes);
        }

        private void DoS1ServerCertNo ()
        {
            DoSxServerCertX (StepRobot.Steps.S1, (uint)SharedEvt.E.ServerCertNo);
        }

        private void DoS2ServerCertYes ()
        {
            DoSxServerCertX (StepRobot.Steps.S2, (uint)SharedEvt.E.ServerCertYes);
        }

        private void DoS2ServerCertNo ()
        {
            DoSxServerCertX (StepRobot.Steps.S2, (uint)SharedEvt.E.ServerCertNo);
        }

        private void DoS3ServerCertYes ()
        {
            DoSxServerCertX (StepRobot.Steps.S3, (uint)SharedEvt.E.ServerCertYes);
        }

        private void DoS3ServerCertNo ()
        {
            DoSxServerCertX (StepRobot.Steps.S3, (uint)SharedEvt.E.ServerCertNo);
        }

        private void DoS4ServerCertYes ()
        {
            DoSxServerCertX (StepRobot.Steps.S4, (uint)SharedEvt.E.ServerCertYes);
        }

        private void DoS4ServerCertNo ()
        {
            DoSxServerCertX (StepRobot.Steps.S4, (uint)SharedEvt.E.ServerCertNo);
        }

        private void DoBaseMaybe ()
        {
            // Check to see if there is still a base domain to search.
            // If yes, Success, else HardFail.
            if (BaseDomain != Domain && !IsTryingBaseDomain) {
                IsTryingBaseDomain = true;
                Sm.PostEvent ((uint)SmEvt.E.Success);
            } else {
                Sm.PostEvent ((uint)SmEvt.E.HardFail);
            }
        }

        private void DoTest ()
        {
            Cancel ();
            OptCmd = new AsOptionsCommand (this);
            OptCmd.Execute (Sm);
        }

        private void DoTestFromUi ()
        {
            ServerCandidate = DataSource.Server;
            DoTest ();
        }

        private void DoTestFromRobot ()
        {
            var robot = (StepRobot)Sm.Arg;
            ServerCandidate = NcServer.Create (robot.SrServerUri);
            DoTest ();
        }

        private void DoUiGetCred ()
        {
            // Ask the UI to either re-get the password, or to get the username + (optional) domain.
            OwnerSm.PostEvent ((uint)AsProtoControl.CtlEvt.E.GetCred);
        }

        private void DoUiGetServer ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.CtlEvt.E.GetServConf);
        }

        private void DoUiServerCertAsk ()
        {
            OwnerSm.PostEvent (Event.Create ((uint)AsProtoControl.CtlEvt.E.GetCertOk, ((StepRobot)Sm.Arg).ServerCertificate));
        }

        private void DoAcceptServerConf ()
        {
            // Save validated server config in DB.
            var serverRecord = DataSource.Server;
            serverRecord.Update (ServerCandidate);
            DataSource.Owner.Db.Update (BackEnd.DbActors.Proto, serverRecord);
            // Signal that we are done and that we have a server config.
            // Success is the only way we finish - either by UI setting or autodiscovery.
            OwnerSm.PostEvent ((uint)SmEvt.E.Success);
        }
        // IAsDataSource proxying.
        public IProtoControlOwner Owner {
            get { return DataSource.Owner; }
            set { DataSource.Owner = value; }
        }

        public AsProtoControl Control {
            get { return DataSource.Control; }
            set { DataSource.Control = value; }
        }

        public NcProtocolState ProtocolState {
            get { return DataSource.ProtocolState; }
            set { DataSource.ProtocolState = value; }
        }

        public NcServer Server {
            get { return ServerCandidate; }
            set { throw new Exception ("Illegal set of Server by AsOptionsCommand."); }
        }

        public NcAccount Account {
            get { return DataSource.Account; }
        }

        public NcCred Cred {
            get { return DataSource.Cred; }
        }
    }
}

