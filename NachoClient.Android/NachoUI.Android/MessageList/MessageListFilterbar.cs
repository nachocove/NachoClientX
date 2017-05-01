//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;

namespace NachoClient.AndroidClient
{
    public class MessageListFilterbar : LinearLayout
    {

        View Separator;
        LinearLayout ContentView;
        List<ImageView> ItemViews;
        Item[] Items;
        Toast LastToast;

        #region Creating a Filterbar

        public MessageListFilterbar (Context context, IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize (attrs);
        }

        public MessageListFilterbar (Context context, IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize (attrs);
        }

        void Initialize (IAttributeSet attrs)
        {
            Orientation = Orientation.Vertical;
            CreateContentView (attrs);
            CreateSeparator (attrs);
            ItemViews = new List<ImageView> ();
        }

        public void Cleanup ()
        {
            foreach (var itemView in ItemViews) {
                itemView.Click -= ItemViewClicked;
            }
        }

        #endregion

        #region Subview Creation

        void CreateContentView (IAttributeSet attrs)
        {
            var attrIds = new int [] {
                Resource.Attribute.contentPaddingLeft,
                Resource.Attribute.contentPaddingTop,
                Resource.Attribute.contentPaddingRight,
                Resource.Attribute.contentPaddingBottom
            };
            ContentView = new LinearLayout (Context);
            ContentView.Orientation = Orientation.Horizontal;
            ContentView.SetGravity (GravityFlags.Center);
            var layoutParams = new LinearLayout.LayoutParams (LayoutParams.MatchParent, 0);
            layoutParams.Weight = 1.0f;
            ContentView.LayoutParameters = layoutParams;
            using (var values = new AttributeValues (Context, attrs, attrIds)) {
                ContentView.SetPadding (
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingLeft, ContentView.PaddingLeft),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingTop, ContentView.PaddingTop),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingRight, ContentView.PaddingRight),
                    values.GetDimensionPixelSize (Resource.Attribute.contentPaddingBottom, ContentView.PaddingBottom)
                );
            }
            AddView (ContentView);
        }

        void CreateSeparator (IAttributeSet attrs)
        {
            Separator = new View (Context);
            var height = (int)Android.Util.TypedValue.ApplyDimension (Android.Util.ComplexUnitType.Dip, 1.0f, Context.Resources.DisplayMetrics);
            var layoutParams = new LinearLayout.LayoutParams (LayoutParams.MatchParent, height);
            Separator.LayoutParameters = layoutParams;
            using (var values = new AttributeValues (Context, attrs, new int [] { Resource.Attribute.filterbarSeparatorColor })) {
                Separator.SetBackgroundColor (values.GetColor (Resource.Attribute.filterbarSeparatorColor, Android.Resource.Color.White));
            }
            AddView (Separator);
        }

        #endregion

        #region Items

        public void SetItems (Item[] items)
        {
            Items = items;
            RegenerateItemViews ();
        }

        void RegenerateItemViews ()
        {
            Item item;
            ImageView itemView;
            int i;
            for (i = 0; i < Items.Length; ++i) {
                item = Items [i];
                if (i < ItemViews.Count) {
                    itemView = ItemViews [i];
                } else {
                    itemView = CreateItemView ();
                    ItemViews.Add (itemView);
                    ContentView.AddView (itemView);
                }
                itemView.Id = i;
                itemView.SetImageResource (item.ImageId);
            }
            for (var j = ItemViews.Count - 1; j >= i; --j) {
                itemView = ItemViews [j];
                ContentView.RemoveView (itemView);
                itemView.Click -= ItemViewClicked;
                ItemViews.RemoveAt (j);
            }
        }

        ImageView CreateItemView ()
        {
            var selectableBackground = Context.Theme.ObtainStyledAttributes (new int [] { Android.Resource.Attribute.SelectableItemBackground }).GetResourceId (0, 0);
            var itemView = new ImageView (Context);
            var layoutParams = new LinearLayout.LayoutParams (0, LayoutParams.MatchParent);
            layoutParams.Weight = 1.0f;
            itemView.LayoutParameters = layoutParams;
            itemView.SetScaleType (ImageView.ScaleType.Center);
            itemView.Clickable = true;
            itemView.SetBackgroundResource (selectableBackground);
            itemView.Click += ItemViewClicked;
            return itemView;
        }

        public void SelectItem (Item selectedItem)
        {
            for (var i = 0; i < Items.Length; ++i) {
                if (selectedItem == Items [i]) {
                    ItemViews [i].SetImageResource (Items [i].SelectedImageId);
                } else {
                    ItemViews [i].SetImageResource (Items [i].ImageId);
                }
            }
        }

        void ItemViewClicked (object sender, EventArgs e)
        {
            var i = (sender as ImageView).Id;
            var item = Items [i];
            item.Action ();
            SelectItem (item);
            var toast = Toast.MakeText (Context, item.TitleId, ToastLength.Short);
            int [] location = new int [2];
            GetLocationInWindow (location);
            toast.SetGravity (GravityFlags.Top | GravityFlags.CenterHorizontal, 0, location [1] + Height);
            if (LastToast != null) {
                LastToast.Cancel ();
            }
            toast.Show ();
            LastToast = toast;
        }

        public class Item
        {

            public readonly int ImageId;
            public readonly int SelectedImageId;
            public readonly int TitleId;
            public readonly Action Action;

            public Item (int titleId, int imageId, int selectedImageId, Action action)
            {
                TitleId = titleId;
                ImageId = imageId;
                SelectedImageId = selectedImageId;
                Action = action;
            }

        }

        #endregion

    }
}
