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
using NachoCore.ActiveSync;
using NachoCore.Model;
using NachoCore.Wbxml;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
    public abstract class AsCommand : IAsCommand, IAsHttpOperationOwner
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
        protected IBEContext BEContext;
        protected AsHttpOperation Op;
        // PendingSingle is for commands that process 1-at-a-time. Pending list is for N-at-a-time commands.
        // Both get loaded-up in the class initalizer. During loading, each gets marked as dispatched.
        // The sublass is responsible for re-writing each from dispatched to something else.
        // This base class has a "diaper" to catch any dispached left behind by the subclass. This base class
        // is responsible for clearing PendingSingle/PendingList. 
        // Because of threading, the PendingResolveLockObj must be locked before resolving.
        // Any resolved pending objects must be removed from PendingSingle/PendingList before unlock.
        protected McPending PendingSingle;
        protected List<McPending> PendingList;
        protected object PendingResolveLockObj;
        protected NcResult SuccessInd;
        protected NcResult FailureInd;
        protected Object LockObj = new Object ();

        public TimeSpan Timeout { set; get; }

        public bool DontReportCommResult { set; get; }

        public Type DnsQueryRequestType { set; get; }

        public Type HttpClientType { set; get; }
        // Initializers.
        public AsCommand (string commandName, string nsName, IBEContext beContext) : this (commandName, beContext)
        {
            m_ns = nsName;
        }

        public AsCommand (string commandName, IBEContext beContext)
        {
            DnsQueryRequestType = typeof(MockableDnsQueryRequest);
            HttpClientType = typeof(MockableHttpClient);
            Timeout = TimeSpan.Zero;
            CommandName = commandName;
            BEContext = beContext;
            PendingList = new List<McPending> ();
            PendingResolveLockObj = new object ();
        }
        // Virtual Methods.
        protected virtual void Execute (NcStateMachine sm, ref AsHttpOperation opRef)
        {
            Op = new AsHttpOperation (CommandName, this, BEContext) {
                HttpClientType = HttpClientType,
                DontReportCommResult = DontReportCommResult,
            };
            if (TimeSpan.Zero != Timeout) {
                Op.Timeout = Timeout;
            }
            opRef = Op;
            Op.Execute (sm);
        }

        public virtual void Execute (NcStateMachine sm)
        {
            // Op is a "dummy" here for DRY purposes.
            Execute (sm, ref Op);
        }
        // Cancel() must be safe to call even when the command has already completed.
        public virtual void Cancel ()
        {
            if (null != Op) {
                Op.Cancel ();
                // Don't null Op here - we might be calling Execute() on another thread. Let GC get it.
            }
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                foreach (var pending in PendingList) {
                /* Q: Do we need another state? We need to be smart about the case where
                 * the cancel comes after the op has been run against the server. The op
                 * may fail the 2nd time because the item exists. Don't want to bug the user.
                 */
                    if (McPending.StateEnum.Dispatched == pending.State) {
                        pending.ResolveAsDeferredForce ();
                    }
                }
                PendingList.Clear ();
            }
        }

        public virtual bool UseWbxml (AsHttpOperation Sender)
        {
            return true;
        }

        public virtual bool IsContentLarge (AsHttpOperation Sender)
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
            var ident = Device.Instance.Identity ();
            return string.Format ("?Cmd={0}&User={1}&DeviceId={2}&DeviceType={3}",
                CommandName, 
                BEContext.Cred.Username,
                ident,
                Device.Instance.Type ());
        }

        public virtual Uri ServerUri (AsHttpOperation Sender)
        {
            var requestLine = QueryString (Sender);
            var rlParams = ExtraQueryStringParams (Sender);
            if (null != rlParams) {
                foreach (KeyValuePair<string,string> pair in rlParams) {
                    requestLine = requestLine + '&' + string.Join (
                        "&", string.Format ("{0}={1}", pair.Key, pair.Value));
                }
            }
            return new Uri (AsCommand.BaseUri (BEContext.Server), requestLine);
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
        // The subclass should for any given instatiation only return non-null from ToXDocument XOR ToMime.
        public virtual XDocument ToXDocument (AsHttpOperation Sender)
        {
            return null;
        }

        public virtual StreamContent ToMime (AsHttpOperation Sender)
        {
            return null;
        }

        public virtual void StatusInd (NcResult result)
        {
            BEContext.Owner.StatusInd (BEContext.ProtoControl, result);
        }

        public virtual void StatusInd (bool didSucceed)
        {
            if (didSucceed) {
                if (null != SuccessInd) {
                    BEContext.Owner.StatusInd (BEContext.ProtoControl, SuccessInd);
                }
            } else {
                if (null != FailureInd) {
                    BEContext.Owner.StatusInd (BEContext.ProtoControl, FailureInd);
                }
            }
        }

        public virtual Event PreProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            PendingNonResolveApply (pending => {
                pending.ResponseHttpStatusCode = (uint)response.StatusCode;
            });
            return null;
        }
        // Called for non-WBXML HTTP 200 responses.
        public virtual Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response)
        {
            return new Event () { EventCode = (uint)SmEvt.E.Success };
        }

        public virtual Event ProcessResponse (AsHttpOperation Sender, HttpResponseMessage response, XDocument doc)
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
                        if (McPending.StateEnum.Dispatched == pending.State) {
                            pending.ResolveAsHardFail (BEContext.ProtoControl, NcResult.WhyEnum.Unknown);
                        }
                    }
                } else {
                    foreach (var pending in PendingList) {
                        if (McPending.StateEnum.Dispatched == pending.State) {
                            pending.ResolveAsDeferredForce ();
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

        public virtual void ResolveAllFailed (NcResult.WhyEnum why)
        {
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                foreach (var pending in PendingList) {
                    pending.ResolveAsHardFail (BEContext.ProtoControl, why);
                }
                PendingList.Clear ();
            }
        }

        public virtual void ResolveAllDeferred ()
        {
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                foreach (var pending in PendingList) {
                    pending.ResolveAsDeferredForce ();
                }
                PendingList.Clear ();
            }
        }

        protected void ConsolidatePending ()
        {
            if (null != PendingSingle) {
                PendingList.Add (PendingSingle);
                PendingSingle = null;
            }
        }

        protected delegate void PendingAction (McPending pending);

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

        protected void PendingResolveApply (PendingAction action)
        {
            lock (PendingResolveLockObj) {
                ConsolidatePending ();
                foreach (var pending in PendingList) {
                    action (pending);
                }
                PendingList.Clear ();
            }
        }

        protected Event CompleteAsHardFail (uint status, NcResult.WhyEnum why)
        {
            PendingResolveApply (pending => {
                pending.ResponseXmlStatusKind = McPending.XmlStatusKindEnum.TopLevel;
                pending.ResponsegXmlStatus = (uint)status;
                pending.ResolveAsHardFail (BEContext.ProtoControl, why);
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
            return Event.Create ((uint)SmEvt.E.HardFail, 
                string.Format ("TLS{0}", ((uint)status).ToString ()), null, 
                string.Format ("{0}", (Xml.StatusCode)status));
        }

        protected Event CompleteAsTempFail (uint status)
        {
            PendingResolveApply (pending => {
                pending.ResponseXmlStatusKind = McPending.XmlStatusKindEnum.TopLevel;
                pending.ResponsegXmlStatus = (uint)status;
                pending.ResolveAsDeferredForce ();
            });
            return Event.Create ((uint)SmEvt.E.TempFail,
                string.Format ("TLS{0}", ((uint)status).ToString ()), null, 
                string.Format ("{0}", (Xml.StatusCode)status));
        }
        // Subclass can override and add specialized support for top-level status codes as needed.
        // Subclass must call base if it does not handle the status code itself.
        // See http://msdn.microsoft.com/en-us/library/ee218647(v=exchg.80).aspx
        public virtual Event ProcessTopLevelStatus (AsHttpOperation Sender, uint status)
        {
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
                    pending.ResolveAsDeferredForce ();
                });
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReSync, "TLS132-6");

            case Xml.StatusCode.CommandNotSupported_137:
            case Xml.StatusCode.VersionNotSupported_138:
            case Xml.StatusCode.DeviceNotFullyProvisionable_139:
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.RemoteWipeRequested_140:
                var protocolState = BEContext.ProtocolState;
                protocolState.IsWipeRequired = true;
                protocolState.Update ();
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce ();
                });
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReProv, "TLS140");

            case Xml.StatusCode.LegacyDeviceOnStrictPolicy_141:
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce ();
                });
                return CompleteAsHardFail (status, NcResult.WhyEnum.ProtocolError);

            case Xml.StatusCode.DeviceNotProvisioned_142:
            case Xml.StatusCode.PolicyRefresh_143:
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce ();
                });
                return Event.Create ((uint)AsProtoControl.AsEvt.E.ReProv, "TLS142-3");

            case Xml.StatusCode.InvalidPolicyKey_144:
                PendingResolveApply (pending => {
                    pending.ResolveAsDeferredForce ();
                });
                BEContext.ProtocolState.AsPolicyKey = McProtocolState.AsPolicyKey_Initial;
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
                // FIXME - The conversation is too large to compute the body parts. 
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

        static internal Uri BaseUri (McServer server)
        {
            var retval = string.Format ("{0}://{1}:{2}{3}",
                             server.Scheme, server.Host, server.Port, server.Path);
            return new Uri (retval);
        }
    }
}

