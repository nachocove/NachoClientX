//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using NachoCore.Utils;
using MailKit.Net.Imap;
using System;
using MailKit.Security;
using MailKit;
using System.IO;
using System.Net.Sockets;

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
            BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_AsAutoDComplete));
            return Event.Create ((uint)SmEvt.E.Success, "IMAPDISCOSUC");
        }
    }
}

