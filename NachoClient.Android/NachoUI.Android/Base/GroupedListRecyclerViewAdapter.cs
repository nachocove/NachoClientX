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
using Android.Support.V7.Widget;
using NachoCore.Model;
using NachoCore;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public abstract class GroupedListRecyclerViewAdapter : RecyclerView.Adapter
    {
        private static int HEADER_ITEM_POSITION = -1;
        private static int FOOTER_ITEM_POSITION = -2;

        #region Methods For Subclasses

        public virtual bool HasFooters {
            get {
                return true;
            }
        }

        public abstract int GroupCount { get; }

        public abstract int GroupItemCount (int groupPosition);

        public virtual int GetItemViewType (int groupPosition, int position)
        {
            return 0;
        }

        public virtual int GetHeaderViewType (int groupPosition)
        {
            return HeaderItemViewHolder.VIEW_TYPE;
        }

        public virtual int GetFooterViewType (int groupPosition)
        {
        	return FooterItemViewHolder.VIEW_TYPE;
        }

        public abstract RecyclerView.ViewHolder OnCreateGroupedViewHolder (ViewGroup parent, int viewType);

        public abstract void OnBindViewHolder (RecyclerView.ViewHolder holder, int groupPosition, int position);

        public virtual string GroupHeaderValue (Context context, int groupPosition)
        {
            return null;
        }

        public virtual void OnBindHeaderViewHolder (RecyclerView.ViewHolder holder, int groupPosition)
        {
            var headerValue = GroupHeaderValue (holder.ItemView.Context, groupPosition);
            (holder as HeaderItemViewHolder).SetHeader (headerValue);
        }

        public virtual void OnBindFooterViewHolder (RecyclerView.ViewHolder holder, int groupPosition)
        {
            // Nothing to do for footers because they have no text/values, but subclasses can override
        }

        public virtual void OnViewHolderClick (RecyclerView.ViewHolder holder, int groupPosition, int position)
        {
        }

        #endregion

        #region RecyclerView Overrides

        public override int ItemCount {
            get {
                int groups = GroupCount;
                int count = 0;
                for (int i = 0; i < groups; ++i) {
                    count += GroupItemCount (i) + 1 + (HasFooters ? 1 : 0);
                }
                return count;
            }
        }

        public override int GetItemViewType (int position)
        {
            int groupPosition;
            int itemPosition;
            GetGroupPosition(position, out groupPosition, out itemPosition);
            if (itemPosition == HEADER_ITEM_POSITION){
                return GetHeaderViewType (groupPosition);
            }else if (itemPosition == FOOTER_ITEM_POSITION){
                return GetFooterViewType (groupPosition);
            }else{
                return GetItemViewType(groupPosition, itemPosition);
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            switch (viewType){
            case HeaderItemViewHolder.VIEW_TYPE:
                return HeaderItemViewHolder.Create (parent);
            case FooterItemViewHolder.VIEW_TYPE:
                return FooterItemViewHolder.Create (parent);
            }
            var holder = OnCreateGroupedViewHolder(parent, viewType) as ViewHolder;
            holder.ItemView.Click += (sender, e) => {
                OnViewHolderClick (holder, holder.groupPosition, holder.itemPosition);
            };
            return holder;
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            var groupedHolder = (holder as ViewHolder);
            GetGroupPosition (position, out groupedHolder.groupPosition, out groupedHolder.itemPosition);
            if (groupedHolder.itemPosition == HEADER_ITEM_POSITION){
                OnBindHeaderViewHolder (holder, groupedHolder.groupPosition);
            }else if (groupedHolder.itemPosition == FOOTER_ITEM_POSITION){
                OnBindFooterViewHolder (holder, groupedHolder.groupPosition);
            }else{
                OnBindViewHolder(holder, groupedHolder.groupPosition, groupedHolder.itemPosition);
            }
        }

        private void GetGroupPosition (int position, out int groupPosition, out int itemPosition)
        {
            int groupCount = GroupCount;
            int groupItemCount;
            groupPosition = 0;
            itemPosition = HEADER_ITEM_POSITION;
            for (; groupPosition < groupCount; ++groupPosition){
                itemPosition = HEADER_ITEM_POSITION;
                if (position == 0){
                    return;
                }
                position -= 1;
                groupItemCount = GroupItemCount(groupPosition);
                if (position < groupItemCount){
                    itemPosition = position;
                    return;
                }
                position -= groupItemCount;
                if (HasFooters) {
                    if (position == 0) {
                        itemPosition = FOOTER_ITEM_POSITION;
                        return;
                    }
                    position -= 1;
                }
            }
            throw new NcAssert.NachoDefaultCaseFailure (String.Format ("GroupedListRecyclerViewAdapter.GetGroupPosition: Unexpecetd position: {0}", position));
        }

        private int GetPosition (int groupPosition, int itemPosition)
        {
            int position = 0;
            for (int i = 0; i < groupPosition; ++i) {
                position += GroupItemCount (i) + 1 + (HasFooters ? 1 : 0); // header and footer
            }
            position += 1; // header
            position += itemPosition;
            return position;
        }

        #endregion

        #region Update Notifications

        public void NotifyItemChanged (int groupPosition, int itemPosition)
        {
            var position = GetPosition (groupPosition, itemPosition);
            NotifyItemChanged (position);
        }

        public void NotifyItemRemoved (int groupPosition, int itemPosition)
        {
            var position = GetPosition (groupPosition, itemPosition);
            NotifyItemRemoved (position);
        }

        public void NotifyItemRangeInserted (int groupPosition, int positionStart, int count)
        {
            var position = GetPosition (groupPosition, positionStart);
            NotifyItemRangeInserted (position, count);
        }

        #endregion

        #region View Holders

        public class ViewHolder : RecyclerView.ViewHolder
        {
            public int groupPosition;
            public int itemPosition;

            public ViewHolder (View view) : base (view)
            {
            }
        }

        public class HeaderItemViewHolder : ViewHolder
        {

            public const int VIEW_TYPE = -1;

            TextView HeaderTextView;

            public static HeaderItemViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ListHeaderItem, parent, false);
                return new HeaderItemViewHolder (view);
            }

            public HeaderItemViewHolder (View view) : base (view)
            {
                HeaderTextView = view.FindViewById (Resource.Id.section_name) as TextView;
            }

            public void SetHeader (string header)
            {
                if (String.IsNullOrEmpty(header)){
                    HeaderTextView.Visibility = ViewStates.Gone;
                }else{
                    HeaderTextView.Visibility = ViewStates.Visible;
                    HeaderTextView.Text = header;
                }
            }

        }

        public class FooterItemViewHolder : ViewHolder
        {
            public const int VIEW_TYPE = -2;

            public static FooterItemViewHolder Create (ViewGroup parent)
            {
                var inflater = LayoutInflater.From (parent.Context);
                var view = inflater.Inflate (Resource.Layout.ListFooterItem, parent, false);
                return new FooterItemViewHolder (view);
            }

            public FooterItemViewHolder (View view) : base (view)
            {
            }

        }

        #endregion

    }
}
