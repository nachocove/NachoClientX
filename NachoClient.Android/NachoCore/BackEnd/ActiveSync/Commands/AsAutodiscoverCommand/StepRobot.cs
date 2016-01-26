// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using DnDns.Enums;
using DnDns.Query;
using DnDns.Records;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;
using NachoCore.Wbxml;

namespace NachoCore.ActiveSync
{
    public partial class AsAutodiscoverCommand : AsCommand
    {
        private class StepRobot : IAsHttpOperationOwner, IAsDnsOperationOwner
        {
            private const int KDefaultCertTimeoutSeconds = 8;
            private double KDefaultTimeoutExpander = 2.0;

            public enum RobotLst : uint
            {
                PostWait = (St.Last + 1),
                GetWait,
                SrvDnsWait,
                MxDnsWait,
                CertWait,
                OkWait,
                ReDirWait,
            };

            public class RobotEvt : SharedEvt
            {
                new public enum E : uint
                {
                    // 302.
                    ReDir = (SharedEvt.E.Last + 1),
                    Cancel,
                    // Not a real event. A not-yet-set value.
                    NullCode,
                };
            }
            // Pseudo-constants.
            public AsAutodiscoverCommand Command;
            public XNamespace Ns;
            // Initial programming of the Robot.
            public enum Steps
            {
                S1,
                S2,
                S3,
                S4,
                // S5 - isn't in the MSFT doc. It is us looking at MX records to check for Google Apps.
                S5,
            };

            public Steps Step;
            public HttpMethod MethodToUse;
            // Stored results. These will be copied back to the configuration upon top-level success.
            // EmailAddr and Domain are pre-loaded at the start.
            public string SrEmailAddr;
            public string SrDomain;
            public string SrDisplayName;
            public string SrCulture;
            public Uri SrServerUri;
            public X509Certificate2 ServerCertificate;
            // Owned operations and execution values.
            public NcStateMachine StepSm;
            public AsHttpOperation HttpOp;
            public AsDnsOperation DnsOp;
            public uint RetriesLeft;
            public bool IsReDir;
            public Uri ReDirUri;
            public bool IsUserSpecifiedDomain;


            private TimeSpan CertTimeout;
            private ConcurrentBag<object> DisposedJunk;
            // Allocated at constructor, thereafter only accessed by Cancel.
            private CancellationTokenSource Cts;
            // Initialized at constructor, thereafter accessed anywhere.
            private CancellationToken Ct;
            // Record re-dir source URLs to avoid any loops.
            private List<Uri> ReDirSource = new List<Uri> ();
            private Uri LastUri;
            // A pointer to the cmd's event Q.
            private ConcurrentQueue<Event> RobotEventsQ;

            public StepRobot (AsAutodiscoverCommand command, Steps step, string emailAddr, string domain, bool isUserSpecifiedDomain, ConcurrentQueue<Event> robotEventsQ)
            {
                int timeoutSeconds = McMutables.GetOrCreateInt (McAccount.GetDeviceAccount ().Id, "AUTOD", "CertTimeoutSeconds", KDefaultCertTimeoutSeconds);
                CertTimeout = new TimeSpan (0, 0, timeoutSeconds);
                Cts = new CancellationTokenSource ();
                Ct = Cts.Token;
                DisposedJunk = new ConcurrentBag<object> ();
                RefreshRetries ();

                Command = command;
                Ns = AsAutodiscoverCommand.RequestSchema;

                Step = step;
                switch (step) {
                case Steps.S1:
                case Steps.S2:
                    // After DNS, will use HTTP/POST on resolved host.
                case Steps.S4:
                    MethodToUse = HttpMethod.Post;
                    break;
                case Steps.S3:
                    MethodToUse = HttpMethod.Get;
                    break;
                case Steps.S5:
                    // After DNS, if there is a match, then there is no HTTP/S access to follow.
                    break;
                default:
                    throw new Exception ("Invalid step value.");
                }

                SrEmailAddr = emailAddr;
                SrDomain = domain;
                IsUserSpecifiedDomain = isUserSpecifiedDomain;
                RobotEventsQ = robotEventsQ;

                StepSm = new NcStateMachine ("AUTODSTEP") {
                    /* NOTE: There are three start states:
                     * PostWait - used for S1/S2,
                     * GetWait - used for S3,
                     * SrvDnsWait - used for S4.
                     * MxDnsWait - used for S5.
                     */
                    Name = "SR",
                    LocalEventType = typeof(RobotEvt),
                    LocalStateType = typeof(RobotLst),
                    TransTable = new [] {
                        new Node {State = (uint)RobotLst.PostWait, 
                            Invalid = new [] {
                                (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                                (uint)NcProtoControl.PcEvt.E.PendQHot,
                                (uint)NcProtoControl.PcEvt.E.Park,
                                (uint)SharedEvt.E.SrvCertN, 
                                (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.NullCode,
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.PostWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotPostSuccess,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotPostHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotPostHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                    Act = DoRobotPostHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                    Act = DoRobotPostHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                    Act = DoRobotPostHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                    Act = DoRobotAuthFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SharedEvt.E.ReStart,
                                    Act = DoRobotReStart,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)RobotEvt.E.ReDir,
                                    Act = DoRobotPostReDir,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.GetWait,
                            Invalid = new [] {
                                (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                                (uint)NcProtoControl.PcEvt.E.PendQHot,
                                (uint)NcProtoControl.PcEvt.E.Park,
                                (uint)AsProtoControl.AsEvt.E.AuthFail,
                                (uint)SharedEvt.E.ReStart,
                                (uint)SharedEvt.E.SrvCertN,
                                (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.NullCode,
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.GetWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotGetHardFail, // Only 302 is okay w/a GET.
                                    State = (uint)St.Stop
                                }, 
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotGetHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotGetHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                    Act = DoRobotGetHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                    Act = DoRobotGetHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                    Act = DoRobotGetHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)RobotEvt.E.ReDir,
                                    Act = DoRobotGet2ReDir,
                                    State = (uint)RobotLst.CertWait
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.SrvDnsWait,
                            Invalid = new [] {
                                (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                                (uint)NcProtoControl.PcEvt.E.PendQHot,
                                (uint)NcProtoControl.PcEvt.E.Park,
                                (uint)AsProtoControl.AsEvt.E.ReDisc, 
                                (uint)AsProtoControl.AsEvt.E.ReProv, 
                                (uint)AsProtoControl.AsEvt.E.ReSync,
                                (uint)AsProtoControl.AsEvt.E.AuthFail, 
                                (uint)SharedEvt.E.ReStart, 
                                (uint)SharedEvt.E.SrvCertN, 
                                (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.ReDir, 
                                (uint)RobotEvt.E.NullCode,
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotDns,
                                    State = (uint)RobotLst.SrvDnsWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotDns2ReDir,
                                    State = (uint)RobotLst.CertWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotDnsSrvHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotDnsSrvHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.MxDnsWait,
                            Invalid = new [] {
                                (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                                (uint)NcProtoControl.PcEvt.E.PendQHot,
                                (uint)NcProtoControl.PcEvt.E.Park,
                                (uint)AsProtoControl.AsEvt.E.ReDisc,
                                (uint)AsProtoControl.AsEvt.E.ReProv,
                                (uint)AsProtoControl.AsEvt.E.ReSync,
                                (uint)AsProtoControl.AsEvt.E.AuthFail,
                                (uint)SharedEvt.E.ReStart, 
                                (uint)SharedEvt.E.SrvCertN,
                                (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.ReDir, 
                                (uint)RobotEvt.E.NullCode,
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotDns,
                                    State = (uint)RobotLst.MxDnsWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotMxDnsSuccess,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotDnsMxHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotDnsMxHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.CertWait,
                            Invalid = new [] {
                                (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                                (uint)NcProtoControl.PcEvt.E.PendQHot,
                                (uint)NcProtoControl.PcEvt.E.Park,
                                (uint)AsProtoControl.AsEvt.E.ReDisc, 
                                (uint)AsProtoControl.AsEvt.E.ReProv, 
                                (uint)AsProtoControl.AsEvt.E.ReSync,
                                (uint)AsProtoControl.AsEvt.E.AuthFail, 
                                (uint)SharedEvt.E.ReStart,
                                (uint)SharedEvt.E.SrvCertN, 
                                (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.ReDir, 
                                (uint)RobotEvt.E.NullCode,
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotGetServerCert,
                                    State = (uint)RobotLst.CertWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotGotCert,
                                    State = (uint)RobotLst.OkWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotGetCertHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotGetCertHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.OkWait,
                            Invalid = new [] {
                                (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                                (uint)NcProtoControl.PcEvt.E.PendQHot,
                                (uint)NcProtoControl.PcEvt.E.Park,
                                (uint)SmEvt.E.Success,
                                (uint)SmEvt.E.HardFail, 
                                (uint)SmEvt.E.TempFail,
                                (uint)AsProtoControl.AsEvt.E.ReDisc,
                                (uint)AsProtoControl.AsEvt.E.ReProv, 
                                (uint)AsProtoControl.AsEvt.E.ReSync, 
                                (uint)AsProtoControl.AsEvt.E.AuthFail,
                                (uint)SharedEvt.E.ReStart,
                                (uint)RobotEvt.E.ReDir,
                                (uint)RobotEvt.E.NullCode,
                            }, 
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotUiCertAsk,
                                    State = (uint)RobotLst.OkWait
                                },
                                new Trans {
                                    Event = (uint)SharedEvt.E.SrvCertY,
                                    Act = DoRobotGotCertOk,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans {
                                    Event = (uint)SharedEvt.E.SrvCertN,
                                    Act = DoRobotCertOkHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.ReDirWait,
                            Invalid = new [] {
                                (uint)NcProtoControl.PcEvt.E.PendQOrHint,
                                (uint)NcProtoControl.PcEvt.E.PendQHot,
                                (uint)NcProtoControl.PcEvt.E.Park,
                                (uint)SharedEvt.E.SrvCertN, 
                                (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.NullCode,
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotSuccess,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotReDirHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotReDirHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                    Act = DoRobotReDirHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                    Act = DoRobotReDirHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                    Act = DoRobotReDirHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.AuthFail,
                                    Act = DoRobotAuthFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SharedEvt.E.ReStart,
                                    Act = DoRobotReStart,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)RobotEvt.E.ReDir,
                                    Act = DoRobotReDirAgain,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },
                    }
                };
                StepSm.Validate ();
            }

            public virtual double TimeoutInSeconds
            {
                get {
                    return 10.0;
                }
            }

            public void Execute ()
            {
                StepSm.Name = Command.Sm.Name + ":" + StepSm.Name + "(" + Enum.GetName (typeof(Steps), Step) + ")";
                switch (Step) {
                case Steps.S1:
                case Steps.S2:
                    StepSm.Start ((uint)RobotLst.PostWait);
                    break;
                case Steps.S3:
                    StepSm.Start ((uint)RobotLst.GetWait);
                    break;
                case Steps.S4:
                    StepSm.Start ((uint)RobotLst.SrvDnsWait);
                    break;
                case Steps.S5:
                    StepSm.Start ((uint)RobotLst.MxDnsWait);
                    break;
                default:
                    throw new Exception ("Unknown Step value.");
                }
            }

            public void Cancel ()
            {
                if (null != Cts) {
                    Cts.Cancel ();
                    DisposedJunk.Add (Cts);
                    Cts = null;
                }
                StepSm.PostEvent ((uint)RobotEvt.E.Cancel, "SRCANCEL");
            }
            // UTILITY METHODS.
            private void RefreshRetries ()
            {
                RetriesLeft = 1;
            }

            private void ForTopLevel (Event Event)
            {
                // Once cancelled, we must post NO event to TL SM.
                if (!Ct.IsCancellationRequested) {
                    Command.ProcessEventFromRobot (Event, this, RobotEventsQ);
                }
            }

            public void ServerCertificateEventHandler (IHttpWebRequest sender,
                                                       X509Certificate2 certificate,
                                                       X509Chain chain,
                                                       SslPolicyErrors sslPolicyErrors, 
                                                       EventArgs e)
            {
                if (sender.RequestUri.Host.Equals (ReDirUri.Host)) {
                    // Capture the server cert.
                    ServerCertificate = certificate;
                }
            }
            // *********************************************************************************
            // Step Robot state machine action commands.
            // *********************************************************************************

            private bool IsNotReDirLoop (Uri suspect)
            {
                if (ReDirSource.Contains (suspect)) {
                    Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: Re-direct loop: {1}.", Step, suspect);
                    return false;
                }
                return true;
            }

            private AsHttpOperation HttpOpFactory ()
            {
                return new AsHttpOperation (Command.CommandName, this, Command.BEContext) {
                    Allow451Follow = false,
                    DontReportCommResult = true,
                    TriesLeft = 2,
                    TimeoutExpander = KDefaultTimeoutExpander,
                };
            }

            private void DoRobotHttp ()
            {
                Uri currentUri;
                try {
                    currentUri = CurrentServerUri ();
                } catch (UriFormatException) {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDRHHARDURI");
                    return;
                }
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS:Sending HTTP {2} request to {1}", Step, currentUri, MethodToUse);
                if (IsNotReDirLoop (currentUri) && 0 < RetriesLeft--) {
                    HttpOp = HttpOpFactory ();
                    LastUri = currentUri;
                    HttpOp.Execute (StepSm);
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDRHHARD");
                }
            }

            private void DoRobotDns ()
            {
                if (0 < RetriesLeft--) {
                    Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS:Sending DNS request to {1}", Step, this.DnsHost (null));
                    DnsOp = new AsDnsOperation (this, new TimeSpan (0, 0, 10));
                    DnsOp.Execute (StepSm);
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDRDHARD");
                }
            }

            private void DoRobotGotCertOk ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: User approved server SSL cert.", Step);
                DoRobot302 ();
            }

            private void DoRobotPostReDir ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: POST => 302: {1}.", Step, LastUri);
                DoRobot302 ();
            }

            private void DoRobotReDirAgain ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: {1} => 302: {2}.", Step, MethodToUse, LastUri);
                DoRobot302 ();
            }

            private void DoRobot302 ()
            {
                // NOTE: this handles the 302 case, NOT the <Redirect> in XML after 200 case.
                Uri currentUri;
                try {
                    currentUri = CurrentServerUri ();
                } catch (UriFormatException) {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDR302HARDURI");
                    return;
                }
                if (IsNotReDirLoop (currentUri) && 0 < Command.ReDirsLeft--) {
                    RefreshRetries ();
                    HttpOp = HttpOpFactory ();
                    LastUri = currentUri;
                    HttpOp.Execute (StepSm);
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDR302HARD");
                }
            }

            private void DoRobotGet2ReDir ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: ReDir after GET to {1}", Step, ReDirUri);
                DoRobot2ReDir ();
            }

            private void DoRobot2ReDir ()
            {
                MethodToUse = HttpMethod.Post;
                RefreshRetries ();
                DoRobotGetServerCert ();
            }

            private void DoRobotDns2ReDir ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: DNS SRV response.", Step);
                // Make the successful SRV lookup seem like a 302 so that the code for
                // the remaining flow is unified for both paths (GET/DNS).
                IsReDir = true;
                try {
                    ReDirUri = new Uri (string.Format ("https://{0}/autodiscover/autodiscover.xml", SrDomain));
                }
                catch (UriFormatException) {
                    Log.Warn (Log.LOG_AS, "SRV record does not look like a hostname: {0}", SrDomain);
                    StepSm.PostEvent ((uint)SmEvt.E.TempFail, "SRDRD2RD");
                    return;
                }
                DoRobot2ReDir ();
            }

            private void DoRobotGetServerCert ()
            {
                X509Certificate2 cached;
                if (ServerCertificatePeek.Instance.Cache.TryGetValue (ReDirUri.Host, out cached)) {
                    ServerCertificate = cached;
                    StepSm.PostEvent ((uint)SmEvt.E.Success, "SRDRGSCC");
                    return;
                }
                if (0 < RetriesLeft--) {
                    ServerCertificate = null;
                    var request = new NcHttpRequest (HttpMethod.Get, ReDirUri);
                    ServerCertificatePeek.Instance.ValidationEvent += ServerCertificateEventHandler;
                    LastUri = ReDirUri;
                    Command.ProtoControl.HttpClient.GetRequest (request, CertTimeout.Milliseconds, GetServerCertSuccess, GetServerCertError, Cts.Token);
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDRGSC3");
                    return;
                }
            }

            void GetServerCertSuccess (NcHttpResponse response, CancellationToken token)
            {
                ServerCertificatePeek.Instance.ValidationEvent -= ServerCertificateEventHandler;
                if (null == ServerCertificate) {
                    StepSm.PostEvent ((uint)SmEvt.E.TempFail, "SRDRGSC1");
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.Success, "SRDRGSC2");
                }
            }

            void GetServerCertError (Exception exception, CancellationToken token)
            {
                Log.Info (Log.LOG_AS, "SR:GetAsync Exception: {0}", exception.ToString ());
                GetServerCertSuccess (null, token);
            }

            private void DoRobotGotCert ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: Retrieved server SSL cert.", Step);
                DoRobotUiCertAsk ();
            }

            private void DoRobotUiCertAsk ()
            {
                ForTopLevel (Event.Create ((uint)TlEvt.E.ServerCertAsk, "SRCERTASK", this));
            }

            private void DoRobotReStart ()
            {
                ForTopLevel (Event.Create ((uint)SharedEvt.E.ReStart, "SRRESTART", this));
            }

            private void DoRobotAuthFail ()
            {
                string currentUriString;
                try {
                    currentUriString = CurrentServerUri ().ToString ();
                } catch (UriFormatException) {
                    currentUriString = string.Format ("Could not format Uri, SrDomain: {0}", SrDomain);
                }
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: Auth failed: {2}:{1}.", Step, currentUriString, MethodToUse);
                ForTopLevel (Event.Create ((uint)AsProtoControl.AsEvt.E.AuthFail, "SRAUTHFAIL", this));
            }

            private void DoRobotPostSuccess ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: POST succeeded: {1}.", Step, LastUri);
                DoRobotSuccess ();
            }

            private void DoRobotMxDnsSuccess ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:PROGRESS: MX DNS succeeded: {1}.", Step, SrServerUri);
                DoRobotSuccess ();
            }

            private void DoRobotSuccess ()
            {
                NcAssert.NotNull (SrServerUri);
                ForTopLevel (Event.Create ((uint)SmEvt.E.Success, "SRSUCCESS", this));
            }

            private void DoRobotPostHardFail ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: POST failed: {1}.", Step, LastUri);
                DoRobotHardFail ();
            }

            private void DoRobotGetHardFail ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: GET failed: {1}.", Step, LastUri);
                DoRobotHardFail ();
            }

            private void DoRobotDnsSrvHardFail ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: DNS query SRV rec to {1} failed.", Step, this.DnsHost (null));
                DoRobotHardFail ();
            }

            private void DoRobotDnsMxHardFail ()
            {
                Command.ProtoControl.AutoDInfo = AutoDInfoEnum.MXNotFound;
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: DNS query MX rec to {1} failed.", Step, this.DnsHost (null));
                DoRobotHardFail ();
            }

            private void DoRobotGetCertHardFail ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: Could not retrieve server SSL cert.", Step);
                DoRobotHardFail ();
            }

            private void DoRobotCertOkHardFail ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: User rejected server SSL cert.", Step);
                DoRobotHardFail ();
            }

            private void DoRobotReDirHardFail ()
            {
                Log.Info (Log.LOG_AS, "AUTOD:{0}:FAIL: {1} failed: {2}.", Step, MethodToUse, LastUri);
                DoRobotHardFail ();
            }

            private void DoRobotHardFail ()
            {
                ForTopLevel (Event.Create ((uint)SmEvt.E.HardFail, "SRHARDFAIL", this));
            }

            private void DoCancel ()
            {
                if (null != HttpOp) {
                    HttpOp.Cancel ();
                    DisposedJunk.Add (HttpOp);
                    HttpOp = null;
                }
                if (null != DnsOp) {
                    DnsOp.Cancel ();
                    DisposedJunk.Add (DnsOp);
                    DnsOp = null;
                }
            }
            // *********************************************************************************
            // AsHttpOperationOwner callbacks.
            // *********************************************************************************

            private Uri CurrentServerUri ()
            {
                if (IsReDir) {
                    return ReDirUri;
                }
                switch (Step) {
                case StepRobot.Steps.S1:
                    return new Uri (string.Format ("https://{0}/autodiscover/autodiscover.xml", SrDomain));
                case StepRobot.Steps.S2:
                    return new Uri (string.Format ("https://autodiscover.{0}/autodiscover/autodiscover.xml", SrDomain));
                case StepRobot.Steps.S3:
                    return new Uri (string.Format ("http://autodiscover.{0}/autodiscover/autodiscover.xml", SrDomain));
                default:
                    throw new Exception ();
                }
            }

            public Uri ServerUri (AsHttpOperation Sender, bool isEmailRedacted = false)
            {
                return CurrentServerUri ();
            }

            public virtual void ServerUriChanged (Uri ServerUri, AsHttpOperation Sender)
            {
                throw new Exception ("We should not be getting this (HTTP 451) while doing autodiscovery.");
            }

            public virtual bool SafeToMime (AsHttpOperation Sender, out FileStream mime)
            {
                // We don't generate MIME.
                mime = null;
                return true;
            }

            public Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status, XDocument doc)
            {
                // There is no AS XML <Status> to report on.
                return null;
            }

            public virtual Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
            {
                // We take over URI generation elsewhere.
                return null;
            }

            public virtual bool WasAbleToRephrase ()
            {
                // Autodiscover does not touch pending.
                return false;
            }

            public virtual void ResolveAllFailed (NcResult.WhyEnum why)
            {
                // Autodiscover does not touch pending.
            }

            public virtual void ResolveAllDeferred ()
            {
                // Autodiscover does not touch pending.
            }

            public HttpMethod Method (AsHttpOperation Sender)
            {
                return MethodToUse;
            }

            public void StatusInd (NcResult result)
            {
                Command.BEContext.Owner.StatusInd (Command.BEContext.ProtoControl, result);
            }

            public void StatusInd (bool didSucceed)
            {
            }

            public bool UseWbxml (AsHttpOperation Sender)
            {
                // Autodiscovery is XML only.
                return false;
            }

            public bool IgnoreBody (AsHttpOperation Sender)
            {
                return false;
            }

            public bool DoSendPolicyKey (AsHttpOperation Sender)
            {
                return false;
            }

            public bool SafeToXDocument (AsHttpOperation Sender, out XDocument doc)
            {
                if (HttpMethod.Post != MethodToUse) {
                    doc = null;
                    return true;
                }
                doc = AsCommand.ToEmptyXDocument ();
                doc.Add (new XElement (Ns + Xml.Autodisco.Autodiscover,
                    new XElement (Ns + Xml.Autodisco.Request,
                        new XElement (Ns + Xml.Autodisco.EMailAddress, SrEmailAddr),
                        new XElement (Ns + Xml.Autodisco.AcceptableResponseSchema, AsAutodiscoverCommand.ResponseSchema))));
                return true;
            }

            public Event PreProcessResponse (AsHttpOperation Sender, NcHttpResponse response)
            {
                switch (response.StatusCode) {
                case HttpStatusCode.Found:
                    try {
                        ReDirUri = new Uri (response.Headers.GetValues ("Location").First ());
                        Log.Info (Log.LOG_AS, "REDIRURI: {0}", ReDirUri);
                        IsReDir = true;
                    } catch {
                        return Event.Create ((uint)SmEvt.E.HardFail, "SRPPPHARD");
                    }
                    return Event.Create ((uint)RobotEvt.E.ReDir, "SRPPPREDIR");

                case HttpStatusCode.OK:
                case HttpStatusCode.Unauthorized:
                    // We want to use the existing AsHttpOperation logic in the 200/401 cases.
                    return null;

                default:
                    // The only acceptable status codes are 200, 302 & 401.
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRPPPDEFHARD");
                }
            }

            public Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, CancellationToken cToken)
            {
                // We should never get back content that isn't XML.
                return Event.Create ((uint)SmEvt.E.HardFail, "SRPR0HARD");
            }

            public Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
            {
                LogRedactedXml ("Autodiscover.xml", doc);
                var xmlResponse = doc.Root.ElementAnyNs (Xml.Autodisco.Response);
                var xmlUser = xmlResponse.ElementAnyNs (Xml.Autodisco.User);
                if (null != xmlUser) {
                    var xmlEMailAddress = xmlUser.ElementAnyNs (Xml.Autodisco.EMailAddress);
                    if (null != xmlEMailAddress) {
                        SrEmailAddr = xmlEMailAddress.Value;
                    }
                    var xmlDisplayName = xmlUser.ElementAnyNs (Xml.Autodisco.DisplayName);
                    if (null != xmlDisplayName) {
                        SrDisplayName = xmlDisplayName.Value;
                    }
                }
                var xmlCulture = xmlResponse.ElementAnyNs (Xml.Autodisco.Culture);
                if (null != xmlCulture) {
                    SrCulture = xmlCulture.Value;
                }

                var xmlError = xmlResponse.ElementAnyNs (Xml.Autodisco.Error);
                if (null != xmlError) {
                    return ProcessXmlError (Sender, xmlError);
                }

                var xmlAction = xmlResponse.ElementAnyNs (Xml.Autodisco.Action);
                if (null != xmlAction) {
                    xmlError = xmlAction.ElementAnyNs (Xml.Autodisco.Error);
                    if (null != xmlError) {
                        return ProcessXmlError (Sender, xmlError);
                    }
                    bool circularRedirect = false;
                    Event redirectEvt = null;
                    var xmlRedirect = xmlAction.ElementAnyNs (Xml.Autodisco.Redirect);
                    if (null != xmlRedirect) {
                        // if we get a circular redirect, but we have settings, let's see if we can process the
                        // settings, ignoring the circular redirect.
                        redirectEvt = ProcessXmlRedirect (Sender, xmlRedirect, out circularRedirect);
                        if (!circularRedirect) {
                            return redirectEvt;
                        } else {
                            Log.Warn (Log.LOG_AS, "Circular Redirect detected. Checking Settings.");
                        }
                    }
                    Event settingsEvt = null;
                    bool settingsError = false;
                    var xmlSettings = xmlAction.ElementAnyNs (Xml.Autodisco.Settings);
                    if (null != xmlSettings) {
                        settingsEvt = ProcessXmlSettings (Sender, xmlSettings, out settingsError);
                        if (settingsError && null != redirectEvt && circularRedirect) {
                            // settings failed, but redirect failed first, so return that error instead.
                            Log.Warn (Log.LOG_AS, "Settings Failed. Going with Redirect response.");
                            return redirectEvt;
                        } else {
                            if (null != redirectEvt && circularRedirect) {
                                // settings worked, so continue with those, ignoring the circular redirect
                                Log.Warn (Log.LOG_AS, "Settings Succeeded. Ignoring Redirect response.");
                            }
                            return settingsEvt;
                        }
                    } else if (null != redirectEvt) {
                        // there were no settings, and we have a redirect event, so return that.
                        return redirectEvt;
                    }
                }
                // We should never get here. The XML response is missing both Error and Action.
                return Event.Create ((uint)SmEvt.E.HardFail, "SRPR1HARD");
            }

            void LogRedactedXml (string tag, XDocument doc)
            {
                NcXmlFilterSet filter = new NcXmlFilterSet ();
                filter.Add (new AutoDiscoverXmlFilter ());
                XDocument docOut = filter.Filter (doc, Cts.Token);
                Log.Info (Log.LOG_AS, "{0}:\n{1}", tag, docOut.ToString ());
            }

            public void PostProcessEvent (Event evt)
            {
            }

            private Event ProcessXmlError (AsHttpOperation Sender, XElement xmlError)
            {
                var xmlStatus = xmlError.ElementAnyNs (Xml.Autodisco.Status);
                var xmlAttrId = xmlError.Attribute (Xml.Autodisco.Error_Attr_Id);
                if (null != xmlAttrId) {
                    Log.Error (Log.LOG_AS, "ProcessXmlError: Id = {0}. Step = {1}.", xmlAttrId.Value, Step);
                }
                var xmlAttrTime = xmlError.Attribute (Xml.Autodisco.Error_Attr_Time);
                if (null != xmlAttrTime) {
                    Log.Error (Log.LOG_AS, "ProcessXmlError: Time = {0}. Step = {1}.", xmlAttrTime.Value, Step);
                }
                if (null != xmlStatus) {
                    // ProtocolError is the only valid value, but MSFT does not always obey! See
                    // http://blogs.msdn.com/b/exchangedev/archive/2011/07/08/autodiscover-for-exchange-activesync-developers.aspx
                    switch (uint.Parse (xmlStatus.Value)) {
                    case (uint)Xml.Autodisco.StatusCode.Success_1:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Status code {0}. Step = {1}.", Xml.Autodisco.StatusCode.Success_1, Step);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDStatus1));
                        break;
                    case (uint)Xml.Autodisco.StatusCode.ProtocolError_2:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Status code {0}. Step = {1}.", Xml.Autodisco.StatusCode.ProtocolError_2, Step);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDStatus2));
                        break;
                    default:
                        Log.Error (Log.LOG_AS, "Rx of unknown Auto-d Status code {0}. Step = {1}.", xmlStatus.Value, Step);
                        break;
                    }
                }
                var xmlMessage = xmlError.ElementAnyNs (Xml.Autodisco.Message);
                if (null != xmlMessage) {
                    Log.Error (Log.LOG_AS, "Rx of Message: {0}. Step = {1}.", xmlMessage.Value, Step);
                    var result = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
                    result.Message = xmlMessage.Value;
                    StatusInd (result);
                }
                var xmlDebugData = xmlError.ElementAnyNs (Xml.Autodisco.DebugData);
                if (null != xmlDebugData) {
                    Log.Error (Log.LOG_AS, "Rx of DebugData: {0}. Step = {1}.", xmlDebugData.Value, Step);
                    var result = NcResult.Error (NcResult.SubKindEnum.Error_AutoDAdminMessage);
                    result.Message = xmlMessage.Value;
                    StatusInd (result); 
                }
                var xmlErrorCode = xmlError.ElementAnyNs (Xml.Autodisco.ErrorCode);
                if (null != xmlErrorCode) {
                    switch (uint.Parse (xmlErrorCode.Value)) {
                    case (uint)Xml.Autodisco.ErrorCodeCode.InvalidRequest_600:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Error code {0}. Step = {1}.", Xml.Autodisco.ErrorCodeCode.InvalidRequest_600, Step);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDError600));
                        break;
                    case (uint)Xml.Autodisco.ErrorCodeCode.NoProviderForSchema_601:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Error code {0}. Step = {1}.", Xml.Autodisco.ErrorCodeCode.NoProviderForSchema_601, Step);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDError601));
                        break;
                    default:
                        Log.Error (Log.LOG_AS, "Rx of unknown Auto-d Error code {0}. Step = {1}.", xmlErrorCode.Value, Step);
                        break;
                    }
                }
                return Event.Create ((uint)SmEvt.E.HardFail, "SRPXEHARD");
            }

            private Event ProcessXmlRedirect (AsHttpOperation Sender, XElement xmlRedirect, out bool circularRedirect)
            {
                circularRedirect = false;
                // if email address changed, restart Auto discovery
                if (!SrEmailAddr.Equals (xmlRedirect.Value, StringComparison.Ordinal)) {
                    SrEmailAddr = xmlRedirect.Value;
                    SrDomain = DomainFromEmailAddr (SrEmailAddr);
                    return Event.Create ((uint)SharedEvt.E.ReStart, "SRPXRHARD");
                } else { // else fail hard
                    Log.Info (Log.LOG_AS, "Circular Redirect Action. Step = {0}",Step);
                    circularRedirect = true;
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRPXEHARD");
                }
            }

            private Event ProcessXmlSettings (AsHttpOperation Sender, XElement xmlSettings, out bool error)
            {
                bool haveServerSettings = false;
                error = false;
                var xmlServers = xmlSettings.ElementsAnyNs (Xml.Autodisco.Server);
                foreach (var xmlServer in xmlServers) {
                    var xmlType = xmlServer.ElementAnyNs (Xml.Autodisco.Type);
                    string serverType;
                    if (null != xmlType) {
                        serverType = xmlType.Value;
                    } else {
                        Log.Error (Log.LOG_AS, "ProcessXmlSettings: Type is missing.");
                        serverType = Xml.Autodisco.TypeCode.MobileSync;
                    }
                    var xmlUrl = xmlServer.ElementAnyNs (Xml.Autodisco.Url);
                    string xmlUrlValue = null;
                    if (null != xmlUrl) {
                        Uri serverUri;
                        try {
                            xmlUrlValue = xmlUrl.Value;
                            serverUri = new Uri (xmlUrlValue);
                        } catch (ArgumentNullException) {
                            Log.Error (Log.LOG_AS, "ProcessXmlSettings: illegal value {0}.", xmlUrl.ToString ());
                            error = true;
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRPXRHARD0");
                        } catch (UriFormatException) {
                            Log.Error (Log.LOG_AS, "ProcessXmlSettings: illegal value {0}.", xmlUrl.ToString ());
                            error = true;
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRPXRHARD1");
                        }
                        if (Xml.Autodisco.TypeCode.MobileSync == serverType) {
                            if (null == serverUri) {
                                Log.Error (Log.LOG_AS, "URI not extracted from: {0} in: {1}", xmlUrlValue, xmlSettings);
                            } else {
                                if (McServer.PathIsEWS (serverUri.ToString ())) {
                                    Log.Error (Log.LOG_AS, "ProcessXmlSettings: Url seems to be EWS: {0}.", serverUri.ToString ());
                                } else {
                                    SrServerUri = serverUri;
                                    haveServerSettings = true;
                                }
                            }
                        }
                        // TODO: add support for CertEnroll.
                    }
                    var xmlName = xmlServer.ElementAnyNs (Xml.Autodisco.Name);
                    if (null == xmlName) {
                        Log.Warn (Log.LOG_AS, "ProcessXmlSettings: missing Name: {0}", xmlServer.ToString ());
                        // We should have gotten our server info from Url.
                    }
                    var xmlServerData = xmlServer.ElementAnyNs (Xml.Autodisco.ServerData);
                    if (null != xmlServerData) {
                        Log.Error (Log.LOG_AS, "ProcessXmlSettings: (no CertEnroll) got ServerData {0}", xmlServerData.Value);
                    }
                }
                if (haveServerSettings) {
                    return Event.Create ((uint)SmEvt.E.Success, "SRPXRSUCCESS");
                } else {
                    error = true;
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRPXRHARD1");
                }
            }
            // *********************************************************************************
            // AsDnsOperationOwner callbacks.
            // *********************************************************************************
            public void CancelCleanup (AsDnsOperation Sender)
            {
                // Do nothing on cancellation.
            }

            public string DnsHost (AsDnsOperation Sender)
            {
                switch (Step) {
                case Steps.S4:
                    return "_autodiscover._tcp." + SrDomain;
                case Steps.S5:
                    return SrDomain;
                default:
                    NcAssert.CaseError (string.Format ("DnsHost with Step {0}", Step.ToString ()));
                    return "_autodiscover._tcp." + SrDomain;
                }
            }

            public NsType DnsType (AsDnsOperation Sender)
            {
                switch (Step) {
                case Steps.S4:
                    return NsType.SRV;
                case Steps.S5:
                    return NsType.MX;
                default:
                    NcAssert.CaseError (string.Format ("DnsType with Step {0}", Step.ToString ()));
                    return NsType.SRV;
                }
            }

            public NsClass DnsClass (AsDnsOperation Sender)
            {
                return NsClass.INET;
            }
            // Must be static to work properly.
            static Random picker = new Random ();

            public Event ProcessResponse (AsDnsOperation Sender, DnsQueryResponse response)
            {
                if (null == response) {
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRPR2NULL");
                }
                switch (Step) {
                case Steps.S4:
                    if (RCode.NoError == response.RCode &&
                        0 < response.AnswerRRs &&
                        NsType.SRV == response.NsType &&
                        AtLeastOne<SrvRecord> (response.Answers)) {
                        SrvRecord best = null;
                        var allBest = new List<SrvRecord> ();
                        foreach (var answer in response.Answers) {
                            var srv = answer as SrvRecord;
                            if (null != srv) {
                                if (null == best || BetterThan (srv, best)) {
                                    // The best record found so far.
                                    best = srv;
                                    allBest.Clear ();
                                    allBest.Add (srv);
                                } else if (SameValue (srv, best)) {
                                    // Equal to the best one so far.  Add it to the set.
                                    allBest.Add (srv);
                                } else {
                                    // Not the best record. Ignore it.
                                }
                            }
                        }
                        NcAssert.True (0 < allBest.Count, "No SRV records were found.");
                        // Pick one of the best records at random.
                        best = allBest [picker.Next (allBest.Count)];
                        SrDomain = best.HostName;
                        return Event.Create ((uint)SmEvt.E.Success, "SRPR2SUCCESS");
                    } else {
                        return Event.Create ((uint)SmEvt.E.HardFail, "SRPR2HARD");
                    }

                case Steps.S5:
                    if (RCode.NoError == response.RCode &&
                        0 < response.AnswerRRs &&
                        NsType.MX == response.NsType &&
                        AtLeastOne<MxRecord> (response.Answers)) {
                        var aBest = (MxRecord)response.Answers.Where (r0 => r0 is MxRecord).OrderBy (r1 => ((MxRecord)r1).Preference).First ();
                        if (aBest.MailExchange.EndsWith (McServer.GMail_MX_Suffix, StringComparison.OrdinalIgnoreCase) ||
                            aBest.MailExchange.EndsWith (McServer.GMail_MX_Suffix2, StringComparison.OrdinalIgnoreCase)) {
                            Command.ProtoControl.AutoDInfo = AutoDInfoEnum.MXFoundGoogle;
                            SrServerUri = McServer.BaseUriForHost (McServer.AS_GMail_Host);
                            return Event.Create ((uint)SmEvt.E.Success, "SRPRMXSUCCESS");
                        } else {
                            Command.ProtoControl.AutoDInfo = AutoDInfoEnum.MXFoundNonGoogle;
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRPRMXHARD1");
                        }

                    } else {
                        Command.ProtoControl.AutoDInfo = AutoDInfoEnum.MXNotFound;
                        return Event.Create ((uint)SmEvt.E.HardFail, "SRPRMXHARD2");
                    }

                default:
                    NcAssert.CaseError (string.Format ("ProcessResponse with Step {0}", Step.ToString ()));
                    return null;
                }
            }

            /// <summary>
            /// Does the IDnsRecord array have at least one element of the given type?
            /// </summary>
            private static bool AtLeastOne<T> (IDnsRecord[] answers) where T : IDnsRecord
            {
                foreach (var record in answers) {
                    if (record is T) {
                        return true;
                    }
                }
                return false;
            }

            /// <summary>
            /// Do the two SrvRecords have the same priority and weight?
            /// </summary>
            private static bool SameValue (SrvRecord a, SrvRecord b)
            {
                return a.Priority == b.Priority && a.Weight == b.Weight;
            }

            /// <summary>
            /// Does the first SrvRecord better than the second one?  "Better" is defined
            /// as a lower priority, or the same priority with a greater weight.
            /// </summary>
            private static bool BetterThan (SrvRecord a, SrvRecord b)
            {
                return a.Priority < b.Priority || (a.Priority == b.Priority && a.Weight > b.Weight);
            }
        }
    }
}
