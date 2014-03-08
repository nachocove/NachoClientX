//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using NachoCore.Model;
using NachoCore.Utils;
using NachoPlatform;

namespace NachoCore.ActiveSync
{
	public partial class AsProtoControl : ProtoControl, IAsDataSource
	{
		private void DeletePendingSearchReqs (string token, bool ignoreDispatched)
		{
			var query = BackEnd.Instance.Db.Table<McPending> ().Where (rec => rec.AccountId == Account.Id &&
			            rec.Token == token);
			if (ignoreDispatched) {
				query = query.Where (rec => false == rec.IsDispatched);
			}
			var killList = query.ToList ();
			foreach (var kill in killList) {
				kill.Delete ();
			}
		}

		public override string StartSearchContactsReq (string prefix, uint? maxResults)
		{
			var token = Guid.NewGuid ().ToString ();
			SearchContactsReq (prefix, maxResults, token);
			return token;
		}

		public override void SearchContactsReq (string prefix, uint? maxResults, string token)
		{
			DeletePendingSearchReqs (token, true);
			var newSearch = new McPending (Account.Id) {
				Operation = McPending.Operations.ContactSearch,
				Prefix = prefix,
				MaxResults = (null == maxResults) ? 0 : (uint)maxResults,
				Token = token
			};
			newSearch.Insert ();
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)CtlEvt.E.UiSearch, "ASPCSRCH");
			});
		}

		public override string SendEmailCmd (int emailMessageId)
		{
			var sendUpdate = new McPending (Account.Id) {
				Operation = McPending.Operations.EmailSend,
				EmailMessageId = emailMessageId
			};
			sendUpdate.Insert ();
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCSEND");
			});
			return sendUpdate.Token;
		}

        public override string SendEmailCmd (int emailMessageId, int calId)
        {
            var cal = McObject.QueryById<McCalendar> (calId);
            var emailMessage = McObject.QueryById<McEmailMessage> (emailMessageId);
            if (null == cal || null == emailMessage) {
                return null;
            }

            var pendingCalCre = BackEnd.Instance.Db.Table<McPending> ().LastOrDefault (x => calId == x.CalId);
            var pendingCalCreId = (null == pendingCalCre) ? 0 : pendingCalCre.Id;

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.EmailSend,
                EmailMessageId = emailMessageId,
                PredPendingId = pendingCalCreId,
            };

            pending.Insert ();

            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SendMail, "ASPCSENDCAL");
            });
            return pending.Token;
        }

		private string SmartEmailCmd (McPending.Operations Op, int newEmailMessageId, int refdEmailMessageId,
		                              int folderId, bool originalEmailIsEmbedded)
		{
			if (originalEmailIsEmbedded && 14.0 > Convert.ToDouble (ProtocolState.AsProtocolVersion)) {
				return SendEmailCmd (newEmailMessageId);
			}

			McEmailMessage refdEmailMessage;
			McFolder folder;

            refdEmailMessage = McObject.QueryById<McEmailMessage> (refdEmailMessageId);
            folder = McObject.QueryById<McFolder> (folderId);
			if (null == refdEmailMessage || null == folder) {
				return null;
			}

			var smartUpdate = new McPending (Account.Id) {
				Operation = Op,
				EmailMessageId = newEmailMessageId,
				ServerId = refdEmailMessage.ServerId,
				FolderServerId = folder.ServerId,
				OriginalEmailIsEmbedded = originalEmailIsEmbedded,
			};
			smartUpdate.Insert ();
			Task.Run (delegate {
				if (Op == McPending.Operations.EmailForward) {
					Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SFwdMail, "ASPCSMF");
				} else {
					Sm.PostAtMostOneEvent ((uint)CtlEvt.E.SRplyMail, "ASPCSMR");
				}
			});
			return smartUpdate.Token;
		}

		public override string ReplyEmailCmd (int newEmailMessageId, int repliedToEmailMessageId,
		                                      int folderId, bool originalEmailIsEmbedded)
		{
			return SmartEmailCmd (McPending.Operations.EmailReply,
				newEmailMessageId, repliedToEmailMessageId, folderId, originalEmailIsEmbedded);
		}

		public override string ForwardEmailCmd (int newEmailMessageId, int forwardedEmailMessageId,
		                                        int folderId, bool originalEmailIsEmbedded)
		{
			return SmartEmailCmd (McPending.Operations.EmailForward,
				newEmailMessageId, forwardedEmailMessageId, folderId, originalEmailIsEmbedded);
		}

		public override string DeleteEmailCmd (int emailMessageId)
		{
            var emailMessage = McObject.QueryById<McEmailMessage> (emailMessageId);
			if (null == emailMessage) {
				return null;
			}

            var folders = McFolder.QueryByItemId<McEmailMessage> (Account.Id, emailMessageId);
			if (null == folders || 0 == folders.Count) {
				return null;
			}

			var folder = folders.First ();

			var deleUpdate = new McPending (Account.Id) {
				Operation = McPending.Operations.EmailDelete,
				FolderServerId = folder.ServerId,
				ServerId = emailMessage.ServerId
			};   
			deleUpdate.Insert ();

			// Delete the actual item.
			var maps = BackEnd.Instance.Db.Table<McMapFolderItem> ().Where (x =>
				x.AccountId == Account.Id &&
			           x.ItemId == emailMessageId &&
			           x.ClassCode == (uint)McItem.ClassCodeEnum.Email);

			foreach (var map in maps) {
				map.Delete ();
			}
			emailMessage.Delete ();
			StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDELMSG");
			});
			return deleUpdate.Token;
		}

		public override string MoveItemCmd (int emailMessageId, int destFolderId)
		{
            var emailMessage = McObject.QueryById<McEmailMessage> (emailMessageId);
			if (null == emailMessage) {
				return null;
			}
            var destFolder = McObject.QueryById<McFolder> (destFolderId);
			if (null == destFolder) {
				return null;
			}
            var srcFolders = McFolder.QueryByItemId<McEmailMessage> (Account.Id, emailMessageId);
			if (null == srcFolders || 0 == srcFolders.Count) {
				return null;
			}
			var srcFolder = srcFolders.First ();
			var moveUpdate = new McPending (Account.Id) {
				Operation = McPending.Operations.EmailMove,
				EmailMessageServerId = emailMessage.ServerId,
				EmailMessageId = emailMessageId,
				FolderServerId = srcFolder.ServerId,
				DestFolderServerId = destFolder.ServerId,
			};

			moveUpdate.Insert ();
			// Move the actual item.
			var newMapEntry = new McMapFolderItem (Account.Id) {
				FolderId = destFolderId,
				ItemId = emailMessageId,
				ClassCode = (uint)McItem.ClassCodeEnum.Email,
			};
			newMapEntry.Insert ();

			var oldMapEntry = BackEnd.Instance.Db.Table<McMapFolderItem> ().Single (x =>
				x.AccountId == Account.Id &&
			                  x.ItemId == emailMessageId &&
			                  x.FolderId == srcFolder.Id &&
			                  x.ClassCode == (uint)McItem.ClassCodeEnum.Email);
			oldMapEntry.Delete ();

			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)CtlEvt.E.Move, "ASPCMOVMSG");
			});
			return moveUpdate.Token;
		}

        // FIXME - which folder? also move to Model.
        private bool GetItemAndFolder<T> (int itemId, 
            out T item,
            out McFolder folder) where T : McItem, new()
		{
			folder = null;
            item = McObject.QueryById<T> (itemId);
            if (null == item) {
				return false;
			}

            var folders = McFolder.QueryByItemId<T> (Account.Id, itemId);
			if (null == folders || 0 == folders.Count) {
				return false;
			}

			folder = folders.First ();
			return true;
		}

		public override string MarkEmailReadCmd (int emailMessageId)
		{
			McEmailMessage emailMessage;
			McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, out folder)) {
				return null;
			}

			var markUpdate = new McPending (Account.Id) {
				Operation = McPending.Operations.EmailMarkRead,
				ServerId = emailMessage.ServerId,
				FolderServerId = folder.ServerId,
			};   
			markUpdate.Insert ();

			// Mark the actual item.
			emailMessage.IsRead = true;
			emailMessage.Update ();
			StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageMarkedRead));
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCMRMSG");
			});
			return markUpdate.Token;
		}

		public override string SetEmailFlagCmd (int emailMessageId, string flagType, 
		                                        DateTime start, DateTime utcStart, DateTime due, DateTime utcDue)
		{
			McEmailMessage emailMessage;
			McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, out folder)) {
				return null;
			}

			var setFlag = new McPending (Account.Id) {
				Operation = McPending.Operations.EmailSetFlag,
				ServerId = emailMessage.ServerId,
				FolderServerId = folder.ServerId,
				FlagType = flagType,
				Start = start,
				UtcStart = utcStart,
				Due = due,
				UtcDue = utcDue,
			};
			setFlag.Insert ();

			// Set the Flag info in the DB item.
			emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Active;
			emailMessage.FlagType = flagType;
			emailMessage.FlagDeferUntil = start;
			emailMessage.FlagUtcDeferUntil = utcStart;
			emailMessage.FlagDue = due;
			emailMessage.FlagUtcDue = utcDue;
			emailMessage.Update ();
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCSF");
			});
			return setFlag.Token;
		}

		public override string ClearEmailFlagCmd (int emailMessageId)
		{
			McEmailMessage emailMessage;
			McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, out folder)) {
				return null;
			}

			var clearFlag = new McPending (Account.Id) {
				Operation = McPending.Operations.EmailClearFlag,
				ServerId = emailMessage.ServerId,
				FolderServerId = folder.ServerId,
			};
			clearFlag.Insert ();

			emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Cleared;
			emailMessage.Update ();
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCCF");
			});
			return clearFlag.Token;
		}

		public override string MarkEmailFlagDone (int emailMessageId,
		                                          DateTime completeTime, DateTime dateCompleted)
		{
			McEmailMessage emailMessage;
			McFolder folder;
            if (!GetItemAndFolder<McEmailMessage> (emailMessageId, out emailMessage, out folder)) {
				return null;
			}

			var markFlagDone = new McPending (Account.Id) {
				Operation = McPending.Operations.EmailMarkFlagDone,
				ServerId = emailMessage.ServerId,
				FolderServerId = folder.ServerId,
				CompleteTime = completeTime,
				DateCompleted = dateCompleted,
			};
			markFlagDone.Insert ();

			emailMessage.FlagStatus = (uint)McEmailMessage.FlagStatusValue.Complete;
			emailMessage.FlagCompleteTime = completeTime;
			emailMessage.FlagDateCompleted = dateCompleted;
			emailMessage.Update ();
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCCF");
			});
			return markFlagDone.Token;
		}

		public override string DnldAttCmd (int attId)
		{
            var att = McObject.QueryById<McAttachment> (attId);
            if (null == att) {
                return null;
			}
            if (att.IsDownloaded) {
                return null; // FIXME - need to say "done already".
            }
            var update = new McPending (Account.Id) {
				Operation = McPending.Operations.AttachmentDownload,
				IsDispatched = false,
				AttachmentId = attId,
			};
			update.Insert ();
			att.PercentDownloaded = 1;
			att.Update ();
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDNLDATT");
			});
			return update.Token;
		}

        public override string CreateCalCmd (int calId)
        {
            McCalendar cal;
            McFolder folder;
            if (!GetItemAndFolder<McCalendar> (calId, out cal, out folder)) {
                return null;
            }

            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.CalCreate,
                CalId = calId,
                FolderServerId = folder.ServerId,
                ClientId = cal.ClientId,
            };

            pending.Insert ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCCRECAL");
            });

            return pending.Token;
        }

        public override string RespondCalCmd (int calId, RespondCalEnum response)
        {
            McCalendar cal;
            McFolder folder;
            if (!GetItemAndFolder<McCalendar> (calId, out cal, out folder)) {
                return null;
            }
            var pending = new McPending (Account.Id) {
                Operation = McPending.Operations.CalRespond,
                ServerId = cal.ServerId,
                FolderServerId = folder.ServerId,
                CalResponse = (uint)response,
            };

            pending.Insert ();
            Task.Run (delegate {
                Sm.PostAtMostOneEvent ((uint)CtlEvt.E.CalResp, "ASPCRESPCAL");
            });

            return pending.Token;
        }

		public override string CreateFolderCmd (int destFolderId, string displayName, uint folderType,
		                                        bool isClientOwned, bool isHidden)
		{
			var serverId = DateTime.UtcNow.Ticks.ToString ();
			string destFldServerId;

			if (0 > destFolderId) {
				// Root case.
				destFldServerId = "0";
			} else {
				// Sub-folder case.
                var destFld = McObject.QueryById<McFolder> (destFolderId);
				if (null == destFld) {
					return null;
				}
				if (isClientOwned ^ destFld.IsClientOwned) {
					// Keep client/server-owned domains separate for now.
					return null;
				}
				destFldServerId = destFld.ServerId;
			}

			if (isHidden && !isClientOwned) {
				return null;
			}

			McFolder.Create (Account.Id,
				isClientOwned,
				isHidden,
				destFldServerId,
				serverId,
				displayName,
				folderType);

			if (isClientOwned) {
				return McPending.KSynchronouslyCompleted;
			}

			var createFolder = new McPending (Account.Id) {
				Operation = McPending.Operations.FolderCreate,
				ServerId = serverId,
				DestFolderServerId = destFldServerId,
				DisplayName = displayName,
				FolderType = folderType,
			};

			createFolder.Insert ();

			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)CtlEvt.E.FCre, "ASPCFCRE");
			});

			return createFolder.Token;
		}

		public override string CreateFolderCmd (string displayName, uint folderType,
		                                        bool isClientOwned, bool isHidden)
		{
			return CreateFolderCmd (-1, displayName, folderType, isClientOwned, isHidden);
		}

		public override string DeleteFolderCmd (int folderId)
		{
            var folder = McObject.QueryById<McFolder> (folderId);
			if (folder.IsClientOwned) {
				folder.Delete ();
				return McPending.KSynchronouslyCompleted;
			}

			var delFolder = new McPending (Account.Id) {
				Operation = McPending.Operations.FolderDelete,
				ServerId = folder.ServerId,
			};

			folder.Delete ();

			delFolder.Insert ();

			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)CtlEvt.E.FDel, "ASPCFDEL");
			});

			return delFolder.Token;
		}

		public override string MoveFolderCmd (int folderId, int destFolderId)
		{
            var folder = McObject.QueryById<McFolder> (folderId);
            var destFolder = McObject.QueryById<McFolder> (destFolderId);
			if (folder.IsClientOwned ^ destFolder.IsClientOwned) {
				return null;
			}

			folder.ParentId = destFolder.ServerId;
			folder.Update ();

			if (folder.IsClientOwned) {
				return McPending.KSynchronouslyCompleted;
			}

			var upFolder = new McPending (Account.Id) {
				Operation = McPending.Operations.FolderUpdate,
				ServerId = folder.ServerId,
				DestFolderServerId = destFolder.ServerId,
				DisplayName = folder.DisplayName,
			};

			upFolder.Insert ();

			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)CtlEvt.E.FUp, "ASPCFUP1");
			});

			return upFolder.Token;
		}

		public override string RenameFolderCmd (int folderId, string displayName)
		{
            var folder = McObject.QueryById<McFolder> (folderId);

			folder.DisplayName = displayName;
			folder.Update ();

			if (folder.IsClientOwned) {
				return McPending.KSynchronouslyCompleted;
			}

			var upFolder = new McPending (Account.Id) {
				Operation = McPending.Operations.FolderUpdate,
				ServerId = folder.ServerId,
				DestFolderServerId = folder.ParentId,
				DisplayName = displayName,
			};

			upFolder.Insert ();

			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)CtlEvt.E.FUp, "ASPCFUP2");
			});
			return upFolder.Token;
		}
	}
}