//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;
using MailKit.Security;
using MailKit;

namespace NachoCore.SMTP
{
    public class SmtpValidateConfig : IBEContext
    {
        private IBEContext BEContext;
        private McServer ServerCandidate;
        private McCred CredCandidate;
        private SmtpCommand Cmd;
        NcSmtpClient Client;

        public SmtpValidateConfig (IBEContext bEContext)
        {
            BEContext = bEContext;
            Client = new NcSmtpClient ();
        }

        public void Cancel ()
        {
            if (null != Cmd) {
                Cmd.Cancel ();
                Cmd = null;
            }
        }

        public void Execute (McServer server, McCred cred)
        {
            ServerCandidate = server;
            CredCandidate = cred;
            Cmd = new SmtpDiscoveryCommand (this, Client);
            NcTask.Run (() => {
                ExecuteValidation ();
                Cmd = null;
            }, "SmtpValidateConfig");
        }
        public void ExecuteValidation()
        {
            try {
                Cmd.ExecuteConnectAndAuthEvent ();
                BEContext.ProtoControl.StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_ValidateConfigSucceeded));
            } catch (OperationCanceledException) {
                Log.Info (Log.LOG_SMTP, "OperationCanceledException");
            } catch (AuthenticationException) {
                Log.Info (Log.LOG_SMTP, "AuthenticationException");
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth));
            } catch (ServiceNotAuthenticatedException) {
                Log.Info (Log.LOG_SMTP, "ServiceNotAuthenticatedException");
                BEContext.ProtoControl.StatusInd (NcResult.Error (NcResult.SubKindEnum.Error_ValidateConfigFailedAuth));
            } catch (Exception ex) {
                Log.Error (Log.LOG_SMTP, "Exception : {0}", ex.Message);
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
