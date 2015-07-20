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

namespace NachoCore.IMAP
{
    public class ImapCommand : NcCommand
    {
        protected NcImapClient Client { get; set; }
        protected RedactProtocolLogFuncDel RedactProtocolLogFunc;

        public ImapCommand (IBEContext beContext, NcImapClient imapClient) : base (beContext)
        {
            Client = imapClient;
            RedactProtocolLogFunc = null;
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
            // FIXME - not a long term soln. There are issues with MailKit and cancellation.
            lock (Client.SyncRoot) {
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
            ImapDiscoverCommand.guessServiceType (BEContext);

            lock(Client.SyncRoot) {
                try {
                    if (null != RedactProtocolLogFunc) {
                        Client.MailKitProtocolLogger.Start (RedactProtocolLogFunc);
                    }
                    if (!Client.IsConnected || !Client.IsAuthenticated) {
                        var authy = new ImapAuthenticateCommand (BEContext, Client);
                        authy.ConnectAndAuthenticate ();
                    }
                    using (var cap = NcCapture.CreateAndStart (this.GetType ().Name)) {
                        var evt = ExecuteCommand ();
                        cap.Stop ();
                        return evt;
                    }
                } finally {
                    if (Client.MailKitProtocolLogger.Enabled ()) {
                        ProtocolLoggerStopAndPostTelemetry ();
                    }
                }
            }
        }

        public void ExecuteNoTask(NcStateMachine sm)
        {
            try {
                Event evt = ExecuteConnectAndAuthEvent();
                // In the no-exception case, ExecuteCommand is resolving McPending.
                sm.PostEvent (evt);
            } catch (OperationCanceledException) {
                Log.Info (Log.LOG_IMAP, "OperationCanceledException");
                ResolveAllDeferred ();
                // No event posted to SM if cancelled.
            } catch (ServiceNotConnectedException) {
                // FIXME - this needs to feed into NcCommStatus, not loop forever.
                Log.Info (Log.LOG_IMAP, "ServiceNotConnectedException");
                ResolveAllDeferred ();
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.ReDisc, "IMAPCONN");
            } catch (AuthenticationException) {
                Log.Info (Log.LOG_IMAP, "AuthenticationException");
                ResolveAllDeferred ();
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH1");
            } catch (ServiceNotAuthenticatedException) {
                Log.Info (Log.LOG_IMAP, "ServiceNotAuthenticatedException");
                ResolveAllDeferred ();
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.AuthFail, "IMAPAUTH2");
            } catch (ImapCommandException ex) {
                Log.Info (Log.LOG_IMAP, "ImapCommandException {0}", ex.Message);
                ResolveAllDeferred ();
                sm.PostEvent ((uint)ImapProtoControl.ImapEvt.E.Wait, "IMAPCOMMWAIT", 60);
            } catch (IOException ex) {
                Log.Info (Log.LOG_IMAP, "IOException: {0}", ex.ToString ());
                ResolveAllDeferred ();
                sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPIO");
            } catch (SocketException ex) {
                // We check the server connectivity pretty well in Discovery. If this happens with
                // other commands, it's probably a temporary failure.
                Log.Error (Log.LOG_IMAP, "SocketException: {0}", ex.Message);
                ResolveAllDeferred ();
                sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPCONNTEMPAUTH");
            } catch (InvalidOperationException ex) {
                Log.Error (Log.LOG_IMAP, "InvalidOperationException: {0}", ex.ToString ());
                ResolveAllFailed (NcResult.WhyEnum.ProtocolError);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPHARD1");
            } catch (Exception ex) {
                Log.Error (Log.LOG_IMAP, "Exception : {0}", ex.ToString ());
                ResolveAllFailed (NcResult.WhyEnum.Unknown);
                sm.PostEvent ((uint)SmEvt.E.HardFail, "IMAPHARD2");
            }
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

        protected bool CreateOrUpdateFolder (IMailFolder mailKitFolder, ActiveSync.Xml.FolderHierarchy.TypeCode folderType, string folderDisplayName, bool isDisinguished, out McFolder folder)
        {
            bool added_or_changed = false;
            var ParentId = GetParentId (mailKitFolder);
            if (isDisinguished) {
                folder = McFolder.GetDistinguishedFolder (BEContext.Account.Id, folderType);
            } else {
                folder = McFolder.GetUserFolders (BEContext.Account.Id, folderType, ParentId, mailKitFolder.Name).SingleOrDefault ();
            }

            // If more than UidValidity is needed, add it here.
            if (!mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect)) {
                StatusItems items = StatusItems.UidValidity;
                mailKitFolder.Status (items);
            }

            if ((null != folder) && (folder.ImapUidValidity != mailKitFolder.UidValidity)) {
                Log.Info (Log.LOG_IMAP, "Deleting folder {0} due to UidValidity ({1} != {2})", folder.ImapFolderNameRedacted (), folder.ImapUidValidity, mailKitFolder.UidValidity.ToString ());
                folder.Delete ();
                folder = null;
            }

            if (null == folder) {
                // Add it
                folder = McFolder.Create (BEContext.Account.Id, false, false, isDisinguished, ParentId, mailKitFolder.FullName, mailKitFolder.Name, folderType);
                folder.ImapUidValidity = mailKitFolder.UidValidity;
                folder.ImapNoSelect = mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect);
                folder.Insert ();
                added_or_changed = true;
            } else if (folder.ServerId != mailKitFolder.FullName ||
                folder.DisplayName != folderDisplayName ||
                folder.ParentId != ParentId ||
                folder.ImapUidValidity != mailKitFolder.UidValidity) {
                // update.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ServerId = mailKitFolder.FullName;
                    target.DisplayName = folderDisplayName;
                    target.ParentId = ParentId;
                    target.ImapUidValidity = mailKitFolder.UidValidity;
                    return true;
                });
                added_or_changed = true;
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
            using (var cap = NcCapture.CreateAndStart ("IMAP Folder Metadata")) {
                // Just load UID with SELECT.
                Log.Info (Log.LOG_IMAP, "ImapSyncCommand {0}: Getting Folderstate", folder.ImapFolderNameRedacted ());

                var query = SearchQuery.NotDeleted;
                if (TimeSpan.Zero != timespan) {
                    query = query.And (SearchQuery.DeliveredAfter (DateTime.UtcNow.Subtract (timespan)));
                }
                UniqueIdSet uids = SyncKit.MustUniqueIdSet (mailKitFolder.Search (query, Cts.Token));

                Cts.Token.ThrowIfCancellationRequested ();

                Log.Info (Log.LOG_IMAP, "{1}: Uids from last {2} days: {0}",
                    uids.ToString (),
                    folder.ImapFolderNameRedacted (), TimeSpan.Zero == timespan ? "Forever" : timespan.Days.ToString ());

                UpdateImapSetting (mailKitFolder, ref folder);

                Cts.Token.ThrowIfCancellationRequested ();

                if (!string.IsNullOrEmpty (folder.ImapUidSet)) {
                    UniqueIdSet current;
                    if (UniqueIdSet.TryParse (folder.ImapUidSet, out current)) {
                        var added = new UniqueIdSet (uids.Except (current));
                        var removed = new UniqueIdSet (current.Except (uids));
                        if (added.Any ()) {
                            Log.Info (Log.LOG_IMAP, "{0}: Added UIDs: {1}", folder.ImapFolderNameRedacted (), added.ToString ());
                        }
                        if (removed.Any ()) {
                            Log.Info (Log.LOG_IMAP, "{0}: Removed UIDs: {1}", folder.ImapFolderNameRedacted (), removed.ToString ());
                        }
                    }
                }

                Cts.Token.ThrowIfCancellationRequested ();

                var uidstring = uids.ToString ();
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    if (uidstring != target.ImapUidSet) {
                        Log.Info (Log.LOG_IMAP, "Updating ImapUidSet");
                        target.ImapUidSet = uidstring;
                    }
                    target.ImapLastExamine = DateTime.UtcNow;
                    return true;
                });
                cap.Stop ();
                return true;
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
