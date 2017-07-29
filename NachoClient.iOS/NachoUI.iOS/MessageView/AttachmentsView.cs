//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.IO;
using Foundation;
using UIKit;
using CoreGraphics;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.iOS
{

    public interface AttachmentsViewDelegate 
    {
        void AttachmentsViewDidChangeSize (AttachmentsView view);
        void AttachmentsViewDidSelectAttachment (AttachmentsView view, McAttachment attachment);
    }

    public class AttachmentsView : UIView, IUITableViewDataSource, IUITableViewDelegate, AttachmentDownloaderDelegate, SwipeTableViewDelegate, ThemeAdopter
    {

        const string AttachmentCellIdentifier = "AttachmentCellIdentifier";

        private List<McAttachment> _Attachments;
        public List<McAttachment> Attachments {
            get {
                return _Attachments;
            }
            set {
                _Attachments.Clear ();
                if (value != null) {
                    _Attachments.AddRange (value);
                }
                Update ();
            }
        }

        public AttachmentsViewDelegate Delegate;
        AttachmentsHeaderView HeaderView;
        UITableView TableView;
        public readonly UIView BottomBorder;
        int NumberOfAttachmentsToCollapse = 3;
        nfloat BorderWidth = 0.5f;
        nfloat RowHeight = 44.0f;
        bool IsExpanded;
        UIEdgeInsets SeparatorInset;
        PressGestureRecognizer HeaderPressRecognizer;
        NSIndexPath SwipingIndexPath;

        Dictionary<int, AttachmentDownloader> DownloadersByAttachmentId;

        public AttachmentsView (CGRect frame) : base (frame)
        {
            DownloadersByAttachmentId = new Dictionary<int, AttachmentDownloader> ();

            ClipsToBounds = true;
            SeparatorInset = new UIEdgeInsets (0.0f, 14.0f, 0.0f, 0.0f);

            _Attachments = new List<McAttachment> ();

            HeaderView = new AttachmentsHeaderView (new CGRect (0.0f, 0.0f, Bounds.Width, RowHeight));
            HeaderView.Inset = SeparatorInset.Left;
            HeaderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;

            HeaderPressRecognizer = new PressGestureRecognizer (HeaderPressed);
            HeaderPressRecognizer.IsCanceledByPanning = true;
            HeaderView.AddGestureRecognizer (HeaderPressRecognizer);
            
            TableView = new UITableView (new CGRect(0.0f, HeaderView.Frame.Height, Bounds.Width, RowHeight), UITableViewStyle.Plain);
            TableView.ScrollEnabled = false;
            TableView.WeakDelegate = this;
            TableView.WeakDataSource = this;
            TableView.RowHeight = RowHeight;
            TableView.SeparatorStyle = UITableViewCellSeparatorStyle.None;
            TableView.RegisterClassForCellReuse (typeof(AttachmentCell), AttachmentCellIdentifier);

            BottomBorder = new UIView (new CGRect (0.0f, 0.0f, Bounds.Width, BorderWidth));
            BottomBorder.BackgroundColor = UIColor.White.ColorDarkenedByAmount (0.15f);

            AddSubview (HeaderView);
            AddSubview (TableView);
            AddSubview (BottomBorder);
        }

        public void Cleanup ()
        {
            HeaderView.RemoveGestureRecognizer (HeaderPressRecognizer);
            HeaderPressRecognizer = null;

            TableView.WeakDelegate = null;
            TableView.WeakDataSource = null;

            foreach (var pair in DownloadersByAttachmentId) {
                pair.Value.Delegate = null;
            }
            DownloadersByAttachmentId.Clear ();
        }

        Theme adoptedTheme;

        public void AdoptTheme (Theme theme)
        {
            if (adoptedTheme != theme) {
                adoptedTheme = theme;
                TableView.AdoptTheme (theme);
            }
        }

        void HeaderPressed ()
        {
            if (HeaderPressRecognizer.State == UIGestureRecognizerState.Began) {
                HeaderView.SetSelected (true, animated: false);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Ended) {
                IsExpanded = !IsExpanded;
                Update ();
                if (Delegate != null) {
                    Delegate.AttachmentsViewDidChangeSize (this);
                }
                HeaderView.SetSelected (false, animated: false);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Failed) {
                HeaderView.SetSelected (false, animated: true);
            } else if (HeaderPressRecognizer.State == UIGestureRecognizerState.Cancelled) {
                HeaderView.SetSelected (false, animated: false);
            }
        }

        void Update ()
        {
            EndSwiping ();
            if (_Attachments.Count < NumberOfAttachmentsToCollapse) {
                IsExpanded = true;
                HeaderView.Hidden = true;
            } else {
                HeaderView.Hidden = false;
            }
            UpdateHeaderLabel ();
            HeaderView.SetExpanded (IsExpanded);
            SetNeedsLayout ();
            TableView.ReloadData ();
        }

        void UpdateHeaderLabel ()
        {
            if (IsExpanded) {
                HeaderView.TextLabel.Text = NSBundle.MainBundle.LocalizedString ("Hide attachments", "Button title to hide attachments list");
            } else {
                HeaderView.TextLabel.Text = String.Format (NSBundle.MainBundle.LocalizedString ("Show {0} attachments", "Button title to show attachments list"), _Attachments.Count);
            }
        }

        #region Table View Delegate & Data Source

        [Export ("numberOfSectionsInTableView:")]
        public nint NumberOfSections (UITableView tableView)
        {
            return 1;
        }

        [Foundation.Export("tableView:numberOfRowsInSection:")]
        public nint RowsInSection (UITableView tableView, nint section)
        {
            return _Attachments.Count;
        }

        [Foundation.Export("tableView:cellForRowAtIndexPath:")]
        public UITableViewCell GetCell (UITableView tableView, NSIndexPath indexPath)
        {
            var attachment = _Attachments [indexPath.Row];
            var cell = tableView.DequeueReusableCell (AttachmentCellIdentifier) as AttachmentCell;
            cell.TextLabel.Text = Path.GetFileNameWithoutExtension (attachment.DisplayName);
            if (String.IsNullOrWhiteSpace (cell.TextLabel.Text)) {
                cell.TextLabel.Text = NSBundle.MainBundle.LocalizedString ("(no name) (attachment)", "Fallback name for attachment with no name");
                cell.TextLabel.TextColor = adoptedTheme.DisabledTextColor;
            } else {
                cell.TextLabel.TextColor = adoptedTheme.DefaultTextColor;
            }
            cell.DetailTextLabel.Text = Pretty.GetAttachmentDetail (attachment);
            cell.IconView.Image = FilesTableViewSource.FileIconFromExtension (attachment);
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Error) {
                if (!(cell.AccessoryView is ErrorAccessoryView)) {
                    cell.AccessoryView = new ErrorAccessoryView (width: 30.0f, indicatorSize: 16.0f);
                }
            } else if (attachment.FilePresence != McAbstrFileDesc.FilePresenceEnum.Complete) {
                if (!(cell.AccessoryView is DownloadAccessoryView)){
                    cell.AccessoryView = new DownloadAccessoryView ();
                }
                if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Partial) {
                    var pending = McPending.QueryByAttachmentId (attachment.AccountId, attachment.Id);
                    if (pending != null && pending.State != McPending.StateEnum.Failed) {
                        (cell.AccessoryView as DownloadAccessoryView).StartAnimating ();
                    } else {
                        (cell.AccessoryView as DownloadAccessoryView).StopAnimating ();
                    }
                } else {
                    (cell.AccessoryView as DownloadAccessoryView).StopAnimating ();
                }
            } else {
                cell.AccessoryView = null;
            }
            cell.AdoptTheme (adoptedTheme);
            return cell;
        }

        [Foundation.Export("tableView:didSelectRowAtIndexPath:")]
        public void RowSelected (UITableView tableView, NSIndexPath indexPath)
        {
            var attachment = _Attachments [indexPath.Row];
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                if (Delegate != null) {
                    Delegate.AttachmentsViewDidSelectAttachment (this, attachment);
                }
            } else {
                var cell = TableView.CellAt (indexPath) as AttachmentCell;
                if (!(cell.AccessoryView is DownloadAccessoryView)){
                    cell.AccessoryView = new DownloadAccessoryView ();
                }
                (cell.AccessoryView as DownloadAccessoryView).StartAnimating ();
                if (!DownloadersByAttachmentId.ContainsKey (attachment.Id)) {
                    var downloader = new AttachmentDownloader ();
                    DownloadersByAttachmentId.Add (attachment.Id, downloader);
                    downloader.Delegate = this;
                    downloader.Download (attachment);
                }
            }
            // TODO: download or show attachment
            tableView.DeselectRow (indexPath, true);
        }
            
        public void AttachmentDownloadDidFinish (AttachmentDownloader downloader)
        {
            DownloadersByAttachmentId.Remove (downloader.Attachment.Id);
            ReplaceAttachment (downloader.Attachment);
        }

        public void AttachmentDownloadDidFail (AttachmentDownloader downloader, NcResult result)
        {
            DownloadersByAttachmentId.Remove (downloader.Attachment.Id);
            ReplaceAttachment (downloader.Attachment);
        }

        void ReplaceAttachment (McAttachment attachment)
        {
            for (int i = 0; i < _Attachments.Count; ++i) {
                if (_Attachments [i].Id == attachment.Id) {
                    _Attachments.RemoveAt (i);
                    _Attachments.Insert (i, attachment);
                    var indexPath = NSIndexPath.FromRowSection (i, 0);
                    var cell = TableView.CellAt (indexPath) as AttachmentCell;
                    if (cell != null && cell.AccessoryView is DownloadAccessoryView) {
                        (cell.AccessoryView as DownloadAccessoryView).StopAnimating ();
                    }
                    TableView.ReloadRows(new NSIndexPath[] { indexPath }, UITableViewRowAnimation.None);
                    break;
                }
            }
        }

        public List<SwipeTableRowAction> ActionsForSwipingRightInRow (UITableView tableView, NSIndexPath indexPath)
        {
            // TODO: add forwarding
            return null;
        }

        public List<SwipeTableRowAction> ActionsForSwipingLeftInRow (UITableView tableView, NSIndexPath indexPath)
        {
            var attachment = _Attachments [indexPath.Row];
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                return new List<SwipeTableRowAction> (new SwipeTableRowAction[] {
                    new SwipeTableRowAction(NSBundle.MainBundle.LocalizedString ("Delete", ""), UIImage.FromBundle("email-delete-swipe"), UIColor.FromRGB (0xd2, 0x47, 0x47), DeleteAttachmentAtIndexPath)
                });
            }
            return null;
        }

        public virtual void WillBeginSwiping (UITableView tableView, NSIndexPath indexPath)
        {

            if (SwipingIndexPath != null) {
                EndSwiping ();
            }
            SwipingIndexPath = indexPath;
        }

        public virtual void DidEndSwiping (UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.IsEqual (SwipingIndexPath)) {
                SwipingIndexPath = null;
            }
        }

        protected virtual void EndSwiping ()
        {
            if (SwipingIndexPath != null) {
                var cell = TableView.CellAt (SwipingIndexPath) as SwipeTableViewCell;
                cell.EndSwiping ();
                SwipingIndexPath = null;
            }
        }

        void DeleteAttachmentAtIndexPath (NSIndexPath indexPath)
        {
            var attachment = _Attachments [indexPath.Row];
            if (attachment.FilePresence == McAbstrFileDesc.FilePresenceEnum.Complete) {
                attachment.DeleteFile ();
                var cell = TableView.CellAt (indexPath) as SwipeTableViewCell;
                cell.AccessoryView = new DownloadAccessoryView ();
                cell.LayoutIfNeeded ();
                cell.EndSwiping ();
            }
        }

        #endregion
            
        #region Layout

        public override void LayoutSubviews ()
        {
            base.LayoutSubviews ();
            nfloat y = 0.0f;
            if (!HeaderView.Hidden) {
                y = HeaderView.Frame.Height;
            }
            TableView.Frame = new CGRect (0.0f, y, Bounds.Width, TableView.ContentSize.Height);
            BottomBorder.Frame = new CGRect (SeparatorInset.Left, Bounds.Height - BorderWidth, Bounds.Width - SeparatorInset.Left - SeparatorInset.Right, BorderWidth);
        }

        public override void SizeToFit ()
        {
            LayoutIfNeeded ();
            if (IsExpanded) {
                Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, TableView.Frame.Y + TableView.Frame.Height);
            } else {
                Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, HeaderView.Frame.Y + HeaderView.Frame.Height);
            }
        }

        #endregion

        #region Private Classes

        private class AttachmentCell : SwipeTableViewCell, ThemeAdopter
        {

            public readonly UIImageView IconView;
            nfloat Inset = 14.0f;
            nfloat IconSize = 24.0f;
            nfloat TextSpacing = 5.0f;

            public AttachmentCell (IntPtr handle) : base (handle)
            {
                DetailTextSpacing = 0.0f;

                IconView = new UIImageView(new CGRect(0.0f, 0.0f, IconSize, IconSize));
                SeparatorInset = new UIEdgeInsets(0.0f, Inset + IconSize + TextSpacing, 0.0f, 0.0f);

                ContentView.AddSubview (IconView);
            }

            public void AdoptTheme (Theme theme)
            {
                TextLabel.Font = theme.DefaultFont.WithSize (17.0f);
                TextLabel.TextColor = theme.DefaultTextColor;
                DetailTextLabel.Font = theme.DefaultFont.WithSize (12.0f);
                DetailTextLabel.TextColor = theme.DisabledTextColor;
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                IconView.Center = new CGPoint (Inset + IconSize / 2.0f, ContentView.Bounds.Height / 2.0f);
            }
        }

        private class AttachmentsHeaderView : UIView
        {

            public nfloat Inset;
            nfloat ImageSize = 24.0f;
            nfloat TextSpacing = 5.0f;
            nfloat RightSpacing = 14.0f;
            UIImageView ImageView;
            UIImageView DisclosureIndicator;
            public readonly UILabel TextLabel;
            public bool Selected { get; private set; }

            public AttachmentsHeaderView (CGRect frame) : base (frame)
            {
                using (var image = UIImage.FromBundle("subject-attach")){
                    ImageView = new UIImageView (image);
                }
                using (var image = UIImage.FromBundle("gen-readmore")){
                    DisclosureIndicator = new UIImageView (image);
                }

                TextLabel = new UILabel ();
                TextLabel.Font = A.Font_AvenirNextRegular17;
                TextLabel.TextColor = A.Color_NachoDarkText;
                TextLabel.LineBreakMode = UILineBreakMode.TailTruncation;

                AddSubview (ImageView);
                AddSubview (DisclosureIndicator);
                AddSubview (TextLabel);
            }

            public void SetSelected (bool selected, bool animated = false)
            {
                Selected = selected;
                if (animated) {
                    UIView.BeginAnimations (null, IntPtr.Zero);
                    UIView.SetAnimationDuration (0.25f);
                }
                if (selected) {
                    BackgroundColor = UIColor.FromRGB (0xE0, 0xE0, 0xE0);
                } else {
                    BackgroundColor = UIColor.White;
                }
                if (animated) {
                    UIView.CommitAnimations ();
                }
            }

            public void SetExpanded (bool isExpanded)
            {
                if (isExpanded) {
                    using (var image = UIImage.FromBundle ("gen-readmore-active")) {
                        DisclosureIndicator.Image = image;
                    }
                } else {
                    using (var image = UIImage.FromBundle("gen-readmore")){
                        DisclosureIndicator.Image = image;
                    }
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                ImageView.Center = new CGPoint (Inset + ImageSize / 2.0f, Bounds.Height / 2.0f);
                DisclosureIndicator.Center = new CGPoint (Bounds.Width - RightSpacing - DisclosureIndicator.Frame.Width / 2.0f, Bounds.Height / 2.0f);
                nfloat x = Inset + ImageSize + TextSpacing;
                var height = TextLabel.Font.RoundedLineHeight (1.0f);
                TextLabel.Frame = new CGRect (x, (Bounds.Height - height) / 2.0f, DisclosureIndicator.Frame.X - x, height);
            }

        }

        private class DownloadAccessoryView : ImageAccessoryView
        {

            NcActivityIndicatorView ActivityIndicator;

            public DownloadAccessoryView () : base ("email-att-download")
            {
            }

            public void StartAnimating ()
            {
                if (ActivityIndicator == null) {
                    ActivityIndicator = new NcActivityIndicatorView (ImageView.Frame);
                    ActivityIndicator.Speed = 1.5f;
                    AddSubview (ActivityIndicator);
                }
                ImageView.Hidden = true;
                ActivityIndicator.StartAnimating ();
            }

            public void StopAnimating ()
            {
                ImageView.Hidden = false;
                if (ActivityIndicator != null) {
                    ActivityIndicator.StopAnimating ();
                    ActivityIndicator.RemoveFromSuperview ();
                    ActivityIndicator = null;
                }
            }

            public override void LayoutSubviews ()
            {
                base.LayoutSubviews ();
                if (ActivityIndicator != null) {
                    ActivityIndicator.Frame = ImageView.Frame;
                }
            }

        }

        #endregion
    }
}

