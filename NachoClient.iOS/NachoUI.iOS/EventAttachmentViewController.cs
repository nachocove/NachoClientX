// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using System.IO;
using SWRevealViewControllerBinding;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public partial class EventAttachmentViewController : NcUIViewController, IAttachmentTableViewSourceDelegate, INachoAttachmentListChooser, INachoFileChooserParent
    {
        public EventAttachmentViewController (IntPtr handle) : base (handle)
        {
        }
            
        protected AttachmentTableViewSource attachmentSource;
        protected McAccount account;
        protected McCalendar c;
        protected bool editing;
        protected INachoAttachmentListChooserDelegate owner;
        List<McAttachment> AttachmentsList = new List<McAttachment> ();

        UILabel emptyListLabel;

        protected static int SEGMENTED_CONTROL_TAG = 100;
        protected static float SCREEN_WIDTH = UIScreen.MainScreen.Bounds.Width;
        public void SetOwner (INachoAttachmentListChooserDelegate owner, List<McAttachment> attachments, McCalendar c, bool editing)
        {
            this.owner = owner;
            this.AttachmentsList = attachments;
            this.c = c;
            this.editing = editing;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();

            account = NcModel.Instance.Db.Table<McAccount> ().Where (x => x.AccountType == McAccount.AccountTypeEnum.Exchange).FirstOrDefault ();
            attachmentSource = new AttachmentTableViewSource ();
            attachmentSource.SetOwner (this);

            EventAttachmentsTableView.Source = attachmentSource;

            EventAttachmentsTableView.ReloadData ();
            CreateEventAttachmentView ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            if (null != this.NavigationController) {
                this.NavigationController.ToolbarHidden = true;
            }
            LoadAttachments ();
            ConfigureEventAttachmentView ();
        }

        public override void ViewDidAppear (bool animated)
        {
            base.ViewDidAppear (animated);
            EventAttachmentsTableView.ReloadData ();
        }

        public override void ViewWillDisappear (bool animated)
        {
            base.ViewWillDisappear (animated);
            owner.UpdateAttachmentList (this.AttachmentsList);
            NcApplication.Instance.StatusIndEvent -= StatusIndicatorCallback;
        }
            
        public override void PrepareForSegue (UIStoryboardSegue segue, NSObject sender)
        {
            if (segue.Identifier.Equals ("EventAttachmentToFiles")) {
                var dc = (FilesHierarchyViewController)segue.DestinationViewController;
                dc.SetOwner (this);
                return;
            }
                
            Log.Info (Log.LOG_UI, "Unhandled segue identifer {0}", segue.Identifier);
            NcAssert.CaseError ();
        }

        public void LoadAttachments ()
        {
            NachoClient.Util.HighPriority ();
            attachmentSource.SetAttachmentList (this.AttachmentsList);
            attachmentSource.SetAccount (account);
            attachmentSource.SetEditing (editing);
            EventAttachmentsTableView.ReloadData ();
            NachoClient.Util.RegularPriority ();
        }

        public void SetAttachmentsList (List<McAttachment> attachments)
        {
            this.AttachmentsList = new List<McAttachment> ();
            foreach (var attachment in attachments) {
                this.AttachmentsList.Add (attachment);
            }
        }

        public List<McAttachment> GetAttachmentsList ()
        {
            return this.AttachmentsList;
        }

        public void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;
            if (NcResult.SubKindEnum.Info_CalendarSetChanged == s.Status.SubKind) {
                Log.Debug (Log.LOG_UI, "StatusIndicatorCallback");
                EventAttachmentsTableView.ReloadData ();
            }
        }

        protected void CreateEventAttachmentView ()
        {
            if (editing) {
                NavigationItem.RightBarButtonItem = addAttachmentButton;
                addAttachmentButton.Clicked += (object sender, EventArgs e) => {
                    AttachFileActionSheet ();
                };
            } else {
                NavigationItem.RightBarButtonItem = null;
            }
            EventAttachmentsTableView.Frame = new RectangleF (0, 0, View.Frame.Width, View.Frame.Height);
            emptyListLabel = new UILabel (new RectangleF (0, 80, SCREEN_WIDTH, 20));
            emptyListLabel.TextAlignment = UITextAlignment.Center;
            emptyListLabel.Font = A.Font_AvenirNextDemiBold14;
            emptyListLabel.TextColor = A.Color_NachoSeparator;
            emptyListLabel.Hidden = true;
            View.AddSubview (emptyListLabel);
        }

        protected void ConfigureEventAttachmentView ()
        {
            if (0 == AttachmentsList.Count) {
                EventAttachmentsTableView.Hidden = true;
                emptyListLabel.Hidden = false;
                emptyListLabel.Text = "No attachments";
            } else {
                attachmentSource.SetAttachmentList (this.AttachmentsList);
                EventAttachmentsTableView.ReloadData ();
                EventAttachmentsTableView.Hidden = false;
                emptyListLabel.Hidden = true;
            }
        }
         
        protected void AttachFileActionSheet ()
        {
            var actionSheet = new UIActionSheet ();
            actionSheet.Add ("Add Photo");
            actionSheet.Add ("Add Attachment");
            actionSheet.Add ("Cancel");
            actionSheet.CancelButtonIndex = 2;

            actionSheet.Clicked += delegate(object sender, UIButtonEventArgs b) {
                switch (b.ButtonIndex) {
                case 0:
                    SetupPhotoPicker ();
                    break; 
                case 1:
                    if (null != owner) {
                        PerformSegue ("EventAttachmentToFiles", this);
                    }
                    break;
                case 2:

                    break;// Cancel
                default:
                    NcAssert.CaseError ();
                    break;
                }
            };
            actionSheet.ShowInView (View);
        }

        void SetupPhotoPicker ()
        {
            var imagePicker = new UIImagePickerController ();
            imagePicker.SourceType = UIImagePickerControllerSourceType.PhotoLibrary;
            imagePicker.FinishedPickingMedia += Handle_FinishedPickingMedia;
            imagePicker.Canceled += Handle_Canceled;
            imagePicker.ModalPresentationStyle = UIModalPresentationStyle.CurrentContext;
            this.PresentViewController (imagePicker, true, null);
        }

        void Handle_Canceled (object sender, EventArgs e)
        {
            var imagePicker = sender as UIImagePickerController;
            imagePicker.DismissViewController (true, null);
        }

        protected void Handle_FinishedPickingMedia (object sender, UIImagePickerMediaPickedEventArgs e)
        {
            var imagePicker = sender as UIImagePickerController;

            bool isImage = false;
            switch (e.Info [UIImagePickerController.MediaType].ToString ()) {
            case "public.image":
                isImage = true;
                break;
            case "public.video":
                // TODO: Implement videos
                Log.Info (Log.LOG_UI, "video ignored");
                break;
            default:
                // TODO: Implement videos
                Log.Error (Log.LOG_UI, "unknown media type selected");
                break;
            }

            if (isImage) {
                var image = e.Info [UIImagePickerController.EditedImage] as UIImage;
                if (null == image) {
                    image = e.Info [UIImagePickerController.OriginalImage] as UIImage;
                }
                NcAssert.True (null != image);
                var attachment = new McAttachment ();
                attachment.AccountId = account.Id;
                attachment.Insert ();
                attachment.DisplayName = attachment.Id.ToString () + ".jpg";
                var guidString = Guid.NewGuid ().ToString ("N");
                using (var stream = McAttachment.TempFileStream (guidString)) {
                    using (var jpg = image.AsJPEG ().AsStream ()) {
                        jpg.CopyTo (stream);
                        jpg.Close ();
                    }
                }
                attachment.SaveFromTemp (guidString);
                attachment.Update ();
                AttachmentsList.Add (attachment);
            }

            e.Info.Dispose ();
            imagePicker.DismissViewController (true, null);
        }

        public void RemoveAttachment (McAttachment attachment)
        {
            List<McAttachment> tempList = new List<McAttachment> ();
            foreach (var a in AttachmentsList) {
                if (a.Id != attachment.Id) {
                    tempList.Add (a);
                }
            }
            AttachmentsList = tempList;
            ConfigureEventAttachmentView ();
        }

        public void DeleteEmailAddress (NcEmailAddress address)
        {
            NcAssert.CaseError ();
        }

        public void DismissINachoContactChooser (INachoContactChooser vc)
        {
            NcAssert.CaseError ();
        }

        /// IContactsTableViewSourceDelegate
        public void PerformSegueForDelegate (string identifier, NSObject sender)
        {
            PerformSegue (identifier, sender);
        }

        /// IContactsTableViewSourceDelegate
        public void ContactSelectedCallback (McContact contact)
        {
            PerformSegue ("ContactsToContactDetail", new SegueHolder (contact));
        }
            
        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void SelectFile (INachoFileChooser vc, McAbstrObject obj)
        {
            // Attachment
            var a = obj as McAttachment;
            if (null != a) {
                AttachmentsList.Add (a);
                vc.DismissFileChooser (true, null);
                ConfigureEventAttachmentView ();
                return;
            }

            // File
            var file = obj as McDocument;
            if (null != file) {
                var attachment = new McAttachment ();
                attachment.DisplayName = file.DisplayName;
                attachment.AccountId = account.Id;
                attachment.Insert ();
                var guidString = Guid.NewGuid ().ToString ("N");
                // TODO: Decide on copy, move, delete, etc
                File.Copy (file.FilePath (), McAttachment.TempPath (guidString));
                //                File.Move (file.FilePath (), McAttachment.TempPath (guidString));
                //                file.Delete ();
                attachment.SaveFromTemp (guidString);
                attachment.IsDownloaded = true;
                attachment.IsInline = true;
                attachment.Update ();
                AttachmentsList.Add (attachment);
                vc.DismissFileChooser (true, null);
                ConfigureEventAttachmentView ();
                return;
            }

            // Note
            var note = obj as McNote;
            if (null != note) {
                var attachment = new McAttachment ();
                attachment.DisplayName = note.DisplayName + ".txt";
                attachment.AccountId = account.Id;
                attachment.Insert ();
                var guidString = Guid.NewGuid ().ToString ("N");
                File.WriteAllText (McAttachment.TempPath (guidString), note.noteContent);
                attachment.SaveFromTemp (guidString);
                attachment.IsDownloaded = true;
                attachment.IsInline = true;
                attachment.Update ();
                AttachmentsList.Add (attachment);
                vc.DismissFileChooser (true, null);
                ConfigureEventAttachmentView ();
                return;
            }

            NcAssert.CaseError ();
        }

        /// <summary>
        /// INachoFileChooserParent delegate
        /// </summary>
        public void DismissChildFileChooser (INachoFileChooser vc)
        {
            vc.DismissFileChooser (true, null);
        }

    }

}
