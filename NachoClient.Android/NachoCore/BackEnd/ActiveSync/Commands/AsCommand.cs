using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Wbxml;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public abstract class AsCommand : NcCommand, IAsHttpOperationOwner
    {
        // Constants.
        private const string ContentTypeWbxml = "application/vnd.ms-sync.wbxml";
        private const string ContentTypeWbxmlMultipart = "application/vnd.ms-sync.multipart";
        private const string ContentTypeMail = "message/rfc822";
        private const string KXsd = "xsd";
        private const string KCommon = "common";
        private const string KRequest = "request";
        private const string KResponse = "response";
        // Properties & IVars.
        public string CommandName;
        public XNamespace m_ns;
        protected XNamespace m_baseNs = Xml.AirSyncBase.Ns;
        protected NcStateMachine OwnerSm;
        protected AsHttpOperation Op;
        private bool Cancelled = false;
        private bool ProcessResponseOwnsPendingCleanup = false;

        public virtual double TimeoutInSeconds
        {
            get {
                return 0.0;
            }
        }

        public uint MaxTries { set; get; }

        public bool DontReportCommResult { set; get; }

        // Initializers.
        public AsCommand (string commandName, string nsName, IBEContext beContext) : this (commandName, beContext)
        {
            m_ns = nsName;
        }

        public AsCommand (string commandName, IBEContext beContext) : base (beContext)
        {
            CommandName = commandName;
        }
        // Virtual Methods.
        protected virtual void Execute (NcStateMachine sm, ref AsHttpOperation opRef)
        {
            Op = new AsHttpOperation (CommandName, this, BEContext) {
                DontReportCommResult = DontReportCommResult,
            };
            if (0 != MaxTries) {
                Op.MaxRetries = MaxTries - 1;
                Op.TriesLeft = MaxTries;
            }
            opRef = Op;
            Op.Execute (sm);
        }

        public override void Execute (NcStateMachine sm)
        {
            base.Execute (sm);
            // Op is a "dummy" here for DRY purposes.
            Execute (sm, ref Op);
        }
        // Cancel() must be safe to call even when the command has already completed.
        public override void Cancel ()
        {
            if (null != Op) {
                Op.Cancel ();
                // Don't null Op here - we might be calling Execute() on another thread. Let GC get it.
            }
            lock (LockObj) {
                Cancelled = true;
            }
            if (!ProcessResponseOwnsPendingCleanup) {
                lock (PendingResolveLockObj) {
                    ConsolidatePending ();
                    foreach (var pending in PendingList) {
                        if (McPending.StateEnum.Dispatched == pending.State) {
                            pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                        }
                    }
                    PendingList.Clear ();
                }
            }
        }

        public virtual bool UseWbxml (AsHttpOperation Sender)
        {
            return true;
        }

        /// <summary>
        /// Makes AsHttpOperation pretend like there is no body in the response.
        /// </summary>
        /// <returns><c>true</c>, if body should be ignored, <c>false</c> otherwise.</returns>
        /// <param name="Sender">Sender.</param>
        public virtual bool IgnoreBody (AsHttpOperation Sender)
        {
            return false;
        }

        public virtual bool DoSendPolicyKey (AsHttpOperation Sender)
        {
            return true;
        }
        // Override if the subclass wants to add more parameters to the query string.
        public virtual HttpMethod Method (AsHttpOperation Sender)
        {
            return HttpMethod.Post;
        }

        public virtual Dictionary<string,string> ExtraQueryStringParams (AsHttpOperation Sender)
        {
            return null;
        }

        // Override if the subclass wants total control over the query string.
        public virtual string QueryString (AsHttpOperation Sender)
        {
            return QueryString (Sender);
        }

        private string QueryString (AsHttpOperation Sender, bool isEmailRedacted=false)
        {
            var ident = Device.Instance.Identity ();
            string username;

            if (isEmailRedacted) {
                username = "REDACTED";
            } else {
                username = BEContext.Cred.Username;
            }
            return string.Format ("?Cmd={0}&User={1}&DeviceId={2}&DeviceType={3}",
                CommandName, 
                username,
                ident,
                Device.Instance.Type ());
        }

        public virtual Uri ServerUri (AsHttpOperation Sender, bool isEmailRedacted = false)
        {
            var requestLine = QueryString (Sender, isEmailRedacted);
            var rlParams = ExtraQueryStringParams (Sender);
            if (null != rlParams) {
                foreach (KeyValuePair<string,string> pair in rlParams) {
                    requestLine = requestLine + '&' + string.Join (
                        "&", string.Format ("{0}={1}", pair.Key, pair.Value));
                }
            }
            return new Uri (BEContext.Server.BaseUri (), requestLine);
        }
           
        public virtual void ServerUriChanged (Uri ServerUri, AsHttpOperation Sender)
        {
            var server = BEContext.Server;
            server.Scheme = ServerUri.Scheme;
            server.Host = ServerUri.Host;
            server.Port = ServerUri.Port;
            server.Path = ServerUri.AbsolutePath;
            // Updates the value in the DB.
            BEContext.Server = server;
        }
        // This exception is the way to say "don't bother to do this command" during ToXDocument().
        public class AbortCommandException : Exception
        {
        }

        protected virtual bool RequiresPending ()
        {
            return false;
        }

        // SafeToX methods return false only in the case where missing McPending prevent success.
        public bool SafeToXDocument (AsHttpOperation Sender, out XDocument doc)
        {
            lock (PendingResolveLockObj) {
                if (RequiresPending () && null == PendingSingle && 0 == PendingList.Count) {
                    doc = null;
                    return false;
                }
                doc = ToXDocument (Sender);
                return true;
            }
        }

        // The subclass should for any given instatiation only return non-null from ToXDocument XOR ToMime.
        protected virtual XDocument ToXDocument (AsHttpOperation Sender)
        {
            return null;
        }

        public virtual bool SafeToMime (AsHttpOperation Sender, out FileStream mime)
        {
            lock (PendingResolveLockObj) {
                if (RequiresPending () && null == PendingSingle && 0 == PendingList.Count) {
                    mime = null;
                    return false;
                }
                mime = ToMime (Sender);
                return true;
            }
        }

        protected virtual FileStream ToMime (AsHttpOperation Sender)
        {
            return null;
        }

        protected bool SiezePendingCleanup ()
        {
            lock (LockObj) {
                // If we haven't been cancelled yet, own the McPending cleanup.
                // If we have been cancelled, don't even process the response.
                if (Cancelled) {
                    return false;
                } else {
                    ProcessResponseOwnsPendingCleanup = true;
                    return true;
                }
            }
        }

        public virtual Event PreProcessResponse (AsHttpOperation Sender, NcHttpResponse response)
        {
            PendingNonResolveApply (pending => {
                pending.ResponseHttpStatusCode = (uint)response.StatusCode;
            });
            return null;
        }
        // Called for non-WBXML HTTP 200 responses.
        public virtual Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, CancellationToken cToken)
        {
            return new Event () { EventCode = (uint)SmEvt.E.Success };
        }

        public virtual Event ProcessResponse (AsHttpOperation Sender, NcHttpResponse response, XDocument doc, CancellationToken cToken)
        {
            return new Event () { EventCode = (uint)SmEvt.E.Success };
        }
        // In the cases where the subclass overrides don't clean up Pending(s) (or don't even get called)
        // we need to clean up pending.
        public virtual void PostProcessEvent (Event evt)
        {
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                if ((uint)SmEvt.E.HardFail == evt.EventCode) {
                    foreach (var pending in PendingList) {
                        Log.Error (Log.LOG_AS, "PostProcessEvent: AsCommand (HardFail) failed to resolve pending {0}.", pending.Operation.ToString ());
                        if (McPending.StateEnum.Dispatched == pending.State) {
                            pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.Unknown);
                        }
                    }
                } else {
                    foreach (var pending in PendingList) {
                        Log.Error (Log.LOG_AS, "PostProcessEvent: AsCommand failed to resolve pending {0}.", pending.Operation.ToString ());
                        if (McPending.StateEnum.Dispatched == pending.State) {
                            pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                        }
                    }
                }
                PendingList.Clear ();
            }
        }
        // Sub-class can override if there is a way to break-up the pending list (e.g. Sync).
        // If not returning false, this function must fix-up all pending before returning.
        public virtual bool WasAbleToRephrase ()
        {
            return false;
        }

        protected void PendingNonResolveApply (PendingAction action)
        {
            lock (PendingResolveLockObj) {
                if (null != PendingSingle) {
                    action (PendingSingle);
                }
                foreach (var pending in PendingList) {
                    action (pending);
                }
            }
        }

        protected Event CompleteAsHardFail (uint status, NcResult.WhyEnum why)
        {
            PendingResolveApply (pending => {
                pending.ResponseXmlStatusKind = McPending.XmlStatusKindEnum.TopLevel;
                pending.ResponsegXmlStatus = (uint)status;
                pending.ResolveAsHardFail (BEContext.ProtoControl, why);
            });
            var result = NcResult.Info (NcResult.SubKindEnum.Info_ServerStatus);
            result.Value = status;
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Account = BEContext.Account,
                Status = result,
            });
            return Event.Create ((uint)SmEvt.E.HardFail, 
                string.Format ("TLS{0}", ((uint)status).ToString ()), null, 
                string.Format ("{0}", (Xml.StatusCode)status));
        }

        protected Event CompleteAsUserBlocked (uint status, 
                                               McPending.BlockReasonEnum reason, NcResult.WhyEnum why)
        {
            PendingResolveApply (pending => {
                pending.ResponseXmlStatusKind = McPending.XmlStatusKindEnum.TopLevel;
                pending.ResponsegXmlStatus = (uint)status;
                pending.ResolveAsUserBlocked (BEContext.ProtoControl, reason, why);
            });
            var result = NcResult.Info (NcResult.SubKindEnum.Info_ServerStatus);
            result.Value = status;
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Account = BEContext.Account,
                Status = result,
            });
            return Event.Create ((uint)SmEvt.E.HardFail, 
                string.Format ("TLS{0}", ((uint)status).ToString ()), null, 
                string.Format ("{0}", (Xml.StatusCode)status));
        }

        protected Event CompleteAsTempFail (uint status)
        {
            PendingResolveApply (pending => {
                pending.ResponseXmlStatusKind = McPending.XmlStatusKindEnum.TopLevel;
                pending.ResponsegXmlStatus = (uint)status;
                pending.ResolveAsDeferredForce (BEContext.ProtoControl);
            });
            var result = NcResult.Info (NcResult.SubKindEnum.Info_ServerStatus);
            result.Value = status;
            NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () {
                Account = BEContext.Account,
                Status = result,
            });
            return Event.Create ((uint)SmEvt.E.TempFail,
                string.Format ("TLS{0}", ((uint)status).ToString ()), null, 
                string.Format ("{0}", (Xml.StatusCode)status));
        }
        // Subclass can override and add specialized support for top-level status codes as needed.
        // Subclass must call base if it does not handle the status code itself.
        // See http://msdn.microsoft.com/en-us/library/ee218647(v=exchg.80).aspx
        public virtual Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status, XDocument doc)
        {
            McProtocolState protocolState = null;
            switch ((Xml.StatusCode)status) {
            case Xml.StatusCode.InvalidContent_101:
            case Xml.StatusCode.InvalidWBXML_102:
            case Xml.StatusCode.InvalidXML_103:
            case Xml.StatusCode.InvalidDateTime_104:
            case Xml.StatusCode.InvalidCombinationOfIDs_105:
            case Xml.StatusCode.InvalidIDs_106:
            case Xml.StatusCode.InvalidMIME_107:
            case Xml.StatusCode.DeviceIdMissingOrInvalid_108:
            case Xml.StatusCode.DeviceTypeMissingOrInvalid_109:
                return CompleteAsHardFail (status, NcResult.WhyEnum.BadOrMalformed);

            case Xml.StatusCode.ServerError_110:
                if (WasAbleToRephrase ()) {
                    return CompleteAsTempFail (status);
                }
                return CompleteAsHardFail (status, NcResult.WhyEnum.ServerError);

            case Xml.StatusCode.ServerErrorRetryLater_111:
                return CompleteAsTempFail (status);

            case Xml.StatusCode.ActiveDirectoryAccessDenied_112:
                return CompleteAsHardFail (status, NcResult.WhyEnum.AccessDeniedOrBlocked);

            case Xml.StatusCode.MailboxQuotaExceeded_113:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.NoSpace);

            case Xml.StatusCode.MailboxServerOffline_114:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.AdminRemediation,
                    NcResult.WhyEnum.ServerOffline);

            case Xml.StatusCode.SendQuotaExceeded_115:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.AdminRemediation,
                    NcResult.WhyEnum.QuotaExceeded);

            case Xml.StatusCode.MessageRecipientUnresolved_116:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.UnresolvedRecipient);

            case Xml.StatusCode.MessageReplyNotAllowed_117:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ReplyNotAllowed);

            case Xml.StatusCode.MessagePreviouslySent_118:
                // If server says previously sent, then we succeeded!
                PendingResolveApply (pending => {
                    pending.ResolveAsSuccess (BEContext.ProtoControl,
                        NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSendSucceeded));
                });
                return Event.Create ((uint)SmEvt.E.HardFail, "TLS118");

            case Xml.StatusCode.MessageHasNoRecipient_119:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.NoRecipient);

            case Xml.StatusCode.MailSubmissionFailed_120:
            case Xml.StatusCode.MessageReplyFailed_121:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ServerError);

            case Xml.StatusCode.UserHasNoMailbox_123:
                return CompleteAsHardFail (status, NcResult.WhyEnum.MissingOnServer);

            case Xml.StatusCode.UserCannotBeAnonymous_124:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.UserPrincipalCouldNotBeFound_125:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.UserDisabledForSync_126:
                return CompleteAsHardFail (status, NcResult.WhyEnum.AccessDeniedOrBlocked);

            case Xml.StatusCode.UserOnNewMailboxCannotSync_127:
            case Xml.StatusCode.UserOnLegacyMailboxCannotSync_128:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ServerError);

            case Xml.StatusCode.DeviceIsBlockedForThisUser_129:
            case Xml.StatusCode.AccessDenied_130:
            case Xml.StatusCode.AccountDisabled_131:
                return CompleteAsHardFail (status, NcResult.WhyEnum.AccessDeniedOrBlocked);

            case Xml.StatusCode.SyncStateNotFound_132:
            case Xml.StatusCode.SyncStateLocked_133:
            case Xml.StatusCode.SyncStateCorrupt_134:
            case Xml.StatusCode.SyncStateAlreadyExists_135:
            case Xml.StatusCode.SyncStateVersionInvalid_136:
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                McFolder.UpdateSet_AsSyncMetaToClientExpected (AccountId, true);
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "TLS132-6");

            case Xml.StatusCode.CommandNotSupported_137:
            case Xml.StatusCode.VersionNotSupported_138:
            case Xml.StatusCode.DeviceNotFullyProvisionable_139:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.RemoteWipeRequested_140:
                protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.IsWipeRequired = true;
                    return true;
                });
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReProv, "TLS140");

            case Xml.StatusCode.LegacyDeviceOnStrictPolicy_141:
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.DeviceNotProvisioned_142:
            case Xml.StatusCode.PolicyRefresh_143:
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReProv, "TLS142-3");

            case Xml.StatusCode.InvalidPolicyKey_144:
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce (BEContext.ProtoControl);
                });
                protocolState = BEContext.ProtocolState;
                protocolState = protocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                    var target = (McProtocolState)record;
                    target.AsPolicyKey = McProtocolState.AsPolicyKey_Initial;
                    return true;
                });
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReProv, "TLS142-3");

            case Xml.StatusCode.ExternallyManagedDevicesNotAllowed_145:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.NoRecurrenceInCalendar_146:
            case Xml.StatusCode.UnexpectedItemClass_147:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.RemoteServerHasNoSSL_148:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.AdminRemediation,
                    NcResult.WhyEnum.ServerOffline);

            case Xml.StatusCode.InvalidStoredRequest_149:
                // We don't use the stored-request trick. If we did, we'd need to set a flag
                // to force re-generation, and defer, then retry.
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.ItemNotFound_150:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.BadOrMalformed);

            case Xml.StatusCode.TooManyFolders_151:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.BeyondRange);

            case Xml.StatusCode.NoFoldersFound_152:
            case Xml.StatusCode.ItemsLostAfterMove_153:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.AdminRemediation,
                    NcResult.WhyEnum.MissingOnServer);

            case Xml.StatusCode.FailureInMoveOperation_154:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ServerError);

            case Xml.StatusCode.MoveCommandDisallowedForNonPersistentMoveAction_155:
            case Xml.StatusCode.MoveCommandInvalidDestinationFolder_156:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.AvailabilityTooManyRecipients_160:
            case Xml.StatusCode.AvailabilityDLLimitReached_161:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.BadOrMalformed);

            case Xml.StatusCode.AvailabilityTransientFailure_162:
                return CompleteAsTempFail (status);

            case Xml.StatusCode.AvailabilityFailure_163:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ServerError);

            case Xml.StatusCode.BodyPartPreferenceTypeNotSupported_164:
            case Xml.StatusCode.DeviceInformationRequired_165:
            case Xml.StatusCode.InvalidAccountId_166:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.AccountSendDisabled_167:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.AdminRemediation,
                    NcResult.WhyEnum.AccessDeniedOrBlocked);

            case Xml.StatusCode.IRM_FeatureDisabled_168:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.AdminRemediation,
                    NcResult.WhyEnum.AccessDeniedOrBlocked);

            case Xml.StatusCode.IRM_TransientError_169:
                return CompleteAsTempFail (status);

            case Xml.StatusCode.IRM_PermanentError_170:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ServerError);

            case Xml.StatusCode.IRM_InvalidTemplateID_171:
            case Xml.StatusCode.IRM_OperationNotPermitted_172:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.NoPicture_173:
                return CompleteAsHardFail (status, NcResult.WhyEnum.MissingOnServer);

            case Xml.StatusCode.PictureTooLarge_174:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.TooBig);

            case Xml.StatusCode.PictureLimitReached_175:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.UserRemediation,
                    NcResult.WhyEnum.QuotaExceeded);

            case Xml.StatusCode.BodyPart_ConversationTooLarge_176:
                // TODO - The conversation is too large to compute the body parts. 
                // Try requesting the body of the item again, without body parts.
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.MaximumDevicesReached_177:
                return CompleteAsUserBlocked (status, McPending.BlockReasonEnum.AdminRemediation,
                    NcResult.WhyEnum.QuotaExceeded);

            // DO NOT add a default: here. We return null so that success codes and 
            // command-specific codes can be processed elsewhere.
            }
            return null;
        }

        protected virtual void DoSucceed ()
        {
            OwnerSm.PostEvent ((uint)SmEvt.E.Success, "ASCDSUCCESS");
        }

        protected virtual void DoHardFail ()
        {
            OwnerSm.PostEvent ((uint)SmEvt.E.HardFail, "ASCDHARD0");
        }

        // Note: we don't do McFolder.AsSetExpected() here, because this Action function
        // is intended for propagating a ReSync to another SM. The originator of the ReSync
        // must have called AsSetExpected().
        protected virtual void DoReSync ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReSync, "ASCDRESYNC");
        }

        protected virtual void DoReDisc ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.AsEvt.E.ReDisc, "ASCDREDISC");
        }

        protected virtual void DoUiGetCred ()
        {
            OwnerSm.PostEvent ((uint)AsProtoControl.AsEvt.E.AuthFail, "ASCDAUTH");
        }

        protected void DoNop ()
        {
        }
        // Static internal helper methods.
        static internal XDocument ToEmptyXDocument ()
        {
            var doc = new XDocument ();
            doc.Declaration = new XDeclaration ("1.0", "utf-8", "no");
            return doc;
        }
    }

    public class AsWaitCommand : AsCommand
    {
        NcCommand WaitCommand;
        public AsWaitCommand (IBEContext dataSource, int duration, bool earlyOnECChange) : base ("AsWaitCommand", Xml.AirSyncBase.Ns, dataSource)
        {
            WaitCommand = new NcWaitCommand (dataSource, duration, earlyOnECChange);
        }
        public override void Execute (NcStateMachine sm)
        {
            WaitCommand.Execute (sm);
        }
        public override void Cancel ()
        {
            WaitCommand.Cancel ();
        }
    }
}

