//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Views;
using Android.Widget;
using Android.Support.V7.Widget;
using Android.Content;
using Android.Text;
using NachoCore.Utils;
using NachoCore.Model;
using NachoPlatform;
using System.Collections.Generic;
using System.IO;

namespace NachoClient.AndroidClient
{

    public class MessageComposeHeaderField : LinearLayout
    {

        public TextView Label { get; private set; }
        protected LinearLayout ContainerView { get; private set; }
        protected View SeparatorView { get; private set; }
        protected LinearLayout ContentView { get; private set; }

        public MessageComposeHeaderField (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            Orientation = Orientation.Vertical;
            CreateContainerView (attrs);
            CreateSeparator (attrs);
            CreateLabel (attrs);
            CreateContentView ();

            AddView (ContainerView);
            AddView (SeparatorView);
            ContainerView.AddView (Label);
            ContainerView.AddView (ContentView);
        }

        void CreateContainerView (Android.Util.IAttributeSet attrs)
        {
            var attrIds = new int [] {
                Resource.Attribute.contentPaddingLeft,
                Resource.Attribute.contentPaddingTop,
                Resource.Attribute.contentPaddingRight,
                Resource.Attribute.contentPaddingBottom
            };
            ContainerView = new LinearLayout (Context);
            ContainerView.LayoutParameters = new LinearLayout.LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            ContainerView.Orientation = Orientation.Horizontal;
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                ContainerView.SetPadding (
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingLeft, ContainerView.PaddingLeft),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingTop, ContainerView.PaddingTop),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingRight, ContainerView.PaddingRight),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingBottom, ContainerView.PaddingBottom)
                );
            }
        }

        void CreateSeparator (Android.Util.IAttributeSet attrs)
        {
            var attrIds = new int [] {
                Resource.Attribute.separatorColor
            };
            SeparatorView = new View (Context);
            var height = (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, 1.0f, Context.Resources.DisplayMetrics);
            SeparatorView.LayoutParameters = new LinearLayout.LayoutParams (LayoutParams.MatchParent, height);
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                SeparatorView.SetBackgroundColor (values.GetColor (Resource.Attribute.separatorColor, Android.Resource.Color.White));
            }
        }

        void CreateLabel (Android.Util.IAttributeSet attrs)
        {
            var attrIds = new int [] {
                Resource.Attribute.labelTextAppearance,
                Resource.Attribute.labelMarginTop,
                Resource.Attribute.labelMarginRight,
                Resource.Attribute.labelWidth,
                Resource.Attribute.labelText
            };
            Label = new TextView (Context);
            Label.SetSingleLine (true);
            Label.Ellipsize = TextUtils.TruncateAt.End;
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                var width = values.GetDimensionPixelSize (Resource.Attribute.labelWidth, LayoutParams.WrapContent);
                var layoutParams = new LinearLayout.LayoutParams (width, LayoutParams.WrapContent);
                layoutParams.Gravity = GravityFlags.Top;
                layoutParams.TopMargin = values.GetDimensionPixelSize (Resource.Attribute.labelMarginTop, layoutParams.TopMargin);
                layoutParams.RightMargin = values.GetDimensionPixelSize (Resource.Attribute.labelMarginRight, layoutParams.RightMargin);
                Label.LayoutParameters = layoutParams;

                var textAppearance = values.GetResourceId (Resource.Attribute.labelTextAppearance, 0);
                if (textAppearance != 0) {
                    Label.SetTextAppearanceCompat (textAppearance);
                }

                Label.Text = values.GetString (Resource.Attribute.labelText);
            }

        }

        void CreateContentView ()
        {
            ContentView = new LinearLayout (Context);
            var layoutParams = new LinearLayout.LayoutParams (0, LayoutParams.WrapContent);
            layoutParams.Weight = 1.0f;
            layoutParams.Gravity = GravityFlags.Top;
            ContentView.LayoutParameters = layoutParams;
        }
    }

    public class MessageComposeHeaderAddressField : MessageComposeHeaderField
    {
        public EmailAddressField AddressField { get; private set; }

        public MessageComposeHeaderAddressField (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            if (Id == 0) {
                Id = GenerateViewId ();
            }
            CreateAddressField (attrs);
            Clickable = true;
            Click += (sender, e) => {
                AddressField.RequestFocus ();
            };
        }

        void CreateAddressField (Android.Util.IAttributeSet attrs)
        {
            AddressField = new EmailAddressField (Context);
            var attrIds = new int [] {
                Resource.Attribute.valueTextAppearance
            };
            var layoutParams = new LinearLayout.LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                var textAppearance = values.GetResourceId (Resource.Attribute.valueTextAppearance, 0);
                if (textAppearance != 0) {
                    AddressField.SetTextAppearanceCompat (textAppearance);
                }
            }
            AddressField.Background = null;
            AddressField.SetPadding (0, 0, 0, 0);
            AddressField.DropDownAnchor = Id;
            var spacing = Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, 3.0f, Context.Resources.DisplayMetrics);
            AddressField.SetLineSpacing (spacing, 1.0f);
            AddressField.LayoutParameters = layoutParams;
            ContentView.AddView (AddressField);
        }
    }

    public class MessageComposeHeaderTextField : MessageComposeHeaderField
    {
        public EditText TextField { get; private set; }

        public MessageComposeHeaderTextField (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            CreateTextField (attrs);
            Clickable = true;
            Click += (sender, e) => {
                TextField.RequestFocus ();
            };
        }

        void CreateTextField (Android.Util.IAttributeSet attrs)
        {
            TextField = new EditText (Context);
            var attrIds = new int [] {
                Resource.Attribute.valueTextAppearance
            };
            var layoutParams = new LinearLayout.LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                var textAppearance = values.GetResourceId (Resource.Attribute.valueTextAppearance, 0);
                if (textAppearance != 0) {
                    TextField.SetTextAppearanceCompat (textAppearance);
                }
            }
            TextField.SetSingleLine (true);
            TextField.Background = null;
            TextField.SetPadding (0, 0, 0, 0);
            TextField.LayoutParameters = layoutParams;
            ContentView.AddView (TextField);
        }
    }

    public class MessageComposeHeaderLabelField : MessageComposeHeaderField
    {
        public TextView ValueLabel { get; private set; }

        public MessageComposeHeaderLabelField (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            CreateValueLabel (attrs);
        }

        void CreateValueLabel (Android.Util.IAttributeSet attrs)
        {
            ValueLabel = new TextView (Context);
            var attrIds = new int [] {
                Resource.Attribute.valueTextAppearance
            };
            var layoutParams = new LinearLayout.LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                var textAppearance = values.GetResourceId (Resource.Attribute.valueTextAppearance, 0);
                if (textAppearance != 0) {
                    ValueLabel.SetTextAppearanceCompat (textAppearance);
                }
            }
            ValueLabel.SetSingleLine (true);
            ValueLabel.Ellipsize = TextUtils.TruncateAt.End;
            ValueLabel.Background = null;
            ValueLabel.SetPadding (0, 0, 0, 0);
            ValueLabel.LayoutParameters = layoutParams;
            ContentView.AddView (ValueLabel);
        }
    }

    public class MessageComposeHeaderAttachmentsField : MessageComposeHeaderLabelField, MessageComposeAttachmentsAdapter.Listener
    {

        public RecyclerView ListView { get; private set; }
        public ImageView IconView { get; private set; }
        public event EventHandler AddAttachment;
        public event EventHandler<McAttachment> AttachmentClicked;
        public event EventHandler<McAttachment> AttachmentRemoved;
        public MessageComposeAttachmentsAdapter Adapter { get; private set; }

        public MessageComposeHeaderAttachmentsField (Context context, Android.Util.IAttributeSet attrs) : base (context, attrs)
        {
            CreateIconView (attrs);
            CreateRecyclerView (attrs);
            var values = Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.SelectableItemBackground });
            ContainerView.Clickable = true;
            ContainerView.SetBackgroundResource (values.GetResourceId (0, 0));
            ContainerView.Click += (sender, e) => {
                AddAttachment.Invoke (this, new EventArgs ());
            };
        }

        void CreateIconView (Android.Util.IAttributeSet attrs)
        {
            IconView = new ImageView (Context);
            var layoutParams = new LinearLayout.LayoutParams (LayoutParams.WrapContent, LayoutParams.WrapContent);
            layoutParams.Gravity = GravityFlags.CenterVertical;
            IconView.LayoutParameters = layoutParams;
            var attrIds = new int [] {
                Resource.Attribute.iconSrc
            };
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                var iconId = values.GetResourceId (Resource.Attribute.iconSrc, 0);
                if (iconId != 0) {
                    IconView.SetImageResource (iconId);
                }
            }
            ContainerView.AddView (IconView);
        }

        void CreateRecyclerView (Android.Util.IAttributeSet attrs)
        {
            ListView = new RecyclerView (Context);
            ListView.SetLayoutManager (new LinearLayoutManager (Context));
            var layoutParams = new LinearLayout.LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            ListView.LayoutParameters = layoutParams;
            Adapter = new MessageComposeAttachmentsAdapter (this);
            ListView.SetAdapter (Adapter);
            AddView (ListView, ChildCount - 1);
        }

        public void OnAttachmentSelected (McAttachment attachment)
        {
            AttachmentClicked.Invoke (this, attachment);
        }

        public void OnAttachmentRemoved (McAttachment attachment)
        {
            AttachmentRemoved.Invoke (this, attachment);
        }

    }

    public class MessageComposeAttachmentsAdapter : RecyclerView.Adapter
    {

        public interface Listener
        {
            void OnAttachmentSelected (McAttachment attachment);
            void OnAttachmentRemoved (McAttachment attachment);
        }

        List<McAttachment> Attachments;
        WeakReference<Listener> WeakListener;

        public MessageComposeAttachmentsAdapter (Listener listener) : base ()
        {
            WeakListener = new WeakReference<Listener> (listener);
        }

        public override int ItemCount {
            get {
                return Attachments != null ? Attachments.Count : 0;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            return AttachmentViewHolder.Create (parent);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var attachment = Attachments [position];
            var attachmentHolder = (holder as AttachmentViewHolder);
            attachmentHolder.SetAttachment (attachment);
            attachmentHolder.SetClickHandler ((sender, e) => {
                Listener listener;
                if (WeakListener.TryGetTarget (out listener)) {
                    listener.OnAttachmentSelected (attachment);
                }
            });
            attachmentHolder.SetRemoveHandler ((sender, e) => {
                Listener listener;
                if (WeakListener.TryGetTarget (out listener)) {
                    listener.OnAttachmentRemoved (attachment);
                    Attachments.RemoveAt (attachmentHolder.AdapterPosition);
                    NotifyItemRemoved (attachmentHolder.AdapterPosition);
                }
            });
        }

        public void SetAttachments (List<McAttachment> attachments)
        {
            Attachments = attachments;
            NotifyDataSetChanged ();
        }

        public void AddAttachment (McAttachment attachment)
        {
            Attachments.Add (attachment);
            NotifyItemInserted (Attachments.Count - 1);
        }

        class AttachmentViewHolder : RecyclerView.ViewHolder
        {
            ImageView IconView;
            TextView NameLabel;
            TextView DetailLabel;
            View DeleteButton;

            EventHandler ClickHandler;
            EventHandler RemoveHandler;

            public event EventHandler RemoveClicked;

            public static AttachmentViewHolder Create (ViewGroup parent)
            {
                var view = LayoutInflater.From (parent.Context).Inflate (Resource.Layout.MessageComposeAttachmentListItem, parent, false);
                return new AttachmentViewHolder (view);
            }

            public AttachmentViewHolder (View view) : base (view)
            {
                IconView = view.FindViewById (Resource.Id.icon) as ImageView;
                DeleteButton = view.FindViewById (Resource.Id.remove_button);
                NameLabel = view.FindViewById (Resource.Id.attachment_name) as TextView;
                DetailLabel = view.FindViewById (Resource.Id.attachment_detail) as TextView;
                DeleteButton.Click += (sender, e) => {
                    RemoveClicked.Invoke (this, new EventArgs ());
                };
            }

            public void SetAttachment (McAttachment attachment)
            {
                var name = Path.GetFileNameWithoutExtension (attachment.DisplayName);
                if (String.IsNullOrEmpty (name)) {
                    name = "(no name)";
                }
                IconView.SetImageResource (AttachmentHelper.FileIconFromExtension (attachment));
                NameLabel.Text = name;
                DetailLabel.Text = Pretty.GetAttachmentDetail (attachment);
            }

            public void SetClickHandler (EventHandler clickHandler)
            {
                if (ClickHandler != null) {
                    ItemView.Click -= ClickHandler;
                }
                ClickHandler = clickHandler;
                if (ClickHandler != null) {
                    ItemView.Click += ClickHandler;
                }
            }

            public void SetRemoveHandler (EventHandler removeHandler)
            {
                if (RemoveHandler != null) {
                    RemoveClicked -= RemoveHandler;
                }
                RemoveHandler = removeHandler;
                if (RemoveHandler != null) {
                    RemoveClicked += RemoveHandler;
                }
            }
        }
    }

}
