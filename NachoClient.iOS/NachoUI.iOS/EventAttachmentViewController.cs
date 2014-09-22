// This file has been autogenerated from a class added in the UI designer.

using System;
using System.Drawing;
using System.Collections.Generic;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using MonoTouch.EventKit;
using System.IO;
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

        UILabel attachedLabel;
        UIView addButtonView;
        UIView line;

        const int ADD_BUTTON_VIEW_TAG = 101;
        const int ATTACHED_LABEL_TAG = 102;
        const int ATTACHMENTS_TABLE_TAG = 103;

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

        public override bool HidesBottomBarWhenPushed {
            get {
                return this.NavigationController.TopViewController == this;
            }
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
            Util.SetBackButton (NavigationController, NavigationItem, A.Color_NachoBlue);

            float yOffset = 0f;

            addButtonView = new UIView (new RectangleF (0, 20, View.Frame.Width, 44));
            addButtonView.Tag = ADD_BUTTON_VIEW_TAG;
            addButtonView.BackgroundColor = UIColor.White;
            addButtonView.Hidden = true;

            UIButton addPhotoButton = new UIButton (UIButtonType.RoundedRect);
            addPhotoButton.TintColor = A.Color_NachoIconGray;
            addPhotoButton.TouchUpInside += (object sender, EventArgs e) => {
                SetupPhotoPicker ();
            };
            addPhotoButton.SetTitle ("+", UIControlState.Normal);
            addPhotoButton.TitleEdgeInsets = new UIEdgeInsets (-2f, -52f, 0f, 0f);
            addPhotoButton.SetImage (UIImage.FromBundle ("icn-photos"), UIControlState.Normal);

            addPhotoButton.Frame = new RectangleF (0, 0, View.Frame.Width / 2, 44);
            addButtonView.Add (addPhotoButton);

            UIButton addAttachmentButton = new UIButton (UIButtonType.RoundedRect);
            addAttachmentButton.TintColor = A.Color_NachoIconGray;
            addAttachmentButton.TouchUpInside += (object sender, EventArgs e) => {
                PerformSegue ("EventAttachmentToFiles", this);
            };
            addAttachmentButton.SetTitle ("+", UIControlState.Normal);
            addAttachmentButton.TitleEdgeInsets = new UIEdgeInsets (-2f, -52f, 0f, 0f);
            addAttachmentButton.SetImage (UIImage.FromBundle ("icn-attach-files"), UIControlState.Normal);
            addAttachmentButton.Frame = new RectangleF (160, 0, View.Frame.Width / 2, 44);
            addButtonView.Add (addAttachmentButton);

            Util.AddVerticalLine (addButtonView.Frame.Width / 2, 6, 32, A.Color_NachoBorderGray, addButtonView);
            line = Util.AddHorizontalLineView (0, addButtonView.Frame.Bottom, View.Frame.Width, A.Color_NachoBorderGray);
            yOffset += addButtonView.Frame.Bottom;

            yOffset += 16;
            attachedLabel = new UILabel (new RectangleF (15, yOffset, 160, 20));
            attachedLabel.Tag = ATTACHED_LABEL_TAG;
            attachedLabel.Text = "ATTACHED";
            attachedLabel.Font = A.Font_AvenirNextRegular12;
            attachedLabel.TextColor = A.Color_NachoIconGray;
            attachedLabel.Hidden = true;
            yOffset += attachedLabel.Frame.Height;

            yOffset += 4;
            EventAttachmentsTableView.Frame = new RectangleF (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset);
            EventAttachmentsTableView.SeparatorColor = A.Color_NachoBorderGray;
            EventAttachmentsTableView.Tag = ATTACHMENTS_TABLE_TAG;

            View.BackgroundColor = A.Color_NachoBackgroundGray;

            View.Add (line);
            View.Add (addButtonView);
            View.AddSubview (attachedLabel);
        }

        protected void ConfigureEventAttachmentView ()
        {
            if (editing) {
                NavigationItem.Title = "Add Attachments";
                addButtonView.Hidden = false;
            } else {
                NavigationItem.Title = "Attachments";
                line.Hidden = true;
            }

            if (0 == AttachmentsList.Count) {
                EventAttachmentsTableView.Hidden = true;
                attachedLabel.Hidden = true;
            } else {
                attachmentSource.SetAttachmentList (this.AttachmentsList);
                EventAttachmentsTableView.ReloadData ();
                EventAttachmentsTableView.Hidden = false;
                attachedLabel.Hidden = false;
            }

            LayoutView ();
        }

        protected void LayoutView ()
        {
            var yOffset = 20f;

            if (editing) {
                var abv = View.ViewWithTag (ADD_BUTTON_VIEW_TAG) as UIView;
                abv.Frame = new RectangleF (0, yOffset, View.Frame.Width, 44);
                yOffset += abv.Frame.Height;

                line.Frame = new RectangleF (0, yOffset, View.Frame.Width, line.Frame.Height);
                yOffset += 16;
            }

            var al = View.ViewWithTag (ATTACHED_LABEL_TAG) as UILabel;
            al.Frame = new RectangleF (15, yOffset, 160, 20);
            yOffset += al.Frame.Height;

            yOffset += 4;
            var at = View.ViewWithTag (ATTACHMENTS_TABLE_TAG) as UITableView;
            at.Frame = new RectangleF (0, yOffset, View.Frame.Width, View.Frame.Height - yOffset);

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
                var attachment = McAttachment.InsertFile (account.Id, ((FileStream stream) => {
                    using (var jpg = image.AsJPEG ().AsStream ()) {
                        jpg.CopyTo (stream);
                    }
                }));
                attachment.SetDisplayName (attachment.Id.ToString () + ".jpg");
                attachment.UpdateSaveFinish ();
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
            var a = obj as McAttachment;
            if (null != a) {
                AttachmentsList.Add (a);
                vc.DismissFileChooser (true, null);
                ConfigureEventAttachmentView ();
                return;
            }

            var file = obj as McDocument;
            if (null != file) {
                var attachment = McAttachment.InsertSaveStart (account.Id);
                attachment.SetDisplayName (file.DisplayName);
                attachment.IsInline = true;
                attachment.UpdateFileCopy (file.GetFilePath ());
                AttachmentsList.Add (attachment);
                vc.DismissFileChooser (true, null);
                ConfigureEventAttachmentView ();
                return;
            }

            var note = obj as McNote;
            if (null != note) {
                var attachment = McAttachment.InsertSaveStart (account.Id);
                attachment.SetDisplayName (note.DisplayName + ".txt");
                attachment.IsInline = true;
                attachment.UpdateData (note.noteContent);
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
