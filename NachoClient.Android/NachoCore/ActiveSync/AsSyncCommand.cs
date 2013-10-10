using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Xml.Linq;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.ActiveSync
{
	public class AsSyncCommand : AsCommand
	{
		public AsSyncCommand (IAsDataSource dataSource) : base(Xml.AirSync.Sync, Xml.AirSync.Ns, dataSource) {}

		protected override XDocument ToXDocument () {
			var collections = new XElement (m_ns+Xml.AirSync.Collections);
			var folders = FoldersNeedingSync ();
			foreach (var folder in folders) {
				var collection = new XElement (m_ns + Xml.AirSync.Collection,
				                              new XElement (m_ns + Xml.AirSync.SyncKey, folder.AsSyncKey),
				                              new XElement (m_ns + Xml.AirSync.CollectionId, folder.ServerId));
				if (Xml.AirSync.SyncKey_Initial != folder.AsSyncKey) {
					collection.Add (new XElement (m_ns + Xml.AirSync.GetChanges));
					// If there are email deletes, then push them up to the server.
					var deles = m_dataSource.Owner.Db.Table<NcPendingUpdate> ()
						.Where (x => x.AccountId == m_dataSource.Account.Id &&
						        x.FolderId == folder.Id &&
						        x.Operation == NcPendingUpdate.Operations.Delete &&
						        x.DataType == NcPendingUpdate.DataTypes.EmailMessage);
					if (0 != deles.Count ()) {
						var commands = new XElement (m_ns + Xml.AirSync.Commands);
						foreach (var change in deles) {
							commands.Add (new XElement (m_ns + Xml.AirSync.Delete,
							                            new XElement (m_ns + Xml.AirSync.ServerId, change.ServerId)));
							change.IsDispatched = true;
							m_dataSource.Owner.Db.Update (BackEnd.DbActors.Proto, change);
						}
						collection.Add (commands);
					}
				}
				collections.Add (collection);
			}
			var sync = new XElement (m_ns+Xml.AirSync.Sync, collections);
			var doc = AsCommand.ToEmptyXDocument();
			doc.Add (sync);
			return doc;
		}
		protected override uint ProcessResponse (HttpResponseMessage response, XDocument doc)
		{
			var collections = doc.Root.Element (m_ns + Xml.AirSync.Collections).Elements (m_ns + Xml.AirSync.Collection);
			foreach (var collection in collections) {
				var serverId = collection.Element (m_ns + Xml.AirSync.CollectionId).Value;
				var folder = m_dataSource.Owner.Db.Table<NcFolder> ().Single (rec => rec.AccountId == m_dataSource.Account.Id &&
				                                                              rec.ServerId == serverId);
				var oldSyncKey = folder.AsSyncKey;
				folder.AsSyncKey = collection.Element (m_ns + Xml.AirSync.SyncKey).Value;

				folder.AsSyncRequired = (Xml.AirSync.SyncKey_Initial == oldSyncKey) ||
					(null != collection.Element (m_ns + Xml.AirSync.MoreAvailable));
				Console.WriteLine ("MoreAvailable presence {0}", (null != collection.Element (m_ns + Xml.AirSync.MoreAvailable)));
				Console.WriteLine ("Folder:{0}, Old SyncKey:{1}, New SyncKey:{2}", 
				                   folder.ServerId.ToString (), oldSyncKey, folder.AsSyncKey);
				switch (uint.Parse(collection.Element (m_ns + Xml.AirSync.Status).Value)) {
				case (uint)Xml.AirSync.StatusCode.Success:
					// Clear any deletes dispached in the request.
					var deles = m_dataSource.Owner.Db.Table<NcPendingUpdate> ()
						.Where (x => x.AccountId == m_dataSource.Account.Id &&
						x.FolderId == folder.Id &&
						x.Operation == NcPendingUpdate.Operations.Delete &&
						x.DataType == NcPendingUpdate.DataTypes.EmailMessage &&
						x.IsDispatched == true);
					if (0 != deles.Count ()) {
						foreach (var change in deles) {
							m_dataSource.Owner.Db.Delete (BackEnd.DbActors.Proto, change);
						}
					}
					// Perform all commands.
					var commandsNode = collection.Element (m_ns + Xml.AirSync.Commands);
					if (null != commandsNode) {
						var commands = commandsNode.Elements ();
						foreach (var command in commands) {
							switch (command.Name.LocalName) {
								case Xml.AirSync.Add:
								// If the Class element is present, respect it. Otherwise key off
								// the type of the folder.
								// FIXME - 12.1-isms.
								var xmlClass = command.Element (m_ns + Xml.AirSync.Class);
								string classCode;
								if (null != xmlClass) {
									classCode = xmlClass.Value;
								} else {
									classCode = Xml.FolderHierarchy.TypeCodeToAirSyncClassCode (folder.Type);
								}
								switch (classCode) {
									case Xml.AirSync.ClassCode.Contacts:
									AddContact (command, folder);
									break;
									case Xml.AirSync.ClassCode.Email:
									AddEmail (command, folder);
									break;
								}
								break;
							}
						}
					}
					break;
					// FIXME - other status code values.
				}

				m_dataSource.Owner.Db.Update (BackEnd.DbActors.Proto, folder);
			}
			return (FoldersNeedingSync ().Any ()) ? (uint)AsProtoControl.Lev.ReSync : (uint)Ev.Success;
		}

		private SQLite.TableQuery<NcFolder> FoldersNeedingSync () {
			// FIXME - we need strategy on what folders to sync & when.
			return m_dataSource.Owner.Db.Table<NcFolder> ().Where (x => x.AccountId == m_dataSource.Account.Id &&
			                                                       true == x.AsSyncRequired &&
			                                                       ((uint)Xml.FolderHierarchy.TypeCode.DefaultInbox == x.Type ||
			 														(uint)Xml.FolderHierarchy.TypeCode.DefaultContacts == x.Type ||
			 														(uint)Xml.FolderHierarchy.TypeCode.UserCreatedContacts == x.Type));
		}

		// FIXME - these XML-to-object coverters suck! Use reflection & naming convention?
		private void AddEmail (XElement command, NcFolder folder) {
			IEnumerable<XElement> xmlAttachments = null;
			var emailMessage = new NcEmailMessage {
				AccountId = m_dataSource.Account.Id,
				FolderId = folder.Id,
				ServerId = command.Element(m_ns + Xml.AirSync.ServerId).Value
			};
			var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
			foreach (var child in appData.Elements()) {
				switch (child.Name.LocalName) {
					case Xml.AirSyncBase.Attachments:
					xmlAttachments = child.Elements (m_baseNs + Xml.AirSyncBase.Attachment);
					break;
					case Xml.AirSyncBase.Body:
					emailMessage.Encoding = child.Element (m_baseNs + Xml.AirSyncBase.Type).Value;
					var body = child.Element (m_baseNs + Xml.AirSyncBase.Data);
					// NOTE: We have seen EstimatedDataSize of 0 and no Truncate here.
					if (null != body) {
						emailMessage.Body = body.Value;
					}
					break;
					case Xml.Email.To:
					emailMessage.To = child.Value;
					break;
					case Xml.Email.From:
					emailMessage.From = child.Value;
					break;
					case Xml.Email.ReplyTo:
					emailMessage.ReplyTo = child.Value;
					break;
					case Xml.Email.Subject:
					emailMessage.Subject = child.Value;
					break;
					case Xml.Email.DateReceived:
					try {
						emailMessage.DateReceived = DateTime.Parse (child.Value);
					} catch {
						// FIXME - just log it.
					}
					break;
					case Xml.Email.DisplayTo:
					emailMessage.DisplayTo = child.Value;
					break;
					case Xml.Email.Importance:
					try {
						emailMessage.Importance = UInt32.Parse (child.Value);
					} catch {
						// FIXME - just log it.
					}
					break;
					case Xml.Email.Read:
					if ("1" == child.Value) {
						emailMessage.Read = true;
					} else {
						emailMessage.Read = false;
					}
					break;
					case Xml.Email.MessageClass:
					emailMessage.MessageClass = child.Value;
					break;
				}
			}
			m_dataSource.Owner.Db.Insert (BackEnd.DbActors.Proto, emailMessage);
			if (null != xmlAttachments) {
				foreach (XElement xmlAttachment in xmlAttachments) {
					if ((uint)Xml.AirSyncBase.MethodCode.NormalAttachment !=
						uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.Method).Value)) {
						continue;
					}
					// Create & save the attachment record.
					var attachment = new NcAttachment {
						AccountId = emailMessage.AccountId,
						EmailMessageId = emailMessage.Id,
						IsDownloaded = false,
						IsInline = false,
						FileReference = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.FileReference).Value,
						DataSize = uint.Parse (xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.EstimatedDataSize).Value)
					};
					var contentLocation = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.ContentLocation);
					if (null != contentLocation) {
						attachment.ContentLocation = contentLocation.Value;
					}
					var isInline = xmlAttachment.Element (m_baseNs + Xml.AirSyncBase.IsInline);
					if (null != isInline) {
						attachment.IsInline = ParseXmlBoolean (isInline);
					}
					m_dataSource.Owner.Db.Insert (BackEnd.DbActors.Proto, attachment);
					/*
					 * DON'T do this here. Download attachments strategically.
					// Create & save the pending update record.
					var update = new NcPendingUpdate {
						Operation = NcPendingUpdate.Operations.Download,
						DataType = NcPendingUpdate.DataTypes.Attachment,
						AccountId = emailMessage.AccountId,
						IsDispatched = false,
						AttachmentId = attachment.Id
					};
					m_dataSource.Owner.Db.Insert (BackEnd.DbActors.Proto, update);
					*/
				}
			}
		}

		private void AddContact (XElement command, NcFolder folder) {
			var contact = new NcContact {
				AccountId = m_dataSource.Account.Id,
				FolderId = folder.Id,
				ServerId = command.Element(m_ns + Xml.AirSync.ServerId).Value
			};
			var appData = command.Element (m_ns + Xml.AirSync.ApplicationData);
			foreach (var child in appData.Elements()) {
				switch (child.Name.LocalName) {
				case Xml.Contacts.LastName:
					contact.LastName = child.Value;
					break;
				case Xml.Contacts.FirstName:
					contact.FirstName = child.Value;
					break;
				case Xml.Contacts.Email1Address:
					contact.Email1Address = child.Value;
					break;
				case Xml.Contacts.MobilePhoneNumber:
					contact.MobilePhoneNumber = child.Value;
					break;
				}
			}
			m_dataSource.Owner.Db.Insert (BackEnd.DbActors.Proto, contact);
		}

		// FIXME - make this a generic extension.
		private bool ParseXmlBoolean (XElement bit) {
			if (bit.IsEmpty) {
				return true;
			}
			switch (bit.Value) {
			case "0":
				return false;
			case "1":
				return true;
			default:
				throw new Exception ();
			}
		}
	}
}
