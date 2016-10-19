//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using UIKit;
using Foundation;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using CoreGraphics;
using System.Linq;

namespace NachoClient.iOS
{

    public interface ChatMessageComposeDelegate
    {

        void ChatComposeDidSend (ChatMessageComposeView composeView);
        void ChatComposeWantsAttachment (ChatMessageComposeView composeView);
        void ChatComposeShowAttachment (ChatMessageComposeView composeView, McAttachment attachment);
        void ChatComposeDidRemoveAttachment (ChatMessageComposeView composeView, McAttachment attachment);
        bool ChatComposeCanSend (ChatMessageComposeView composeView);
        void ChatComposeChangedHeight (ChatMessageComposeView composeView);

    }

    public class ChatMessageComposeView : UIView
    {

        public ChatMessageComposeDelegate ComposeDelegate;
        UITextView MessageField;
        UIButton AttachButton;
        UIButton SendButton;
        UIView TopBorderView;
        UILabel MessagePlaceholderLabel;
        List<UcAttachmentCell> AttachmentViews;
        UIScrollView AttachmentsScrollView;
        public static readonly nfloat STANDARD_HEIGHT = 41.0f;

        public ChatMessageComposeView (CGRect frame) : base (frame)
        {
            AttachmentViews = new List<UcAttachmentCell> ();
            TopBorderView = new UIView (new CGRect (0.0f, 0.0f, Bounds.Size.Width, 1.0f));
            TopBorderView.BackgroundColor = A.Color_NachoBorderGray;
            TopBorderView.AutoresizingMask = UIViewAutoresizing.FlexibleWidth;
            BackgroundColor = UIColor.White;
            AttachButton = new UIButton (UIButtonType.Custom);
            using (var image = UIImage.FromBundle ("chat-attachfile")) {
                AttachButton.SetImage (image, UIControlState.Normal);
            }
            AttachButton.TouchUpInside += Attach;
            AttachButton.ContentMode = UIViewContentMode.Center;
            MessageField = new UITextView (Bounds);
            MessageField.Changed += MessageChanged;
            MessageField.Font = A.Font_AvenirNextRegular17;
            MessagePlaceholderLabel = new UILabel (MessageField.Bounds);
            MessagePlaceholderLabel.UserInteractionEnabled = false;
            MessagePlaceholderLabel.Font = MessageField.Font;
            MessagePlaceholderLabel.TextColor = A.Color_NachoTextGray;
            MessagePlaceholderLabel.Text = "Type a message...";
            SendButton = new UIButton (UIButtonType.Custom);
            SendButton.SetTitle ("Send", UIControlState.Normal);
            SendButton.TouchUpInside += Send;
            SendButton.SetTitleColor (A.Color_NachoGreen, UIControlState.Normal);
            SendButton.SetTitleColor (A.Color_NachoTextGray, UIControlState.Disabled);
            SendButton.TitleEdgeInsets = new UIEdgeInsets (0.0f, 7.0f, 0.5f, 7.0f);
            SendButton.Font = A.Font_AvenirNextMedium17;
            AttachmentsScrollView = new UIScrollView (new CGRect(0.0f, Bounds.Height, Bounds.Width, 0.0f));
            AddSubview (TopBorderView);
            AddSubview (AttachButton);
            AddSubview (MessageField);
            AddSubview (MessagePlaceholderLabel);
            AddSubview (SendButton);
            AddSubview (AttachmentsScrollView);
            UpdateSendEnabled ();
        }

        void MessageChanged (object sender, EventArgs e)
        {
            UpdateSendEnabled ();
            ResizeMessageField ();
        }

        void ResizeMessageField ()
        {
            var frame = MessageField.Frame;
            MessageField.Frame = new CGRect (frame.X, frame.Y, frame.Width, 1.0f);
            if (MessageField.ContentSize.Height != frame.Height) {
                SetNeedsLayout ();
                LayoutIfNeeded ();
            } else {
                MessageField.Frame = frame;
            }
        }

        void Send (object sender, EventArgs e)
        {
            ComposeDelegate.ChatComposeDidSend (this);
        }

        public void SetEnabled (bool isEnabled)
        {
            SendButton.Enabled = isEnabled;
            AttachButton.Enabled = isEnabled;
        }

        void Attach (object sender, EventArgs e)
        {
            ComposeDelegate.ChatComposeWantsAttachment (this);
        }

        public string GetMessage ()
        {
            return MessageField.Text;
        }

        public void SetMessage (string text)
        {
            if (text != null) {
                MessageField.Text = text;
            } else {
                MessageField.Text = "";
            }
            UpdateSendEnabled ();
            ResizeMessageField ();
        }

        public void Clear ()
        {
            MessageField.Text = "";
            UpdateSendEnabled ();
            foreach (var view in AttachmentViews) {
                view.RemoveFromSuperview ();
                view.Dispose ();
            }
            AttachmentViews.Clear ();
            SetNeedsLayout ();
        }

        public void UpdateSendEnabled ()
        {
            bool controllerCanSend = ComposeDelegate != null && ComposeDelegate.ChatComposeCanSend (this);
            bool hasText = !String.IsNullOrWhiteSpace (MessageField.Text);
            bool hasAttachment = AttachmentViews.Count > 0;
            SendButton.Enabled = controllerCanSend && (hasText || hasAttachment);
            MessagePlaceholderLabel.Hidden = hasText;
        }

        public override void LayoutSubviews ()
        {
            nfloat maxHeight = 5.0f * STANDARD_HEIGHT;
            var preferredHeight = Math.Max (MessageField.ContentSize.Height + 1.0f, STANDARD_HEIGHT);
            if (AttachmentViews.Count > 0) {
                nfloat attachmentsHeight = 0.0f;
                nfloat y = 0.0f;
                foreach (var attachmentView in AttachmentViews) {
                    preferredHeight += attachmentView.Frame.Height;
                    attachmentView.Frame = new CGRect (0.0f, y, Bounds.Width, attachmentView.Frame.Height);
                    y += attachmentView.Frame.Height;
                    attachmentsHeight += attachmentView.Frame.Height;
                }
                AttachmentsScrollView.ContentSize = new CGSize (AttachmentsScrollView.Bounds.Width, attachmentsHeight);
                if (preferredHeight > maxHeight) {
                    attachmentsHeight = (nfloat)Math.Min (maxHeight - STANDARD_HEIGHT, attachmentsHeight);
                    preferredHeight = maxHeight;
                }
                AttachmentsScrollView.Frame = new CGRect (0.0f, preferredHeight - attachmentsHeight, Bounds.Width, attachmentsHeight);
            } else {
                preferredHeight = (nfloat)Math.Min (preferredHeight, maxHeight);
                AttachmentsScrollView.Frame = new CGRect (0.0f, preferredHeight, Bounds.Width, 0.0f);
            }
            var previousHeight = Frame.Height;
            Frame = new CGRect (Frame.X, Frame.Y, Frame.Width, preferredHeight);
            bool changedHeight = (Math.Abs (preferredHeight - previousHeight) > 0.5) && ComposeDelegate != null;
            SendButton.SizeToFit ();
            var sendButtonHeight = STANDARD_HEIGHT - 1.0f;
            SendButton.Frame = new CGRect (
                Bounds.Width - SendButton.Frame.Size.Width - SendButton.TitleEdgeInsets.Left - SendButton.TitleEdgeInsets.Right,
                AttachmentsScrollView.Frame.Y - sendButtonHeight,
                SendButton.Frame.Size.Width + SendButton.TitleEdgeInsets.Left + SendButton.TitleEdgeInsets.Right,
                sendButtonHeight
            );
            AttachButton.Frame = new CGRect (
                0.0f,
                AttachmentsScrollView.Frame.Y - STANDARD_HEIGHT,
                STANDARD_HEIGHT,
                STANDARD_HEIGHT
            );
            MessageField.Frame = new CGRect (
                AttachButton.Frame.X + AttachButton.Frame.Width,
                1.0f,
                SendButton.Frame.X - AttachButton.Frame.X - AttachButton.Frame.Width,
                AttachmentsScrollView.Frame.Y - 1.0f
            );
            MessagePlaceholderLabel.SizeToFit ();
            MessagePlaceholderLabel.Frame = new CGRect (MessageField.Frame.X + 5.0f, MessageField.Frame.Y + 8.0f, MessagePlaceholderLabel.Frame.Width, MessagePlaceholderLabel.Frame.Height);
            if (changedHeight) {
                ComposeDelegate.ChatComposeChangedHeight (this);
            }
        }

        public void AddAttachment (McAttachment attachment)
        {
            var view = new UcAttachmentCell (attachment, Bounds.Width, true, TapAttachment, RemoveAttachment);
            AttachmentsScrollView.AddSubview (view);
            AttachmentViews.Add (view);
            UpdateSendEnabled ();
            SetNeedsLayout ();
        }

        public void UpdateAttachment (McAttachment attachment)
        {
            foreach (var view in AttachmentViews) {
                if (view.attachment.Id == attachment.Id) {
                    view.attachment = attachment;
                    view.ConfigureView ();
                }
            }
        }

        public void TapAttachment (UcAttachmentCell cell)
        {
            ComposeDelegate.ChatComposeShowAttachment (this, cell.attachment);
        }

        public void RemoveAttachment (UcAttachmentCell cell)
        {
            AttachmentViews.Remove (cell);
            cell.RemoveFromSuperview ();
            cell.Dispose ();
            UpdateSendEnabled ();
            SetNeedsLayout ();
            ComposeDelegate.ChatComposeDidRemoveAttachment (this, cell.attachment);
        }

    }

}
