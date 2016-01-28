using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using SQLite;
using NachoCore;
using NachoCore.ActiveSync;
using NachoCore.IMAP;
using NachoCore.SMTP;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

/* Back-End:
 * The BE manages all protocol interaction with all servers (will be
 * extended beyond EAS). The BE communicates with the UI through APIs
 * and through the DB:
 * - The UI can call a BE API,
 * - The UI can modify the DB, and the BE detects it,
 * - The BE can modify the DB, and the UI detects it,
 * - The BE can invoke a callback API () on the UI.
 * There is only one BE object in the app. The BE is responsible for the
 * setup of the DB, and the UI gets access to the DB though the BE's
 * Db property.
 * 
 * The UI Must have started all accounts before modding the DB records associated
 * with those accounts - otherwise mod events will get dropped and not end up on the server.
 * */
using System.Threading;


namespace NachoCore
{
    // This should be treated as a sealed class, although the sealed keyword is not here.
    // We need to be able to inherit and override some functions for testing.
    public class BackEnd : IBackEnd, INcProtoControlOwner
    {
        private static volatile BackEnd instance;
        private static object syncRoot = new Object ();

        public static BackEnd Instance {
            get {
                if (instance == null) {
                    lock (syncRoot) {
                        if (instance == null) {
                            instance = new BackEnd ();
                        }
                    }
                }
                return instance; 
            }
        }

        public enum DbActors
        {
            Ui,
            Proto,
        };

        public enum DbEvents
        {
            DidWrite,
            WillDelete,
        };

        public enum AutoDFailureReasonEnum : uint
        {
            Unknown,
            CannotConnectToServer,
            CannotFindServer,
        }

        #region ConcurrentQueue<NcProtoControl>

        private ConcurrentDictionary<int, ConcurrentQueue<NcProtoControl>> Services = new ConcurrentDictionary<int, ConcurrentQueue<NcProtoControl>> ();

        private bool AccountHasServices (int accountId)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            return Services.ContainsKey (accountId);
        }

        public NcProtoControl GetService (int accountId, McAccount.AccountCapabilityEnum capability)
        {
            var services = GetServices (accountId);
            return services != null ? services.FirstOrDefault (x => capability == (x.Capabilities & capability)) : null;
        }

        public ConcurrentQueue<NcProtoControl> GetServices (int accountId)
        {
            NcAssert.True (0 != accountId, "0 != accountId");
            ConcurrentQueue<NcProtoControl> services;
            if (!Services.TryGetValue (accountId, out services)) {
                return null;
            }
            return services;
        }

        public ConcurrentQueue<NcProtoControl> GetOrCreateServices (int accountId)
        {
            var services = GetServices (accountId);
            if (null == services) {
                CreateServices (accountId);
                services = GetServices (accountId);
                if (services == null) {
                    Log.Warn (Log.LOG_BACKEND, "StartBackendIfNotStarted ({1}) could not find services.", accountId);
                    return null;
                }
            }
            return services;
        }

        public void AddServices (int accountId, ConcurrentQueue<NcProtoControl> services)
        {
            if (!Services.TryAdd (accountId, services)) {
                // Concurrency. Another thread has jumped in and done the add.
                Log.Info (Log.LOG_BACKEND, "Another thread has already called CreateServices for Account.Id {0}", accountId);
            }
        }

        public void DeleteServices (int accountId)
        {
            ConcurrentQueue<NcProtoControl> dummy;
            if (!Services.TryRemove (accountId, out dummy)) {
                Log.Info (Log.LOG_BACKEND, "Another thread has already called DeleteServices for Account.Id {0}", accountId);
            }
        }

        #endregion

        private NcTimer PendingOnTimeTimer = null;

        /// <summary>
        /// Gets or sets the refresh cancel source. 
        /// </summary>
        /// <value>The refresh cancel source.</value>
        /// 
        private CancellationTokenSource _Oauth2RefreshCancelSource;
        // For test use only.
        protected CancellationTokenSource Oauth2RefreshCancelSource { 
            set {
                lock (_Oauth2RefreshCancelSourceLock) {
                    _Oauth2RefreshCancelSource = value;
                }
            }
        }

        object _Oauth2RefreshCancelSourceLock = new object ();

        public enum CredReqActiveState
        {
            /// <summary>
            /// We are attempting a token refresh.
            /// </summary>
            CredReqActive_AwaitingRefresh = 0,
            /// <summary>
            /// We're not waiting for anything, The UI should be told.
            /// </summary>
            CredReqActive_NeedUI,
        }

        protected class CredReqActiveStatus
        {
            /// <summary>
            /// Current state of the CredReq.
            /// </summary>
            /// <value>The state.</value>
            public CredReqActiveState State { get; set; }

            /// <summary>
            /// Set to true if the Controller called CredReq. This is a
            /// reminder that we need to call CredResp when we have a new token.
            /// </summary>
            /// <value><c>true</c> if need cred resp; otherwise, <c>false</c>.</value>
            public bool NeedCredResp { get; set; }

            /// <summary>
            /// Number of retries for the token.
            /// </summary>
            /// <value>The refresh retries.</value>
            public uint RefreshRetries { get; set; }

            public CredReqActiveStatus (CredReqActiveState state, bool needCredResp)
            {
                State = state;
                NeedCredResp = needCredResp;
                RefreshRetries = 0;
            }
        }

        /// <summary>
        /// Dictionary to keep track of currently being processed Cred-Requests.
        /// If the account ID is an existing key in the dictionary, we know we 
        /// are processing a cred request. The Dictionary value tells us
        /// details about the CredReq.
        /// 
        /// An entry for an account must exist until a CredResp has been initiated. In the 
        /// case of an OAuth2 refresh, where there might not have been a CredReq, and thus
        /// no CredResp is called, the OAuth2 refresh callback must clean up the entry.
        /// 
        /// Since this is an in-memory dictionary, it will have no entries on reboot of the app.
        /// This is OK, since when the app starts, either the Oauth2RefreshTimer will catch
        /// expired tokens, or the controller will (when it tries to use it), which will cause a new
        /// entry to be created. At worst, we retry a few more times. If this causes undue delay
        /// for users, we should adjust KOauth2RefreshMaxFailure downwards.
        /// </summary>
        protected Dictionary<int, CredReqActiveStatus> CredReqActive = new Dictionary<int, CredReqActiveStatus> ();

        public NcPreFetchHints BodyFetchHints { get; set; }

        private IBackEndOwner Owner { set; get; }

        ConcurrentQueue<NcProtoControl> GetServicesAndStartBackendIfNotStarted (int accountId)
        {
            var services = GetOrCreateServices (accountId);
            if (null == services) {
                return null; // error was logged in GetOrCreateServices()
            }

            // see if there's any not-started services. If so, assume the backend is not up, so start it
            if (services.Any (x => !x.IsStarted)) {
                NcTask.Run (() => Start (accountId), "GetServicesAndStartBackendIfNotStarted");
            }
            return services;
        }

        /// <summary>
        /// Applies a func to a service matching the given capabilities
        /// </summary>
        /// <returns>An NcResult.Ok if everything went well, NcResult.Error otherwise.</returns>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="capability">Capability.</param>
        /// <param name="func">Func.</param>
        private NcResult ApplyToService (int accountId, McAccount.AccountCapabilityEnum capability, Func<NcProtoControl, NcResult> func)
        {
            if (GetServicesAndStartBackendIfNotStarted (accountId) == null) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NoCapableService);
            }

            var protoControl = GetService (accountId, capability);
            if (null == protoControl) {
                Log.Error (Log.LOG_BACKEND, "ApplyToService: can't find controller with desired capability {0}", capability);
                return NcResult.Error (NcResult.SubKindEnum.Error_NoCapableService);
            }
            return func (protoControl);
        }

        /// <summary>
        /// Applies a func across all services assciated with this account.
        /// If services are not created, create them.
        /// If services are not started, start them.
        /// </summary>
        /// <returns>An NcResult.Ok if everything went well, NcResult.Error otherwise.</returns>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="capabilities">Capabilities</param>
        /// <param name="name">Name.</param>
        /// <param name="func">Func.</param>
        /// <param name="startIfNotStarted">start services if they aren't started yet.</param>
        private NcResult ApplyAcrossServices (int accountId, McAccount.AccountCapabilityEnum capabilities, string name, Func<NcProtoControl, NcResult> func, bool startIfNotStarted)
        {
            var result = NcResult.OK ();
            NcResult iterResult = null;

            ConcurrentQueue<NcProtoControl> services;
            if (startIfNotStarted) {
                services = GetServicesAndStartBackendIfNotStarted (accountId);
                if (services == null) {
                    Log.Warn (Log.LOG_BACKEND, "BackEnd.ApplyAcrossServices {0}({1}) could not find services.", name, accountId);
                    return NcResult.Error ("Could not create services");
                }
            } else {
                services = GetOrCreateServices (accountId);
                if (null == services) {
                    return NcResult.Error (NcResult.SubKindEnum.Error_NoCapableService, NcResult.WhyEnum.Unsupported);
                }
            }

            List<NcProtoControl> matchingServices = services.Where (x => (0 != (capabilities & x.Capabilities))).ToList ();
            foreach (var service in matchingServices) {
                iterResult = func (service);
                if (iterResult.isError ()) {
                    result = iterResult;
                }
            }
            if (result.isOK ()) {
                Log.Info (Log.LOG_BACKEND, "{0}({1})", name, accountId);
            } else {
                Log.Warn (Log.LOG_BACKEND, "BackEnd.ApplyAcrossServices {0}({1}):{2}.", name, accountId, result.Message);
            }
            return result;
        }

        private void ApplyAcrossAccounts (string name, Action<int> func)
        {
            var accounts = NcModel.Instance.Db.Table<McAccount> ();
            foreach (var account in accounts) {
                func (account.Id);
            }
        }

        // For IBackEnd.
        protected BackEnd ()
        {
            // Adjust system settings.
            ServicePointManager.DefaultConnectionLimit = 25;
            BodyFetchHints = new NcPreFetchHints ();
        }

        public void Enable (IBackEndOwner owner)
        {
            Owner = owner;
        }

        public void Start ()
        {
            Log.Info (Log.LOG_BACKEND, "BackEnd.Start() called");
            NcCommStatus.Instance.CommStatusNetEvent += Oauth2NetStatusEventHandler;
            // The callee does Task.Run.
            ApplyAcrossAccounts ("Start", (accountId) => Start (accountId));
        }

        // DON'T PUT Stop in a Task.Run. We want to execute as much as possible immediately.
        // Under iOS, there is a deadline. The ProtoControl's ForceStop must stop everything and
        // return without waiting.
        public void Stop ()
        {
            Log.Info (Log.LOG_BACKEND, "BackEnd.Stop() called");
            // Cancel the refresh tokens, killing all currently active OAuth2 refresh attempts.
            if (null != _Oauth2RefreshCancelSource) {
                _Oauth2RefreshCancelSource.Cancel ();
            }
            // stop the OAuth2 refresh timer.
            // TODO: This is the only place we stop the timer. If we have the timer
            // running, and stop/delete/remove all accounts with Oauth2 creds, the timer
            // will keep running uselessly. This should OK, since it will not be started
            // again, if all oauth2 accounts are disabled/deleted, after it has been stopped.
            StopOauthRefreshTimer ();
            NcCommStatus.Instance.CommStatusNetEvent -= Oauth2NetStatusEventHandler;
            if (null != PendingOnTimeTimer) {
                PendingOnTimeTimer.Dispose ();
                PendingOnTimeTimer = null;
            }
            ApplyAcrossAccounts ("Stop", (accountId) => Stop (accountId));
            BodyFetchHints.Reset ();
        }

        public void Stop (int accountId)
        {
            if (!AccountHasServices (accountId)) {
                CreateServices (accountId);
            }

            // Don't use ApplyAcrossServices, as that will start the services if they aren't already.
            var services = GetServices (accountId);
            foreach (var service in services) {
                service.Stop ();
            }
            lock (CredReqActive) {
                CredReqActive.Remove (accountId);
            }
        }

        public void Remove (int accountId)
        {
            Stop (accountId);
            RemoveServices (accountId);
        }

        public void CreateServices (int accountId)
        {
            var services = new ConcurrentQueue<NcProtoControl> ();
            var account = McAccount.QueryById<McAccount> (accountId);
            switch (account.AccountType) {
            case McAccount.AccountTypeEnum.Device:
                services.Enqueue (new DeviceProtoControl (this, accountId));
                break;

            case McAccount.AccountTypeEnum.Exchange:
                services.Enqueue (new AsProtoControl (this, accountId));
                break;

            case McAccount.AccountTypeEnum.IMAP_SMTP:
                services.Enqueue (new ImapProtoControl (this, accountId));
                services.Enqueue (new SmtpProtoControl (this, accountId));
                break;

            case McAccount.AccountTypeEnum.Unified:
                // TODO: what should happen here?
                break;

            default:
                NcAssert.True (false);
                break;
            }
            Log.Info (Log.LOG_BACKEND, "CreateServices {0}", accountId);
            AddServices (accountId, services);
        }

        // Service must be Stop()ed before calling RemoveService().
        public void RemoveServices (int accountId)
        {
            ApplyAcrossServices (accountId, McAccount.AccountCapabilityEnum.All, "RemoveService", (service) => {
                service.Remove ();
                return NcResult.OK ();
            }, false);
            DeleteServices (accountId); // free up the memory, too.
        }

        public void Start (int accountId)
        {
            Log.Info (Log.LOG_BACKEND, "BackEnd.Start({0}) called", accountId);
            NcCommStatus.Instance.Refresh ();
            if (!AccountHasServices (accountId)) {
                CreateServices (accountId);
            }
            if (null == PendingOnTimeTimer) {
                PendingOnTimeTimer = new NcTimer ("BackEnd:PendingOnTimeTimer", state => {
                    McPending.MakeEligibleOnTime ();
                }, null, 1000, 1000);
                PendingOnTimeTimer.Stfu = true;
            }

            // don't use ApplyAcrossServices, as we'll wind up right back here.
            var services = GetServices (accountId);
            foreach (var service in services) {
                NcTask.Run (() => service.Start (), "Start");
            }

            // See if we have an OAuth2 credential for this account. 
            // If so, make sure the timer is started to refresh the token.
            McCred cred = McCred.QueryByAccountId<McCred> (accountId).FirstOrDefault ();
            if (null != cred && cred.CredType == McCred.CredTypeEnum.OAuth2) {
                if (_Oauth2RefreshCancelSource == null) {
                    lock (_Oauth2RefreshCancelSourceLock) {
                        if (_Oauth2RefreshCancelSource == null) {
                            Oauth2RefreshCancelSource = new CancellationTokenSource ();
                        }
                    }
                }
                StartOauthRefreshTimer ();
            }
            Log.Info (Log.LOG_BACKEND, "BackEnd.Start({0}) exited", accountId);
        }

        public void CertAskResp (int accountId, McAccount.AccountCapabilityEnum capabilities, bool isOkay)
        {
            NcTask.Run (delegate {
                ApplyToService (accountId, capabilities, (service) => {
                    service.CertAskResp (isOkay);
                    return NcResult.OK ();
                });
            }, "CertAskResp");
        }

        public void ServerConfResp (int accountId, McAccount.AccountCapabilityEnum capabilities, bool forceAutodiscovery)
        {
            NcTask.Run (delegate {
                ApplyToService (accountId, capabilities, (service) => {
                    service.ServerConfResp (forceAutodiscovery);
                    return NcResult.OK ();
                });
            }, "ServerConfResp");
        }

        public virtual void CredResp (int accountId)
        {
            NcTask.Run (() => {
                // Let every service know about the new creds.
                ApplyAcrossServices (accountId, McAccount.AccountCapabilityEnum.All, "CredResp", (service) => {
                    service.CredResp ();
                    return NcResult.OK ();
                }, true);
                lock (CredReqActive) {
                    CredReqActive.Remove (accountId);
                }
            }, "CredResp");
        }

        public void PendQHotInd (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            ApplyAcrossServices (accountId, capabilities, "PendQHotInd", (service) => {
                service.PendQHotInd ();
                return NcResult.OK ();
            }, true);
        }

        /// <summary>
        /// Sends a status Indication of a PendQ event to all services in the account
        /// </summary>
        /// <description>
        /// This function is identical to HintInd at the moment, and exists mainly for completeness.
        /// Should we ever split the PendQOrHint event, we won't have to change anyone using the PendQInd.
        /// </description>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="capabilities">Capabilities.</param>
        public void PendQInd (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            ApplyAcrossServices (accountId, capabilities, "PendQInd", (service) => {
                service.PendQOrHintInd ();
                return NcResult.OK ();
            }, true);
        }

        /// <summary>
        /// Sends a status Indication of a Hint event to all services in the account
        /// </summary>
        /// <description>
        /// This function is identical to PendQInd at the moment.
        /// Should we ever split the PendQOrHint event, we won't have to change anyone using the PendQInd.
        /// </description>
        /// <param name="accountId">Account identifier.</param>
        /// <param name="capabilities">Capabilities.</param>
        public void HintInd (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            ApplyAcrossServices (accountId, capabilities, "HintInd", (service) => {
                service.PendQOrHintInd ();
                return NcResult.OK ();
            }, true);
        }

        private NcResult CmdInDoNotDelayContext (int accountId, McAccount.AccountCapabilityEnum capability, Func<NcProtoControl, NcResult> cmd)
        {
            NcCommStatus.Instance.ForceUp ("CmdInDoNotDelayContext");
            return ApplyToService (accountId, capability, (service) => {
                if (null != service.Server) { // device server is null
                    NcCommStatus.Instance.Reset (service.Server.Id);
                }
                if (!service.IsDoNotDelayOk) {
                    return NcResult.Error (service.DoNotDelaySubKind);
                }
                return cmd (service);
            });
        }

        // Commands need to do Task.Run as appropriate in protocol controller.
        public NcResult StartSearchEmailReq (int accountId, string keywords, uint? maxResults)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.StartSearchEmailReq (keywords, maxResults));
        }

        public NcResult SearchEmailReq (int accountId, string keywords, uint? maxResults, string token)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SearchEmailReq (keywords, maxResults, token));
        }

        public NcResult StartSearchContactsReq (int accountId, string prefix, uint? maxResults)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.ContactReader, (service) => service.StartSearchContactsReq (prefix, maxResults));
        }

        public NcResult SearchContactsReq (int accountId, string prefix, uint? maxResults, string token)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.ContactReader, (service) => service.SearchContactsReq (prefix, maxResults, token));
        }

        public NcResult SendEmailCmd (int accountId, int emailMessageId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.SendEmailCmd (emailMessageId));
        }

        public NcResult SendEmailCmd (int accountId, int emailMessageId, int calId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SendEmailCmd (emailMessageId, calId));
        }

        public NcResult ForwardEmailCmd (int accountId, int newEmailMessageId, int forwardedEmailMessageId,
                                         int folderId, bool originalEmailIsEmbedded)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.ForwardEmailCmd (newEmailMessageId, forwardedEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

        public NcResult ReplyEmailCmd (int accountId, int newEmailMessageId, int repliedToEmailMessageId,
                                       int folderId, bool originalEmailIsEmbedded)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.ReplyEmailCmd (newEmailMessageId, repliedToEmailMessageId,
                folderId, originalEmailIsEmbedded));
        }

       
        private List<NcResult> DeleteMultiCmd (int accountId, McAccount.AccountCapabilityEnum capability, List<int> Ids,
                                               Func<NcProtoControl, int, bool, NcResult> deleter)
        {
            var outer = ApplyToService (accountId, capability, (service) => {
                var retval = new List<NcResult> ();
                for (var iter = 0; iter < Ids.Count; ++iter) {
                    if (Ids.Count - 1 == iter) {
                        retval.Add (deleter (service, Ids [iter], true));
                    } else {
                        retval.Add (deleter (service, Ids [iter], false));
                    }
                }
                return NcResult.OK (retval);
            });
            return (List<NcResult>)outer.Value;
        }

        private List<NcResult> MoveMultiCmd (int accountId, McAccount.AccountCapabilityEnum capability, List<int> Ids, int destFolderId,
                                             Func<NcProtoControl, int, int, bool, NcResult> mover)
        {
            var outer = ApplyToService (accountId, capability, (service) => {
                var retval = new List<NcResult> ();
                for (var iter = 0; iter < Ids.Count; ++iter) {
                    if (Ids.Count - 1 == iter) {
                        retval.Add (mover (service, Ids [iter], destFolderId, true));
                    } else {
                        retval.Add (mover (service, Ids [iter], destFolderId, false));
                    }
                }
                return NcResult.OK (retval);
            });
            return (List<NcResult>)outer.Value;
        }

        public List<NcResult> DeleteEmailsCmd (int accountId, List<int> emailMessageIds, bool justDelete = false)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, emailMessageIds, (service, id, lastInSeq) => {
                return service.DeleteEmailCmd (id, lastInSeq, justDelete);
            });
        }

        public NcResult DeleteEmailCmd (int accountId, int emailMessageId, bool justDelete = false)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DeleteEmailCmd (emailMessageId, true, justDelete));
        }

        public List<NcResult> MoveEmailsCmd (int accountId, List<int> emailMessageIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, emailMessageIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveEmailCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveEmailCmd (int accountId, int emailMessageId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MoveEmailCmd (emailMessageId, destFolderId));
        }

        public NcResult DnldAttCmd (int accountId, int attId, bool doNotDelay = false)
        {
            if (doNotDelay) {
                return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldAttCmd (attId, doNotDelay));
            } else {
                return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldAttCmd (attId, doNotDelay));
            }
        }

        public NcResult DnldEmailBodyCmd (int accountId, int emailMessageId, bool doNotDelay = false)
        {
            if (doNotDelay) {
                return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldEmailBodyCmd (emailMessageId, doNotDelay));
            } else {
                return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DnldEmailBodyCmd (emailMessageId, doNotDelay));
            }
        }

        public NcResult CreateCalCmd (int accountId, int calId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.CreateCalCmd (calId, folderId));
        }

        public NcResult UpdateCalCmd (int accountId, int calId, bool sendBody)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.UpdateCalCmd (calId, sendBody));
        }

        public List<NcResult> DeleteCalsCmd (int accountId, List<int> calIds)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.CalWriter, calIds, (service, id, lastInSeq) => {
                return service.DeleteCalCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteCalCmd (int accountId, int calId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.DeleteCalCmd (calId));
        }

        public NcResult MoveCalCmd (int accountId, int calId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalWriter, (service) => service.MoveCalCmd (calId, destFolderId));
        }

        public List<NcResult> MoveCalsCmd (int accountId, List<int> calIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.CalWriter, calIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveCalCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult RespondEmailCmd (int accountId, int emailMessageId, NcResponseType response)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.RespondEmailCmd (emailMessageId, response));
        }

        public NcResult RespondCalCmd (int accountId, int calId, NcResponseType response, DateTime? instance = null)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.RespondCalCmd (calId, response, instance));
        }

        public NcResult DnldCalBodyCmd (int accountId, int calId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.CalReader, (service) => service.DnldCalBodyCmd (calId));
        }

        public NcResult ForwardCalCmd (int accountId, int newEmailMessageId, int forwardedCalId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailSender, (service) => service.ForwardCalCmd (newEmailMessageId, forwardedCalId, folderId));
        }

        public NcResult MarkEmailReadCmd (int accountId, int emailMessageId, bool read)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MarkEmailReadCmd (emailMessageId, read));
        }

        public NcResult SetEmailFlagCmd (int accountId, int emailMessageId, string flagType, 
                                         DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SetEmailFlagCmd (emailMessageId, flagType, 
                start, utcStart, due, utcDue));
        }

        public NcResult ClearEmailFlagCmd (int accountId, int emailMessageId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.ClearEmailFlagCmd (emailMessageId));
        }

        public NcResult MarkEmailFlagDone (int accountId, int emailMessageId,
                                           DateTime completeTime, DateTime dateCompleted)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MarkEmailFlagDone (emailMessageId,
                completeTime, dateCompleted));
        }

        public NcResult CreateContactCmd (int accountId, int contactId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.CreateContactCmd (contactId, folderId));
        }

        public NcResult UpdateContactCmd (int accountId, int contactId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.UpdateContactCmd (contactId));
        }

        public List<NcResult> DeleteContactsCmd (int accountId, List<int> contactIds)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.ContactWriter, contactIds, (service, id, lastInSeq) => {
                return service.DeleteContactCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteContactCmd (int accountId, int contactId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.DeleteContactCmd (contactId));
        }

        public List<NcResult> MoveContactsCmd (int accountId, List<int> contactIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.ContactWriter, contactIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveContactCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveContactCmd (int accountId, int contactId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactWriter, (service) => service.MoveContactCmd (contactId, destFolderId));
        }

        public NcResult DnldContactBodyCmd (int accountId, int contactId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.ContactReader, (service) => service.DnldContactBodyCmd (contactId));
        }

        public NcResult CreateTaskCmd (int accountId, int taskId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.CreateTaskCmd (taskId, folderId));
        }

        public NcResult UpdateTaskCmd (int accountId, int taskId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.UpdateTaskCmd (taskId));
        }

        public List<NcResult> DeleteTasksCmd (int accountId, List<int> taskIds)
        {
            return DeleteMultiCmd (accountId, McAccount.AccountCapabilityEnum.TaskWriter, taskIds, (service, id, lastInSeq) => {
                return service.DeleteTaskCmd (id, lastInSeq);
            });
        }

        public NcResult DeleteTaskCmd (int accountId, int taskId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.DeleteTaskCmd (taskId));
        }

        public List<NcResult> MoveTasksCmd (int accountId, List<int> taskIds, int destFolderId)
        {
            return MoveMultiCmd (accountId, McAccount.AccountCapabilityEnum.TaskWriter, taskIds, destFolderId, (service, id, folderId, lastInSeq) => {
                return service.MoveTaskCmd (id, folderId, lastInSeq);
            });
        }

        public NcResult MoveTaskCmd (int accountId, int taskId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskWriter, (service) => service.MoveTaskCmd (taskId, destFolderId));
        }

        public NcResult DnldTaskBodyCmd (int accountId, int taskId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.TaskReader, (service) => service.DnldTaskBodyCmd (taskId));
        }

        // TODO it is likely that we will need to use folderId to help us find the right service someday.
        // Think of the folder tree being "mounted" on the service/NcProtoControl.
        public NcResult CreateFolderCmd (int accountId, int destFolderId, string displayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.CreateFolderCmd (destFolderId, displayName, folderType));
        }

        public NcResult CreateFolderCmd (int accountId, string DisplayName, Xml.FolderHierarchy.TypeCode folderType)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.CreateFolderCmd (DisplayName, folderType));
        }

        public NcResult DeleteFolderCmd (int accountId, int folderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.DeleteFolderCmd (folderId));
        }

        public NcResult MoveFolderCmd (int accountId, int folderId, int destFolderId)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.MoveFolderCmd (folderId, destFolderId));
        }

        public NcResult RenameFolderCmd (int accountId, int folderId, string displayName)
        {
            return ApplyToService (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.RenameFolderCmd (folderId, displayName));
        }

        public NcResult SyncCmd (int accountId, int folderId)
        {
            return CmdInDoNotDelayContext (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter, (service) => service.SyncCmd (folderId));
        }

        public NcResult ValidateConfig (int accountId, McServer server, McCred cred)
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                return NcResult.Error (NcResult.SubKindEnum.Error_NetworkUnavailable);
            }
            return ApplyToService (accountId, server.Capabilities, (service) => {
                service.ValidateConfig (server, cred);
                return NcResult.OK ();
            });
        }

        public void CancelValidateConfig (int accountId)
        {
            ApplyAcrossServices (accountId, McAccount.AccountCapabilityEnum.All, "CancelValidateConfig", (service) => {
                service.CancelValidateConfig ();
                return NcResult.OK ();
            }, false);
        }

        public List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> BackEndStates (int accountId)
        {
            var states = new List<Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum>> ();
            ApplyAcrossServices (accountId, McAccount.AccountCapabilityEnum.All, "BackEndStates", (service) => {
                states.Add (new Tuple<BackEndStateEnum, McAccount.AccountCapabilityEnum> (service.BackEndState, service.Capabilities));
                return NcResult.OK ();
            }, false);
            return states;
        }

        public BackEndStateEnum BackEndState (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            // ApplyToService may in some cases start the backend, which we may not want (like at startup
            // where we want services delayed). So check if the backend is started here first.
            var svc = GetService (accountId, capabilities);
            if (null == svc || !svc.IsStarted) {
                return BackEndStateEnum.NotYetStarted;
            }

            var result = ApplyToService (accountId, capabilities, (service) => {
                BackEndStateEnum state = service.BackEndState;
                if (BackEndStateEnum.CredWait == state) {
                    // HACK HACK: Lie to the UI, if we're in CredWait, but we're waiting for an OAUTH2 refresh.
                    CredReqActiveStatus status;
                    lock (CredReqActive) {
                        if (CredReqActive.TryGetValue (accountId, out status)) {
                            if (BackEnd.CredReqActiveState.CredReqActive_AwaitingRefresh == status.State) {
                                state = (service.ProtocolState.HasSyncedInbox) ? 
                                    BackEndStateEnum.PostAutoDPostInboxSync : 
                                    BackEndStateEnum.PostAutoDPreInboxSync;
                            }
                        }
                    }
                }
                return NcResult.OK (state);
            });
            return result.isOK () ? result.GetValue<BackEndStateEnum> () : BackEndStateEnum.NotYetStarted;
        }

        public AutoDFailureReasonEnum AutoDFailureReason (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var result = ApplyToService (accountId, capabilities,
                             (service) => NcResult.OK (service.AutoDFailureReason));
            return result.isOK () ? result.GetValue<AutoDFailureReasonEnum> () : AutoDFailureReasonEnum.Unknown;
        }

        public AutoDInfoEnum AutoDInfo (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            var result = ApplyToService (accountId, capabilities,
                             (service) => NcResult.OK (service.AutoDInfo));
            return result.isOK () ? result.GetValue<AutoDInfoEnum> () : AutoDInfoEnum.Unknown;
        }

        public X509Certificate2 ServerCertToBeExamined (int accountId, McAccount.AccountCapabilityEnum capabilities)
        {
            return ApplyToService (accountId, capabilities, 
                (service) => NcResult.OK (service.ServerCertToBeExamined)).GetValue<X509Certificate2> ();
        }

        //
        // For IProtoControlOwner.
        //
        private void InvokeStatusIndEvent (StatusIndEventArgs e)
        {
            NcApplication.Instance.InvokeStatusIndEvent (e);
        }

        /// <summary>
        /// Do we need to tell the UI about refreshing this Credentials?
        /// </summary>
        /// <returns><c>true</c>, if to pass req to user interface was needed, <c>false</c> otherwise.</returns>
        /// <param name="accountId">Account identifier.</param>
        protected bool NeedToPassReqToUi (int accountId)
        {
            lock (CredReqActive) {
                CredReqActiveStatus status;
                if (CredReqActive.TryGetValue (accountId, out status)) {
                    // We want to pass the request up to the UI in the following cases:
                    // - the ProtoController asked for creds, and we failed.
                    //   We should have refreshed long before this, so if we reach this point, pass it up.
                    // - The state was set to CredReqActive_NeedUI somehow (password auth gets this)
                    if (status.NeedCredResp ||
                        status.State == CredReqActiveState.CredReqActive_NeedUI) {
                        return true;
                    }
                } else {
                    Log.Warn (Log.LOG_BACKEND, "NeedToPassReqToUi: No CredReqActive entry.");
                    return true;
                }
            }
            return false;
        }

        #region INcProtoControlOwner

        public void StatusInd (NcProtoControl sender, NcResult status)
        {
            InvokeStatusIndEvent (new StatusIndEventArgs () { 
                Account = sender.Account,
                Status = status,
            });
        }

        public void StatusInd (NcProtoControl sender, NcResult status, string[] tokens)
        {
            InvokeStatusIndEvent (new StatusIndEventArgs () {
                Account = sender.Account,
                Status = status,
                Tokens = tokens,
            });
        }

        /// <summary>
        /// Attempt a CredReq. Called from ProtoControl. If one is already active, just return.
        /// </summary>
        /// <param name="sender">Sender.</param>
        public void CredReq (NcProtoControl sender)
        {
            lock (CredReqActive) {
                CredReqActiveStatus status;
                if (CredReqActive.TryGetValue (sender.Account.Id, out status)) {
                    // remember that we had a CredReq, so that we send a response when we get a token updated.
                    // This path can happen if the timer is already refreshing the token, but the controller
                    // got an AuthFail and asks us for new credentials.
                    status.NeedCredResp = true;
                    return;
                }
                CredReqActive.Add (sender.Account.Id, new CredReqActiveStatus (default(CredReqActiveState), true));
            }
            // This should only really happen if the timer is messed up and didn't try to refresh already.
            // But we'll leave here as a backup.
            RefreshToken (sender.Cred);
        }

        public void ServConfReq (NcProtoControl sender, BackEnd.AutoDFailureReasonEnum arg)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.ServConfReq (sender.AccountId, sender.Capabilities, arg);
            });
        }

        public void CertAskReq (NcProtoControl sender, X509Certificate2 certificate)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.CertAskReq (sender.AccountId, sender.Capabilities, certificate);
            });
        }

        public void SearchContactsResp (NcProtoControl sender, string prefix, string token)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.SearchContactsResp (sender.AccountId, prefix, token);
            });
        }

        public void SendEmailResp (NcProtoControl sender, int emailMessageId, bool didSend)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                Owner.SendEmailResp (sender.AccountId, emailMessageId, didSend);
            });
        }

        public void BackendAbateStart ()
        {
            Owner.BackendAbateStart ();
        }

        public void BackendAbateStop ()
        {
            Owner.BackendAbateStop ();
        }

        #endregion

        public void SendEmailBodyFetchHint (int accountId, int emailMessageId)
        {
            bool needInd = BodyFetchHints.Count (accountId) == 0;
            Log.Info (Log.LOG_BACKEND, "SendEmailBodyFetchHint: {0} hints in queue", BodyFetchHints.Count (accountId));
            BodyFetchHints.AddHint (accountId, emailMessageId);
            if (needInd) {
                HintInd (accountId, McAccount.AccountCapabilityEnum.EmailReaderWriter);
            }
        }

        public void SendEmailBodyFetchHints (List<Tuple<int,int>> Ids)
        {
            HashSet<int> needInd = new HashSet<int> ();
            foreach (var t in Ids) {
                if (0 == BodyFetchHints.Count (t.Item1)) {
                    needInd.Add (t.Item1);
                }
            }
            foreach (var t in Ids) {
                BodyFetchHints.AddHint (t.Item1, t.Item2);
            }
            foreach (var n in needInd) {
                HintInd (n, McAccount.AccountCapabilityEnum.EmailReaderWriter);
            }
        }

        #region Oauth2Refresh

        /// OAuth2 refresh has the following cases:
        /// 1) Oauth2RefreshTimer callback find a credentials and initiates a refresh.
        ///    It creates an entry in CredReqActive to keep track of the refresh. This
        ///    is also needed so that we know we're already working on it, should the controller
        ///    initiate a CredReq. If the timer-initiated refresh finishes without the controller
        ///    sending a CredReq, we don't need to send a CredResp. If it does, we do.
        /// 2) Controller initiates a CredReq before the Oauth2RefreshTimer callback notices
        ///    it needs to refresh a token. This can happen when the Backend is first Start()'ed.
        ///    The timer will wait KOauth2RefreshIntervalSecs to run, but the Controller will
        ///    start immediately, so we'll start the refresh via the CredReq. When the timer callback
        ///    runs and notices it needs to refresh the token (assuming it hasn't finished already),
        ///    it will not restart the refresh, and simply keep track.
        /// 
        /// Failures:
        /// 1) If the refresh fails and there was a CredReq, we send a CredReq up the line to the UI.
        /// 2) If there was no CredReq, we simply try again, until KOauth2RefreshMaxFailure have been tried.
        /// 
        /// Success:
        /// 1) The refresh succeeds, and there was a CredReq: We need to send a CredResp. The CredResp 
        ///    deletes the CredReqActive record.
        /// 2) The refresh succeeds, and there was NO CredReq. We don't send a CredResp, and thus
        ///    need to delete the CredReqActive entry ourselves.
        /// 
        /// TODO: We currently don't handle the case where the refreshtoken is invalidated somehow. Tokens
        /// for google are valid forever (Other services may limit this to 2 weeks). The refresh token
        /// can also be invalidated by the user. Currently we retry KOauth2RefreshMaxFailure times, but
        /// we can do better if we catch an invalid refresh token in the McCred.RefreshOAuth2() and
        /// immediately punt up the UI.

        /// <summary>
        /// Interval in seconds after which we (re-)check the OAuth2 credentials.
        /// </summary>
        const int KOauth2RefreshIntervalSecs = 300;

        /// <summary>
        /// The percentage of OAuth2-expiry after which we refresh the token.
        /// </summary>
        const int KOauth2RefreshPercent = 80;

        /// <summary>
        /// The OAuth2 Refresh NcTimer
        /// </summary>
        NcTimer Oauth2RefreshTimer = null;

        /// <summary>
        /// Number of retries after which we call the attempts failed, and tell the UI
        /// to ask the user to log in anew. Not saved in the DB.
        /// </summary>
        public const uint KOauth2RefreshMaxFailure = 3;

        /// <summary>
        /// Starts the oauth refresh timer. 
        /// 
        /// This will keep ALL oauth2 tokens up to date, whether the backed for the associated
        /// account is active or not.
        /// </summary>
        void StartOauthRefreshTimer ()
        {
            if (null == Oauth2RefreshTimer) {
                var refreshMsecs = KOauth2RefreshIntervalSecs * 1000;
                Oauth2RefreshTimer = new NcTimer ("McCred:Oauth2RefreshTimer", state => RefreshAllDueTokens (),
                    null, refreshMsecs, refreshMsecs);
            }
        }

        public void Oauth2NetStatusEventHandler (Object sender, NetStatusEventArgs e)
        {
            switch (e.Status) {
            case NetStatusStatusEnum.Down:
                StopOauthRefreshTimer ();
                break;

            case NetStatusStatusEnum.Up:
                // If we haven't started it (i.e. never ran through BackEnd.Start(int)),
                // then don't start it now, either.
                if (null != Oauth2RefreshTimer) {
                    StartOauthRefreshTimer ();
                }
                break;
            }
        }

        protected virtual void ChangeOauthRefreshTimer (long nextUpdate)
        {
            NcAssert.NotNull (_Oauth2RefreshCancelSource);
            if (!_Oauth2RefreshCancelSource.IsCancellationRequested) {
                NcAssert.NotNull (Oauth2RefreshTimer);
                var nextUpdateMsecs = nextUpdate * 1000;
                var refreshMsecs = KOauth2RefreshIntervalSecs * 1000;
                Oauth2RefreshTimer.Change (nextUpdateMsecs, refreshMsecs);
            }
        }

        protected virtual void ResetOauthRefreshTimer ()
        {
            NcAssert.NotNull (_Oauth2RefreshCancelSource);
            if (!_Oauth2RefreshCancelSource.IsCancellationRequested) {
                NcAssert.NotNull (Oauth2RefreshTimer);
                var refreshMsecs = KOauth2RefreshIntervalSecs * 1000;
                Oauth2RefreshTimer.Change (refreshMsecs, refreshMsecs);
            }
        }

        /// <summary>
        /// Stops the oauth refresh timer.
        /// </summary>
        void StopOauthRefreshTimer ()
        {
            if (null != Oauth2RefreshTimer) {
                Oauth2RefreshTimer.Dispose ();
                Oauth2RefreshTimer = null;
            }
        }

        /// <summary>
        /// Find all the McCred's that are OAuth2, and which are within KOauth2RefreshPercent of expiring.
        /// Mark them as CredReqActive, so we have handle this properly if/when the Controller sends
        /// a CredReq for the same item (can happen if we timeout with no network connectivity, for example).
        /// </summary>
        protected void RefreshAllDueTokens ()
        {
            if (NcCommStatus.Instance.Status != NetStatusStatusEnum.Up) {
                // why bother.. no one listens to me anyway...
                return;
            }
            foreach (var cred in McCred.QueryByCredType (McCred.CredTypeEnum.OAuth2)) {
                if (_Oauth2RefreshCancelSource.IsCancellationRequested) {
                    return;
                }
                var expiryFractionSecs = Math.Round ((double)(cred.ExpirySecs * (100 - KOauth2RefreshPercent)) / 100);
                if (cred.Expiry.AddSeconds (-expiryFractionSecs) <= DateTime.UtcNow) {
                    lock (CredReqActive) {
                        CredReqActiveStatus status;
                        if (!CredReqActive.TryGetValue (cred.AccountId, out status)) {
                            CredReqActive.Add (cred.AccountId, new CredReqActiveStatus (CredReqActiveState.CredReqActive_AwaitingRefresh, false));
                        } else {
                            if (status.State == CredReqActiveState.CredReqActive_NeedUI) {
                                // We've decided to give up on this one
                                continue;
                            }
                        }
                    }
                    RefreshToken (cred);
                }
            }
        }

        /// <summary>
        /// Keep track of retries and do some error checking. Then Refresh the credential.
        /// </summary>
        /// <param name="cred">Cred.</param>
        protected void RefreshToken (McCred cred)
        {
            if (!cred.CanRefresh ()) {
                TokenRefreshFailure (cred);
                return;
            }
            lock (CredReqActive) {
                CredReqActiveStatus status;
                if (CredReqActive.TryGetValue (cred.AccountId, out status)) {
                    if (status.State != CredReqActiveState.CredReqActive_AwaitingRefresh) {
                        Log.Warn (Log.LOG_BACKEND, "RefreshToken ({0}): State should be CredReqActive_AwaitingRefresh", cred.AccountId);
                    }
                    // We've retried too many times. Guess we need the UI afterall.
                    if (status.RefreshRetries >= KOauth2RefreshMaxFailure) {
                        status.State = CredReqActiveState.CredReqActive_NeedUI;
                        return;
                    }
                }
            }
            RefreshMcCred (cred);
        }

        /// <summary>
        /// Refreshs the McCred. Exists so we can override it in testing
        /// </summary>
        /// <param name="cred">Cred.</param>
        protected virtual void RefreshMcCred (McCred cred)
        {
            NcAssert.NotNull (_Oauth2RefreshCancelSource, "_Oauth2RefreshCancelSource is null");

            cred.RefreshOAuth2 (TokenRefreshSuccess, TokenRefreshFailure, _Oauth2RefreshCancelSource.Token);
        }

        /// <summary>
        /// Callback called after a failure to refresh the oauth2 token
        /// </summary>
        /// <param name="cred">Cred.</param>
        protected virtual void TokenRefreshFailure (McCred cred)
        {
            lock (CredReqActive) {
                CredReqActiveStatus status;
                if (CredReqActive.TryGetValue (cred.AccountId, out status)) {
                    if (++status.RefreshRetries >= KOauth2RefreshMaxFailure) {
                        status.State = CredReqActiveState.CredReqActive_NeedUI;
                    }
                }
            }
            if (NeedToPassReqToUi (cred.AccountId)) {
                alertUi (cred.AccountId);
            }
            ChangeOauthRefreshTimer (10);
        }

        protected virtual void alertUi (int accountId)
        {
            InvokeOnUIThread.Instance.Invoke (() => Owner.CredReq (accountId));
        }

        /// <summary>
        /// Callback called after a successful OAuth2 refresh.
        /// </summary>
        /// <param name="cred">Cred.</param>
        protected virtual void TokenRefreshSuccess (McCred cred)
        {
            lock (CredReqActive) {
                CredReqActiveStatus status;
                if (CredReqActive.TryGetValue (cred.AccountId, out status)) {
                    if (status.NeedCredResp) {
                        CredResp (cred.AccountId);
                    } else {
                        CredReqActive.Remove (cred.AccountId);
                    }
                }
                if (CredReqActive.Count == 0) {
                    // there's currently none active, and thus not a failed
                    // attempt we need to re-check at a quicker pace, so reset
                    // the timer.
                    ResetOauthRefreshTimer ();
                }
            }
        }

        #endregion
    }
}
