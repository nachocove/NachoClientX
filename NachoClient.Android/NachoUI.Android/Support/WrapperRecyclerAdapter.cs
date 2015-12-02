//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using Android.Support.V7.Widget;
using Android.Views;

namespace NachoClient.AndroidClient
{
    public class WrapperRecyclerAdapter : RecyclerView.Adapter
    {

        private RecyclerView.Adapter mAdapter;

        public WrapperRecyclerAdapter (RecyclerView.Adapter adapter)
        {
            base.HasStableIds = adapter.HasStableIds;
            mAdapter = adapter;
            mAdapter.RegisterAdapterDataObserver (new WrapperDataObserver (this));
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            return mAdapter.OnCreateViewHolder (parent, viewType);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            mAdapter.OnBindViewHolder (holder, position);
        }

        public override int GetItemViewType (int position)
        {
            return mAdapter.GetItemViewType (position);
        }

        public override void OnViewRecycled (Java.Lang.Object holder)
        {
            mAdapter.OnViewRecycled (holder);
        }

        public override void OnViewAttachedToWindow (Java.Lang.Object holder)
        {
            mAdapter.OnViewAttachedToWindow (holder);
        }

        public override void OnViewDetachedFromWindow (Java.Lang.Object holder)
        {
            mAdapter.OnViewDetachedFromWindow (holder);
        }

        public override void OnAttachedToRecyclerView (RecyclerView recyclerView)
        {
            mAdapter.OnAttachedToRecyclerView (recyclerView);
        }

        public override void OnDetachedFromRecyclerView (RecyclerView recyclerView)
        {
            mAdapter.OnDetachedFromRecyclerView (recyclerView);
        }

        public override long GetItemId (int position)
        {
            return mAdapter.GetItemId (position);
        }

        public override int ItemCount {
            get {
                return mAdapter.ItemCount;
            }
        }

        private class WrapperDataObserver : RecyclerView.AdapterDataObserver
        {
            WrapperRecyclerAdapter parent;

            public WrapperDataObserver (WrapperRecyclerAdapter parent)
            {
                this.parent = parent;
            }

            public override void OnChanged ()
            {
                base.OnChanged ();
                parent.NotifyDataSetChanged ();
            }

            public override void OnItemRangeChanged (int positionStart, int itemCount)
            {
                base.OnItemRangeChanged (positionStart, itemCount);
                parent.NotifyItemRangeChanged (positionStart, itemCount);
            }

            public override void OnItemRangeInserted (int positionStart, int itemCount)
            {
                base.OnItemRangeInserted (positionStart, itemCount);
                parent.NotifyItemRangeInserted (positionStart, itemCount);
            }

            public override void OnItemRangeRemoved (int positionStart, int itemCount)
            {
                base.OnItemRangeRemoved (positionStart, itemCount);
                parent.NotifyItemRangeRemoved (positionStart, itemCount);
            }

            public override void OnItemRangeMoved (int fromPosition, int toPosition, int itemCount)
            {
                // Android doesn't impleent NotifyItemRangeMoves with itemCount
                throw new NotImplementedException ();
            }
        }

        public RecyclerView.Adapter GetWrappedAdapter ()
        {
            return mAdapter;
        }
    }
}
