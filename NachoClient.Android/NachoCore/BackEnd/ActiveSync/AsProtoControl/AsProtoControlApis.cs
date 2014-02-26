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

		private string SmartEmailCmd (McPending.Operations Op, int newEmailMessageId, int refdEmailMessageId,
			int folderId, bool originalEmailIsEmbedded)
		{
			if (originalEmailIsEmbedded && 14.0 > Convert.ToDouble (ProtocolState.AsProtocolVersion)) {
				return SendEmailCmd (newEmailMessageId);
			}

			McEmailMessage refdEmailMessage;
			McFolder folder;

			refdEmailMessage = McEmailMessage.QueryById (refdEmailMessageId);
			folder = McFolder.QueryById (folderId);
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
			var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
			if (null == emailMessage) {
				return null;
			}

			var folders = McFolder.QueryByItemId (Account.Id, emailMessageId);
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
			emailMessage.DeleteBody ();
			emailMessage.Delete ();
			StatusInd (NcResult.Info (NcResult.SubKindEnum.Info_EmailMessageSetChanged));
			Task.Run (delegate {
				Sm.PostAtMostOneEvent ((uint)AsEvt.E.ReSync, "ASPCDELMSG");
			});
			return deleUpdate.Token;
		}

		public override string MoveItemCmd (int emailMessageId, int destFolderId)
		{
			var emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
			if (null == emailMessage) {
				return null;
			}
			var destFolder = BackEnd.Instance.Db.Table<McFolder> ().SingleOrDefault (x => destFolderId == x.Id);
			if (null == destFolder) {
				return null;
			}
			var srcFolders = McFolder.QueryByItemId (Account.Id, emailMessageId);
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

		private bool GetEmailMessageAndFolder (int emailMessageId, 
			out McEmailMessage emailMessage,
			out McFolder folder)
		{
			folder = null;
			emailMessage = BackEnd.Instance.Db.Table<McEmailMessage> ().SingleOrDefault (x => emailMessageId == x.Id);
			if (null == emailMessage) {
				return false;
			}

			var folders = McFolder.QueryByItemId (Account.Id, emailMessageId);
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
			if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
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
			if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
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
			if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
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
			if (!GetEmailMessageAndFolder (emailMessageId, out emailMessage, out folder)) {
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
			var att = BackEnd.Instance.Db.Table<McAttachment> ().SingleOrDefault (x => x.Id == attId);
			if (null == att || att.IsDownloaded) {
				return null;
			}
			var update = new McPending {
				Operation = McPending.Operations.AttachmentDownload,
				AccountId = AccountId,
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

		public override string CreateFolderCmd (int destFolderId, string displayName, uint folderType,
			bool IsClientOwned, bool isHidden)
		{
			return null;
		}

		public override string CreateFolderCmd (string DisplayName, uint folderType,
			bool IsClientOwned, bool isHidden)
		{
			return null;
		}

		public override string DeleteFolderCmd (int folderId)
		{
			return null;
		}

		public override string MoveFolder (int folderId, int destFolderId)
		{
			return null;
		}

		public override string RenameFolder (int folderId, string displayName)
		{
			return null;
		}
	}
}