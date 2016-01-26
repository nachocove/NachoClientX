//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit.Net.Imap;
using System;
using MailKit.Security;
using MailKit;
using System.IO;
using System.Net.Sockets;
using NachoCore.Model;

namespace NachoCore.IMAP
{
    public class ImapDiscoverCommand : ImapCommand
    {
        public ImapDiscoverCommand (IBEContext beContext, NcImapClient imap) : base (beContext, imap)
        {
            RedactProtocolLogFunc = RedactProtocolLog;
            DontReportCommResult = BEContext.ProtocolState.ImapDiscoveryDone ? false : true;
        }

        public string RedactProtocolLog (bool isRequest, string logData)
        {
            // Redaction is done in the base class, since it's more complicated than just string replacement
            return logData;
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                Event evt = ExecuteCommandInternal ();
                if (!Cts.Token.IsCancellationRequested) {
                    sm.PostEvent (evt);
                }
            }, "ImapDiscoverCommand");
        }

        private Event ExecuteCommandInternal ()
        {
            bool Initial = !BEContext.ProtocolState.ImapDiscoveryDone;
            Tuple<ResolveAction, string> action = new Tuple<ResolveAction, string> (ResolveAction.None, null);

            Log.Info (Log.LOG_IMAP, "{0}({1}): Started", this.GetType ().Name, AccountId);
            Event evt;
            bool serverFailedGenerally = false;
            try {
                Cts.Token.ThrowIfCancellationRequested ();
                evt = TryLock (Client.SyncRoot, KLockTimeout, () => {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    return base.ExecuteConnectAndAuthEvent ();
                });
                Cts.Token.ThrowIfCancellationRequested ();
            } catch (CommandLockTimeOutException ex) {
                Log.Error (Log.LOG_IMAP, "ImapDiscoverCommand: CommandLockTimeOutException: {0}", ex.Message);
                action = new Tuple<ResolveAction, string> (ResolveAction.DeferAll, ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPDISCOLOKTIME");
                Client.DOA = true;
            } catch (OperationCanceledException ex) {
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPDISCOCANCEL"); // will be ignored by the caller
                action = new Tuple<ResolveAction, string> (ResolveAction.DeferAll, ex.Message);
            } catch (UriFormatException ex) {
                // this can't (shouldn't?) really happen except if Initial=true
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: UriFormatException: {0}", ex.Message);
                if (Initial) {
                    evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.GetServConf, "IMAPURICONF", BackEnd.AutoDFailureReasonEnum.CannotFindServer);
                    evt.Arg = BackEnd.AutoDFailureReasonEnum.CannotFindServer;
                } else {
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPURLTEMP1");
                }
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
            } catch (SocketException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: SocketException: {0}", ex.Message);
                if (Initial) {
                    evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.GetServConf, "IMAPCONNFAIL", BackEnd.AutoDFailureReasonEnum.CannotFindServer);
                    evt.Arg = BackEnd.AutoDFailureReasonEnum.CannotConnectToServer;
                } else {
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPCONNTEMP");
                }
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
                serverFailedGenerally = true;
            } catch (AuthenticationException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: AuthenticationException {0}", ex.Message);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH1");
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
            } catch (ServiceNotAuthenticatedException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: ServiceNotAuthenticatedException: {0}", ex.Message);
                evt =  Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTHFAIL2");
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
            } catch (InvalidOperationException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: InvalidOperationException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmEvt.E.TempFail, "IMAPINVOPTEMP");
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
            } catch (ImapProtocolException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: ImapProtocolException {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPPROTOEXTEMP");
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
                serverFailedGenerally = true;
            } catch (ImapCommandException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: ImapCommandException {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPCOMMEXTEMP");
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
            } catch (IOException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: IOException: {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPIOTEMP");
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
                serverFailedGenerally = true;
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "ImapDiscoverCommand: Exception : {0}", ex);
                if (Initial) {
                    evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.GetServConf, "IMAPUNKFAIL");
                    evt.Arg = BackEnd.AutoDFailureReasonEnum.CannotConnectToServer;
                } else {
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPUNKTEMP");
                }
                action = new Tuple<ResolveAction, string> (ResolveAction.FailAll, ex.Message);
                serverFailedGenerally = true;
            } finally {
                Log.Info (Log.LOG_IMAP, "{0}({1}): Finished", this.GetType ().Name, AccountId);
            }

            if (Cts.Token.IsCancellationRequested) {
                Log.Info (Log.LOG_IMAP, "{0}({1}): Cancelled", this.GetType ().Name, AccountId);
                return Event.Create ((uint)SmEvt.E.TempFail, "IMAPDISCOCANCEL1"); // will be ignored by the caller
            }
            ReportCommResult (BEContext.Server.Host, serverFailedGenerally);
            switch (action.Item1) {
            case ResolveAction.None:
                break;
            case ResolveAction.DeferAll:
                ResolveAllDeferred ();
                break;
            case ResolveAction.FailAll:
                if (Initial) {
                    var errResult = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
                    errResult.Message = action.Item2; // gets filled in by the various exceptions.
                    StatusInd (errResult);
                }
                break;
            }
            return evt;
        }

        protected override Event ExecuteCommand ()
        {
            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
            return Event.Create ((uint)SmEvt.E.Success, "IMAPDISCOVSUC");
        }

        /// <summary>
        /// Guess the type of the IMAP service. This is important only in a few cases, for example
        /// yahoo (which is horribly broken and I need to do things differently in a few places), and
        /// icloud (which does something funky on authentication).
        /// 
        /// Other cases may arise, at which point we need to identify service.
        /// </summary>
        public static void guessServiceType (IBEContext BEContext)
        {
            if (McAccount.AccountServiceEnum.None != BEContext.ProtocolState.ImapServiceType) {
                // we've already done this.
                return;
            }

            McAccount.AccountServiceEnum service;
            string username = BEContext.Cred.Username;

            // See if we can identify the service type
            switch (BEContext.Account.AccountService) {
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                if (isiCloud (username)) {
                    service = McAccount.AccountServiceEnum.iCloud;
                } else if (isYahoo (username)) {
                    service = McAccount.AccountServiceEnum.Yahoo;
                } else {
                    // we don't know (or don't care)
                    service = BEContext.Account.AccountService;
                }
                break;

            default:
                service = BEContext.Account.AccountService;
                break;
            }
            if (BEContext.ProtocolState.ImapServiceType != service) {
                BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.ImapServiceType = service;
                    return true;
                });
            }
        }

        public static void possiblyFixUsername (IBEContext BEContext)
        {
            string username = BEContext.Cred.Username;
            switch (BEContext.ProtocolState.ImapServiceType) {
            case McAccount.AccountServiceEnum.iCloud:
                if (username.Contains ("@")) {
                    // https://support.apple.com/en-us/HT202304
                    var parts = username.Split ('@');
                    var domain = parts [1].ToLowerInvariant ();
                    if (DomainIsOrEndsWith (domain, McServer.ICloud_Suffix) ||
                        DomainIsOrEndsWith (domain, McServer.ICloud_Suffix2) ||
                        DomainIsOrEndsWith (domain, McServer.ICloud_Suffix3)) {
                        username = parts [0];
                    }
                }
                break;

            default:
                break;

            }
            if (BEContext.Cred.Username != username) {
                BEContext.Cred.UpdateWithOCApply<McCred> ((record) => {
                    var target = (McCred)record;
                    target.Username = username;
                    return true;
                });
            }
        }

        private static bool isiCloud (string emailAddress)
        {
            if (string.IsNullOrEmpty (emailAddress)) {
                return false;
            }
            if (emailAddress.Contains ("@")) {
                var domain = emailAddress.Split ('@') [1].ToLowerInvariant ();
                if (DomainIsOrEndsWith (domain, McServer.ICloud_Suffix) ||
                    DomainIsOrEndsWith (domain, McServer.ICloud_Suffix2) ||
                    DomainIsOrEndsWith (domain, McServer.ICloud_Suffix3)) {
                    return true;
                }
            }
            return false;
        }

        private static bool isYahoo (string emailAddress)
        {
            if (string.IsNullOrEmpty (emailAddress)) {
                return false;
            }
            // For now check a known list of domains. Better would be to do a DNS lookup (which might
            // indicate that the DNS Server belongs to yahoo), but that would require a whole
            // new discovery statemachine. We'll see if this gets us far enough along.
            if (emailAddress.Contains ("@")) {
                var domain = emailAddress.Split ('@') [1].ToLowerInvariant ();
                foreach (var suffix in McServer.Yahoo_Suffixes) {
                    if (DomainIsOrEndsWith (domain, suffix)) {
                        return true;
                    }
                }
            }
            return false;
        }

        // The intended use of this function is for the caller to do ToLower( on both domain and mightBe.
        private static bool DomainIsOrEndsWith (string domain, string mightBe)
        {
            if (string.IsNullOrEmpty (domain) || string.IsNullOrEmpty (mightBe)) {
                return false;
            }
            return domain == mightBe || domain.EndsWith ("." + mightBe);
        }
    }
}

