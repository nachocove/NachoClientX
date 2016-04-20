// Loosely based on https://github.com/baoyongzhang/SwipeMenuListView
// MIT license.

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

namespace NachoClient.AndroidClient
{
    public class SwipeMenuItem
    {
        private int id;
        private Context mContext;
        private String title;
        private Drawable icon;
        private Drawable background;
        private Color titleColor;
        private int titleSize;
        private int width;

        public SwipeMenuItem (Context context)
        {
            mContext = context;
        }

        public int getId ()
        {
            return id;
        }

        public void setId (int id)
        {
            this.id = id;
        }

        public Color getTitleColor ()
        {
            return titleColor;
        }

        public int getTitleSize ()
        {
            return titleSize;
        }

        public void setTitleSize (int titleSize)
        {
            this.titleSize = titleSize;
        }

        public void setTitleColor (Color titleColor)
        {
            this.titleColor = titleColor;
        }

        public String getTitle ()
        {
            return title;
        }

        public void setTitle (String title)
        {
            this.title = title;
        }

        public void setTitle (int resId)
        {
            setTitle (mContext.GetString (resId));
        }

        public Drawable getIcon ()
        {
            return icon;
        }

        public void setIcon (Drawable icon)
        {
            this.icon = icon;
        }

        public void setIcon (int resId)
        {
            this.icon = mContext.Resources.GetDrawable (resId);
        }

        public Drawable getBackground ()
        {
            return background;
        }

        public void setBackground (Drawable background)
        {
            this.background = background;
        }

        public void setBackground (int resId)
        {
            this.background = mContext.Resources.GetDrawable (resId);
        }

        public int getWidth ()
        {
            return width;
        }

        public void setWidth (int width)
        {
            this.width = width;
        }
    }

    public class SwipeMenu
    {
        public enum SwipeSide
        {
            LEFT,
            RIGHT,
        };

        private Context mContext;
        private List<SwipeMenuItem> mLeftItems;
        private List<SwipeMenuItem> mRightItems;
        private int mViewType;

        public SwipeMenu (Context context)
        {
            mContext = context;
            mLeftItems = new List<SwipeMenuItem> ();
            mRightItems = new List<SwipeMenuItem> ();
        }

        public Context Context {
            get { return mContext; }
        }

        public void addMenuItem (SwipeMenuItem item, SwipeSide side)
        {
            if (SwipeSide.LEFT == side) {
                mLeftItems.Add (item);
            } else {
                mRightItems.Add (item);
            }
        }

        public void removeMenuItem (SwipeMenuItem item, SwipeSide side)
        {
            if (SwipeSide.LEFT == side) {
                mLeftItems.Remove (item);
            } else {
                mRightItems.Remove (item);
            }
        }

        public List<SwipeMenuItem> getMenuItems (SwipeSide side)
        {
            if (SwipeSide.LEFT == side) {
                return mLeftItems;
            } else {
                return mRightItems;
            }
        }

        public SwipeMenuItem getMenuItem (int index, SwipeSide side)
        {
            if (SwipeSide.LEFT == side) {
                return mLeftItems [index];
            } else {
                return mRightItems [index];
            }
        }

        public int getViewType ()
        {
            return mViewType;
        }

        public void setViewType (int viewType)
        {
            this.mViewType = viewType;
        }
    }

    public delegate void SwipeMenuCreator (SwipeMenu Menu);

    public class SwipeMenuView : LinearLayout, View.IOnClickListener
    {
        private SwipeMenuLayout mLayout;
        private SwipeMenu mMenu;
        private OnSwipeItemClickListener onItemClickListener;
        private int position;
        private int mWidth;

        public int getPosition ()
        {
            return position;
        }

        public void setPosition (int position)
        {
            this.position = position;
        }

        public SwipeMenuView (SwipeMenu menu, SwipeMenu.SwipeSide side) : base (menu.Context)
        {
            mMenu = menu;
            mWidth = 0;
            List<SwipeMenuItem> items = menu.getMenuItems (side);
            foreach (var item in items) {
                addItem (item, item.getId ());
                mWidth += item.getWidth ();
            }
        }

        public int GetMenuWidth ()
        {
            return mWidth;
        }

        private void addItem (SwipeMenuItem item, int id)
        {
            var parameters = new Android.Widget.LinearLayout.LayoutParams (item.getWidth (), Android.Widget.LinearLayout.LayoutParams.MatchParent);
            LinearLayout parent = new LinearLayout (Context);
            parent.Id = id;
            parent.SetGravity (GravityFlags.Center);
            parent.Orientation = Orientation.Vertical;
            parent.LayoutParameters = parameters;
            parent.Background = item.getBackground ();
            parent.SetOnClickListener (this);
            AddView (parent);

            parent.Click += (object sender, EventArgs e) => {
                OnClick ((View)sender);
            };

            if (item.getIcon () != null) {
                parent.AddView (createIcon (item));
            }
            if (!TextUtils.IsEmpty (item.getTitle ())) {
                parent.AddView (createTitle (item));
            }
        }

        private ImageView createIcon (SwipeMenuItem item)
        {
            ImageView iv = new ImageView (Context);
            iv.SetImageDrawable (item.getIcon ());
            return iv;
        }

        private TextView createTitle (SwipeMenuItem item)
        {
            TextView tv = new TextView (Context);
            tv.Text = item.getTitle ();
            tv.Gravity = GravityFlags.Center;
            tv.TextSize = item.getTitleSize ();
            tv.SetTextColor (item.getTitleColor ());
            return tv;
        }

        public void OnClick (View v)
        {
            if (onItemClickListener != null && mLayout.isOpen ()) {
                onItemClickListener.onItemClick (this, mMenu, v.Id);
            }
        }

        public OnSwipeItemClickListener getOnSwipeItemClickListener ()
        {
            return onItemClickListener;
        }

        public void setOnSwipeItemClickListener (OnSwipeItemClickListener onItemClickListener)
        {
            this.onItemClickListener = onItemClickListener;
        }

        public void setLayout (SwipeMenuLayout mLayout)
        {
            this.mLayout = mLayout;
        }

        public  interface OnSwipeItemClickListener
        {
            void onItemClick (SwipeMenuView view, SwipeMenu menu, int index);
        }
    }

    public class SwipeMenuLayout : FrameLayout,  GestureDetector.IOnGestureListener
    {
        private const int CONTENT_VIEW_ID = 1;
        private const  int LEFT_MENU_VIEW_ID = 2;
        private const  int RIGHT_MENU_VIEW_ID = 3;

        private const  int STATE_CLOSE = 0;
        private const  int STATE_OPEN = 1;

        private View mContentView;
        private SwipeMenuView mLeftMenuView;
        private SwipeMenuView mRightMenuView;
        private int mDownX;
        private int state = STATE_CLOSE;
        private GestureDetectorCompat mGestureDetector;
        private GestureDetector.IOnGestureListener mGestureListener;
        private bool isFling;
        private  int MIN_FLING;
        private  int MAX_VELOCITYX;
        private ScrollerCompat mOpenScroller;
        private ScrollerCompat mCloseScroller;
        private int mBaseX;
        private int position;
        private IInterpolator mCloseInterpolator;
        private IInterpolator mOpenInterpolator;

        public SwipeMenuLayout (View contentView, SwipeMenuView leftMenuView, SwipeMenuView rightMenuView) :
            this (contentView, leftMenuView, rightMenuView, null, null)
        {
        }

        public SwipeMenuLayout (View contentView, SwipeMenuView leftMenuView, SwipeMenuView rightMenuView, IInterpolator closeInterpolator, IInterpolator openInterpolator) :
            base (contentView.Context)
        {
            MIN_FLING = dp2px (15);
            MAX_VELOCITYX = -dp2px (500);
            mCloseInterpolator = closeInterpolator;
            mOpenInterpolator = openInterpolator;
            mContentView = contentView;
            mLeftMenuView = leftMenuView;
            mLeftMenuView.setLayout (this);
            mRightMenuView = rightMenuView;
            mRightMenuView.setLayout (this);
            init ();
        }

        // private SwipeMenuLayout(Context context, IAttributeSet attrs, int
        // defStyle) {
        // super(context, attrs, defStyle);
        // }

        private SwipeMenuLayout (Context context, IAttributeSet attrs) : base (context, attrs)
        {
        }

        private SwipeMenuLayout (Context context) : base (context)
        {
        }

        public int getPosition ()
        {
            return position;
        }

        public void setPosition (int position)
        {
            this.position = position;
            mLeftMenuView.setPosition (position);
            mRightMenuView.setPosition (position);
        }

        public bool OnDown (MotionEvent e)
        {
            isFling = false;
            return true;
        }

        public bool OnFling (MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
            if (Math.Abs (e1.GetX () - e2.GetX ()) > MIN_FLING && velocityX < MAX_VELOCITYX) {
                isFling = true;
                return true;
            }
            return false;
        }

        public void OnLongPress (MotionEvent e)
        {
        }

        public bool OnScroll (MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
        {
            return false;
        }

        public void OnShowPress (MotionEvent e)
        {
        }

        public bool OnSingleTapUp (MotionEvent e)
        {
            return false;
        }

        private void init ()
        {
            LayoutParameters = new AbsListView.LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            mGestureListener = new Android.Views.GestureDetector.SimpleOnGestureListener ();

            mGestureDetector = new GestureDetectorCompat (Context, mGestureListener);

            // mScroller = ScrollerCompat.create(getContext(), new
            // BounceInterpolator());
            if (mCloseInterpolator != null) {
                mCloseScroller = ScrollerCompat.Create (Context, mCloseInterpolator);
            } else {
                mCloseScroller = ScrollerCompat.Create (Context);
            }
            if (mOpenInterpolator != null) {
                mOpenScroller = ScrollerCompat.Create (Context, mOpenInterpolator);
            } else {
                mOpenScroller = ScrollerCompat.Create (Context);
            }

            LayoutParams contentParams = new LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
            mContentView.LayoutParameters = contentParams;
            if (mContentView.Id < 1) {
                mContentView.Id = CONTENT_VIEW_ID;
            }

            mLeftMenuView.Id = LEFT_MENU_VIEW_ID;
            mLeftMenuView.LayoutParameters = new LayoutParams (LayoutParams.WrapContent, LayoutParams.WrapContent);

            mRightMenuView.Id = RIGHT_MENU_VIEW_ID;
            mRightMenuView.LayoutParameters = new LayoutParams (LayoutParams.WrapContent, LayoutParams.WrapContent);

            AddView (mContentView);
            AddView (mLeftMenuView);
            AddView (mRightMenuView);
        }

        protected override void OnAttachedToWindow ()
        {
            base.OnAttachedToWindow ();
        }

        protected override void OnSizeChanged (int w, int h, int oldw, int oldh)
        {
            base.OnSizeChanged (w, h, oldw, oldh);
        }

        private SwipeMenuView GetMenu (int dis)
        {
            if (0 == dis) {
                return null;
            } else if (0 > dis) {
                return mRightMenuView;
            } else {
                return mLeftMenuView;
            }
        }

        public void OnSwipe (MotionEvent ev)
        {
            int dis;
            mGestureDetector.OnTouchEvent (ev);
            switch (ev.Action) {
            case MotionEventActions.Down:
                mDownX = (int)ev.GetX ();
                isFling = false;
                break;
            case MotionEventActions.Move:
                dis = (int)(ev.GetX () - mDownX);
                // Log.i("byz", "onSwipe move dis=" + dis);
                swipe (dis);
                break;
            case MotionEventActions.Up:
                dis = (int)(ev.GetX () - mDownX);
                // Log.i ("byz", "onSwipe up dis=" + dis);
                if (isFling) {
                    smoothOpenMenu ();
                    return;
                } else if (isOpen () && (0 == dis)) {
                    smoothCloseMenu ();
                } else if (0 == dis) {
                    ;
                } else if (Math.Abs (dis) > (GetMenu (dis).GetMenuWidth () / 2)) {
                    smoothOpenMenu ();
                } else {
                    smoothCloseMenu ();
                }
                break;
            case MotionEventActions.Cancel:
                smoothCloseMenu ();
                break;
            default:
                Log.Info ("SwipeMenuLayout", "unhandled action=" + ev.Action);
                break;
            }
        }

        public bool isOpen ()
        {
            return state == STATE_OPEN;
        }

        public override bool OnTouchEvent (MotionEvent ev)
        {
            return base.OnTouchEvent (ev);
        }

        // Dis is the new left of mContentView
        private void swipe (int dis)
        {
            if (0 != dis) {
                SwipeMenuView menu = GetMenu (dis);
                if (Math.Abs (dis) > menu.GetMenuWidth ()) {
                    dis = Math.Sign (dis) * menu.GetMenuWidth ();
                }
            }

            int top = mContentView.Top;
            mContentView.Layout (dis, top, mContentView.Width + dis, MeasuredHeight);
            mLeftMenuView.Layout (dis - mLeftMenuView.GetMenuWidth (), top, dis, MeasuredHeight);
            int start = mContentView.Width + dis;
            mRightMenuView.Layout (start, top, start + mRightMenuView.GetMenuWidth (), MeasuredHeight);
        }

        public override void ComputeScroll ()
        {
            if (state == STATE_OPEN) {
                if (mOpenScroller.ComputeScrollOffset ()) {
                    swipe (mBaseX + mOpenScroller.CurrX);
                    PostInvalidate ();
                }
            } else {
                if (mCloseScroller.ComputeScrollOffset ()) {
                    swipe ((mBaseX - mCloseScroller.CurrX));
                    PostInvalidate ();
                }
            }
        }

        public void smoothCloseMenu ()
        {
            state = STATE_CLOSE;
            mBaseX = mContentView.Left;
            mCloseScroller.StartScroll (0, 0, mContentView.Left, 0, 350);
            PostInvalidate ();
        }

        public void smoothOpenMenu ()
        {
            state = STATE_OPEN;
            mBaseX = mContentView.Left;
            if (0 < mBaseX) {
                // Swiping right, show left menu
                int dis = mLeftMenuView.GetMenuWidth () - mBaseX;
                mOpenScroller.StartScroll (0, 0, dis, 0, 350);
            } else {
                // Swiping left
                int dis = -mRightMenuView.GetMenuWidth () - mBaseX;
                mOpenScroller.StartScroll (0, 0, dis, 0, 350);
            }
            PostInvalidate ();
        }

        public bool touchingMenu (float touchX)
        {
            mBaseX = mContentView.Left;
            if (0 < mBaseX) {
                // Swiped right
                return (touchX < mLeftMenuView.GetMenuWidth ());
            } else {
                // Swiped left
                return (touchX > (Width - mRightMenuView.GetMenuWidth ()));
            }
        }

        public void closeMenu ()
        {
            if (mCloseScroller.ComputeScrollOffset ()) {
                mCloseScroller.AbortAnimation ();
            }
            if (state == STATE_OPEN) {
                state = STATE_CLOSE;
                swipe (0);
            }
        }

        public View getContentView ()
        {
            return mContentView;
        }

        private int dp2px (int dp)
        {
            return (int)TypedValue.ApplyDimension (ComplexUnitType.Dip, (float)dp, Context.Resources.DisplayMetrics);
        }

        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            base.OnMeasure (widthMeasureSpec, heightMeasureSpec);

            mLeftMenuView.Measure (MeasureSpec.MakeMeasureSpec (0, MeasureSpecMode.Unspecified), MeasureSpec.MakeMeasureSpec (MeasuredHeight, MeasureSpecMode.Exactly));

            mRightMenuView.Measure (MeasureSpec.MakeMeasureSpec (0, MeasureSpecMode.Unspecified), MeasureSpec.MakeMeasureSpec (MeasuredHeight, MeasureSpecMode.Exactly));
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            mContentView.Layout (0, 0, MeasuredWidth, mContentView.MeasuredHeight);
            mLeftMenuView.Layout (-mLeftMenuView.MeasuredWidth, 0, 0, mContentView.MeasuredHeight);
            mRightMenuView.Layout (MeasuredWidth, 0, MeasuredWidth + mRightMenuView.MeasuredWidth, 0);
        }

        public void setMenuHeight (int measuredHeight)
        {
            SwipeMenuView menu = GetMenu (9);

            if (null == menu) {
                return;
            }

            LayoutParams parameters = (LayoutParams)menu.LayoutParameters;
            if (parameters.Height != measuredHeight) {
                parameters.Height = measuredHeight;
                menu.LayoutParameters = menu.LayoutParameters;
            }
        }
    }

    public class SwipeMenuListView : ListView
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

        public SwipeMenuListView (Context context) : base (context)
        {
            init ();
        }

        public SwipeMenuListView (Context context, IAttributeSet attrs, int defStyle) : base (context, attrs, defStyle)
        {
            init ();
        }

        public SwipeMenuListView (Context context, IAttributeSet attrs) : base (context, attrs)
        {
            init ();
        }

        private void init ()
        {
            MAX_X = dp2px (MAX_X);
            MAX_Y = dp2px (MAX_Y);
            mTouchState = TOUCH_STATE_NONE;
        }

        public override IListAdapter Adapter {
            set {
                var adapter = new SwipeMenuAdapter (this.Context, value);
                adapter.setOnMenuItemClickListener (MenuItemClickListener);
                base.Adapter = adapter;
            }
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

                mTouchPosition = PointToPosition ((int)ev.GetX (), (int)ev.GetY ());

                View view = GetChildAt (mTouchPosition - this.FirstVisiblePosition);

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
                break;
            case MotionEventActions.Move:
                float dy = Math.Abs ((ev.GetY () - mDownY));
                float dx = Math.Abs ((ev.GetX () - mDownX));
                if (mTouchState == TOUCH_STATE_X) {
                    if (mTouchView != null) {
                        mTouchView.OnSwipe (ev);
                    }
                    this.Selector.SetState (new int[] { 0 });
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

        public void smoothOpenMenu (int position)
        {
            if (position >= FirstVisiblePosition && position <= LastVisiblePosition) {
                View view = GetChildAt (position - FirstVisiblePosition);
                if (view is SwipeMenuLayout) {
                    mTouchPosition = position;
                    if (mTouchView != null && mTouchView.isOpen ()) {
                        mTouchView.smoothCloseMenu ();
                    }
                    mTouchView = (SwipeMenuLayout)view;
                    mTouchView.smoothOpenMenu ();
                }
            }
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
            var adapter = (SwipeMenuAdapter)base.Adapter;
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

        /**
     * This method animates all other views in the ListView container (not including ignoreView)
     * into their final positions. It is called after ignoreView has been removed from the
     * adapter, but before layout has been run. The approach here is to figure out where
     * everything is now, then allow layout to run, then figure out where everything is after
     * layout, and then to run animations between all of those start/end positions.
     */
        //        HashMap<Long, Integer> mItemIdTopMap = new HashMap<Long, Integer>();

        //    public void animateRemoval(final ListView listview, int index) {
        //        int firstVisiblePosition = listview.getFirstVisiblePosition();
        //        for (int i = 0; i < listview.getChildCount(); ++i) {
        //            View child = listview.getChildAt(position);
        //            if (child != viewToRemove) {
        //                int position = firstVisiblePosition + i;
        //                long itemId = getAdapter().getItemId(position);
        //                mItemIdTopMap.put(itemId, child.getTop());
        //            }
        //        }
        //        // Delete the item from the adapter
        //        int position = this.getPositionForView(viewToRemove);
        //
        //        final ViewTreeObserver observer = listview.getViewTreeObserver();
        //        observer.addOnPreDrawListener(new ViewTreeObserver.OnPreDrawListener() {
        //            public boolean onPreDraw() {
        //                observer.removeOnPreDrawListener(this);
        //                boolean firstAnimation = true;
        //                int firstVisiblePosition = listview.getFirstVisiblePosition();
        //                for (int i = 0; i < listview.getChildCount(); ++i) {
        //                    final View child = listview.getChildAt(i);
        //                    int position = firstVisiblePosition + i;
        //                    long itemId = getAdapter().getItemId(position);
        //                    Integer startTop = mItemIdTopMap.get(itemId);
        //                    int top = child.getTop();
        //                    if (startTop != null) {
        //                        if (startTop != top) {
        //                            int delta = startTop - top;
        //                            child.setTranslationY(delta);
        //                            child.animate().setDuration(MOVE_DURATION).translationY(0);
        //                            if (firstAnimation) {
        //                                child.animate().withEndAction(new Runnable() {
        //                                    public void run() {
        ////                                        mBackgroundContainer.hideBackground();
        ////                                        mSwiping = false;
        //                                        listview.setEnabled(true);
        //                                    }
        //                                });
        //                                firstAnimation = false;
        //                            }
        //                        }
        //                    } else {
        //                        // Animate new views along with the others. The catch is that they did not
        //                        // exist in the start state, so we must calculate their starting position
        //                        // based on neighboring views.
        //                        int childHeight = child.getHeight() + listview.getDividerHeight();
        //                        startTop = top + (i > 0 ? childHeight : -childHeight);
        //                        int delta = startTop - top;
        //                        child.setTranslationY(delta);
        //                        child.animate().setDuration(MOVE_DURATION).translationY(0);
        //                        if (firstAnimation) {
        //                            child.animate().withEndAction(new Runnable() {
        //                                public void run() {
        ////                                    mBackgroundContainer.hideBackground();
        ////                                    mSwiping = false;
        //                                    listview.setEnabled(true);
        //                                }
        //                            });
        //                            firstAnimation = false;
        //                        }
        //                    }
        //                }
        //                mItemIdTopMap.clear();
        //                return true;
        //            }
        //        });
        //    }
    }

    public class SwipeMenuAdapter : Java.Lang.Object, IWrapperListAdapter, SwipeMenuView.OnSwipeItemClickListener
    {
        public delegate void OnMenuItemClickListener (SwipeMenuView view, SwipeMenu menu, int index);

        private IListAdapter mAdapter;
        private Context mContext;
        private OnMenuItemClickListener onMenuItemClickListener;

        private SwipeMenuCreator mMenuCreator;

        public SwipeMenuAdapter (Context context, IListAdapter adapter)
        {
            mAdapter = adapter;
            mContext = context;
        }

        public int Count
        { get { return mAdapter.Count; } }

        public Java.Lang.Object GetItem (int position)
        {
            return mAdapter.GetItem (position);
        }

        public long GetItemId (int position)
        {
            return mAdapter.GetItemId (position);
        }

        // FIXME: Do not re-use the view if the view type changes
        public View GetView (int position, View convertView, ViewGroup parent)
        {
            var layout = convertView as SwipeMenuLayout;
            View contentView;
            if (null == layout) {
                contentView = mAdapter.GetView (position, null, parent);
            } else {
                contentView = mAdapter.GetView (position, layout.getContentView (), parent);
            }

            if (null == layout || contentView != layout.getContentView ()) {

                // The view that was passed in can't be reused because
                //   (1) no view was passed in, or
                //   (2) the view wasn't a SwipeMenuLayout object, or
                //   (3) the adapter chose to not reusing the SwipeMenuLayout's content view.
                // Create a whole new SwipeMenuLayout object.
                SwipeMenu menu = new SwipeMenu (mContext);
                menu.setViewType (mAdapter.GetItemViewType (position));
                if (null != mMenuCreator) {
                    mMenuCreator (menu);
                }
                SwipeMenuView leftMenuView = new SwipeMenuView (menu, SwipeMenu.SwipeSide.LEFT);
                leftMenuView.setOnSwipeItemClickListener (this);
                SwipeMenuView rightMenuView = new SwipeMenuView (menu, SwipeMenu.SwipeSide.RIGHT);
                rightMenuView.setOnSwipeItemClickListener (this);
                SwipeMenuListView listView = (SwipeMenuListView)parent;
                layout = new SwipeMenuLayout (contentView,
                    leftMenuView, rightMenuView,
                    listView.getCloseInterpolator (),
                    listView.getOpenInterpolator ());
                layout.setPosition (position);

            } else {

                // Reuse the existing view for the cell.  The underlying content view has already
                // been adjusted.  The SwipeMenuLayout object still needs to be adjusted.
                layout.closeMenu ();
                layout.setPosition (position);
            }
            return layout;
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

        public void RegisterDataSetObserver (DataSetObserver observer)
        {
            mAdapter.RegisterDataSetObserver (observer);
        }

        public void UnregisterDataSetObserver (DataSetObserver observer)
        {
            mAdapter.UnregisterDataSetObserver (observer);
        }

        public bool AreAllItemsEnabled ()
        {
            return mAdapter.AreAllItemsEnabled ();
        }

        public bool IsEnabled (int position)
        {
            return mAdapter.IsEnabled (position);
        }

        public bool HasStableIds {
            get {
                return mAdapter.HasStableIds;
            }
        }

        public int GetItemViewType (int position)
        {
            return mAdapter.GetItemViewType (position);
        }

        public int ViewTypeCount { get { return mAdapter.ViewTypeCount; } }

        public bool IsEmpty { get { return mAdapter.IsEmpty; } }

        public IListAdapter WrappedAdapter { get { return mAdapter; } }

        public void setMenuCreator (SwipeMenuCreator menuCreator)
        {
            this.mMenuCreator = menuCreator;
        }
    }
}

