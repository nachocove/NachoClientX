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
            Log.Info (Log.LOG_IMAP, "{0}({1}): Started", this.GetType ().Name, AccountId);
            var errResult = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
            errResult.Message = "Unknown error"; // gets filled in by the various exceptions.
            Event evt;
            try {
                return TryLock (Client.SyncRoot, KLockTimeout, () => {
                    if (Client.IsConnected) {
                        Client.Disconnect (false, Cts.Token);
                    }
                    return base.ExecuteConnectAndAuthEvent ();
                });
            } catch (CommandLockTimeOutException ex) {
                Log.Error (Log.LOG_IMAP, "ImapDiscoverCommand: CommandLockTimeOutException: {0}", ex.Message);
                ResolveAllDeferred ();
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPDISCOLOKTIME");
                errResult.Message = ex.Message;
            } catch (OperationCanceledException ex) {
                ResolveAllDeferred ();
                evt = Event.Create ((uint)SmEvt.E.HardFail, "IMAPDISCOCANCEL"); // will be ignored by the caller
                errResult.Message = ex.Message;
            } catch (UriFormatException ex) {
                Log.Error (Log.LOG_IMAP, "ImapDiscoverCommand: UriFormatException: {0}", ex.Message);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.GetServConf, "IMAPCONNFAIL2", AutoDFailureReason.CannotFindServer);
                errResult.Message = ex.Message;
            } catch (SocketException ex) {
                Log.Error (Log.LOG_IMAP, "ImapDiscoverCommand: SocketException: {0}", ex.Message);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.GetServConf, "IMAPCONNFAIL", AutoDFailureReason.CannotFindServer);
                errResult.Message = ex.Message;
            } catch (AuthenticationException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: AuthenticationException {0}", ex.Message);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH1");
                errResult.Message = ex.Message;
            } catch (ServiceNotAuthenticatedException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: ServiceNotAuthenticatedException: {0}", ex.Message);
                evt =  Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTHFAIL2");
                errResult.Message = ex.Message;
            } catch (InvalidOperationException ex) {
                Log.Warn (Log.LOG_IMAP, "ImapDiscoverCommand: InvalidOperationException: {0}", ex.Message);
                evt =  Event.Create ((uint)SmEvt.E.TempFail, "IMAPINVOPTEMP");
                errResult.Message = ex.Message;
            } catch (ImapProtocolException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: ImapProtocolException {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPPROTOEXTEMP");
                errResult.Message = ex.Message;
            } catch (ImapCommandException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: ImapCommandException {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPCOMMEXTEMP");
                errResult.Message = ex.Message;
            } catch (IOException ex) {
                Log.Info (Log.LOG_IMAP, "ImapDiscoverCommand: IOException: {0}", ex.Message);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPIOTEMP");
                errResult.Message = ex.Message;
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "ImapDiscoverCommand: Exception : {0}", ex);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.GetServConf, "IMAPUNKFAIL");
                errResult.Message = ex.Message;
            } finally {
                Log.Info (Log.LOG_IMAP, "{0}({1}): Finished", this.GetType ().Name, AccountId);
            }
            StatusInd (errResult);
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
            switch (BEContext.Account.AccountType) {
            case McAccount.AccountTypeEnum.IMAP_SMTP:
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

            // Now that we know (perhaps) the service type, see if we need to do anything with the username
            switch (service) {
            case McAccount.AccountServiceEnum.iCloud:
                if (username.Contains ("@")) {
                    // https://support.apple.com/en-us/HT202304
                    var parts = username.Split ('@');
                    if (DomainIsOrEndsWith(parts [1].ToLowerInvariant (), "icloud.com")) {
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
                if (DomainIsOrEndsWith (domain, "icloud.com") ||
                    DomainIsOrEndsWith (domain, "me.com")) {
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
                if (DomainIsOrEndsWith (domain, "yahoo.com") ||
                    DomainIsOrEndsWith (domain, "yahoo.net") ||
                    DomainIsOrEndsWith (domain, "ymail.com") ||
                    DomainIsOrEndsWith (domain, "rocketmail.com")) {
                    return true;
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

