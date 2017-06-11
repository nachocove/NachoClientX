//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Support.V4.View;
using Android.Animation;

using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    public delegate void CalendarDateActionDelegate (DateTime date);
    public delegate bool CalendarDateCheckDelegate (DateTime date);

    public class CalendarPagerView : ViewGroup, GestureDetector.IOnGestureListener
    {
        GestureDetectorCompat GestureDetector;
        PagesContainerView PagesContainer;
        List<PageView> PageViews;
        TextView MonthLabelA;
        TextView MonthLabelB;
        public CalendarDateActionDelegate DateSelected;
        public CalendarDateCheckDelegate HasEvents;
        public CalendarDateCheckDelegate IsSupportedDate;
        Calendar Calendar;
        DateTime FocusDate;
        DateTime HighlightedDate;
        DateTime? QueuedHighlightDate;
        DateTime VisibleStartDate {
            get {
                return PageViews [BufferedPageCount].StartDate;
            }
        }
        DateTime VisibleEndDate {
            get {
                return PageViews [BufferedPageCount].EndDate;
            }
        }
        DateTime BufferedStartDate {
            get {
                return PageViews [0].StartDate;
            }
        }
        DateTime BufferedEndDate {
            get {
                return PageViews [PageViews.Count - 1].EndDate;
            }
        }
        DateTime FocusMonth {
            get {
                return new DateTime (FocusDate.Year, FocusDate.Month, 1, 0, 0, 0, FocusDate.Kind);
            }
        }
        DateTime BufferedStartMonth {
            get {
                var date = Calendar.AddMonths (FocusMonth, -BufferedPageCount);
                return DateTime.SpecifyKind (date, FocusMonth.Kind);
            }
        }
        DateTime BufferedEndMonth {
            get {
                var date = Calendar.AddMonths (FocusMonth, BufferedPageCount + 1);
                return DateTime.SpecifyKind (date, FocusMonth.Kind);
            }
        }
//        ImageView SelectionIndicatorA;
//        ImageView SelectionIndicatorB;

        public int Weeks {
            get {
                if (DisplayMode == PagerDisplayMode.Weeks) {
                    return Rows;
                }
                return 0;
            }
            set {
                if (value == 0) {
                    DisplayMode = PagerDisplayMode.Months;
                    MonthLabelTransition = PagerMonthLabelTransition.Page;
                    Rows = 6;
                } else {
                    DisplayMode = PagerDisplayMode.Weeks;
                    MonthLabelTransition = PagerMonthLabelTransition.Fade;
                    Rows = value;
                }
            }
        }

        int Rows;
        int BufferedPageCount = 1;
        float PageTransitionProgress;
        int FirstDayOfWeek = 0;
        enum PagerScrollDirection {
            None,
            Horizontal,
            Vertical
        };
        PagerScrollDirection ScrollDirection;
        int FlingY = 0;
        float LastY = 0.0f;
        bool IsPaging;
        enum PagerDisplayMode {
            Weeks,
            Months
        };

        enum PagerMonthLabelTransition {
            Page,
            Fade
        };

        PagerDisplayMode DisplayMode;
        PagerMonthLabelTransition MonthLabelTransition;

        public CalendarPagerView (Context context) :
            base (context)
        {
            Initialize ();
        }

        public CalendarPagerView (Context context, Android.Util.IAttributeSet attrs) :
            base (context, attrs)
        {
            Initialize ();
        }

        public CalendarPagerView (Context context, Android.Util.IAttributeSet attrs, int defStyle) :
            base (context, attrs, defStyle)
        {
            Initialize ();
        }

        void Initialize ()
        {
            PageViews = new List<PageView> (2 * BufferedPageCount + 1);
            Weeks = 1;
            PageTransitionProgress = 0.0f;
            Calendar = new GregorianCalendar ();

            GestureDetector = new GestureDetectorCompat (Context, this);

            PagesContainer = new PagesContainerView (Context);
            AddView (PagesContainer);

            MonthLabelA = new TextView (Context);
            MonthLabelB = new TextView (Context);
            MonthLabelA.TextAlignment = TextAlignment.Center;
            MonthLabelA.Gravity = GravityFlags.Center;
            MonthLabelB.TextAlignment = TextAlignment.Center;
            MonthLabelB.Gravity = GravityFlags.Center;
            MonthLabelA.SetTextSize (Android.Util.ComplexUnitType.Dip, 11.0f);
            MonthLabelB.SetTextSize (Android.Util.ComplexUnitType.Dip, 11.0f);
            MonthLabelA.SetTextColor (Resources.GetColor(Resource.Color.FixmeCalendarPagerText));
            MonthLabelB.SetTextColor (Resources.GetColor(Resource.Color.FixmeCalendarPagerText));

            AddView (MonthLabelA);
            AddView (MonthLabelB);

            FocusDate = DateTime.Now.ToLocalTime ().Date;
            HighlightedDate = FocusDate;
            ConfigurePageViews ();
        }

        public void Update ()
        {
            UpdateMonthLabels (1);
            foreach (var pageView in PageViews) {
                pageView.Update ();
            }
        }

        void UpdateMonthLabels (int transitionPage)
        {
            if (DisplayMode == PagerDisplayMode.Months) {
                MonthLabelA.Text = LabelStringForMonth (FocusMonth);
                var month = DateTime.SpecifyKind (Calendar.AddMonths (FocusMonth, transitionPage), FocusMonth.Kind);
                MonthLabelB.Text = LabelStringForMonth (month);
            } else {
                MonthLabelA.Text = LabelStringForSpan (VisibleStartDate, VisibleEndDate);
                var pageView = PageViews [BufferedPageCount + transitionPage];
                MonthLabelB.Text = LabelStringForSpan (pageView.StartDate, pageView.EndDate);
            }
        }

        string LabelStringForMonth (DateTime month)
        {
            var now = DateTime.Now.ToLocalTime ();
            if (FocusMonth.Year == now.Year) {
                return FocusMonth.ToString ("MMMM");
            } else {
                return FocusMonth.ToString ("MMMM yyyy");
            }
        }

        string LabelStringForSpan (DateTime start, DateTime end)
        {
            var now = DateTime.Now.ToLocalTime ();
            end = DateTime.SpecifyKind (Calendar.AddDays (end, -1), end.Kind);
            if (start.Month == end.Month) {
                if (start.Year == now.Year) {
                    return start.ToString ("MMMM");
                } else {
                    return start.ToString ("MMMM yyyy");
                }
            } else {
                if (start.Year == end.Year) {
                    if (start.Year == now.Year) {
                        return String.Format ("{0}/{1}", start.ToString ("MMMM"), end.ToString ("MMMM"));
                    } else {
                        return String.Format ("{0}/{1}", start.ToString ("MMMM"), end.ToString ("MMMM yyyy"));
                    }
                } else {
                    return String.Format ("{0}/{1}", start.ToString ("MMMM yyyy"), end.ToString ("MMMM yyyy"));
                }
            }
        }

        public override bool OnInterceptTouchEvent (MotionEvent ev)
        {
            // Disallow our parent to intercept any events, otherwise we may not be able to scroll horizontally,
            // like when we're inside a PagerView
            Parent.RequestDisallowInterceptTouchEvent (true);
            if (ScrollDirection != PagerScrollDirection.None) {
                // If we're scrolling, we want all the events so nothing gets to child click listeners
                return true;
            }
            if (ev.Action == MotionEventActions.Down) {
                LastY = ev.GetY ();
                // If it's a down event (and we're not scrolling), our GestureDetector needs to know about it,
                // but we need to let the event reach child click listeners because a down starts a click.
                // Since a child view may or may not handle the event, we may or may not get an OnTouchEvent call.
                // Since we may not get an OnTouchEvent call, we have to let our GestureDetector look at the event now.
                GestureDetector.OnTouchEvent (ev);
                return false;
            }
            if (ev.Action == MotionEventActions.Up) {
                // If it's an up event (and we're not scrolling), we need to let the event reach click click listeners
                // because the click happens on up after a down.
                return false;
            }
            if (ev.Action == MotionEventActions.Move && ScrollDirection == PagerScrollDirection.None) {
                GestureDetector.OnTouchEvent (ev);
                return ScrollDirection != PagerScrollDirection.None;
            }
            // If it's some other event like a move, we'll take it becaue no children care about these other events.
            return true;
        }

        public override bool OnTouchEvent (MotionEvent e)
        {
            if (e.Action == MotionEventActions.Up) {
                if (ScrollDirection == PagerScrollDirection.Horizontal) {
                    // If we're scrolling and there's an up, the scroll is finished and we need to snap to a page boundary
                    ScrollDirection = PagerScrollDirection.None;
                    if (PageTransitionProgress < 0.0f) {
                        PageFocusDateNext ();
                        PageViewsNext ();
                    } else if (PageTransitionProgress > 0.0f) {
                        PageFocusDatePrevious ();
                        PageViewsPrevious ();
                    }
                    return true;
                } else if (ScrollDirection == PagerScrollDirection.Vertical) {
                    ScrollDirection = PagerScrollDirection.None;
                    if (FlingY < 0 && DisplayMode == PagerDisplayMode.Months) {
                        // TODO: animate to weeks
                        Weeks = 1;
                        ConfigurePageViews ();
                        RequestLayout ();
                        FlingY = 0;
                        return true;
                    } else if (FlingY > 0 && DisplayMode == PagerDisplayMode.Weeks) {
                        // TODO: animate to months
                        Weeks = 0;
                        ConfigurePageViews ();
                        RequestLayout ();
                        FlingY = 0;
                        return true;
                    }
                }
            }
            if (e.Action == MotionEventActions.Down) {
                // If there's a down event, we've already told our GestureDetector about it in OnInterceptTouchEvent,
                // so we don't need to tell it again, but we do want to return true so the subsequent move events will
                // be sent.
                return true;
            }
            // If it's any other event, have our GestureDetector handle it
            return GestureDetector.OnTouchEvent (e);
        }

        public bool OnDown(MotionEvent e)
        {
            return false;
        }

        public bool OnScroll(MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
        {
            var totalDistanceX = e2.GetX () - e1.GetX ();
            var totalDistanceY = e2.GetY () - e1.GetY ();
            var absX = Math.Abs (totalDistanceX);
            var absY = Math.Abs (totalDistanceY);
            if (ScrollDirection == PagerScrollDirection.Horizontal || (absX >= absY && absX > (float)Width / 21.0f)) {
                ScrollDirection = PagerScrollDirection.Horizontal;
                var newProgress = totalDistanceX / (float)Width;
                if (newProgress > 0.0f && PageTransitionProgress <= 0.0f) {
                    UpdateMonthLabels (-1);
                } else if (newProgress < 0.0f && PageTransitionProgress >= 0.0f) {
                    UpdateMonthLabels (1);
                }
                PageTransitionProgress = newProgress;
                RequestLayout ();
                return true;
            }
            if (ScrollDirection == PagerScrollDirection.Vertical || (absY > absX && absY > (float)Width / 21.0f)) {
                ScrollDirection = PagerScrollDirection.Vertical;
                var delta = e2.GetY () - LastY;
                if (delta > 0) {
                    FlingY = 1;
                } else if (delta < 0) {
                    FlingY = -1;
                }
                LastY = e2.GetY ();
                return true;
            }
            return false;
        }

        public bool OnFling(MotionEvent e1, MotionEvent e2, float velocityX, float velocityY)
        {
            return false;
        }

        public void OnLongPress(MotionEvent e)
        {
        }

        public void OnShowPress(MotionEvent e)
        {
        }

        public bool OnSingleTapUp(MotionEvent e)
        {
            return false;
        }

        void ConfigurePageViews ()
        {
            var pages = 2 * BufferedPageCount + 1;
            PageView pageView;
            DateTime date;
            int i = 0;
            for (; i < pages; ++i) {
                if (i < PageViews.Count) {
                    pageView = PageViews [i];
                } else {
                    pageView = new PageView (Context);
                    pageView.CalendarView = this;
                    PageViews.Add (pageView);
                    PagesContainer.AddView (pageView);
                }
            }
            for (int j = pages - 1; j >= i; --j) {
                pageView = PageViews [j];
                pageView.CalendarView = null;
                PagesContainer.RemoveView (pageView);
                PageViews.RemoveAt (j);
            }
            if (DisplayMode == PagerDisplayMode.Months) {
                var focusMonth = new DateTime (FocusDate.Year, FocusDate.Month, 1, 0, 0, 0, DateTimeKind.Local);
                var month = DateTime.SpecifyKind (Calendar.AddMonths (focusMonth, -BufferedPageCount), focusMonth.Kind);
                for (i = 0; i < pages; ++i) {
                    pageView = PageViews [i];
                    date = StartOfWeek (month);
                    pageView.SetStartDate (date, Calendar, Rows, month);
                    month = DateTime.SpecifyKind (Calendar.AddMonths (month, 1), month.Kind);
                }
            } else {
                date = StartOfWeek (FocusDate);
                date = DateTime.SpecifyKind (Calendar.AddWeeks (date, -BufferedPageCount), date.Kind);
                for (i = 0; i < pages; ++i) {
                    pageView = PageViews [i];
                    pageView.SetStartDate (date, Calendar, Rows, null);
                    date = DateTime.SpecifyKind (Calendar.AddWeeks (date, Rows), date.Kind);
                }
            }
            UpdateMonthLabels (1);
        }

        public void SetHighlightedDate (DateTime date)
        {
            if (date == HighlightedDate) {
                return;
            }
            if (IsPaging) {
                QueuedHighlightDate = date;
                return;
            }
            HighlightedDate = date;
            if (DisplayMode == PagerDisplayMode.Months) {
                if (date.Year != FocusDate.Year || date.Month != FocusDate.Month) {
                    if (date < FocusDate) {
                        if (date < BufferedStartMonth) {
                            FocusDate = date;
                            ConfigurePageViews ();
                        } else {
                            int pages = 1;
                            while (pages < BufferedPageCount && date < DateTime.SpecifyKind (Calendar.AddMonths (FocusMonth, -pages), FocusMonth.Kind)) {
                                ++pages;
                            }
                            UpdateMonthLabels (-pages);
                            FocusDate = date;
                            PageViewsPrevious (0.0f, pages);
                        }
                    } else {
                        if (date >= BufferedEndMonth) {
                            FocusDate = date;
                            ConfigurePageViews ();
                        } else {
                            int pages = 1;
                            while (pages < BufferedPageCount && date >= DateTime.SpecifyKind (Calendar.AddMonths (FocusMonth, pages + 1), FocusMonth.Kind)) {
                                ++pages;
                            }
                            UpdateMonthLabels (pages);
                            FocusDate = date;
                            PageViewsNext (0.0f, pages);
                        }
                    }
                } else {
                    FocusDate = date;
                    Update ();
                }
            } else {
                if (date >= VisibleStartDate && date < VisibleEndDate) {
                    FocusDate = date;
                    Update ();
                } else if (date >= BufferedStartDate && date < VisibleStartDate) {
                    int pages = 1;
                    while (pages < BufferedPageCount && date < PageViews[BufferedPageCount - pages].StartDate) {
                        ++pages;
                    }
                    FocusDate = date;
                    UpdateMonthLabels (-pages);
                    PageViewsPrevious (0.0f, pages);
                } else if (date >= VisibleEndDate && date < BufferedEndDate) {
                    int pages = 1;
                    while (pages < BufferedPageCount && date >= PageViews[BufferedPageCount + pages].EndDate) {
                        ++pages;
                    }
                    UpdateMonthLabels (pages);
                    FocusDate = date;
                    PageViewsNext (0.0f, pages);
                } else {
                    FocusDate = date;
                    ConfigurePageViews ();
                }
            }
        }

        void PageFocusDatePrevious (int pages = 1)
        {
            if (DisplayMode == PagerDisplayMode.Months) {
                FocusDate = DateTime.SpecifyKind (Calendar.AddMonths (FocusDate, -pages), FocusDate.Kind);
            } else {
                FocusDate = DateTime.SpecifyKind (Calendar.AddWeeks (FocusDate, -pages * Rows), FocusDate.Kind);
            }
        }

        void PageFocusDateNext (int pages = 1)
        {
            if (DisplayMode == PagerDisplayMode.Months) {
                FocusDate = DateTime.SpecifyKind (Calendar.AddMonths (FocusDate, pages), FocusDate.Kind);
            } else {
                FocusDate = DateTime.SpecifyKind (Calendar.AddWeeks (FocusDate, pages * Rows), FocusDate.Kind);
            }
        }

        void PageViewsPrevious (float velocity = 0.0f, int pages = 1)
        {
            if (IsPaging) {
                return;
            }
            IsPaging = true;
            if (pages <= BufferedPageCount) {
                var duration = 0.2f;
                velocity = Math.Max (velocity, (float)Width / duration);
                var animator = ValueAnimator.OfFloat(PageTransitionProgress, (float)pages);
                animator.SetDuration (200);
                animator.Update += (object sender, ValueAnimator.AnimatorUpdateEventArgs e) => {
                    PageTransitionProgress = (float)e.Animation.AnimatedValue;
                    RequestLayout ();
                };
                animator.AnimationEnd += (object sender, EventArgs e) => {
                    PageTransitionProgress = 0.0f;
                    var remaining = pages;
                    while (remaining > 0) {
                        var startDate = DateTime.SpecifyKind (Calendar.AddWeeks (BufferedStartDate, -Rows), BufferedStartDate.Kind);
                        var lastPageView = PageViews [PageViews.Count - 1];
                        PageViews.RemoveAt (PageViews.Count - 1);
                        PageViews.Insert (0, lastPageView);
                        PagesContainer.RemoveView (lastPageView);
                        PagesContainer.AddView (lastPageView, 0);
                        if (DisplayMode == PagerDisplayMode.Months) {
                            var month = DateTime.SpecifyKind (Calendar.AddMonths(FocusMonth, -BufferedPageCount + remaining - 1), FocusMonth.Kind);
                            lastPageView.SetStartDate (StartOfWeek (month), Calendar, Rows, month);
                        } else {
                            lastPageView.SetStartDate (startDate, Calendar, Rows, null);
                        }
                        --remaining;
                    }
                    Update ();
                    RequestLayout ();
                    IsPaging = false;
                    if (QueuedHighlightDate.HasValue && QueuedHighlightDate.Value != HighlightedDate){
                        SetHighlightedDate(QueuedHighlightDate.Value);
                        QueuedHighlightDate = null;
                    }
                };
                animator.Start ();
            } else {
                ConfigurePageViews ();
            }
        }

        void PageViewsNext (float velocity = 0.0f, int pages = 1)
        {
            if (IsPaging) {
                return;
            }
            IsPaging = true;
            if (pages <= BufferedPageCount) {
                velocity = Math.Max (velocity, (float)Width * 5.0f);
                var animator = ValueAnimator.OfFloat (PageTransitionProgress, (float)(-pages));
                animator.Update += (object sender, ValueAnimator.AnimatorUpdateEventArgs e) => {
                    PageTransitionProgress = (float)e.Animation.AnimatedValue;
                    RequestLayout ();
                };
                animator.AnimationEnd += (object sender, EventArgs e) => {
                    var startDate = BufferedEndDate;
                    PageTransitionProgress = 0.0f;
                    var remaining = pages;
                    while (remaining > 0) {
                        var firstPageView = PageViews [0];
                        PageViews.RemoveAt (0);
                        PageViews.Add (firstPageView);
                        PagesContainer.RemoveView (firstPageView);
                        PagesContainer.AddView (firstPageView);
                        if (DisplayMode == PagerDisplayMode.Months) {
                            var month = DateTime.SpecifyKind (Calendar.AddMonths(FocusMonth, BufferedPageCount - remaining + 1), FocusMonth.Kind);
                            firstPageView.SetStartDate (StartOfWeek (month), Calendar, Rows, month);
                        } else {
                            firstPageView.SetStartDate (startDate, Calendar, Rows, null);
                        }
                        --remaining;
                    }
                    Update ();
                    RequestLayout ();
                    IsPaging = false;
                    if (QueuedHighlightDate.HasValue && QueuedHighlightDate.Value != HighlightedDate){
                        SetHighlightedDate(QueuedHighlightDate.Value);
                        QueuedHighlightDate = null;
                    }
                };
                animator.Start ();
            } else {
                ConfigurePageViews ();
            }
        }

        DateTime StartOfWeek (DateTime date)
        {
            var dow = (int)Calendar.GetDayOfWeek (date);
            if (dow >= FirstDayOfWeek) {
                return DateTime.SpecifyKind (Calendar.AddDays (date, FirstDayOfWeek - dow), date.Kind);
            }
            return DateTime.SpecifyKind (Calendar.AddDays (date, FirstDayOfWeek - dow - 7), date.Kind);
        }

        public void DateClicked (DateTime date)
        {
            if (null != IsSupportedDate && IsSupportedDate (date)) {
                SetHighlightedDate (date);
                if (DateSelected != null) {
                    DateSelected (date);
                }
            }
        }

        protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
        {
            var mode = MeasureSpec.GetMode (widthMeasureSpec);
            var width = 350;
            if (mode == MeasureSpecMode.AtMost || mode == MeasureSpecMode.Exactly) {
                width = MeasureSpec.GetSize (widthMeasureSpec);
            }
            mode = MeasureSpec.GetMode (heightMeasureSpec);
            var visibleRows = Rows;
            if (DisplayMode == PagerDisplayMode.Months) {
                var lastDay = (int)FocusMonth.DayOfWeek + Calendar.GetDaysInMonth (FocusMonth.Year, FocusMonth.Month);
                if (lastDay <= 28) {
                    visibleRows = 4;
                } else if (lastDay <= 35) {
                    visibleRows = 5;
                } else {
                    visibleRows = 6;
                }
            }
            var height = width * visibleRows / 7;
            if (mode == MeasureSpecMode.Exactly) {
                height = MeasureSpec.GetSize (heightMeasureSpec);
            } else if (mode == MeasureSpecMode.AtMost) {
                height = Math.Min (height, MeasureSpec.GetSize(heightMeasureSpec));
            }
            var labelWidthSpec = MeasureSpec.MakeMeasureSpec (width, MeasureSpecMode.Exactly);
            var labelHeightSpec = MeasureSpec.MakeMeasureSpec (height, MeasureSpecMode.Unspecified);
            MonthLabelA.Measure (labelWidthSpec, labelHeightSpec);
            MonthLabelB.Measure (labelWidthSpec, labelHeightSpec);
            height += MonthLabelA.MeasuredHeight; // Accounting for weekday label height in pageview, assumed to be the same height as month labels
            var pagesWidthSpec = MeasureSpec.MakeMeasureSpec (width * (BufferedPageCount * 2 + 1), MeasureSpecMode.Exactly);
            var pagesHeightSpec = MeasureSpec.MakeMeasureSpec (height, MeasureSpecMode.Exactly);
            PagesContainer.Measure (pagesWidthSpec, pagesHeightSpec);
            height += MonthLabelA.MeasuredHeight;
            SetMeasuredDimension (width, height);
        }

        protected override void OnLayout (bool changed, int l, int t, int r, int b)
        {
            var w = r - l;
            var h = b - t;
            var bufferWidth = BufferedPageCount * w;
            var pagesOffset = -bufferWidth + (int)(w * PageTransitionProgress);
            var x = pagesOffset;
            var y = 0;
            if (MonthLabelTransition == PagerMonthLabelTransition.Fade) {
                x = 0;
                MonthLabelA.Layout (x, y, x + w, y + MonthLabelA.MeasuredHeight);
                MonthLabelB.Layout (x, y, x + w, y + MonthLabelB.MeasuredHeight);
                MonthLabelA.Alpha = 1.0f - Math.Abs(PageTransitionProgress);
                MonthLabelB.Alpha = 1.0f - MonthLabelA.Alpha;
            } else {
                x = pagesOffset + bufferWidth;
                MonthLabelA.Layout (x, 0, x + w, y + MonthLabelA.MeasuredHeight);
                if (PageTransitionProgress > 0.0f) {
                    x -= w;
                    MonthLabelB.Layout (x, 0, x + w, y + MonthLabelB.MeasuredHeight);
                } else {
                    x += w;
                    MonthLabelB.Layout (x, 0, x + w, y + MonthLabelB.MeasuredHeight);
                }
                MonthLabelA.Alpha = 1.0f;
                MonthLabelB.Alpha = 1.0f;
            }
            x = pagesOffset;
            y += MonthLabelA.MeasuredHeight;
            PagesContainer.Layout (x, y, x + bufferWidth * 2 + w, y + h);
        }

        private class PagesContainerView : ViewGroup {

            public PagesContainerView (Context context) : base(context)
            {
            }

            protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
            {
                // Assuming specs are set to EXACTLY because the only code that calls here is found above
                var width = MeasureSpec.GetSize (widthMeasureSpec);
                var height = MeasureSpec.GetSize (heightMeasureSpec);
                var pageWidthSpec = MeasureSpec.MakeMeasureSpec (width / 3, MeasureSpecMode.Exactly);
                for (int i = 0; i < ChildCount; ++i) {
                    var view = GetChildAt (i);
                    view.Measure (pageWidthSpec, heightMeasureSpec);
                }
                SetMeasuredDimension (width, height);
            }

            protected override void OnLayout (bool changed, int l, int t, int r, int b)
            {
                var h = b - t;
                var x = 0;
                for (int i = 0; i < ChildCount; ++i) {
                    var view = GetChildAt (i);
                    view.Layout (x, 0, x + view.MeasuredWidth, h);
                    x += view.MeasuredWidth;
                }
            }

        }

        private class PageView : ViewGroup
        {
            public DateTime StartDate { get; private set; }
            public DateTime EndDate { get; private set; }
            public CalendarPagerView CalendarView;
            LinearLayout WeekdayLabelsContainer;
            List<TextView> WeekdayLabels;
            List<WeekView> WeekViews;
            Calendar Calendar;

            public PageView (Context context) : base (context)
            {
                Initialize ();
            }

            void Initialize ()
            {
                WeekdayLabels = new List<TextView> (7);
                WeekViews = new List<WeekView> (1);
                WeekdayLabelsContainer = new LinearLayout (Context);
                WeekdayLabelsContainer.LayoutParameters = new LayoutParams (LayoutParams.MatchParent, LayoutParams.WrapContent);
                WeekdayLabelsContainer.Orientation = Orientation.Horizontal;
                for (var i = 0; i < 7; ++i) {
                    var label = new TextView (Context);
                    label.LayoutParameters = new LinearLayout.LayoutParams (0, LayoutParams.WrapContent, 1);
                    label.TextAlignment = TextAlignment.Center;
                    label.Gravity = GravityFlags.Center;
                    label.SetTextSize (Android.Util.ComplexUnitType.Dip, 11.0f);
                    label.SetTextColor (Resources.GetColor(Resource.Color.FixmeCalendarPagerText));
                    WeekdayLabels.Add (label);
                    WeekdayLabelsContainer.AddView (label);
                }
                AddView (WeekdayLabelsContainer);
            }

            public void SetStartDate (DateTime date, Calendar calendar, int rows, DateTime? focusMonth)
            {
                StartDate = date;
                Calendar = calendar;
                int i = 0;
                WeekView weekView;
                for (; i < rows; ++i) {
                    if (i < WeekViews.Count) {
                        weekView = WeekViews [i];
                    } else {
                        weekView = new WeekView (Context);
                        weekView.PageView = this;
                        WeekViews.Add (weekView);
                        AddView (weekView);
                    }
                    weekView.SetStartDate (date, calendar, focusMonth);
                    date = DateTime.SpecifyKind (calendar.AddWeeks (date, 1), date.Kind);
                }
                EndDate = date;
                for (int j = rows - 1; j >= i; --j) {
                    weekView = WeekViews [j];
                    weekView.PageView = null;
                    RemoveView (weekView);
                    WeekViews.RemoveAt (j);
                }
                UpdateWeekdayLabels ();
            }

            void UpdateWeekdayLabels ()
            {
                var date = StartDate;
                foreach (var label in WeekdayLabels) {
                    label.Text = date.ToString ("dddd").Substring (0, 1);
                    date = DateTime.SpecifyKind (Calendar.AddDays (date, 1), date.Kind);
                }
            }

            public void Update ()
            {
                UpdateWeekdayLabels ();
                foreach (var weekView in WeekViews) {
                    weekView.Update ();
                }
            }

            protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
            {
                // Assuming we're always passed EXACT specs from the above code
                var width = MeasureSpec.GetSize (widthMeasureSpec);
                var height = MeasureSpec.GetSize (heightMeasureSpec);
                var labelWidthSpec = MeasureSpec.MakeMeasureSpec (width, MeasureSpecMode.Exactly);
                var labelHeightSpec = MeasureSpec.MakeMeasureSpec (height, MeasureSpecMode.AtMost);
                WeekdayLabelsContainer.Measure (labelWidthSpec, labelHeightSpec);
                var weekWidthSpec = labelWidthSpec;
                var weekHeightSpec = MeasureSpec.MakeMeasureSpec (width / 7, MeasureSpecMode.Exactly);
                foreach (var weekView in WeekViews) {
                    weekView.Measure (weekWidthSpec, weekHeightSpec);
                }
                SetMeasuredDimension (width, height);
            }

            protected override void OnLayout (bool changed, int l, int t, int r, int b)
            {
                var w = r - l;
                var weekHeight = w / 7;
                var y = 0;
                WeekdayLabelsContainer.Layout (0, y, w, WeekdayLabelsContainer.MeasuredHeight);
                y += WeekdayLabelsContainer.MeasuredHeight;
                foreach (var weekView in WeekViews) {
                    weekView.Layout (0, y, w, y + weekHeight);
                    y += weekHeight;
                }
            }
        }

        private class WeekView : LinearLayout
        {
            public PageView PageView;
            List<DayView> DayViews;

            public WeekView (Context context) : base (context)
            {
                Initialize ();
            }

            void Initialize ()
            {
                LayoutParameters = new LayoutParams (LayoutParams.MatchParent, 0, 1.0f);
                DayViews = new List<DayView> (7);
                Orientation = Orientation.Horizontal;
                for (var i = 0; i < 7; ++i) {
                    var dayView = new DayView (Context);
                    dayView.WeekView = this;
                    DayViews.Add (dayView);
                    AddView (dayView);
                }
            }

            public void SetStartDate (DateTime date, Calendar calendar, DateTime? focusMonth)
            {
                foreach (var dayView in DayViews) {
                    bool isAlt = focusMonth.HasValue && date.Month != focusMonth.Value.Month;
                    dayView.SetDate (date, isAlt);
                    date = DateTime.SpecifyKind (calendar.AddDays (date, 1), date.Kind);
                }
            }

            public void Update ()
            {
                foreach (var dayView in DayViews) {
                    dayView.Update ();
                }
            }
        }

        private class DayView : ViewGroup
        {
            DateTime Date;
            TextView DateLabel;
            ImageView EventIndicator;
            public WeekView WeekView;
            bool IsAlt;
            bool IsToday;
            bool IsHighlighted;

            public DayView (Context context) : base (context)
            {
                Initialize ();
            }

            void Initialize ()
            {
                LayoutParameters = new LinearLayout.LayoutParams (0, LayoutParams.MatchParent, 1);
                DateLabel = new TextView (Context);
                DateLabel.TextAlignment = TextAlignment.Center;
                DateLabel.Gravity = GravityFlags.Center;
                DateLabel.SetTypeface (null, Android.Graphics.TypefaceStyle.Bold);
                DateLabel.SetTextColor (Resources.GetColor (Resource.Color.FixmeCalendarPagerBlack));
                DateLabel.SetBackgroundResource (Resource.Drawable.CalendarPagerDateBackground);
                EventIndicator = new ImageView (Context);
                EventIndicator.SetBackgroundResource (Resource.Drawable.CalendarPagerEventIndicator);
                EventIndicator.Visibility = ViewStates.Invisible;
                AddView (DateLabel);
                AddView (EventIndicator);
                Click += Clicked;
            }

            public void SetDate (DateTime date, bool isAlt)
            {
                Date = date;
                IsAlt = isAlt;
                Update ();
            }

            public void Update ()
            {
                IsToday = Date == DateTime.Now.ToLocalTime ().Date;
                IsHighlighted = Date == WeekView.PageView.CalendarView.HighlightedDate;
                DateLabel.Text = Date.Day.ToString ();
                if (IsAlt) {
                    DateLabel.SetBackgroundResource (Resource.Drawable.CalendarPagerDateBackgroundAlt);
                    DateLabel.SetTextColor (Resources.GetColor (Resource.Color.FixmeCalendarPagerText));
                } else if (IsHighlighted) {
                    DateLabel.SetBackgroundResource (Resource.Drawable.CalendarPagerDateBackgroundHighlighted);
                    DateLabel.SetTextColor (Resources.GetColor (Android.Resource.Color.White));
                } else if (IsToday) {
                    DateLabel.SetBackgroundResource (Resource.Drawable.CalendarPagerDateBackgroundToday);
                    DateLabel.SetTextColor (Resources.GetColor (Resource.Color.FixmeCalendarPagerText));
                } else {
                    DateLabel.SetBackgroundResource (Resource.Drawable.CalendarPagerDateBackground);
                    DateLabel.SetTextColor (Resources.GetColor (Resource.Color.FixmeCalendarPagerBlack));
                }
                if (this.WeekView.PageView.CalendarView.HasEvents != null) {
                    if (this.WeekView.PageView.CalendarView.HasEvents (Date)) {
                        EventIndicator.Visibility = ViewStates.Visible;
                    } else {
                        EventIndicator.Visibility = ViewStates.Invisible;
                    }
                } else {
                    EventIndicator.Visibility = ViewStates.Invisible;
                }
            }

            void Clicked (object sender, EventArgs e)
            {
                WeekView.PageView.CalendarView.DateClicked (this.Date);
            }

            protected override void Dispose (bool disposing)
            {
                Click -= Clicked;
                base.Dispose (disposing);
            }

            protected override void OnMeasure (int widthMeasureSpec, int heightMeasureSpec)
            {
                var w = MeasureSpec.GetSize (widthMeasureSpec);
                var h = MeasureSpec.GetSize (heightMeasureSpec);
                var indicatorSpace = h / 3;
                var indicatorSize = indicatorSpace / 3;
                var labelSize = h - indicatorSpace;
                DateLabel.Measure (MeasureSpec.MakeMeasureSpec (labelSize, MeasureSpecMode.Exactly), MeasureSpec.MakeMeasureSpec (labelSize, MeasureSpecMode.Exactly));
                EventIndicator.Measure (MeasureSpec.MakeMeasureSpec (indicatorSize, MeasureSpecMode.Exactly), MeasureSpec.MakeMeasureSpec (indicatorSize, MeasureSpecMode.Exactly));
                SetMeasuredDimension (w, h);
            }

            protected override void OnLayout (bool changed, int l, int t, int r, int b)
            {
                var w = r - l;
                var h = b - t;
                var indicatorSpace = h / 3;
                var indicatorSize = indicatorSpace / 3;
                var labelSize = h - indicatorSpace;
                var y = 0;
                var x = (w - labelSize) / 2;
                DateLabel.Layout (x, y, x + labelSize, y + labelSize);
                y += labelSize;
                x = (w - indicatorSize) / 2;
                y += (indicatorSpace - indicatorSize) / 2;
                EventIndicator.Layout (x, y, x + indicatorSize, y + indicatorSize);
            }
        }
    }
}

