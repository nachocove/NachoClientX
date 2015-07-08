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
            var errResult = NcResult.Error (NcResult.SubKindEnum.Error_AutoDUserMessage);
            errResult.Message = "Unknown error"; // gets filled in by the various exceptions.
            try {
                
                Event evt = base.ExecuteConnectAndAuthEvent ();
                sm.PostEvent (evt);
                return;

            } catch (SocketException ex) {
                Log.Error (Log.LOG_IMAP, "SocketException: {0}", ex.Message);
                ResolveAllFailed (NcResult.WhyEnum.InvalidDest);
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.GetServConf, "IMAPCONNFAIL", AutoDFailureReason.CannotFindServer);
                errResult.Message = ex.Message;
            } catch (AuthenticationException ex) {
                Log.Info (Log.LOG_IMAP, "AuthenticationException");
                ResolveAllDeferred ();
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH1");
                errResult.Message = ex.Message;
            } catch (ImapCommandException ex) {
                Log.Info (Log.LOG_IMAP, "ImapCommandException {0}", ex.Message);
                ResolveAllDeferred ();
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.Wait, "IMAPCOMMWAIT", 60);
                errResult.Message = ex.Message;
            } catch (IOException ex) {
                Log.Info (Log.LOG_IMAP, "IOException: {0}", ex.ToString ());
                ResolveAllDeferred ();
                sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPIO");
                errResult.Message = ex.Message;
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "Exception : {0}", ex.ToString ());
                ResolveAllFailed (NcResult.WhyEnum.Unknown);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPHARD2");
                errResult.Message = ex.Message;
            }
            StatusInd (errResult);
        }

        protected override Event ExecuteCommand ()
        {
            guessServiceType (BEContext);

            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
            return Event.Create ((uint)SmEvt.E.Success, "IMAPDISCOSUC");
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
            char[] emailDelimiter = { '@' };

            McAccount.AccountServiceEnum service;
            string username = BEContext.Cred.Username;

            switch (BEContext.Account.AccountService) {
            case McAccount.AccountServiceEnum.IMAP_SMTP:
                if (isiCloud (username)) {
                    service = McAccount.AccountServiceEnum.iCloud;
                    if (username.Contains ("@")) {
                        // https://support.apple.com/en-us/HT202304
                        var parts = username.Split (emailDelimiter);
                        if (parts [1].ToLower ().Contains ("icloud.com")) {
                            username = parts [0];
                        }
                    }
                } else if (isYahoo (username)) {
                    service = McAccount.AccountServiceEnum.Yahoo;
                } else {
                    // we don't know (or don't care)
                    if (username.Contains ("@")) {
                        Log.Info (Log.LOG_IMAP, "Unknown generic IMAP server for domain {0}", username.Split (emailDelimiter) [1]);
                    }
                    service = BEContext.Account.AccountService;
                }
                break;

            default:
                service = BEContext.Account.AccountService;
                break;
            }

            NcModel.Instance.RunInTransaction (() => {
                if (BEContext.Cred.Username != username) {
                    BEContext.Cred.UpdateWithOCApply<McCred> ((record) => {
                        var target = (McCred)record;
                        target.Username = username;
                        return true;
                    });
                }
                if (BEContext.ProtocolState.ImapServiceType != service) {
                    BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.ImapServiceType = service;
                        return true;
                    });
                }
            });
        }

        private static bool isiCloud (string emailAddress)
        {
            if (string.IsNullOrEmpty (emailAddress)) {
                return false;
            }
            if (emailAddress.Contains ("@")) {
                var domain = emailAddress.Split ('@') [1].ToLower ();
                if (domain.Contains ("icloud.com") ||
                    domain.Contains ("me.com")) {
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
                var domain = emailAddress.Split ('@') [1].ToLower ();
                if (domain.Contains ("yahoo.com") ||
                    domain.Contains ("yahoo.net") ||
                    domain.Contains ("ymail.com") ||
                    domain.Contains ("rocketmail.com")) {
                    return true;
                }
            }
            return false;
        }


    }
}

