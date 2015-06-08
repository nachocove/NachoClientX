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

namespace NachoCore.IMAP
{
    public class ImapCommand : NcCommand
    {
        protected ImapClient Client { get; set; }

        public ImapCommand (IBEContext beContext) : base (beContext)
        {
            Client = ((ImapProtoControl)BEContext.ProtoControl).ImapClient;
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
            }, "ImapCommand");
        }

        public void ExecuteNoTask(NcStateMachine sm)
        {
            try {
                if (!Client.IsConnected || !Client.IsAuthenticated) {
                    var authy = new ImapAuthenticateCommand (BEContext);
                    lock(Client.SyncRoot) {
                        authy.ConnectAndAuthenticate ();
                    }
                }
                var evt = ExecuteCommand ();
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
            } catch (IOException ex) {
                Log.Info (Log.LOG_IMAP, "IOException: {0}", ex.ToString ());
                ResolveAllDeferred ();
                sm.PostEvent ((uint)SmEvt.E.TempFail, "IMAPIO");
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

        protected IMailFolder GetOpenMailkitFolder(McFolder folder, FolderAccess access = FolderAccess.ReadOnly)
        {
            IMailFolder mailKitFolder;
            mailKitFolder = Client.GetFolder (folder.ServerId);
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

            if ((null != folder) && (folder.ImapUidValidity < mailKitFolder.UidValidity)) {
                Log.Info (Log.LOG_IMAP, "Deleting folder {0} due to UidValidity ({1} < {2})", mailKitFolder.FullName, folder.ImapUidValidity, mailKitFolder.UidValidity.ToString ());
                folder.Delete ();
                folder = null;
            }

            if (null == folder) {
                // Add it
                folder = McFolder.Create (BEContext.Account.Id, false, false, isDisinguished, ParentId, mailKitFolder.FullName, mailKitFolder.Name, folderType);
                folder.ImapUidValidity = mailKitFolder.UidValidity;
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

        protected bool UpdateImapSetting (IMailFolder mailKitFolder, McFolder folder)
        {
            bool changed = false;
            ulong hmodseq = mailKitFolder.SupportsModSeq ? mailKitFolder.HighestModSeq : 0;
            if (folder.ImapNoSelect != mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect) ||
                (mailKitFolder.UidNext.HasValue && folder.ImapUidNext != mailKitFolder.UidNext.Value.Id) ||
                (ulong)folder.CurImapHighestModSeq != hmodseq)
            {
                // update.
                folder = folder.UpdateWithOCApply<McFolder> ((record) => {
                    var target = (McFolder)record;
                    target.ImapNoSelect = mailKitFolder.Attributes.HasFlag (FolderAttributes.NoSelect);
                    target.CurImapHighestModSeq = (long)hmodseq;
                    target.ImapUidNext = mailKitFolder.UidNext.HasValue ? mailKitFolder.UidNext.Value.Id : 0;
                    return true;
                });
                changed = true;
            }
            return changed;
        }

    }

    public class ImapWaitCommand : ImapCommand
    {
        NcCommand WaitCommand;
        public ImapWaitCommand (IBEContext dataSource, int duration, bool earlyOnECChange) : base (dataSource)
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
