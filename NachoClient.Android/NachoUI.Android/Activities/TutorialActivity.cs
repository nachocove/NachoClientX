//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using Android.Support.V4.App;
using Android.Support.V4.View;
using Android.Views;
using Android.App;
using NachoCore.Utils;
using Android.Views.Animations;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "TutorialActivity", ScreenOrientation=Android.Content.PM.ScreenOrientation.Portrait)]            
    public class TutorialActivity : FragmentActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            RequestedOrientation = Android.Content.PM.ScreenOrientation.Portrait;

            SetContentView (Resource.Layout.TutorialActivity);

            var pager = FindViewById<ViewPager> (Resource.Id.pager);
            var adaptor = new GenericFragmentPagerAdaptor (SupportFragmentManager);

            adaptor.AddFragment (Tutorial1Fragment.new_instance ());
            adaptor.AddFragment (Tutorial2Fragment.new_instance ());
            adaptor.AddFragment (Tutorial3Fragment.new_instance ());
            adaptor.AddFragment (Tutorial4Fragment.new_instance ());
            adaptor.AddFragment (Tutorial5Fragment.new_instance ());

            pager.PageSelected += Pager_PageSelected;

            pager.Adapter = adaptor;

            pager.CurrentItem = 0;
            FindViewById<ImageView> (Resource.Id.dot_1).SetImageResource (Resource.Drawable.TutorialProgressDark);

            var dismissButton = FindViewById<Button> (Resource.Id.dismiss);
            dismissButton.Click += DismissButton_Click;
        }

        protected override void OnResume ()
        {
            base.OnResume ();
        }

        void DismissButton_Click (object sender, EventArgs e)
        {
            LoginHelpers.SetHasViewedTutorial (true);
            SetResult (Result.Ok);
            Finish ();
        }

        public override void OnBackPressed ()
        {
            // ignore
        }

        void Pager_PageSelected (object sender, ViewPager.PageSelectedEventArgs e)
        {
            FindViewById<ImageView> (Resource.Id.dot_1).SetImageResource (Resource.Drawable.TutorialProgressLight);
            FindViewById<ImageView> (Resource.Id.dot_2).SetImageResource (Resource.Drawable.TutorialProgressLight);
            FindViewById<ImageView> (Resource.Id.dot_3).SetImageResource (Resource.Drawable.TutorialProgressLight);
            FindViewById<ImageView> (Resource.Id.dot_4).SetImageResource (Resource.Drawable.TutorialProgressLight);
            FindViewById<ImageView> (Resource.Id.dot_5).SetImageResource (Resource.Drawable.TutorialProgressLight);
            FindViewById<Button> (Resource.Id.dismiss).SetText (Resource.String.dismiss);
            switch (e.Position + 1) {
            case 1:
                FindViewById<ImageView> (Resource.Id.dot_1).SetImageResource (Resource.Drawable.TutorialProgressDark);
                break;
            case 2:
                FindViewById<ImageView> (Resource.Id.dot_2).SetImageResource (Resource.Drawable.TutorialProgressDark);
                break;
            case 3:
                FindViewById<ImageView> (Resource.Id.dot_3).SetImageResource (Resource.Drawable.TutorialProgressDark);
                break;
            case 4:
                FindViewById<ImageView> (Resource.Id.dot_4).SetImageResource (Resource.Drawable.TutorialProgressDark);
                break;
            case 5:
                FindViewById<ImageView> (Resource.Id.dot_5).SetImageResource (Resource.Drawable.TutorialProgressDark);
                FindViewById<Button> (Resource.Id.dismiss).SetText (Resource.String.tutorial_done);
                break;
            }
        }
    }

    public class GenericFragmentPagerAdaptor : FragmentPagerAdapter
    {
        private List<Android.Support.V4.App.Fragment> _fragmentList = new List<Android.Support.V4.App.Fragment> ();

        public GenericFragmentPagerAdaptor (Android.Support.V4.App.FragmentManager fm)
            : base (fm)
        {
        }

        public override int Count {
            get { return _fragmentList.Count; }
        }

        public override Android.Support.V4.App.Fragment GetItem (int position)
        {
            return _fragmentList [position];
        }

        public void AddFragment (GenericViewPagerFragment fragment)
        {
            _fragmentList.Add (fragment);
        }

        public void AddFragment (Android.Support.V4.App.Fragment fragment)
        {
            _fragmentList.Add (fragment);
        }

        public void AddFragmentView (Func<LayoutInflater, ViewGroup, Bundle, View> view)
        {
            _fragmentList.Add (new GenericViewPagerFragment (view));
        }
    }

    public class GenericViewPagerFragment : Android.Support.V4.App.Fragment
    {
        public GenericViewPagerFragment ()
        {
        }

        private Func<LayoutInflater, ViewGroup, Bundle, View> _view;

        public GenericViewPagerFragment (Func<LayoutInflater, ViewGroup, Bundle, View> view)
        {
            _view = view;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            base.OnCreateView (inflater, container, savedInstanceState);
            return _view (inflater, container, savedInstanceState);
        }
    }

    public class Tutorial1Fragment :  Android.Support.V4.App.Fragment
    {
        public static Tutorial1Fragment new_instance ()
        {
            return new Tutorial1Fragment ();
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.TutorialFragment01, container, false);
            return view;
        }

        bool didAnimation = false;

        void Animation ()
        {
            if (didAnimation) {
                return;
            }
            didAnimation = true;

            var marquee = View.FindViewById<TextView> (Resource.Id.tutorial_swipe);
            marquee.Visibility = ViewStates.Visible;
            var animation = new TranslateAnimation (
                                Dimension.RelativeToSelf, 1f,
                                Dimension.RelativeToSelf, 0f,
                                Dimension.RelativeToSelf, 0f,
                                Dimension.RelativeToSelf, 0f);
            animation.Duration = 1000;
            animation.StartOffset = 2000;
            marquee.StartAnimation (animation);
        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (UserVisibleHint) {
                Animation ();
            }
        }

        public override void OnPause ()
        {
            base.OnPause ();
        }

        public override bool UserVisibleHint {
            get {
                return base.UserVisibleHint;
            }
            set {
                base.UserVisibleHint = value;
                if (value && IsResumed) {
                    Animation ();
                }
            }
        }
    }

    public class Tutorial2Fragment :  Android.Support.V4.App.Fragment
    {
        public static Tutorial2Fragment new_instance ()
        {
            return new Tutorial2Fragment ();
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.TutorialFragment02, container, false);
            return view;
        }

        bool didAnimation = false;

        void Animation ()
        {
            if (didAnimation) {
                return;
            }
            didAnimation = true;

            var messageUp = View.FindViewById<ImageView> (Resource.Id.message_up);
            var messageDown = View.FindViewById<ImageView> (Resource.Id.message_down);

            messageUp.Alpha = 1;
            messageDown.Alpha = 1;

            var animation = new AlphaAnimation (0f, 1f);
            animation.Duration = 250;
            animation.StartOffset = 1000;
            messageUp.StartAnimation (animation);
            messageDown.StartAnimation (animation);
           
        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (UserVisibleHint) {
                Animation ();
            }
        }

        public override bool UserVisibleHint {
            get {
                return base.UserVisibleHint;
            }
            set {
                base.UserVisibleHint = value;
                if (value && IsResumed) {
                    Animation ();
                }
            }
        }
    }

    public class Tutorial3Fragment :  Android.Support.V4.App.Fragment
    {
        public static Tutorial3Fragment new_instance ()
        {
            return new Tutorial3Fragment ();
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.TutorialFragment03, container, false);
            return view;
        }

        bool didAnimation = false;

        void Animation ()
        {
            if (didAnimation) {
                return;
            }
            didAnimation = true;

            var dotAnimation = new TranslateAnimation (
                                   Dimension.RelativeToSelf, 0f,
                                   Dimension.RelativeToSelf, 0f,
                                   Dimension.RelativeToSelf, 0f,
                                   Dimension.RelativeToSelf, -1f);
            dotAnimation.Duration = 1000;
            dotAnimation.StartOffset = 2000;

            var dotAlphaAnimation = new AlphaAnimation (0f, 1f);
            dotAlphaAnimation.Duration = 250;
            dotAlphaAnimation.StartOffset = 1500;

            var dotAnimationSet = new AnimationSet (true);
            dotAnimationSet.AddAnimation (dotAlphaAnimation);
            dotAnimationSet.AddAnimation (dotAnimation);

            var card1Animation = new TranslateAnimation (
                                     Dimension.RelativeToSelf, 0f,
                                     Dimension.RelativeToSelf, 0f,
                                     Dimension.RelativeToSelf, 0f,
                                     Dimension.RelativeToSelf, -1f);
            card1Animation.Duration = 1000;
            card1Animation.StartOffset = 2000;

            var card2Animation = new TranslateAnimation (
                                     Dimension.RelativeToSelf, 0f,
                                     Dimension.RelativeToSelf, 0f,
                                     Dimension.RelativeToSelf, 1f,
                                     Dimension.RelativeToSelf, 0f);
            card2Animation.Duration = 1000;
            card2Animation.StartOffset = 2000;

            var dotView = View.FindViewById<View> (Resource.Id.dot);
            var card1View = View.FindViewById<View> (Resource.Id.card01);
            var card2View = View.FindViewById<View> (Resource.Id.card02);

            dotAnimationSet.FillAfter = true;
            card1Animation.FillAfter = true;
            card2Animation.FillAfter = true;

            dotView.Visibility = ViewStates.Visible;

            dotView.StartAnimation (dotAnimationSet);
            card1View.StartAnimation (card1Animation);
            card2View.StartAnimation (card2Animation);
        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (UserVisibleHint) {
                Animation ();
            }
        }

        public override bool UserVisibleHint {
            get {
                return base.UserVisibleHint;
            }
            set {
                base.UserVisibleHint = value;
                if (value && IsResumed) {
                    Animation ();
                }
            }
        }
    }

    public class Tutorial4Fragment :  Android.Support.V4.App.Fragment
    {
        public static Tutorial4Fragment new_instance ()
        {
            return new Tutorial4Fragment ();
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.TutorialFragment04, container, false);
            return view;
        }

        bool didAnimation = false;

        void Animation ()
        {
            if (didAnimation) {
                return;
            }
            didAnimation = true;

            var meeting_up = View.FindViewById<ImageView> (Resource.Id.meeting_up);
            var meeting_down = View.FindViewById<ImageView> (Resource.Id.meeting_down);

            meeting_up.Alpha = 1;
            meeting_down.Alpha = 1;

            var animation = new AlphaAnimation (0f, 1f);
            animation.Duration = 250;
            animation.StartOffset = 1000;
            meeting_up.StartAnimation (animation);
            meeting_down.StartAnimation (animation);

        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (UserVisibleHint) {
                Animation ();
            }
        }

        public override bool UserVisibleHint {
            get {
                return base.UserVisibleHint;
            }
            set {
                base.UserVisibleHint = value;
                if (value && IsResumed) {
                    Animation ();
                }
            }
        }
    }

    public class Tutorial5Fragment :  Android.Support.V4.App.Fragment
    {
        public static Tutorial5Fragment new_instance ()
        {
            return new Tutorial5Fragment ();
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.TutorialFragment05, container, false);
            return view;
        }

        bool didAnimation = false;

        View dotView;
        View messageView;
        ImageView swipeLeftView;
        ImageView swipeRightView;

        TranslateAnimation XTranslation (float start, float finish, long duration, long startOffset)
        {
            var animation = new TranslateAnimation (
                                Dimension.RelativeToParent, start,
                                Dimension.RelativeToParent, finish,
                                Dimension.RelativeToSelf, 0f,
                                Dimension.RelativeToSelf, 0f);
            animation.Duration = duration;
            animation.StartOffset = startOffset;
            return animation;
        }

        void Animation ()
        {
            if (didAnimation) {
                return;
            }
            didAnimation = true;

            dotView = View.FindViewById<View> (Resource.Id.dot);
            messageView = View.FindViewById<View> (Resource.Id.message);
            swipeLeftView = View.FindViewById<ImageView> (Resource.Id.swipeleft);
            swipeRightView = View.FindViewById<ImageView> (Resource.Id.swiperight);

            var dotAnimation = XTranslation (0f, -0.5f, 1000, 2000);

            var dotAlphaAnimation = new AlphaAnimation (0f, 1f);
            dotAlphaAnimation.Duration = 250;
            dotAlphaAnimation.StartOffset = 1500;

            var dotAnimationSet = new AnimationSet (true);
            dotAnimationSet.AddAnimation (dotAlphaAnimation);
            dotAnimationSet.AddAnimation (dotAnimation);

            var messageAnimation = XTranslation (0f, -0.5f, 1000, 2000);
            var swipeLeftAnimation = XTranslation (1f, 0.5f, 1000, 2000);

            dotAnimationSet.FillAfter = true;
            messageAnimation.FillAfter = true; 
            swipeLeftAnimation.FillAfter = true;

            dotView.Visibility = ViewStates.Visible;
            swipeLeftView.Visibility = ViewStates.Visible;

            dotView.StartAnimation (dotAnimationSet);
            messageView.StartAnimation (messageAnimation);
            swipeLeftView.StartAnimation (swipeLeftAnimation);

            messageAnimation.AnimationEnd += MessageAnimation_AnimationEnd;
        }

        void MessageAnimation_AnimationEnd (object sender, Android.Views.Animations.Animation.AnimationEndEventArgs e)
        {
            var dotAnimation = XTranslation (-0.5f, 0.5f, 2000, 0);

            var dotAnimationSet = new AnimationSet (true);
            dotAnimationSet.AddAnimation (dotAnimation);

            var messageAnimation = XTranslation (-0.5f, 0.5f, 2000, 0);
            var swipeLeftAnimation = XTranslation (0.5f, 1.0f, 2000, 0);
            var swipeRightAnimation = XTranslation (-1f, -0.25f, 2000, 0);

            dotAnimationSet.FillAfter = true;
            messageAnimation.FillAfter = true; 
            swipeLeftAnimation.FillAfter = true;
            swipeRightAnimation.FillAfter = true;

            swipeRightView.Visibility = ViewStates.Visible;

            dotView.StartAnimation (dotAnimationSet);
            messageView.StartAnimation (messageAnimation);
            swipeLeftView.StartAnimation (swipeLeftAnimation);
            swipeRightView.StartAnimation (swipeRightAnimation);

            swipeRightAnimation.AnimationEnd += MessageAnimation_AnimationEnd1;
        }

        void MessageAnimation_AnimationEnd1 (object sender, Android.Views.Animations.Animation.AnimationEndEventArgs e)
        {
            var dotAnimation = XTranslation (0.5f, 0f, 1000, 0);

            var dotAnimationSet = new AnimationSet (true);
            dotAnimationSet.AddAnimation (dotAnimation);

            var messageAnimation = XTranslation (0.5f, 0f, 1000, 0);
            var swipeRightAnimation = XTranslation (-0.25f, -1f, 1250, 0);

            dotAnimationSet.FillAfter = false;
            messageAnimation.FillAfter = true; 
            swipeRightAnimation.FillAfter = true;

            swipeLeftView.Visibility = ViewStates.Invisible;
            swipeRightView.Visibility = ViewStates.Visible;

            dotView.StartAnimation (dotAnimationSet);
            messageView.StartAnimation (messageAnimation);
            swipeRightView.StartAnimation (swipeRightAnimation);

            swipeRightAnimation.AnimationEnd += MessageAnimation_AnimationEnd2;
        }

        void MessageAnimation_AnimationEnd2 (object sender, Android.Views.Animations.Animation.AnimationEndEventArgs e)
        {
            dotView.Visibility = ViewStates.Invisible;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            if (UserVisibleHint) {
                Animation ();
            }
        }

        public override bool UserVisibleHint {
            get {
                return base.UserVisibleHint;
            }
            set {
                base.UserVisibleHint = value;
                if (value && IsResumed) {
                    Animation ();
                }
            }
        }
    }
}