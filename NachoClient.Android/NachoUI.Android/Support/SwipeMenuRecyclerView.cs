
using System;
using Android.Content;
using Android.Graphics.Drawables;

using System.Collections.Generic;
using Android.Widget;
using Android.Views;
using Android.Support.V4.View;
using Android.Support.V4.Widget;
using Android.Graphics;
using Android.Database;
using Android.Util;
using Android.Text;
using Android.Views.Animations;
using Android.Support.V7.Widget;

namespace NachoClient.AndroidClient
{
    
    public class SwipeMenuRecyclerView : RecyclerView
    {
        public delegate bool OnMenuItemClickListener (int position, SwipeMenu menu, int index);

        public delegate void OnSwipeStartListener (int position);

        public delegate void OnSwipeEndListener (int position);

        public delegate void OnMenuOpenListener (int position);

        public delegate void OnMenuCloseListener (int position);

        private const int TOUCH_STATE_NONE = 0;
        private const int TOUCH_STATE_X = 1;
        private const int TOUCH_STATE_Y = 2;

        private  const int MOVE_DURATION = 150;

        private int MAX_Y = 10;
        private int MAX_X = 10;
        private float mDownX;
        private float mDownY;
        private int mTouchState;
        private int mTouchPosition;
        private SwipeMenuLayout mTouchView;
        private OnSwipeStartListener mOnSwipeStartListener;
        private OnSwipeEndListener mOnSwipeEndListener;
        private OnMenuItemClickListener mOnMenuItemClickListener;

        private OnMenuOpenListener mOnMenuOpenListener;
        private OnMenuCloseListener mOnMenuCloseListener;
        private IInterpolator mCloseInterpolator;
        private IInterpolator mOpenInterpolator;

        private bool mDisableSwipes;

        public SwipeMenuRecyclerView (Context context) : base (context)
        {
            init ();
        }

        public SwipeMenuRecyclerView (Context context, IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
        {
            init ();
        }

        public SwipeMenuRecyclerView (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            init ();
        }

        private void init ()
        {
            MAX_X = dp2px (MAX_X);
            MAX_Y = dp2px (MAX_Y);
            mTouchState = TOUCH_STATE_NONE;
        }

        public override void SetAdapter (RecyclerView.Adapter adapter)
        {
            var a = new SwipeMenuRecyclerAdapter (this.Context, adapter);
            a.setOnMenuItemClickListener (MenuItemClickListener);
            base.SetAdapter (a);
        }

        public override RecyclerView.Adapter GetAdapter ()
        {
            return base.GetAdapter ();
        }

        public void EnableSwipe (bool enable)
        {
            mDisableSwipes = !enable;
        }

        public void MenuItemClickListener (SwipeMenuView view, SwipeMenu menu, int index)
        {
            bool flag = false;
            if (mOnMenuItemClickListener != null) {
                flag = mOnMenuItemClickListener (view.getPosition (), menu, index);
            }
            if (mTouchView != null && !flag) {
                mTouchView.smoothCloseMenu ();
            }
        }

        public void setCloseInterpolator (IInterpolator interpolator)
        {
            mCloseInterpolator = interpolator;
        }

        public void setOpenInterpolator (IInterpolator interpolator)
        {
            mOpenInterpolator = interpolator;
        }

        public IInterpolator getOpenInterpolator ()
        {
            return mOpenInterpolator;
        }

        public IInterpolator getCloseInterpolator ()
        {
            return mCloseInterpolator;
        }

        public override bool OnInterceptTouchEvent (MotionEvent ev)
        {
            var action = ev.Action;
            switch (action) {
            case MotionEventActions.Down:
                mDownX = ev.GetX ();
                mDownY = ev.GetY ();
                var handled = base.OnInterceptTouchEvent (ev);
                mTouchState = TOUCH_STATE_NONE;
                var view = FindChildViewUnder (ev.GetX (), ev.GetY ());
                if (null != view) {
                    var layoutManager = this.GetLayoutManager ();
                    mTouchPosition = layoutManager.GetPosition (view);
                    if ((null == mTouchView) && (view is SwipeMenuLayout)) {
                        mTouchView = (SwipeMenuLayout)view;
                    }
                    if (mTouchView != null && mTouchView.isOpen ()) {
                        if ((mTouchView == view) && mTouchView.touchingMenu (ev.GetX ())) {
                            return false;
                        }
                        handled = true;
                    }
                    if ((mTouchView == null) || !mTouchView.isOpen ()) {
                        mTouchView = (SwipeMenuLayout)view;
                    }
                    if (mTouchView != null) {
                        mTouchView.OnSwipe (ev);
                    }
                }
                return handled;
            case MotionEventActions.Move:
                float dy = Math.Abs ((ev.GetY () - mDownY));
                float dx = Math.Abs ((ev.GetX () - mDownX));
                if (Math.Abs (dy) > MAX_Y || Math.Abs (dx) > MAX_X) {
                    return true;
                }
                break;
            }
            return base.OnInterceptTouchEvent (ev);
        }

        public override bool OnTouchEvent (MotionEvent ev)
        {
            if (mDisableSwipes) {
                return base.OnTouchEvent (ev);
            }
            if (ev.Action != MotionEventActions.Down && mTouchView == null) {
                return base.OnTouchEvent (ev);
            }
            switch (ev.Action) {
            case MotionEventActions.Down:
                int oldPos = mTouchPosition;
                mDownX = ev.GetX ();
                mDownY = ev.GetY ();
                mTouchState = TOUCH_STATE_NONE;

                var view = FindChildViewUnder (ev.GetX (), ev.GetY ());
                if (null != view) {
                    var layoutManager = this.GetLayoutManager ();
                    mTouchPosition = layoutManager.GetPosition (view);
                    if (mTouchView != null && mTouchView.isOpen ()) {
                        mTouchView.smoothCloseMenu ();
                        mTouchView = null;
                        // return super.onTouchEvent(ev);
                        // try to cancel the touch event
                        MotionEvent cancelEvent = MotionEvent.Obtain (ev);
                        cancelEvent.Action = MotionEventActions.Cancel;
                        OnTouchEvent (cancelEvent);
                        if (mOnMenuCloseListener != null) {
                            mOnMenuCloseListener (oldPos);
                        }
                        return true;
                    }
                    if (view is SwipeMenuLayout) {
                        mTouchView = (SwipeMenuLayout)view;
                    }
                    if (mTouchView != null) {
                        mTouchView.OnSwipe (ev);
                    }
                }
                break;
            case MotionEventActions.Move:
                float dy = Math.Abs ((ev.GetY () - mDownY));
                float dx = Math.Abs ((ev.GetX () - mDownX));
                if (mTouchState == TOUCH_STATE_X) {
                    if (mTouchView != null) {
                        mTouchView.OnSwipe (ev);
                    }
                    // FIXME
                    // this.Selector.SetState (new int[] { 0 });
                    ev.Action = MotionEventActions.Cancel;
                    base.OnTouchEvent (ev);
                    return true;
                } else if (mTouchState == TOUCH_STATE_NONE) {
                    if (Math.Abs (dy) > MAX_Y) {
                        mTouchState = TOUCH_STATE_Y;
                    } else if (dx > MAX_X) {
                        mTouchState = TOUCH_STATE_X;
                        if (mOnSwipeStartListener != null) {
                            mOnSwipeStartListener (mTouchPosition);
                        }
                    }
                }
                break;
            case MotionEventActions.Up:
                if (mTouchState == TOUCH_STATE_X) {
                    if (mTouchView != null) {
                        bool isBeforeOpen = mTouchView.isOpen ();
                        mTouchView.OnSwipe (ev);
                        bool isAfterOpen = mTouchView.isOpen ();
                        if (isBeforeOpen != isAfterOpen) {
                            if (isAfterOpen) {
                                if (null != mOnMenuOpenListener) {
                                    mOnMenuOpenListener (mTouchPosition);
                                }
                            } else {
                                if (null != mOnMenuCloseListener) {
                                    mOnMenuCloseListener (mTouchPosition);
                                }
                            }
                        }
                        if (!isAfterOpen) {
                            mTouchPosition = -1;
                            mTouchView = null;
                        }
                    }
                    if (mOnSwipeEndListener != null) {
                        mOnSwipeEndListener (mTouchPosition);
                    }
                    ev.Action = MotionEventActions.Cancel;
                    base.OnTouchEvent (ev);
                    return true;
                }
                break;
            default:
                Log.Info ("SwipeMenuListView", "unhandled action=" + ev.Action);
                if (mTouchView != null) {
                    mTouchView.smoothCloseMenu ();
                }
                break;
            }
            return base.OnTouchEvent (ev);
        }

        public void smoothCloseMenu ()
        {
            if (mTouchView != null && mTouchView.isOpen ()) {
                mTouchView.smoothCloseMenu ();
            }
        }

        private int dp2px (int dp)
        {
            return (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, (float)dp, Context.Resources.DisplayMetrics);
        }

        public void setMenuCreator (SwipeMenuCreator menuCreator)
        {
            var adapter = (SwipeMenuRecyclerAdapter)GetAdapter ();
            adapter.setMenuCreator (menuCreator);
        }

        public void setOnMenuItemClickListener (OnMenuItemClickListener onMenuItemClickListener)
        {
            mOnMenuItemClickListener = onMenuItemClickListener;
        }

        public void setOnSwipeStartListener (OnSwipeStartListener onSwipeStartListener)
        {
            this.mOnSwipeStartListener = onSwipeStartListener;
        }

        public void setOnSwipeEndListener (OnSwipeEndListener onSwipeEndListener)
        {
            this.mOnSwipeEndListener = onSwipeEndListener;
        }

        public void setOnMenuOpenListener (OnMenuOpenListener onMenuOpenListener)
        {
            mOnMenuOpenListener = onMenuOpenListener;
        }

        public void setOnMenuCloseListener (OnMenuCloseListener onMenuCloseListener)
        {
            mOnMenuCloseListener = onMenuCloseListener;
        }
    }

    public class SwipeMenuRecyclerAdapter : WrapperRecyclerAdapter, SwipeMenuView.OnSwipeItemClickListener
    {
        public delegate void OnMenuItemClickListener (SwipeMenuView view, SwipeMenu menu, int index);

        private RecyclerView.Adapter mAdapter;
        private Context mContext;
        private OnMenuItemClickListener onMenuItemClickListener;

        private SwipeMenuCreator mMenuCreator;

        public SwipeMenuRecyclerAdapter (Context context, RecyclerView.Adapter adapter) : base (adapter)
        {
            mAdapter = adapter;
            mContext = context;
        }

        public override int ItemCount {
            get {
                return mAdapter.ItemCount;
            }
        }

        public override long GetItemId (int position)
        {
            return mAdapter.GetItemId (position);
        }

        public override int GetItemViewType (int position)
        {
            return mAdapter.GetItemViewType (position);
        }


        public class MenuViewHolder : RecyclerView.ViewHolder
        {
            public RecyclerView.ViewHolder vh;

            public MenuViewHolder (View itemView, RecyclerView.ViewHolder vh) : base (itemView)
            {
                this.vh = vh;
            }
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder (ViewGroup parent, int viewType)
        {
            var vh = mAdapter.OnCreateViewHolder (parent, viewType);

            // Create a whole new SwipeMenuLayout object.
            var menu = new SwipeMenu (mContext);
            menu.setViewType (viewType);
            if (null != mMenuCreator) {
                mMenuCreator (menu);
            }
            var leftMenuView = new SwipeMenuView (menu, SwipeMenu.SwipeSide.LEFT);
            leftMenuView.setOnSwipeItemClickListener (this);
            var rightMenuView = new SwipeMenuView (menu, SwipeMenu.SwipeSide.RIGHT);
            rightMenuView.setOnSwipeItemClickListener (this);
            var listView = (SwipeMenuRecyclerView)parent;
            var layout = new SwipeMenuLayout (vh.ItemView,
                             leftMenuView, rightMenuView,
                             listView.getCloseInterpolator (),
                             listView.getOpenInterpolator ());
            vh.ItemView = layout;
            return vh;
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position)
        {
            mAdapter.OnBindViewHolder (holder, position);
            var layout = (SwipeMenuLayout)holder.ItemView;
            layout.setPosition (position);
        }

        public override void OnBindViewHolder (RecyclerView.ViewHolder holder, int position, IList<Java.Lang.Object> payloads)
        {
            mAdapter.OnBindViewHolder (holder, position, payloads);
            var layout = (SwipeMenuLayout)holder.ItemView;
            layout.setPosition (position);
        }

        public void onItemClick (SwipeMenuView view, SwipeMenu menu, int index)
        {
            if (onMenuItemClickListener != null) {
                onMenuItemClickListener (view, menu, index);
            }
        }

        public void setOnMenuItemClickListener (OnMenuItemClickListener onMenuItemClickListener)
        {
            this.onMenuItemClickListener = onMenuItemClickListener;
        }

        public void setMenuCreator (SwipeMenuCreator menuCreator)
        {
            this.mMenuCreator = menuCreator;
        }

    }
}