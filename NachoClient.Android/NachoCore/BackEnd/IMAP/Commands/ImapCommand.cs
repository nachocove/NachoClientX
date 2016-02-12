//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.IO;
using NachoCore.Utils;
using MailKit;
using MailKit.Net.Imap;
using NachoCore;
using NachoCore.Model;
using MailKit.Security;
using System.Text;
using System.Net.Sockets;
using HtmlAgilityPack;
using MailKit.Search;
using System.Threading;
using NachoClient.Build;
using MimeKit.IO;
using MimeKit.IO.Filters;
using MimeKit;
using NachoPlatform;

namespace NachoCore.IMAP
{
    public class ImapCommand : NcCommand
    {
        protected NcImapClient Client { get; set; }
        protected RedactProtocolLogFuncDel RedactProtocolLogFunc;
        protected bool DontReportCommResult { get; set; }
        public INcCommStatus NcCommStatusSingleton { set; get; }
        protected string CmdName;
        protected string CmdNameWithAccount;
        private const string KCaptureFolderMetadata = "ImapCommand.FolderMetadata";

        public ImapCommand (IBEContext beContext, NcImapClient imapClient) : base (beContext)
        {
            Client = imapClient;
            RedactProtocolLogFunc = null;
            NcCommStatusSingleton = NcCommStatus.Instance;
            DontReportCommResult = false;
            CmdName = this.GetType ().Name;
            CmdNameWithAccount = string.Format ("{0}{{{1}}}", CmdName, AccountId);
        }

        // MUST be overridden by subclass.
        protected virtual Event ExecuteCommand ()
        {
            NcAssert.True (false);
            return null;
        }

        public override void Cancel ()
        {
            base.Cancel ();
            // When the back end is being shut down, we can't afford to wait for the cancellation
            // to be processed.
            if (!BEContext.ProtoControl.Cts.IsCancellationRequested) {
                // Wait for the command to notice the cancellation and release the lock.
                // TODO MailKit is not always good about cancelling in a timely manner.
                // When MailKit is fixed, this code should be adjusted.
                try {
                    TryLock (Client.SyncRoot, KLockTimeout);
                } catch (CommandLockTimeOutException ex) {
                    Log.Error (Log.LOG_IMAP, "{0}.Cancel(): {1}", CmdNameWithAccount, ex.Message);
                    Client.DOA = true;
                }
            }
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                ExecuteNoTask (sm);
            }, CmdName);
        }

        public virtual Event ExecuteConnectAndAuthEvent()
        {
            Cts.Token.ThrowIfCancellationRequested ();
            NcCapture.AddKind (CmdName);
            ImapDiscoverCommand.guessServiceType (BEContext);

            return TryLock (Client.SyncRoot, KLockTimeout, () => {
                try {
                    if (null != RedactProtocolLogFunc && null != Client.MailKitProtocolLogger) {
                        Client.MailKitProtocolLogger.Start (RedactProtocolLogFunc);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        ConnectAndAuthenticate ();
                    }
                    using (var cap = NcCapture.CreateAndStart (CmdName)) {
                        var evt = ExecuteCommand ();
                        return evt;
                    }
                } finally {
                    if (null != Client.MailKitProtocolLogger && Client.MailKitProtocolLogger.Enabled ()) {
                        ProtocolLoggerStopAndPostTelemetry ();
                    }
                }
            });
        }

        public void ExecuteNoTask(NcStateMachine sm)
        {
            Event evt;
            bool serverFailedGenerally = false;
            Tuple<ResolveAction, NcResult.WhyEnum> action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.None, NcResult.WhyEnum.Unknown);
            Log.Info (Log.LOG_IMAP, "{0}: Started", CmdNameWithAccount);
            try {
                Cts.Token.ThrowIfCancellationRequested ();
                evt = ExecuteConnectAndAuthEvent();
                // In the no-exception case, ExecuteCommand is resolving McPending.
                Cts.Token.ThrowIfCancellationRequested ();
            } catch (OperationCanceledException) {
                Log.Info (Log.LOG_IMAP, "OperationCanceledException");
                ResolveAllDeferred ();
                // No event posted to SM if cancelled.
                return;
            } catch (KeychainItemNotFoundException ex) {
                Log.Error (Log.LOG_IMAP, "KeychainItemNotFoundException: {0}", ex.Message);
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPKEYCHFAIL");
            } catch (CommandLockTimeOutException ex) {
                Log.Error (Log.LOG_IMAP, "CommandLockTimeOutException: {0}", ex.Message);
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPLOKTIME");
                Client.DOA = true;
            } catch (ServiceNotConnectedException) {
                Log.Info (Log.LOG_IMAP, "ServiceNotConnectedException");
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReDisc, "IMAPCONN");
                serverFailedGenerally = true;
            } catch (AuthenticationException ex) {
                Log.Info (Log.LOG_IMAP, "AuthenticationException: {0}", ex.Message);
                if (!HasPasswordChanged ()) {
                    evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH1");
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.AccessDeniedOrBlocked);
                } else {
                    // credential was updated while we were running the command. Just try again.
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPAUTH1TEMP");
                    action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                }
            } catch (ServiceNotAuthenticatedException) {
                Log.Info (Log.LOG_IMAP, "ServiceNotAuthenticatedException");
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                if (!HasPasswordChanged ()) {
                    evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH2");
                } else {
                    // credential was updated while we were running the command. Just try again.
                    evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPAUTH2TEMP");
                }
            } catch (ImapCommandException ex) {
                Log.Info (Log.LOG_IMAP, "ImapCommandException {0}", ex.Message);
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.Wait, "IMAPCOMMWAIT", 60);
            } catch (FolderNotFoundException ex) {
                Log.Info (Log.LOG_IMAP, "FolderNotFoundException {0}", ex.Message);
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.ConflictWithServer);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReFSync, "IMAPFOLDRESYNC");
            } catch (IOException ex) {
                Log.Info (Log.LOG_IMAP, "IOException: {0}", ex.ToString ());
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPIO");
                serverFailedGenerally = true;
            } catch (ImapProtocolException ex) {
                // From MailKit: The exception that is thrown when there is an error communicating with an IMAP server. A
                // <see cref="ImapProtocolException"/> is typically fatal and requires the <see cref="ImapClient"/>
                // to be reconnected.
                Log.Info (Log.LOG_IMAP, "ImapProtocolException: {0}", ex.ToString ());
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPPROTOTEMPFAIL");
                serverFailedGenerally = true;
            } catch (SocketException ex) {
                // We check the server connectivity pretty well in Discovery. If this happens with
                // other commands, it's probably a temporary failure.
                Log.Error (Log.LOG_IMAP, "SocketException: {0}", ex.Message);
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPCONNTEMPAUTH");
                serverFailedGenerally = true;
            } catch (InvalidOperationException ex) {
                Log.Error (Log.LOG_IMAP, "InvalidOperationException: {0}", ex.ToString ());
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.ProtocolError);
                evt = Event.Create ((uint)SmEvt.E.HardFail, "IMAPHARD1");
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "Exception : {0}", ex.ToString ());
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.FailAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)SmEvt.E.HardFail, "IMAPHARD2");
                serverFailedGenerally = true;
            } finally {
                Log.Info (Log.LOG_IMAP, "{0}: Finished (failed {1})", CmdNameWithAccount, serverFailedGenerally);
            }
            if (Cts.Token.IsCancellationRequested) {
                Log.Info (Log.LOG_IMAP, "{0}: Cancelled", CmdNameWithAccount);
                return;
            }
            ReportCommResult (BEContext.Server.Host, serverFailedGenerally);
            switch (action.Item1) {
            case ResolveAction.None:
                break;
            case ResolveAction.DeferAll:
                ResolveAllDeferred ();
                break;
            case ResolveAction.FailAll:
                ResolveAllFailed (action.Item2);
                break;
            }
            sm.PostEvent (evt);
        }

        public void ConnectAndAuthenticate ()
        {
            if (!Client.IsConnected) {
                Log.Info (Log.LOG_IMAP, "Connecting to Server {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, true, Cts.Token);
                var capUnauth = McProtocolState.FromImapCapabilities (Client.Capabilities);

                Log.Info (Log.LOG_IMAP, "saving Unauthenticated Capabilities");
                if (capUnauth != BEContext.ProtocolState.ImapServerCapabilities) {
                    BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.ImapServerCapabilitiesUnAuth = capUnauth;
                        return true;
                    });
                }
                Cts.Token.ThrowIfCancellationRequested ();
            }
            if (!Client.IsAuthenticated) {
                ImapDiscoverCommand.possiblyFixUsername (BEContext);
                string username = BEContext.Cred.Username;
                string cred;
                if (BEContext.Cred.CredType == McCred.CredTypeEnum.OAuth2) {
                    Client.AuthenticationMechanisms.RemoveWhere ((m) => !m.Contains ("XOAUTH2"));
                    cred = BEContext.Cred.GetAccessToken ();
                } else {
                    Client.AuthenticationMechanisms.RemoveWhere ((m) => m.Contains ("XOAUTH"));
                    cred = BEContext.Cred.GetPassword ();
                }

                Cts.Token.ThrowIfCancellationRequested ();
                try {
                    Log.Info (Log.LOG_IMAP, "Authenticating to Server {0}:{1} (type {2})", BEContext.Server.Host, BEContext.Server.Port, BEContext.Cred.CredType);
                    BEContext.Account.LogHashedPassword (Log.LOG_IMAP, "ConnectAndAuthenticate", cred);
                    Client.Authenticate (username, cred, Cts.Token);
                } catch (ImapProtocolException e) {
                    Log.Info (Log.LOG_IMAP, "Protocol Error during auth: {0}", e);
                    if (BEContext.ProtocolState.ImapServiceType == McAccount.AccountServiceEnum.iCloud) {
                        // some servers (icloud.com) seem to close the connection on a bad password/username.
                        throw new AuthenticationException (e.Message);
                    } else {
                        throw;
                    }
                }

                Log.Info (Log.LOG_IMAP, "saving Authenticated Capabilities");
                var capAuth = McProtocolState.FromImapCapabilities (Client.Capabilities);
                if (capAuth != BEContext.ProtocolState.ImapServerCapabilities) {
                    BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.ImapServerCapabilities = capAuth;
                        return true;
                    });
                }

                ImapImplementation serverId = null;
                // if the server supports ID, send one.
                if ((Client.Capabilities & ImapCapabilities.Id) == ImapCapabilities.Id) {
                    Log.Info (Log.LOG_IMAP, "ID exchange with server {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
                    ImapImplementation ourId = new ImapImplementation () {
                        Name = "Nacho Mail",
                        Version = string.Format ("{0}:{1}", BuildInfo.Version, BuildInfo.BuildNumber),
                        ReleaseDate = BuildInfo.Time,
                        SupportUrl = "https://support.nachocove.com/",
                        Vendor = "Nacho Cove, Inc",
                        OS = NachoPlatform.Device.Instance.BaseOs ().ToString (),
                        OSVersion = NachoPlatform.Device.Instance.Os (),
                    };
                    //Log.Info (Log.LOG_IMAP, "Our Id: {0}", dumpImapImplementation(ourId));
                    serverId = Client.Identify (ourId, Cts.Token);
                    if (null == serverId) {
                        // perhaps a bug on some servers (specifically gmx.net)
                        serverId = Client.Identify (null, Cts.Token);
                    }
                }
                Log.Info (Log.LOG_IMAP, "IMAP Server {0}:{1} capabilities: {2} Id: {3}", BEContext.Server.Host, BEContext.Server.Port, Client.Capabilities.ToString (), dumpImapImplementation (serverId));
            }
        }

        private string dumpImapImplementation (ImapImplementation imapId)
        {
            if (null != imapId) {
                return HashHelper.HashEmailAddressesInImapId (string.Join (", ", imapId.Properties));
            } else {
                return "Server did not return an ID or no ID capability";
            }
        }

        protected void ProtocolLoggerStopAndPostTelemetry ()
        {
            string ClassName = CmdName + " ";
            byte[] requestData;
            byte[] responseData;
            //string combinedLog = Encoding.UTF8.GetString (Client.MailKitProtocolLogger.GetCombinedBuffer ());
            //Log.Info (Log.LOG_IMAP, "{0}IMAP exchange\n{1}", ClassName, combinedLog);
            Client.MailKitProtocolLogger.Stop (out requestData, out responseData);
            byte[] ClassNameBytes = Encoding.UTF8.GetBytes (ClassName + "\n");

            if (null != requestData && requestData.Length > 0) {
                //Log.Info (Log.LOG_IMAP, "{0}IMAP Request\n{1}", ClassName, Encoding.UTF8.GetString (RedactProtocolLog(requestData)));
                Telemetry.RecordImapEvent (true, Combine(ClassNameBytes, requestData));
            }
            if (null != responseData && responseData.Length > 0) {
                //Log.Info (Log.LOG_IMAP, "{0}IMAP Response\n{1}", ClassName, Encoding.UTF8.GetString (responseData));
                Telemetry.RecordImapEvent (false, Combine(ClassNameBytes, responseData));
            }
        }

        private static byte[] Combine(byte[] first, byte[] second)
        {
            byte[] ret = new byte[first.Length + second.Length];
            Buffer.BlockCopy(first, 0, ret, 0, first.Length);
            Buffer.BlockCopy(second, 0, ret, first.Length, second.Length);
            return ret;
        }

        protected NcImapFolder GetOpenMailkitFolder(McFolder folder, FolderAccess access = FolderAccess.ReadOnly)
        {
            var mailKitFolder = Client.GetFolder (folder.ServerId, Cts.Token) as NcImapFolder;
            if (null == mailKitFolder) {
                return null;
            }
            if (FolderAccess.None == mailKitFolder.Open (access, Cts.Token)) {
                return null;
            }
            return mailKitFolder;
        }

        protected string GetParentId(IMailFolder mailKitFolder)
        {
            return null != mailKitFolder.ParentFolder && string.Empty != mailKitFolder.ParentFolder.FullName ?
                mailKitFolder.ParentFolder.FullName : McFolder.AsRootServerId;
        }

        /// <summary>
        /// Creates the or update a folder.
        /// </summary>
        /// <remarks>
        /// Folders can be moved in IMAP. The IMAP FullName is the full path for a folder, and we use it as the ServerId.
        /// If a folder were to be moved, its FullName will no longer match what we have locally. We try our best to find the
        /// original folder by name or distinguished type.
        /// </remarks>
        /// <returns><c>true</c>, if or update folder was created, <c>false</c> otherwise.</returns>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="folderType">Folder type.</param>
        /// <param name="folderDisplayName">Folder display name.</param>
        /// <param name="isDisinguished">If set to <c>true</c> is disinguished.</param>
        /// <param name="doFolderMetadata">If set to <c>true</c> do folder metadata.</param>
        /// <param name="folder">Folder.</param>
        protected bool CreateOrUpdateFolder (IMailFolder mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode folderType, string folderDisplayName, bool isDisinguished, bool doFolderMetadata, out McFolder folder)
        {
            NcAssert.NotNull (mailKitFolder, "mailKitFolder is null");

            // if we can, open the folder, so that we get the UidValidity.
            if (!mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
                mailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
            }

            NcAssert.NotNull (mailKitFolder.Attributes, "mailKitFolder.Attributes is null");
            NcAssert.NotNull (mailKitFolder.UidValidity, "mailKitFolder.UidValidity is null");
            NcAssert.NotNull (mailKitFolder.FullName, "mailKitFolder.FullName is null");
            NcAssert.NotNull (mailKitFolder.Name, "mailKitFolder.Name is null");

            bool added_or_changed = false;
            var ParentId = GetParentId (mailKitFolder);

            folder = McFolder.QueryByServerId<McFolder> (AccountId, mailKitFolder.FullName);
            if (null == folder) {
                // perhaps the folder has moved. See if we can find it by folderType (distinguished) or Name.
                if (isDisinguished) {
                    folder = McFolder.GetDistinguishedFolder (AccountId, folderType);
                } else {
                    folder = McFolder.GetUserFolders (AccountId, folderType, ParentId, mailKitFolder.Name).SingleOrDefault ();
                }
            }

            if ((null != folder) && (folder.ImapUidValidity != mailKitFolder.UidValidity)) {
                // perhaps the folder has been deleted and re-created with the same name. Another possibility
                // is that the original folder was moved/renamed, and a different folder was moved/renamed to
                // have the same name. In either case, as per the IMAP specs, we delete this folder, as our
                // view of it is no longer the server's view of it.
                Log.Warn (Log.LOG_IMAP, "CreateOrUpdateFolder: Deleting folder {0} due to UidValidity ({1} != {2})", folder.ImapFolderNameRedacted (), folder.ImapUidValidity, mailKitFolder.UidValidity.ToString ());
                folder.Delete ();
                folder = null;
            }

            if (null == folder) {
                var existing = McFolder.QueryByServerId<McFolder> (AccountId, mailKitFolder.FullName);
                if (null != existing) {
                    // another folder already exists with this same FullName. This should never happen, since
                    // we looked up the folder by FullName above.
                    Log.Error (Log.LOG_IMAP, "CreateOrUpdateFolder: Could not add folder {0}:{1}: the folder already exists", existing.AccountId, existing.ImapFolderNameRedacted ());
                    return false;
                }
                // we need to create the folder locally.
                folder = McFolder.Create (AccountId, false, false, isDisinguished, ParentId, mailKitFolder.FullName, mailKitFolder.Name, folderType);
                Log.Info (Log.LOG_IMAP, "CreateOrUpdateFolder: Adding folder {0} UidValidity {1}", folder.ImapFolderNameRedacted (), mailKitFolder.UidValidity.ToString ());
                folder.ImapUidValidity = mailKitFolder.UidValidity;
                folder.ImapNoSelect = mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect);
                try {
                    folder.Insert ();
                } catch (ArgumentException ex) {
                    Log.Error (Log.LOG_IMAP, "CreateOrUpdateFolder: Failed to add folder {0}:{1}: {2}", folder.AccountId, folder.ImapFolderNameRedacted (), ex.Message);
                    folder = null;
                    return false;
                }
                added_or_changed = true;
            } else if (folder.ServerId != mailKitFolder.FullName ||
                folder.DisplayName != folderDisplayName ||
                folder.ParentId != ParentId) {
                // We found an existing folder, so now we need to make sure to update any values that may have changed.
                Log.Info (Log.LOG_IMAP, "CreateOrUpdateFolder: Updating folder {0} UidValidity {1}", folder.ImapFolderNameRedacted (), mailKitFolder.UidValidity.ToString ());
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = mailKitFolder.FullName;
                    target.DisplayName = folderDisplayName;
                    target.ParentId = ParentId;
                    return true;
                });
                added_or_changed = true;
            }
            NcAssert.NotNull (folder, "folder should not be null");
            // Get the current list of UID's. Don't set added_or_changed. Sync will notice later.
            if (doFolderMetadata && !mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
                var account = BEContext.Account;
                NcAssert.NotNull (account, "BEContext.Account is null");
                GetFolderMetaData (ref folder, mailKitFolder, account.DaysSyncEmailSpan ());
            }

            return added_or_changed;
        }

        public static bool UpdateImapSetting (IMailFolder mailKitFolder, ref McFolder folder)
        {
            bool changed = false;

            bool needFullSync = ((folder.ImapExists != mailKitFolder.Count) ||
                (mailKitFolder.UidNext.HasValue && folder.ImapUidNext != mailKitFolder.UidNext.Value.Id));

            if (needFullSync ||
                folder.ImapNoSelect != mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect))
            {
                // update.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapNoSelect = mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect);
                    target.ImapUidNext = mailKitFolder.UidNext.HasValue ? mailKitFolder.UidNext.Value.Id : 0;
                    target.ImapExists = mailKitFolder.Count;
                    if (needFullSync) {
                        // don't reset to false, if someone else said we need one.
                        target.ImapNeedFullSync = needFullSync;
                    }
                    return true;
                });
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// For a given folder, do IMAP searches:
        /// 
        /// - all emails currently in the folder for a given timespan
        /// 
        /// Calls UpdateImapSetting() to update the corresponding McFolder, and sets the ImapUidSet,
        /// and ImapLastExamine.
        /// </summary>
        /// <returns><c>true</c>, if folder meta data was gotten and has changed, <c>false</c> otherwise.</returns>
        /// <param name="folder">Folder.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="timespan">Timespan.</param>
        public bool GetFolderMetaData (ref McFolder folder, IMailFolder mailKitFolder, TimeSpan timespan)
        {
            NcCapture.AddKind (KCaptureFolderMetadata);
            using (var cap = NcCapture.CreateAndStart (KCaptureFolderMetadata)) {
                var query = SearchQuery.NotDeleted;
                if (TimeSpan.Zero != timespan) {
                    query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
                }
                UniqueIdSet uids = SyncKit.MustUniqueIdSet (mailKitFolder.Search (query, Cts.Token));

                Cts.Token.ThrowIfCancellationRequested ();

                bool changed = UpdateImapSetting (mailKitFolder, ref folder);

                var uidstring = uids.ToString ();
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    if (uidstring != target.ImapUidSet) {
                        target.ImapUidSet = uidstring;
                        changed = true;
                    }
                    target.ImapLastExamine = DateTime.UtcNow;
                    return true;
                });
                McPending.MakeEligibleOnFMetaData (folder);
                return changed;
            }
        }

        /// <summary>
        /// Copies the filtered stream.
        /// </summary>
        /// <param name="inStream">In stream.</param>
        /// <param name="outStream">Out stream.</param>
        /// <param name="inCharSet">Input Char set.</param>
        /// <param name="TransferEncoding">Transfer encoding.</param>
        /// <param name="func">Func.</param>
        /// <param name="outCharSet">Output Char set (default "utf-8").</param>
        protected void CopyFilteredStream (Stream inStream, Stream outStream,
            string inCharSet, string TransferEncoding, Action<Stream, Stream> func,
            string outCharSet = "utf-8")
        {
            using (var filtered = new FilteredStream (outStream)) {
                filtered.Add (DecoderFilter.Create (TransferEncoding));
                if (!string.IsNullOrEmpty (inCharSet)) {
                    try {
                        filtered.Add (new CharsetFilter (inCharSet, outCharSet));
                    } catch (NotSupportedException ex) {
                        // Seems to be a xamarin bug: https://bugzilla.xamarin.com/show_bug.cgi?id=30709
                        Log.Error (Log.LOG_IMAP, "Could not Add CharSetFilter for CharSet {0}\n{1}", inCharSet, ex);
                        // continue without the filter
                    } catch (ArgumentException ex) {
                        // Seems to be a xamarin bug: https://bugzilla.xamarin.com/show_bug.cgi?id=30709
                        Log.Error (Log.LOG_IMAP, "Could not Add CharSetFilter for CharSet {0}\n{1}", inCharSet, ex);
                        // continue without the filter
                    }
                }
                func (inStream, filtered);
            }
        }

        protected void ReportCommResult (string host, bool didFailGenerally)
        {
            if (!DontReportCommResult) {
                NcCommStatusSingleton.ReportCommResult (BEContext.Account.Id, McAccount.AccountCapabilityEnum.EmailReaderWriter, didFailGenerally);
            }
        }

        #region Html2text

        public static string Html2Text (string html)
        {
            HtmlDocument doc = new HtmlDocument ();
            doc.LoadHtml (html);

            StringWriter sw = new StringWriter ();
            ConvertTo (doc.DocumentNode, sw);
            sw.Flush ();
            return sw.ToString ();
        }

        public static string Html2Text (Stream html)
        {
            HtmlDocument doc = new HtmlDocument ();
            doc.Load (html);

            StringWriter sw = new StringWriter ();
            ConvertTo (doc.DocumentNode, sw);
            sw.Flush ();
            return sw.ToString ();
        }

        public static void ConvertTo (HtmlNode node, TextWriter outText)
        {
            string html;
            switch (node.NodeType) {
            case HtmlNodeType.Comment:
                // don't output comments
                break;

            case HtmlNodeType.Document:
                ConvertContentTo (node, outText);
                break;

            case HtmlNodeType.Text:
                // script and style must not be output
                string parentName = node.ParentNode.Name;
                if ((parentName == "script") || (parentName == "style"))
                    break;

                // get text
                html = ((HtmlTextNode)node).Text;

                // is it in fact a special closing node output as text?
                if (HtmlNode.IsOverlappedClosingElement (html))
                    break;

                // check the text is meaningful and not a bunch of whitespaces
                if (html.Trim ().Length > 0) {
                    outText.Write (HtmlEntity.DeEntitize (html));
                }
                break;

            case HtmlNodeType.Element:
                switch (node.Name) {
                case "p":
                    // treat paragraphs as crlf
                    outText.Write ("\r\n");
                    break;
                }

                if (node.HasChildNodes) {
                    ConvertContentTo (node, outText);
                }
                break;
            }
        }

        public static void ConvertContentTo (HtmlNode node, TextWriter outText)
        {
            foreach (HtmlNode subnode in node.ChildNodes) {
                ConvertTo (subnode, outText);
            }
        }

        #endregion

        protected bool IsComcast (McServer server)
        {
            return server.Host.EndsWith (".comcast.net") ||
                server.Host.EndsWith (".comcast.com");
        }
    }

    public class ImapWaitCommand : ImapCommand
    {
        NcCommand WaitCommand;
        public ImapWaitCommand (IBEContext dataSource, NcImapClient imap, int duration, bool earlyOnECChange) : base (dataSource, imap)
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
