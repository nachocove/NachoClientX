//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit;
using MailKit.Security;

namespace NachoCore.IMAP
{
    public class ImapValidateConfig : IBEContext
    {
        IBEContext BEContext;
        McServer ServerCandidate;
        McCred CredCandidate;
        ImapCommand Cmd;
        NcStateMachine Sm;

        public ImapValidateConfig (IBEContext bEContext)
        {
            BEContext = bEContext;
            Sm = new NcStateMachine ("DummyValidation", new ImapStateMachineContext ());
        }

        public void Execute (McServer server, McCred cred)
        {
            ServerCandidate = server;
            CredCandidate = cred;
            Cmd = new ImapDiscoverCommand (this);
            NcTask.Run (() => {
                ExecuteValidation ();
                Cmd = null;
            }, "ImapValidateConfig");
        }

        public void Cancel ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
                Cmd = null;
            }
        }

        public void ExecuteValidation()
        {
            try {
                Cmd.ExecuteConnectAndAuthEvent (Sm);
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ValidateConfigSucceeded));
            } catch (OperationCanceledException) {
                Log.Info (Log.LOG_IMAP, "OperationCanceledException");
            } catch (AuthenticationException) {
                Log.Info (Log.LOG_IMAP, "AuthenticationException");
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth));
            } catch (ServiceNotAuthenticatedException) {
                Log.Info (Log.LOG_IMAP, "ServiceNotAuthenticatedException");
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth));
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "Exception : {0}", ex.Message);
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedComm));
            }
        }

        #region IBEContext implementation

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
            set { throw new Exception ("Illegal set of Server."); }
        }

        public McAccount Account {
            get { return BEContext.Account; }
        }

        public McCred Cred {
            get { return CredCandidate; }
        }

        #endregion
    }
}

