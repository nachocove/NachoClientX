﻿//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
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

namespace NachoCore.IMAP
{
    public class ImapCommand : NcCommand
    {
        const int KAuthRetries = 2;

        protected NcImapClient Client { get; set; }
        protected RedactProtocolLogFuncDel RedactProtocolLogFunc;
        protected bool DontReportCommResult { get; set; }
        public INcCommStatus NcCommStatusSingleton { set; get; }

        private const string KCaptureFolderMetadata = "ImapCommand.FolderMetadata";

        public ImapCommand (IBEContext beContext, NcImapClient imapClient) : base (beContext)
        {
            Client = imapClient;
            RedactProtocolLogFunc = null;
            NcCommStatusSingleton = NcCommStatus.Instance;
            DontReportCommResult = this is ImapDiscoverCommand;
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
            if (!BEContext.ProtoControl.ForceStopped) {
                // Wait for the command to notice the cancellation and release the lock.
                // TODO MailKit is not always good about cancelling in a timely manner.
                // When MailKit is fixed, this code should be adjusted.
                try {
                    TryLock (Client.SyncRoot, KLockTimeout);
                } catch (CommandLockTimeOutException ex) {
                    Log.Error (Log.LOG_IMAP, "{0}.Cancel({1}): {2}", this.GetType ().Name, BEContext.Account.Id, ex.Message);
                }
            }
        }

        public override void Execute (NcStateMachine sm)
        {
            NcTask.Run (() => {
                ExecuteNoTask(sm);
            }, this.GetType ().Name);
        }

        public Event ExecuteConnectAndAuthEvent()
        {
            Cts.Token.ThrowIfCancellationRequested ();
            NcCapture.AddKind (this.GetType ().Name);
            ImapDiscoverCommand.guessServiceType (BEContext);

            return TryLock (Client.SyncRoot, KLockTimeout, () => {
                try {
                    if (null != RedactProtocolLogFunc && null != Client.MailKitProtocolLogger) {
                        Client.MailKitProtocolLogger.Start (RedactProtocolLogFunc);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        ConnectAndAuthenticate ();
                    }
                    using (var cap = NcCapture.CreateAndStart (this.GetType ().Name)) {
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
            Log.Info (Log.LOG_IMAP, "{0}({1}): Started", this.GetType ().Name, BEContext.Account.Id);
            try {
                evt = ExecuteConnectAndAuthEvent();
                // In the no-exception case, ExecuteCommand is resolving McPending.
                Cts.Token.ThrowIfCancellationRequested ();
            } catch (OperationCanceledException) {
                Log.Info (Log.LOG_IMAP, "OperationCanceledException");
                ResolveAllDeferred ();
                // No event posted to SM if cancelled.
                return;
            } catch (CommandLockTimeOutException ex) {
                Log.Error (Log.LOG_IMAP, "CommandLockTimeOutException: {0}", ex.Message);
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)SmEvt.E.TempFail, "IMAPLOKTIME");
            } catch (ServiceNotConnectedException) {
                // FIXME - this needs to feed into NcCommStatus, not loop forever.
                Log.Info (Log.LOG_IMAP, "ServiceNotConnectedException");
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.ReDisc, "IMAPCONN");
                serverFailedGenerally = true;
            } catch (AuthenticationException) {
                Log.Info (Log.LOG_IMAP, "AuthenticationException");
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH1");
            } catch (ServiceNotAuthenticatedException) {
                Log.Info (Log.LOG_IMAP, "ServiceNotAuthenticatedException");
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH2");
            } catch (ImapCommandException ex) {
                Log.Info (Log.LOG_IMAP, "ImapCommandException {0}", ex.Message);
                action = new Tuple<ResolveAction, NcResult.WhyEnum> (ResolveAction.DeferAll, NcResult.WhyEnum.Unknown);
                evt = Event.Create ((uint)ImapProtoControl.ImapEvt.E.Wait, "IMAPCOMMWAIT", 60);
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
                ReportCommResult (BEContext.Server.Host, serverFailedGenerally);
            }
            if (Cts.Token.IsCancellationRequested) {
                Log.Info (Log.LOG_IMAP, "{0}({1}): Cancelled", this.GetType ().Name, BEContext.Account.Id);
                return;
            }
            Log.Info (Log.LOG_IMAP, "{0}({1}): Finished", this.GetType ().Name, BEContext.Account.Id);
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
                Client.Connect (BEContext.Server.Host, BEContext.Server.Port, true, Cts.Token);
                Log.Info (Log.LOG_IMAP, "IMAP Server: {0}:{1}", BEContext.Server.Host, BEContext.Server.Port);
                var capUnauth = McProtocolState.FromImapCapabilities (Client.Capabilities);

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
                string username = BEContext.Cred.Username;
                string cred;
                if (BEContext.Cred.CredType == McCred.CredTypeEnum.OAuth2) {
                    Client.AuthenticationMechanisms.RemoveWhere ((m) => !m.Contains ("XOAUTH2"));
                    cred = BEContext.Cred.GetAccessToken ();
                } else {
                    Client.AuthenticationMechanisms.RemoveWhere ((m) => m.Contains ("XOAUTH"));
                    cred = BEContext.Cred.GetPassword ();
                }

                Exception ex = null;
                for (var i = 0; i < KAuthRetries; i++) {
                    Cts.Token.ThrowIfCancellationRequested ();
                    try {
                        try {
                            Client.Authenticate (username, cred, Cts.Token);
                            break;
                        } catch (ImapProtocolException e) {
                            Log.Info (Log.LOG_IMAP, "Protocol Error during auth: {0}", e);
                            if (BEContext.ProtocolState.ImapServiceType == McAccount.AccountServiceEnum.iCloud) {
                                // some servers (icloud.com) seem to close the connection on a bad password/username.
                                throw new AuthenticationException (e.Message);
                            } else {
                                throw;
                            }
                        }
                    } catch (AuthenticationException e) {
                        ex = e;
                        Log.Info (Log.LOG_IMAP, "ConnectAndAuthenticate: AuthenticationException: (i={0}) {1}", i, e.Message);
                        continue;
                    } catch (ServiceNotAuthenticatedException e) {
                        ex = e;
                        Log.Info (Log.LOG_IMAP, "ConnectAndAuthenticate: ServiceNotAuthenticatedException: (i={0}) {1}", i, e.Message);
                        continue;
                    }
                }
                if (null != ex) {
                    throw ex;
                }

                Log.Info (Log.LOG_IMAP, "IMAP Server capabilities: {0}", Client.Capabilities.ToString ());
                var capAuth = McProtocolState.FromImapCapabilities (Client.Capabilities);
                if (capAuth != BEContext.ProtocolState.ImapServerCapabilities) {
                    BEContext.ProtocolState.UpdateWithOCApply<McProtocolState> ((record) => {
                        var target = (McProtocolState)record;
                        target.ImapServerCapabilities = capAuth;
                        return true;
                    });
                }

                ImapImplementation ourId = new ImapImplementation () {
                    Name = "Nacho Mail",
                    Version = string.Format ("{0}:{1}", BuildInfo.Version, BuildInfo.BuildNumber),
                    ReleaseDate = BuildInfo.Time,
                    SupportUrl = "https://support.nachocove.com/",
                    Vendor = "Nacho Cove, Inc",
                    OS = NachoPlatform.Device.Instance.BaseOs ().ToString (),
                    OSVersion = NachoPlatform.Device.Instance.Os (),
                };
                Log.Info (Log.LOG_IMAP, "Our Id: {0}", dumpImapImplementation(ourId));
                var serverId = Client.Identify (ourId, Cts.Token);
                Log.Info (Log.LOG_IMAP, "Server ID: {0}", dumpImapImplementation (serverId));
            }
        }

        private string dumpImapImplementation (ImapImplementation imapId)
        {
            return HashHelper.HashEmailAddressesInImapId (string.Join (", ", imapId.Properties));
        }

        protected void ProtocolLoggerStopAndPostTelemetry ()
        {
            string ClassName = this.GetType ().Name + " ";
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

        protected bool CreateOrUpdateFolder (IMailFolder mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode folderType, string folderDisplayName, bool isDisinguished, bool doFolderMetadata, out McFolder folder)
        {
            bool added_or_changed = false;
            var ParentId = GetParentId (mailKitFolder);
            if (isDisinguished) {
                folder = McFolder.GetDistinguishedFolder (BEContext.Account.Id, folderType);
            } else {
                folder = McFolder.GetUserFolders (BEContext.Account.Id, folderType, ParentId, mailKitFolder.Name).SingleOrDefault ();
            }

            if (!mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
                mailKitFolder.Open (FolderAccess.ReadOnly, Cts.Token);
            }

            if ((null != folder) && (folder.ImapUidValidity != mailKitFolder.UidValidity)) {
                Log.Warn (Log.LOG_IMAP, "CreateOrUpdateFolder: Deleting folder {0} due to UidValidity ({1} != {2})", folder.ImapFolderNameRedacted (), folder.ImapUidValidity, mailKitFolder.UidValidity.ToString ());
                folder.Delete ();
                folder = null;
            }

            if (null == folder) {
                // Add it
                folder = McFolder.Create (BEContext.Account.Id, false, false, isDisinguished, ParentId, mailKitFolder.FullName, mailKitFolder.Name, folderType);
                Log.Info (Log.LOG_IMAP, "CreateOrUpdateFolder: Adding folder {0} UidValidity {1}", folder.ImapFolderNameRedacted (), mailKitFolder.UidValidity.ToString ());
                folder.ImapUidValidity = mailKitFolder.UidValidity;
                folder.ImapNoSelect = mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect);
                folder.Insert ();
                added_or_changed = true;
            } else if (folder.ServerId != mailKitFolder.FullName ||
                folder.DisplayName != folderDisplayName ||
                folder.ParentId != ParentId ||
                folder.ImapUidValidity != mailKitFolder.UidValidity) {
                // update.
                Log.Info (Log.LOG_IMAP, "CreateOrUpdateFolder: Updating folder {0} UidValidity {1}", folder.ImapFolderNameRedacted (), mailKitFolder.UidValidity.ToString ());
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = mailKitFolder.FullName;
                    target.DisplayName = folderDisplayName;
                    target.ParentId = ParentId;
                    target.ImapUidValidity = mailKitFolder.UidValidity;
                    return true;
                });
                added_or_changed = true;
            } else {
                Log.Info (Log.LOG_IMAP, "CreateOrUpdateFolder: Folder {0} unchanged", folder.ImapFolderNameRedacted ());

            }

            // Get the current list of UID's. Don't set added_or_changed. Sync will notice later.
            if (doFolderMetadata && !mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
                Log.Info (Log.LOG_IMAP, "CreateOrUpdateFolder: Folder {0} updating metadata", folder.ImapFolderNameRedacted ());
                if (!GetFolderMetaData (ref folder, mailKitFolder, BEContext.Account.DaysSyncEmailSpan ())) {
                    Log.Error (Log.LOG_IMAP, "CreateOrUpdateFolder: Folder {0}: Could not refresh folder metadata", folder.ImapFolderNameRedacted ());
                }
            }

            return added_or_changed;
        }

        public static bool UpdateImapSetting (IMailFolder mailKitFolder, ref McFolder folder)
        {
            bool changed = false;
            if (folder.ImapNoSelect != mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect) ||
                (mailKitFolder.UidNext.HasValue && folder.ImapUidNext != mailKitFolder.UidNext.Value.Id))
            {
                // update.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapNoSelect = mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect);
                    target.ImapUidNext = mailKitFolder.UidNext.HasValue ? mailKitFolder.UidNext.Value.Id : 0;
                    target.ImapExists = mailKitFolder.Count;
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
        /// <returns><c>true</c>, if folder meta data was gotten, <c>false</c> otherwise.</returns>
        /// <param name="folder">Folder.</param>
        /// <param name="mailKitFolder">Mail kit folder.</param>
        /// <param name="timespan">Timespan.</param>
        public bool GetFolderMetaData (ref McFolder folder, IMailFolder mailKitFolder, TimeSpan timespan)
        {
            NcCapture.AddKind (KCaptureFolderMetadata);
            using (var cap = NcCapture.CreateAndStart (KCaptureFolderMetadata)) {
                // Just load UID with SELECT.
                Log.Info (Log.LOG_IMAP, "GetFolderMetaData: {0}: Getting Folderstate", folder.ImapFolderNameRedacted ());

                var query = SearchQuery.NotDeleted;
                if (TimeSpan.Zero != timespan) {
                    query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
                }
                UniqueIdSet uids = SyncKit.MustUniqueIdSet (mailKitFolder.Search (query, Cts.Token));

                Cts.Token.ThrowIfCancellationRequested ();

                Log.Info (Log.LOG_IMAP, "GetFolderMetaData: {1}: Uids from last {2} days: {0}",
                    uids.ToString (),
                    folder.ImapFolderNameRedacted (), TimeSpan.Zero == timespan ? "Forever" : timespan.Days.ToString ());

                UpdateImapSetting (mailKitFolder, ref folder);

                Cts.Token.ThrowIfCancellationRequested ();

                Cts.Token.ThrowIfCancellationRequested ();

                var uidstring = uids.ToString ();
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    if (uidstring != target.ImapUidSet) {
                        Log.Info (Log.LOG_IMAP, "GetFolderMetaData: Updating ImapUidSet");
                        target.ImapUidSet = uidstring;
                    }
                    target.ImapLastExamine = DateTime.UtcNow;
                    return true;
                });
                McPending.MakeEligibleOnFMetaData (folder);
                return true;
            }
        }

        protected void CopyFilteredStream (Stream inStream, Stream outStream, 
            string CharSet, string TransferEncoding, Action<Stream, Stream> func)
        {
            using (var filtered = new FilteredStream (outStream)) {
                filtered.Add (DecoderFilter.Create (TransferEncoding));
                if (!string.IsNullOrEmpty (CharSet)) {
                    try {
                        filtered.Add (new CharsetFilter (CharSet, "utf-8"));
                    } catch (NotSupportedException ex) {
                        // Seems to be a xamarin bug: https://bugzilla.xamarin.com/show_bug.cgi?id=30709
                        Log.Error (Log.LOG_IMAP, "Could not Add CharSetFilter for CharSet {0}\n{1}", CharSet, ex);
                        // continue without the filter
                    } catch (ArgumentException ex) {
                        // Seems to be a xamarin bug: https://bugzilla.xamarin.com/show_bug.cgi?id=30709
                        Log.Error (Log.LOG_IMAP, "Could not Add CharSetFilter for CharSet {0}\n{1}", CharSet, ex);
                        // continue without the filter
                    }
                }
                func (inStream, filtered);
            }
        }

        protected void ReportCommResult (string host, bool didFailGenerally)
        {
            if (!DontReportCommResult) {
                NcCommStatusSingleton.ReportCommResult (BEContext.Account.Id, host, didFailGenerally);
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
