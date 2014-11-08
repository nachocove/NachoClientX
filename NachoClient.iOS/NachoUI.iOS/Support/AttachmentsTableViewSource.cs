﻿//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using System.Linq;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using System.Collections.Generic;
using MCSwipeTableViewCellBinding;
using MonoTouch.CoreAnimation;

namespace NachoClient.iOS
{
    public class AttachmentsTableViewSource : UITableViewSource
    {
        // cell Id's
        const string FileCell = "FileCell";

        protected List<NcFileIndex> items;
        protected List<NcFileIndex> searchResults;
        protected Dictionary<NSIndexPath,NcFileIndex> multiSelect = null;
        protected List<string> contactList;
        protected int segmentedIndex;
        protected List<List<NcFileIndex>> nestedContactList;
        int[] sectionLength;
        string[] sectionTitle;
        protected bool isMultiSelecting;

        protected UITapGestureRecognizer multiSelectTapGestureRecognizer;
        protected UIGestureRecognizer.Token multiSelectTapGestureRecognizerTapToken;

        AttachmentsViewController vc;
        UISearchDisplayController SearchDisplayController;
        public IAttachmentTableViewSourceDelegate owner;
        protected McAccount account;

        // icon id's
        string DownloadIcon = "email-att-download.png";
        public static string DownloadCompleteIcon = "icn-file-complete.png";
        string DownloadArrow = "email-att-download-arrow";
        string DownloadLine = "email-att-download-vline";
        string DownloadCircle = "email-att-download-circle";

        protected UIColor CELL_COMPONENT_BG_COLOR = UIColor.White;

        protected const int SWIPE_TAG = 99100;
        protected const int CELL_VIEW_TAG = 99200;
        protected const int MULTI_SELECT_CELL_VIEW_TAG = 99300;
        private const int EMAIL_ATTACH_TAG = 1000;
        private const int OPEN_IN_TAG = 2000;
        private const int DELETE_TAG = 3000;

        protected static int ICON_TAG = 150;
        protected static int MULTI_ICON_TAG = 175;
        protected static int DATE_LABEL_TAG = 200;
        protected static int TEXT_LABEL_TAG = 300;
        protected static int DETAIL_TEXT_LABEL_TAG = 400;
        protected static int DATE_TEXT_LABEL_TAG = 450;
        protected static int DOWNLOAD_IMAGEVIEW_TAG = 500;
        protected static int SEPARATOR_LINE_TAG = 600;
        protected static int BY_CONTACT_SEGMENT = 2;
        protected bool downloaded;

        // Pre-made swipe action descriptors
        private static SwipeActionDescriptor EMAIL_ATTACH_BUTTON =
            new SwipeActionDescriptor (EMAIL_ATTACH_TAG, 0.25f, UIImage.FromBundle ("email-archive-gray"),
                "Attach", A.Color_NachoSwipeActionGreen);
        private static SwipeActionDescriptor OPEN_IN_BUTTON =
            new SwipeActionDescriptor (OPEN_IN_TAG, 0.25f, UIImage.FromBundle ("email-putintofolder-gray"),
                "Open in", A.Color_NachoSwipeActionBlue);
        private static SwipeActionDescriptor DELETE_BUTTON =
            new SwipeActionDescriptor (DELETE_TAG, 0.25f, UIImage.FromBundle ("email-delete-gray"),
                "Delete", A.Color_NachoSwipeActionRed);

        public List<NcFileIndex> Items {
            get { return items; }
            set { items = value; }
        }

        public bool IsMultiSelecting {
            get { return isMultiSelecting; }
            set { isMultiSelecting = value; }
        }

        public Dictionary<NSIndexPath, NcFileIndex> MultiSelect {
            get { return multiSelect; }
            set { multiSelect = value; }
        }

        public List<NcFileIndex> SearchResults {
            get { return searchResults; }
            set { searchResults = value; }
        }

        public AttachmentsTableViewSource (AttachmentsViewController vc, McAccount account)
        {
            this.vc = vc;
            this.account = account;
            this.multiSelect = new Dictionary<NSIndexPath,NcFileIndex> ();
            Items = new List<NcFileIndex> ();
            SearchResults = new List<NcFileIndex> ();
            segmentedIndex = 0;
        }

        public void SetItems (List<NcFileIndex> items)
        {
            this.Items = items;
            contactList = ConfigureContactList (items);
            sectionLength = new int[contactList.Count];
            sectionTitle = new string[contactList.Count];

            int index = 0;

            foreach (var item in contactList) {
                sectionLength [index] = nestedContactList [index].Count;
                sectionTitle [index] = item;
                index++;
            }
        }

        public List<string> ConfigureContactList (List<NcFileIndex> items)
        {
            List<string> tempList = new List<string> ();

            foreach (var item in items) {
                if (!tempList.Contains (item.Contact)) {
                    tempList.Add (item.Contact);
                }
            }

            int i = 0;
            nestedContactList = new List<List<NcFileIndex>> ();

            foreach (var contact in tempList) {
                var sublist = new List<NcFileIndex> ();
                foreach (var item in items) {
                    if (contact == item.Contact) {
                        sublist.Add (item);
                    }
                }
                nestedContactList.Add (sublist);
                i++;
            }
            return tempList;
        }

        public void SetOwner (IAttachmentTableViewSourceDelegate owner, UISearchDisplayController SearchDisplayController)
        {
            this.owner = owner;
            this.SearchDisplayController = SearchDisplayController;

            SearchDisplayController.Delegate = new SearchDisplayDelegate (this);
        }

        public override int NumberOfSections (UITableView tableView)
        {
            if (BY_CONTACT_SEGMENT == segmentedIndex) {
                return contactList.Count;
            }
            return 1;
        }

        public override float GetHeightForRow (UITableView tableView, NSIndexPath indexPath)
        {
            return 80.0f;
        }

        public override UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            UITableViewCell cell = null;
            cell = tableView.DequeueReusableCell (FileCell);
            if (null == cell) {
                cell = CreateCell (tableView, FileCell);
            }
            NcAssert.True (null != cell);
        
            ConfigureCell (tableView, cell, indexPath);

            return cell;
        }

        protected UITableViewCell CreateCell (UITableView tableView, string identifier)
        {
            var cell = tableView.DequeueReusableCell (identifier);
            if (null == cell) {
                cell = new UITableViewCell (UITableViewCellStyle.Default, identifier);
            }
            if (cell.RespondsToSelector (new MonoTouch.ObjCRuntime.Selector ("setSeparatorInset:"))) {
                cell.SeparatorInset = UIEdgeInsets.Zero;
            }
            cell.SelectionStyle = UITableViewCellSelectionStyle.Default;
            cell.ContentView.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            var cellWidth = tableView.Frame.Width;

            var frame = new RectangleF (0, 0, tableView.Frame.Width, 80);
            var view = new SwipeActionView (frame);
            view.SetAction (DELETE_BUTTON, SwipeSide.RIGHT);
            view.SetAction (EMAIL_ATTACH_BUTTON, SwipeSide.LEFT);
            view.SetAction (OPEN_IN_BUTTON, SwipeSide.LEFT);

            cell.AddSubview (view);
            view.Tag = SWIPE_TAG;
            view.BackgroundColor = CELL_COMPONENT_BG_COLOR;

            //Multi select icon
            var multiSelectImageView = new UIImageView ();
            multiSelectImageView.Tag = MULTI_ICON_TAG;
            multiSelectImageView.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            multiSelectImageView.Frame = new RectangleF (18, (view.Frame.Height / 2) - 8, 16, 16);
            multiSelectImageView.Hidden = true;
            view.AddSubview (multiSelectImageView);

            //Cell icon
            var cellIconImageView = new UIImageView (); 
            cellIconImageView.Tag = ICON_TAG;
            cellIconImageView.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            cellIconImageView.Frame = new RectangleF (18, 28, 24, 24);
            view.AddSubview (cellIconImageView);

            //Text label
            var textLabel = new UILabel (); 
            textLabel.Tag = TEXT_LABEL_TAG;
            textLabel.Font = A.Font_AvenirNextDemiBold14;
            textLabel.TextColor = A.Color_NachoDarkText;
            textLabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            textLabel.Frame = new RectangleF (60, 11, cell.Frame.Width - 60 - 52, 19.5f);
            view.AddSubview (textLabel);

            //Detail text label
            var detailTextlabel = new UILabel (); 
            detailTextlabel.Tag = DETAIL_TEXT_LABEL_TAG;
            detailTextlabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            detailTextlabel.Font = A.Font_AvenirNextRegular14;
            detailTextlabel.TextColor = A.Color_NachoTextGray;
            detailTextlabel.Frame = new RectangleF (60, 11 + 19.5f, cell.Frame.Width - 60 - 52, 19.5f);
            view.AddSubview (detailTextlabel);

            //Date text label
            var dateTextlabel = new UILabel (); 
            dateTextlabel.Tag = DATE_TEXT_LABEL_TAG;
            dateTextlabel.BackgroundColor = CELL_COMPONENT_BG_COLOR;
            dateTextlabel.Font = A.Font_AvenirNextRegular14;
            dateTextlabel.TextColor = A.Color_NachoTextGray;
            dateTextlabel.Frame = new RectangleF (60, 11 + 19.5f + 19.5f, cell.Frame.Width - 60 - 52, 19.5f);
            view.AddSubview (dateTextlabel);

            //Download image view
            var dowloadImageView = new UIImageView (new RectangleF (cell.Frame.Width - 18 - 16, (view.Frame.Height / 2) - 8, 16, 16)); 
            dowloadImageView.Tag = DOWNLOAD_IMAGEVIEW_TAG;
            view.AddSubview (dowloadImageView);

            //Separator line
            var separatorLine = Util.AddHorizontalLineView (60, 80, cell.Frame.Width - 60, A.Color_NachoBorderGray);
            separatorLine.Tag = SEPARATOR_LINE_TAG;
            view.AddSubview (separatorLine);

            cell.AddSubview (view);
            return cell;
        }

        public void ConfigureCell (UITableView tableView, UITableViewCell cell, NSIndexPath indexPath)
        {
            float xOffset = isMultiSelecting ? 34 : 0;
            float yOffset = 0;

            //Item
            NcFileIndex item;
            item = FileFromIndexPath (tableView, indexPath);

            //Swipe view
            var view = cell.ViewWithTag (SWIPE_TAG) as SwipeActionView;
            if (isMultiSelecting) {
                view.DisableSwipe ();
            } else {
                view.EnableSwipe ();
            }

            view.OnClick = (int tag) => {
                switch (tag) {
                case OPEN_IN_TAG:
                    OpenFileIn (item, cell);
                    break;
                case EMAIL_ATTACH_TAG:
                    AttachFile (item, cell);
                    break;
                case DELETE_TAG:
                    DeleteFile (item);
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown action tag {0}", tag));
                }
            };
            view.OnSwipe = (SwipeActionView.SwipeState state) => {
                switch (state) {
                case SwipeActionView.SwipeState.SWIPE_BEGIN:
                    tableView.ScrollEnabled = false;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_HIDDEN:
                    tableView.ScrollEnabled = true;
                    break;
                case SwipeActionView.SwipeState.SWIPE_END_ALL_SHOWN:
                    tableView.ScrollEnabled = false;
                    break;
                default:
                    throw new NcAssert.NachoDefaultCaseFailure (String.Format ("Unknown swipe state {0}", (int)state));
                }
            };

            //Multiselect icon
            var multiSelectImageView = view.ViewWithTag (MULTI_ICON_TAG) as UIImageView;
            multiSelectImageView.Hidden = isMultiSelecting ? false : true;
            SetMultiSelectIcon (multiSelectImageView, indexPath);
             
            //Cell icon
            var cellIconImageView = view.ViewWithTag (ICON_TAG) as UIImageView;
            cellIconImageView.Frame = new RectangleF (18 + xOffset, 28, 24, 24);

            //Text label
            var textLabel = view.ViewWithTag (TEXT_LABEL_TAG) as UILabel; 
            textLabel.Frame = new RectangleF (60 + xOffset, 11, cell.Frame.Width - xOffset - 112, 19.5f);
            yOffset += textLabel.Frame.Height;

            //Detail text label
            var detailTextlabel = view.ViewWithTag (DETAIL_TEXT_LABEL_TAG) as UILabel;  
            detailTextlabel.Frame = new RectangleF (60 + xOffset, 11 + yOffset, cell.Frame.Width - xOffset - 112, 19.5f);
            yOffset += detailTextlabel.Frame.Height;

            //Date text label
            var dateTextlabel = view.ViewWithTag (DATE_TEXT_LABEL_TAG) as UILabel; 
            dateTextlabel.Frame = new RectangleF (60 + xOffset, 11 + yOffset, cell.Frame.Width - xOffset - 112, 19.5f);

            //Download image view
            var downloadImageView = view.ViewWithTag (DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            downloadImageView.Image = UIImage.FromFile (DownloadIcon);

            //Separator line
            var separatorLine = view.ViewWithTag (SEPARATOR_LINE_TAG);
            var totalRow = tableView.NumberOfRowsInSection (indexPath.Section);
            if (totalRow - 1 == indexPath.Row) {
                separatorLine.Frame = new RectangleF (0, 79.5f, cell.Frame.Width, .5f);
            } else {
                separatorLine.Frame = new RectangleF (60 + xOffset, 79.5f, cell.Frame.Width - 60 - xOffset, .5f);
            }

            switch (item.FileType) {
            case 0:
                ConfigureAttachmentView (view, cell, McAttachment.QueryById<McAttachment> (item.Id), item, cellIconImageView, textLabel, detailTextlabel, dateTextlabel, downloadImageView);
                break;
            case 1:
                ConfigureNoteView (item, cellIconImageView, textLabel, detailTextlabel, dateTextlabel);
                break;
            case 2:
                ConfigureDocumentView (McDocument.QueryById<McDocument> (item.Id), item, cellIconImageView, textLabel, detailTextlabel, dateTextlabel);
                break;
            }

        }

        protected void ConfigureAttachmentView (SwipeActionView view, UITableViewCell cell, McAttachment attachment, NcFileIndex item, UIImageView iconView, UILabel textLabel, UILabel detailTextLabel, UILabel dateTextLabel, UIImageView downloadImageView)
        {
            if (null != attachment) {
                downloaded = false;
                float xOffset = isMultiSelecting ? 34 : 0;
                StopAnimationsOnCell (cell);

                switch (attachment.FilePresence) {
                case McAbstrFileDesc.FilePresenceEnum.Complete:
                    downloaded = true;
                    downloadImageView.Image = UIImage.FromFile (DownloadIcon);
                    downloadImageView.Hidden = true;
                    view.EnableSwipe ();
                    break;
                case McAbstrFileDesc.FilePresenceEnum.Partial:
                    vc.AttachmentAction (attachment.Id, cell);
                    view.DisableSwipe ();
                    break;
                default:
                    (downloadImageView.Superview).BringSubviewToFront (downloadImageView);
                    downloadImageView.Image = UIImage.FromFile (DownloadIcon);
                    downloadImageView.Hidden = false;
                    view.DisableSwipe ();
                    break;
                }

                textLabel.Text = Path.GetFileNameWithoutExtension (item.DisplayName);

                var detailText = "";
                if (attachment.IsInline) {
                    detailText += "Inline ";
                }
                string extension = Path.GetExtension (attachment.DisplayName).ToUpper ();
                detailText += extension.Length > 1 ? extension.Substring (1) + " " : "Unrecognized "; // get rid of period and format
                detailText += "file";
                if (0 != attachment.FileSize) {
                    detailText += " - " + Pretty.PrettyFileSize (attachment.FileSize);
                } 
                if (downloaded) {
                    detailText += " - Downloaded";
                    detailTextLabel.Frame = new RectangleF (detailTextLabel.Frame.X, detailTextLabel.Frame.Y, detailTextLabel.Superview.Frame.Width - 78 - xOffset, detailTextLabel.Frame.Height);
                }
                detailTextLabel.Text = detailText;

                dateTextLabel.Text = DateToString (item.CreatedAt);

                if (detailText.Contains ("JPG") || detailText.Contains ("JPEG")
                    || detailText.Contains ("TIFF") || detailText.Contains ("PNG")
                    || detailText.Contains ("GIF") || detailText.Contains ("RAW")) {
                    iconView.Image = UIImage.FromBundle ("email-att-photos");
                } else {
                    iconView.Image = UIImage.FromBundle ("email-att-files");
                }
            } else {
                textLabel.Text = "File no longer exists"; 
            }

        }

        protected void ConfigureNoteView (NcFileIndex item, UIImageView iconView, UILabel textLabel, UILabel detailTextLabel, UILabel dateTextLabel)
        {
            textLabel.Text = Path.GetFileNameWithoutExtension (item.DisplayName);
            detailTextLabel.Text = "Note";
            dateTextLabel.Text = DateToString (item.CreatedAt);
            iconView.Image = UIImage.FromBundle ("email-att-files");
        }

        protected void ConfigureDocumentView (McDocument document, NcFileIndex item, UIImageView iconView, UILabel textLabel, UILabel detailTextLabel, UILabel dateTextLabel)
        {
            textLabel.Text = Path.GetFileNameWithoutExtension (item.DisplayName);
            detailTextLabel.Text = "Document";
            dateTextLabel.Text = DateToString (item.CreatedAt);
            iconView.Image = UIImage.FromBundle ("email-att-files");
        }

        private string DateToString (DateTime date)
        {
            string dateText = "Date unknown";
            if (date != DateTime.MinValue) {
                dateText = Pretty.FullDateTimeString (date);
            }
            return dateText;
        }

        protected NcFileIndex FileFromIndexPath (UITableView tableView, NSIndexPath indexPath)
        {
            NcFileIndex file;

            if (SearchDisplayController.SearchResultsTableView == tableView) {
                file = SearchResults [indexPath.Row];
            } else if (BY_CONTACT_SEGMENT == segmentedIndex) {
                var section = indexPath.Section;
                var index = indexPath.Row;
                file = nestedContactList [section] [index];
            } else {
                file = Items [indexPath.Row];
            }
            return file;
        }

        public override void RowSelected (UITableView tableView, MonoTouch.Foundation.NSIndexPath indexPath)
        {
            UITableViewCell cell = tableView.CellAt (indexPath);
            if (isMultiSelecting) {
                var iv = cell.ViewWithTag (MULTI_ICON_TAG) as UIImageView;
                ToggleMultiSelectIcon (iv);

                var file = FileFromIndexPath (tableView, indexPath);
                if (multiSelect.ContainsKey (indexPath)) {
                    multiSelect.Remove (indexPath);
                } else {
                    multiSelect.Add (indexPath, file);
                }
                if (multiSelect.Count >= 2) {
                    vc.ConfigureMultiSelectNavBar (false, multiSelect.Count);
                } else {
                    vc.ConfigureMultiSelectNavBar (true, multiSelect.Count);
                }
            } else {
                NcFileIndex item;
                item = FileFromIndexPath (tableView, indexPath);
            
                switch (item.FileType) {
                case 0:
                    McAttachment attachment = McAttachment.QueryById<McAttachment> (item.Id);

                    if (null != attachment) {
                        vc.AttachmentAction (attachment.Id, cell);
                    }
                    break;
                case 1:
                    McNote note = McNote.QueryById<McNote> (item.Id);
                    if (null != note) {
                        vc.NoteAction (note);
                    }
                    break;
                case 2:
                    McDocument document = McDocument.QueryById<McDocument> (item.Id);
                    if (null != document) {
                        vc.DocumentAction (document);
                    }
                    break;
                }
            }
            tableView.DeselectRow (indexPath, true);
        }

        public void DeleteFile (NcFileIndex item)
        {
            switch (item.FileType) {
            case 0:
                McAttachment attachment = McAttachment.QueryById<McAttachment> (item.Id);
                if (null != attachment) {
                    vc.DeleteAttachment (attachment);
                }
                break;
            case 1:
                McNote note = McNote.QueryById<McNote> (item.Id);
                if (null != note) {
                    vc.DeleteNote (note);
                }
                break;
            case 2:
                McDocument document = McDocument.QueryById<McDocument> (item.Id);
                if (null != document) {
                    vc.DeleteDocument (document);
                }
                break;
            default:
                NcAssert.CaseError ("Deleting unknown file type");
                break;
            }
        }

        public void AttachFile (NcFileIndex item, UITableViewCell cell)
        {
            var tempAttachmentList = new List<McAttachment> ();
            var tempAttachment = new McAttachment ();
            switch (item.FileType) {
            case 0:
                McAttachment attachment = McAttachment.QueryById<McAttachment> (item.Id);
                if (null != attachment) {
                    tempAttachment = attachment;
                }
                break;
            case 1:
                McNote note = McNote.QueryById<McNote> (item.Id);
                if (null != note) {
                    tempAttachment = NoteToAttachment (note);
                }
                break;
            case 2:
                //TODO
                //McDocument document = McDocument.QueryById<McDocument> (item.Id);
                break;
            default:
                NcAssert.CaseError ("Attaching unknown file type");
                break;
            }
            if (McAbstrFileDesc.FilePresenceEnum.Complete != tempAttachment.FilePresence) {
                UIAlertView alert = new UIAlertView (
                                        "Hold on!", 
                                        "All attachments must be downloaded before they can be attached to an email.", 
                                        null, 
                                        "OK"
                                    );
                alert.Show ();
                return;
            } else {
                tempAttachmentList.Add (tempAttachment);
            }
            vc.ForwardAttachments (tempAttachmentList);
        }

        public void OpenFileIn (NcFileIndex item, UITableViewCell cell)
        {
            switch (item.FileType) {
            case 0:
                McAttachment attachment = McAttachment.QueryById<McAttachment> (item.Id);
                if (null != attachment) {
                    vc.OpenInOtherApp (attachment, cell);
                }
                break;
            case 1:
                McNote note = McNote.QueryById<McNote> (item.Id);
                if (null != note) {
                    vc.OpenInOtherApp (NoteToAttachment (note), cell);
                }
                break;
            case 2:
                //TODO
                //McDocument document = McDocument.QueryById<McDocument> (item.Id);
                break;
            default:
                NcAssert.CaseError ("Opening unknown file type");
                break;
            }
        }

        public McAttachment NoteToAttachment (McNote note)
        {
            var attachment = McAttachment.InsertFile (account.Id, ((FileStream stream) => {
                using (Stream s = Util.GenerateStreamFromString (note.noteContent)) {
                    s.CopyTo (stream);
                }
            }));
            attachment.SetDisplayName (note.DisplayName + ".txt");
            attachment.UpdateSaveFinish ();
            return attachment;
        }

        protected void ToggleMultiSelectIcon (UIImageView iv)
        {
            if (iv.UserInteractionEnabled) {
                iv.Image = UIImage.FromBundle ("gen-checkbox");
                iv.UserInteractionEnabled = false;
            } else {
                iv.Image = UIImage.FromBundle ("gen-checkbox-checked");
                iv.UserInteractionEnabled = true;
            }
        }

        protected void SetMultiSelectIcon (UIImageView iv, NSIndexPath indexPath)
        {
            if (multiSelect.ContainsKey (indexPath)) {
                iv.Image = UIImage.FromBundle ("gen-checkbox-checked");
                iv.UserInteractionEnabled = true;        
            } else {
                iv.Image = UIImage.FromBundle ("gen-checkbox");
                iv.UserInteractionEnabled = false;
            }
        }

        public static void StopAnimationsOnCell (UITableViewCell cell)
        {
            var iv = cell.ViewWithTag (DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            foreach (UIView subview in iv) {
                subview.Layer.RemoveAllAnimations ();
                subview.RemoveFromSuperview ();
            }
            cell.UserInteractionEnabled = true;
        }

        UIView ViewWithImageName (string imageName)
        {
            var image = UIImage.FromBundle (imageName);
            var imageView = new UIImageView (image);
            imageView.ContentMode = UIViewContentMode.Center;
            return imageView;
        }

        // Do arrow with line animation followed by repeating arrow-only animations
        public void StartDownloadingAnimation (UITableViewCell cell)
        {
            cell.UserInteractionEnabled = false;
            var iv = cell.ViewWithTag (DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            iv.Image = UIImage.FromBundle (DownloadCircle);
            UIImageView line = new UIImageView (UIImage.FromBundle (DownloadLine));
            UIImageView arrow = new UIImageView (UIImage.FromBundle (DownloadArrow));
            iv.AddSubview (line);
            iv.AddSubview (arrow);

            PointF center = line.Center;
            UIView.Animate (
                duration: 0.4, 
                delay: 0, 
                options: UIViewAnimationOptions.CurveEaseIn,
                animation: () => {
                    line.Center = new PointF (center.X, iv.Image.Size.Height * 3 / 4);
                    arrow.Center = new PointF (center.X, iv.Image.Size.Height * 3 / 4);
                    line.Alpha = 0.0f;
                    arrow.Alpha = 0.4f;
                },
                completion: () => {
                    arrow.Center = new PointF (center.X, 2);
                    arrow.Alpha = 1.0f;
                    ArrowAnimation (cell, arrow, center);
                }
            );
        }

        // Start only the arrow animation
        public void StartArrowAnimation (UITableViewCell cell)
        {
            var iv = cell.ViewWithTag (DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            iv.Image = UIImage.FromBundle (DownloadCircle);
            UIImageView arrow = new UIImageView (UIImage.FromBundle (DownloadArrow));
            iv.AddSubview (arrow);

            ArrowAnimation (cell, arrow, arrow.Center);
        }

        private static void ArrowAnimation (UITableViewCell cell, UIImageView arrow, PointF center)
        {
            var iv = cell.ViewWithTag (DOWNLOAD_IMAGEVIEW_TAG) as UIImageView;
            UIView.Animate (0.4, 0, (UIViewAnimationOptions.Repeat | UIViewAnimationOptions.OverrideInheritedDuration | UIViewAnimationOptions.OverrideInheritedOptions | UIViewAnimationOptions.OverrideInheritedCurve | UIViewAnimationOptions.CurveLinear), () => {
                arrow.Center = new PointF (center.X, iv.Frame.Size.Height * 3 / 4);
                arrow.Alpha = 0.4f;
            }, (() => { 
            }));
        }

        public static void DownloadCompleteAnimation (UITableViewCell cell, Action displayAttachment)
        {
            StopAnimationsOnCell (cell);
            displayAttachment ();
        }

        public override float GetHeightForHeader (UITableView tableView, int section)
        {
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return 0;
            }
            if (BY_CONTACT_SEGMENT == segmentedIndex) {
                if (0 == section) {
                    return 78;
                } else {
                    return 56;
                }
            } else {
                return 0;
            }
        }

        public override float GetHeightForFooter (UITableView tableView, int section)
        {
            if (BY_CONTACT_SEGMENT == segmentedIndex || SearchDisplayController.SearchResultsTableView == tableView) {
                return 0;
            }
            return 32;
        }

        public override UIView GetViewForHeader (UITableView tableView, int section)
        {
            var senderString = TitleForHeader (tableView, section);
            var senderEmail = Pretty.EmailString (senderString);
            var senderDisplayName = Pretty.SenderString (senderString);
            if (SearchDisplayController.SearchResultsTableView == tableView) {
                return new UIView (new RectangleF (0, 0, 0, 0));
            }
            if (BY_CONTACT_SEGMENT != segmentedIndex) {
                return new UIView (new RectangleF (0, 0, 0, 0));
            }
            var view = new UIView ();
            var label = new UILabel ();
            label.Font = A.Font_AvenirNextDemiBold17;
            label.TextColor = A.Color_NachoDarkText;
            label.BackgroundColor = tableView.BackgroundColor;
            label.Text = senderDisplayName;
            label.SizeToFit ();
            var yOffset = 26;
            var iconOffset = 5;
            if (0 == section) {
                yOffset += 19;
                iconOffset += 19;
            }
            label.Center = new PointF (60 + (label.Frame.Width / 2), yOffset);
            view.AddSubview (label);

            var userImageView = new UIImageView (new RectangleF (12, iconOffset, 40, 40));
            userImageView.Center = new PointF (userImageView.Center.X, label.Center.Y);
            userImageView.Layer.CornerRadius = 20;
            userImageView.Layer.MasksToBounds = true;
            view.AddSubview (userImageView);

            var userLabelView = new UILabel (new RectangleF (12, iconOffset, 40, 40));
            userLabelView.Font = A.Font_AvenirNextRegular24;
            userLabelView.TextColor = UIColor.White;
            userLabelView.TextAlignment = UITextAlignment.Center;
            userLabelView.LineBreakMode = UILineBreakMode.Clip;
            userLabelView.Layer.CornerRadius = 20;
            userLabelView.Layer.MasksToBounds = true;
            view.AddSubview (userLabelView);

            // User image view
            userImageView.Hidden = true;
            userLabelView.Hidden = true;

            var userImage = Util.ImageOfSender (account.Id, senderEmail);

            if (null != userImage) {
                userImageView.Hidden = false;
                userImageView.Image = userImage;
            } else {
                userLabelView.Hidden = false;
                int ColorIndex;
                string Initials;
                Util.UserMessageField (senderString, account.Id, out ColorIndex, out Initials);
                userLabelView.Text = Initials;
                userLabelView.BackgroundColor = Util.ColorForUser (ColorIndex);
            }
            return view;
        }

        public override string TitleForHeader (UITableView tableView, int section)
        {
            return sectionTitle [section];
        }

        public override UIView GetViewForFooter (UITableView tableView, int section)
        {
            if (BY_CONTACT_SEGMENT == segmentedIndex || SearchDisplayController.SearchResultsTableView == tableView) {
                return new UIView (new RectangleF (0, 0, 0, 0));
            }
            var view = new UIView (new RectangleF (0, 0, tableView.Frame.Width, 32));
            var label = new UILabel ();
            label.Font = A.Font_AvenirNextRegular12;
            label.TextColor = A.Color_NachoIconGray;
            label.Text = TitleForFooter (tableView, section);
            label.SizeToFit ();
            label.Center = new PointF (tableView.Frame.Width / 2, 16);
            view.AddSubview (label);
            return view;
        }

        public override string TitleForFooter (UITableView tableView, int section)
        {
            if (1 == this.items.Count) {
                return "1 file";
            }
            return this.items.Count + " files";
        }

        /// <summary>
        /// The number of rows in the specified section.
        /// </summary>
        public override int RowsInSection (UITableView tableview, int section)
        {
            int rows;

            if (SearchDisplayController.SearchResultsTableView == tableview) {
                rows = ((null == searchResults) ? 0 : searchResults.Count);
            } else if (BY_CONTACT_SEGMENT == segmentedIndex) {
                rows = sectionLength [section];
            } else {
                rows = Items.Count;
            }
            return rows;
        }

        public void SetSearchResults (List<NcFileIndex> searchResults)
        {
            this.searchResults = searchResults;
        }

        public void SetSegmentedIndex (int index)
        {
            this.segmentedIndex = index;
        }

        public bool UpdateSearchResults (int forSearchOption, string forSearchString)
        {
            NachoClient.Util.HighPriority ();
            var results = SearchByString (forSearchString);
            NachoClient.Util.RegularPriority ();
            InvokeOnMainThread (() => {
                var searchResults = results;
                SetSearchResults (searchResults);
                UpdateSearchResultsCallback ();
            });
            return false;
        }

        public List<NcFileIndex> SearchByString (string searchString)
        {
            List<NcFileIndex> results = new List<NcFileIndex> ();
            foreach (var item in Items) {
                if ((item.DisplayName.ToLower ()).Contains (searchString.ToLower ())) {
                    results.Add (item);
                }
            }
            return results;
        }

        public void UpdateSearchResultsCallback ()
        {
            // Totally a dummy routines that exists to remind us how to trigger 
            // the update after updating the searchResult list of attachments.
            if (null != SearchDisplayController.SearchResultsTableView) {
                NachoClient.Util.HighPriority ();
                SearchDisplayController.SearchResultsTableView.ReloadData ();
                NachoClient.Util.RegularPriority ();
            }
        }
    }

    public class SearchDisplayDelegate : UISearchDisplayDelegate
    {
        AttachmentsTableViewSource owner;

        private SearchDisplayDelegate ()
        {
        }

        public SearchDisplayDelegate (AttachmentsTableViewSource owner)
        {
            this.owner = owner;
        }

        public override bool ShouldReloadForSearchScope (UISearchDisplayController controller, int forSearchOption)
        {
            // TODO: Trigger asynch search & return false
            string searchString = controller.SearchBar.Text;
            return owner.UpdateSearchResults (forSearchOption, searchString);
        }

        public override bool ShouldReloadForSearchString (UISearchDisplayController controller, string forSearchString)
        {
            int searchOption = controller.SearchBar.SelectedScopeButtonIndex;
            return owner.UpdateSearchResults (searchOption, forSearchString);
        }
    }
}




