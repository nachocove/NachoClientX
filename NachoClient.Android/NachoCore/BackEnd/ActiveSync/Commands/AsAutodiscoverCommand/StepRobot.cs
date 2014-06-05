// # Copyright (C) 2013 Nacho Cove, Inc. All rights reserved.
//
using DnDns.Enums;
using DnDns.Query;
using DnDns.Records;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public partial class AsAutodiscoverCommand : AsCommand
    {
        private class StepRobot : IAsHttpOperationOwner, IAsDnsOperationOwner
        {
            public enum RobotLst : uint
            {
                PostWait = (St.Last + 1),
                GetWait,
                DnsWait,
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

            public Type DnsQueryRequestType { set; get; }

            public Type HttpClientType { set; get; }

            private ConcurrentBag<object> DisposedJunk;
            // Allocated at constructor, thereafter only accessed by Cancel.
            private CancellationTokenSource Cts;
            // Initialized at constructor, thereafter accessed anywhere.
            private CancellationToken Ct;

            public StepRobot (AsAutodiscoverCommand command, Steps step, string emailAddr, string domain)
            {
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
                case Steps.S4: // After DNS, will use HTTP/POST on resolved host.
                    MethodToUse = HttpMethod.Post;
                    break;
                case Steps.S3:
                    MethodToUse = HttpMethod.Get;
                    break;
                default:
                    throw new Exception ("Invalid step value.");
                }

                SrEmailAddr = emailAddr;
                SrDomain = domain;

                StepSm = new NcStateMachine ("AUTODSTEP") {
                    /* NOTE: There are three start states:
                     * PostWait - used for S1/S2,
                     * GetWait - used for S3,
                     * DnsWait - used for S4.
                     */
                    Name = "SR",
                    LocalEventType = typeof(RobotEvt),
                    LocalStateType = typeof(RobotLst),
                    TransTable = new [] {
                        new Node {State = (uint)RobotLst.PostWait, 
                            Invalid = new [] {(uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.NullCode
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.PostWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotSuccess,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.PostWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                    Act = DoRobotHardFail,
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
                                    Act = DoRobot302,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.GetWait,
                            Invalid = new [] {(uint)AsProtoControl.AsEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.NullCode
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.GetWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotHardFail, // Only 302 is okay w/a GET.
                                    State = (uint)St.Stop
                                }, 
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.GetWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                    Act = DoRobotHardFail,
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

                        new Node {State = (uint)RobotLst.DnsWait,
                            Invalid = new [] {(uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                                (uint)AsProtoControl.AsEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.ReDir, (uint)RobotEvt.E.NullCode
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotDns,
                                    State = (uint)RobotLst.DnsWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotDns2ReDir,
                                    State = (uint)RobotLst.CertWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotDns,
                                    State = (uint)RobotLst.DnsWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.CertWait,
                            Invalid = new [] {(uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync,
                                (uint)AsProtoControl.AsEvt.E.AuthFail, (uint)SharedEvt.E.ReStart, (uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.ReDir, (uint)RobotEvt.E.NullCode
                            },
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotGetServerCert,
                                    State = (uint)RobotLst.CertWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.Success,
                                    Act = DoRobotUiCertAsk,
                                    State = (uint)RobotLst.OkWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.TempFail,
                                    Act = DoRobotGetServerCert,
                                    State = (uint)RobotLst.CertWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.OkWait,
                            Invalid = new [] {(uint)SmEvt.E.Success, (uint)SmEvt.E.HardFail, (uint)SmEvt.E.TempFail,
                                (uint)AsProtoControl.AsEvt.E.ReDisc, (uint)AsProtoControl.AsEvt.E.ReProv, (uint)AsProtoControl.AsEvt.E.ReSync, 
                                (uint)AsProtoControl.AsEvt.E.AuthFail, (uint)SharedEvt.E.ReStart,
                                (uint)RobotEvt.E.ReDir, (uint)RobotEvt.E.NullCode
                            }, 
                            On = new[] {
                                new Trans {
                                    Event = (uint)SmEvt.E.Launch,
                                    Act = DoRobotUiCertAsk,
                                    State = (uint)RobotLst.OkWait
                                },
                                new Trans {
                                    Event = (uint)SharedEvt.E.SrvCertY,
                                    Act = DoRobot302,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans {
                                    Event = (uint)SharedEvt.E.SrvCertN,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },

                        new Node {State = (uint)RobotLst.ReDirWait,
                            Invalid = new [] {(uint)SharedEvt.E.SrvCertN, (uint)SharedEvt.E.SrvCertY,
                                (uint)RobotEvt.E.NullCode
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
                                    Act = DoRobotHttp,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans {
                                    Event = (uint)SmEvt.E.HardFail,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReDisc,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReProv,
                                    Act = DoRobotHardFail,
                                    State = (uint)St.Stop
                                },
                                new Trans {
                                    Event = (uint)AsProtoControl.AsEvt.E.ReSync,
                                    Act = DoRobotHardFail,
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
                                    Act = DoRobot302,
                                    State = (uint)RobotLst.ReDirWait
                                },
                                new Trans { Event = (uint)RobotEvt.E.Cancel, Act = DoCancel, State = (uint)St.Stop },
                            }
                        },
                    }
                };
                StepSm.Validate ();
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
                    StepSm.Start ((uint)RobotLst.DnsWait);
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
                RetriesLeft = 2;
            }

            private void ForTopLevel (Event Event)
            {
                // Once cancelled, we must post NO event to TL SM.
                if (!Ct.IsCancellationRequested) {
                    Command.Sm.PostEvent (Event);
                }
            }

            public void ServerCertificateEventHandler (IHttpWebRequest sender,
                                                       X509Certificate2 certificate,
                                                       X509Chain chain,
                                                       SslPolicyErrors sslPolicyErrors, 
                                                       EventArgs e)
            {
                if (sender.RequestUri.Equals (ReDirUri)) {
                    // Capture the server cert.
                    ServerCertificate = certificate;
                }
            }
            // *********************************************************************************
            // Step Robot state machine action commands.
            // *********************************************************************************
            private void DoRobotHttp ()
            {
                if (0 < RetriesLeft--) {
                    HttpOp = new AsHttpOperation (Command.CommandName, this, Command.BEContext) {
                        HttpClientType = HttpClientType,
                        Allow451Follow = false,
                        DontReportCommResult = true,
                        TriesLeft = 3,
                    };
                    HttpOp.Execute (StepSm);
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDRHHARD");
                }
            }

            private void DoRobotDns ()
            {
                if (0 < RetriesLeft--) {
                    DnsOp = new AsDnsOperation (this) {
                        DnsQueryRequestType = DnsQueryRequestType,
                    };
                    DnsOp.Execute (StepSm);
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDRDHARD");
                }
            }

            private void DoRobot302 ()
            {
                // NOTE: this handles the 302 case, NOT the <Redirect> in XML after 200 case.
                // FIXME: catch loops by recording been-there URLs.
                if (0 < Command.ReDirsLeft--) {
                    RefreshRetries ();
                    HttpOp = new AsHttpOperation (Command.CommandName, this, Command.BEContext) {
                        HttpClientType = HttpClientType,
                        Allow451Follow = false,
                        DontReportCommResult = true,
                        TriesLeft = 3,
                    };
                    HttpOp.Execute (StepSm);
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDR302HARD");
                }
            }

            private void DoRobotGet2ReDir ()
            {
                MethodToUse = HttpMethod.Post;
                RefreshRetries ();
                DoRobotGetServerCert ();
            }

            private void DoRobotDns2ReDir ()
            {
                // Make the successful SRV lookup seem like a 302 so that the code for
                // the remaining flow is unified for both paths (GET/DNS).
                IsReDir = true;
                ReDirUri = new Uri (string.Format ("https://{0}/autodiscover/autodiscover.xml", SrDomain));
                DoRobotGet2ReDir ();
            }

            private async void DoRobotGetServerCert ()
            {
                X509Certificate2 cached;
                if (ServerCertificatePeek.Instance.Cache.TryGetValue (ReDirUri.Host, out cached)) {
                    ServerCertificate = cached;
                    StepSm.PostEvent ((uint)SmEvt.E.Success, "SRDRGSCC");
                }
                // FIXME: need to set & handle timeout.
                if (0 < RetriesLeft--) {
                    ServerCertificate = null;
                    var client = (IHttpClient)Activator.CreateInstance (HttpClientType, 
                        new HttpClientHandler () { AllowAutoRedirect = false });
                    ServerCertificatePeek.Instance.ValidationEvent += ServerCertificateEventHandler;
                    try {
                        await client.GetAsync (ReDirUri).ConfigureAwait (false);
                    } catch {
                        StepSm.PostEvent ((uint)SmEvt.E.TempFail, "SRDRGSC0");
                        return;
                    }
                    ServerCertificatePeek.Instance.ValidationEvent -= ServerCertificateEventHandler;
                    if (null == ServerCertificate) {
                        StepSm.PostEvent ((uint)SmEvt.E.TempFail, "SRDRGSC1");
                        return;
                    }
                    StepSm.PostEvent ((uint)SmEvt.E.Success, "SRDRGSC2");
                    return;
                } else {
                    StepSm.PostEvent ((uint)SmEvt.E.HardFail, "SRDRGSC3");
                    return;
                }
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
                ForTopLevel (Event.Create ((uint)AsProtoControl.AsEvt.E.AuthFail, "SRAUTHFAIL", this));
            }

            private void DoRobotSuccess ()
            {
                ForTopLevel (Event.Create ((uint)SmEvt.E.Success, "SRSUCCESS", this));
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
            public Uri ServerUri (AsHttpOperation Sender)
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

            public virtual void ServerUriChanged (Uri ServerUri, AsHttpOperation Sender)
            {
                throw new Exception ("We should not be getting this (HTTP 451) while doing autodiscovery.");
            }

            public virtual StreamContent ToMime (AsHttpOperation Sender)
            {
                // We don't generate MIME.
                return null;
            }

            public Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status)
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

            public virtual void ResoveAllFailed (NcResult.WhyEnum why)
            {
                // Autodiscover does not touch pending.
            }

            public virtual void ResoveAllDeferred ()
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

            public bool IsContentLarge (AsHttpOperation Sender)
            {
                return false;
            }

            public bool DoSendPolicyKey (AsHttpOperation Sender)
            {
                return false;
            }

            public XDocument ToXDocument (AsHttpOperation Sender)
            {
                if (HttpMethod.Post != MethodToUse) {
                    return null;
                }
                var doc = AsCommand.ToEmptyXDocument ();
                doc.Add (new XElement (Ns + Xml.Autodisco.Autodiscover,
                    new XElement (Ns + Xml.Autodisco.Request,
                        new XElement (Ns + Xml.Autodisco.EMailAddress, SrEmailAddr),
                        new XElement (Ns + Xml.Autodisco.AcceptableResponseSchema, AsAutodiscoverCommand.ResponseSchema))));
                return doc;
            }

            public Event PreProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
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

            public Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
            {
                // We should never get back content that isn't XML.
                return Event.Create ((uint)SmEvt.E.HardFail, "SRPR0HARD");
            }

            public Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
            {
                var xmlResponse = doc.Root.ElementAnyNs (Xml.Autodisco.Response);
                var xmlUser = xmlResponse.ElementAnyNs (Xml.Autodisco.User);
                if (null != xmlUser) {
                    SrEmailAddr = xmlUser.ElementAnyNs (Xml.Autodisco.EMailAddress).Value;
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
                    var xmlRedirect = xmlAction.ElementAnyNs (Xml.Autodisco.Redirect);
                    if (null != xmlRedirect) {
                        return ProcessXmlRedirect (Sender, xmlRedirect);
                    }
                    var xmlSettings = xmlAction.ElementAnyNs (Xml.Autodisco.Settings);
                    if (null != xmlSettings) {
                        return ProcessXmlSettings (Sender, xmlSettings);
                    }
                }
                // We should never get here. The XML response is missing both Error and Action.
                return Event.Create ((uint)SmEvt.E.HardFail, "SRPR1HARD");
            }

            public void PostProcessEvent (Event evt)
            {
            }

            private Event ProcessXmlError (AsHttpOperation Sender, XElement xmlError)
            {
                var xmlStatus = xmlError.ElementAnyNs (Xml.Autodisco.Status);
                // FIXME: log Time and Id attributes if present:
                // Time="16:56:32.6164027" Id="1054084152".
                if (null != xmlStatus) {
                    // ProtocolError is the only valid value, but MSFT does not always obey! See
                    // http://blogs.msdn.com/b/exchangedev/archive/2011/07/08/autodiscover-for-exchange-activesync-developers.aspx
                    switch (uint.Parse (xmlStatus.Value)) {
                    case (uint)Xml.Autodisco.StatusCode.Success_1:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Status code {0}", Xml.Autodisco.StatusCode.Success_1);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDStatus1));
                        break;
                    case (uint)Xml.Autodisco.StatusCode.ProtocolError_2:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Status code {0}", Xml.Autodisco.StatusCode.ProtocolError_2);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDStatus2));
                        break;
                    default:
                        Log.Error (Log.LOG_AS, "Rx of unknown Auto-d Status code {0}", xmlStatus.Value);
                        break;
                    }
                    if ((uint)Xml.Autodisco.StatusCode.ProtocolError_2 != uint.Parse (xmlStatus.Value)) {
                        
                    }
                }
                var xmlMessage = xmlError.ElementAnyNs (Xml.Autodisco.Message);
                if (null != xmlMessage) {
                    ; // FIXME: pass this back to the user.
                }
                var xmlDebugData = xmlError.ElementAnyNs (Xml.Autodisco.DebugData);
                if (null != xmlDebugData) {
                    ; // FIXME: pass back to admin.
                }
                var xmlErrorCode = xmlError.ElementAnyNs (Xml.Autodisco.ErrorCode);
                if (null != xmlErrorCode) {
                    switch (uint.Parse (xmlErrorCode.Value)) {
                    case (uint)Xml.Autodisco.ErrorCodeCode.InvalidRequest_600:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Error code {0}", Xml.Autodisco.ErrorCodeCode.InvalidRequest_600);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDError600));
                        break;
                    case (uint)Xml.Autodisco.ErrorCodeCode.NoProviderForSchema_601:
                        Log.Error (Log.LOG_AS, "Rx of Auto-d Error code {0}", Xml.Autodisco.ErrorCodeCode.NoProviderForSchema_601);
                        StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_AutoDError601));
                        break;
                    default:
                        Log.Error (Log.LOG_AS, "Rx of unknown Auto-d Error code {0}", xmlErrorCode.Value);
                        break;
                    }
                }
                return Event.Create ((uint)SmEvt.E.HardFail, "SRPXEHARD");
            }

            private Event ProcessXmlRedirect (AsHttpOperation Sender, XElement xmlRedirect)
            {
                SrEmailAddr = xmlRedirect.Value;
                SrDomain = DomainFromEmailAddr (SrEmailAddr);
                return Event.Create ((uint)SharedEvt.E.ReStart, "SRPXRHARD");
            }

            private Event ProcessXmlSettings (AsHttpOperation Sender, XElement xmlSettings)
            {
                bool haveServerSettings = false;
                var xmlServers = xmlSettings.ElementsAnyNs (Xml.Autodisco.Server);
                foreach (var xmlServer in xmlServers) {
                    var xmlType = xmlServer.ElementAnyNs (Xml.Autodisco.Type);
                    string serverType;
                    if (null != xmlType) {
                        serverType = xmlType.Value;
                    } else {
                        // FIXME: log that Type is missing, assume MobileSync.
                        serverType = Xml.Autodisco.TypeCode.MobileSync;
                    }
                    var xmlUrl = xmlServer.ElementAnyNs (Xml.Autodisco.Url);
                    if (null != xmlUrl) {
                        Uri serverUri;
                        try {
                            serverUri = new Uri (xmlUrl.Value);
                        } catch (ArgumentNullException) {
                            // FIXME - log it.
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRPXRHARD0");
                        } catch (UriFormatException) {
                            // FIXME - log it.
                            return Event.Create ((uint)SmEvt.E.HardFail, "SRPXRHARD1");
                        }
                        if (Xml.Autodisco.TypeCode.MobileSync == serverType) {
                            SrServerUri = serverUri;
                            haveServerSettings = true;
                        }
                        // FIXME: add support for CertEnroll.
                    }
                    var xmlName = xmlServer.ElementAnyNs (Xml.Autodisco.Name);
                    if (null != xmlName) {
                        // FIXME - log it.
                        // We should have gotten our server info from Url.
                    }
                    var xmlServerData = xmlServer.ElementAnyNs (Xml.Autodisco.ServerData);
                    if (null != xmlServerData) {
                        // FIXME - add support for CertEnroll.
                    }
                }
                if (haveServerSettings) {
                    return Event.Create ((uint)SmEvt.E.Success, "SRPXRSUCCESS");
                } else {
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
                return "_autodiscover._tcp." + SrDomain;
            }

            public NsType DnsType (AsDnsOperation Sender)
            {
                return NsType.SRV;
            }

            public NsClass DnsClass (AsDnsOperation Sender)
            {
                return NsClass.INET;
            }
            // Must be static to work properly.
            static Random picker = new Random ();

            public Event ProcessResponse (AsDnsOperation Sender, DnsQueryResponse response)
            {
                if (RCode.NoError == response.RCode &&
                    0 < response.AnswerRRs &&
                    NsType.SRV == response.NsType) {
                    var aBest = (SrvRecord)response.Answers.OrderBy (r1 => ((SrvRecord)r1).Priority).ThenByDescending (r2 => ((SrvRecord)r2).Weight).First ();
                    var bestRecs = response.Answers.Where (r1 => aBest.Priority == ((SrvRecord)r1).Priority &&
                                   aBest.Weight == ((SrvRecord)r1).Weight).ToArray ();
                    var index = (1 == bestRecs.Length) ? 0 : picker.Next (bestRecs.Length - 1);
                    var chosen = (SrvRecord)bestRecs [index];
                    SrDomain = chosen.HostName;
                    return Event.Create ((uint)SmEvt.E.Success, "SRPR2SUCCESS");
                } else {
                    return Event.Create ((uint)SmEvt.E.HardFail, "SRPR2HARD");
                }
            }
        }
    }
}

