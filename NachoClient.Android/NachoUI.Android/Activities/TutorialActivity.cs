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
    [Activity (Label = "TutorialActivity")]            
    public class TutorialActivity : FragmentActivity
    {
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            SetContentView (Resource.Layout.TutorialActivity);

            var pager = FindViewById<ViewPager> (Resource.Id.pager);
            var adaptor = new GenericFragmentPagerAdaptor (SupportFragmentManager);

            adaptor.AddFragmentView ((i, v, b) => {
                var view = i.Inflate (Resource.Layout.TutorialFragment01, v, false);
                return view;
            }
            );

            adaptor.AddFragmentView ((i, v, b) => {
                var view = i.Inflate (Resource.Layout.TutorialFragment02, v, false);
                return view;
            }
            );

            adaptor.AddFragmentView ((i, v, b) => {
                var view = i.Inflate (Resource.Layout.TutorialFragment03, v, false);
                return view;
            }
            );

            adaptor.AddFragmentView ((i, v, b) => {
                var view = i.Inflate (Resource.Layout.TutorialFragment04, v, false);
                return view;
            }
            );

            adaptor.AddFragmentView ((i, v, b) => {
                var view = i.Inflate (Resource.Layout.TutorialFragment05, v, false);
                return view;
            }
            );

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
                AnimateTutorial1 ();
                break;
            case 2:
                FindViewById<ImageView> (Resource.Id.dot_2).SetImageResource (Resource.Drawable.TutorialProgressDark);
                var marquee = FindViewById<TextView> (Resource.Id.tutorial_swipe);
                if (null != marquee) {
                    marquee.Visibility = ViewStates.Invisible;
                }
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

        void AnimateTutorial1 ()
        {
            var marquee = FindViewById<TextView> (Resource.Id.tutorial_swipe);
            if (null != marquee) {
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

        public void AddFragmentView (Func<LayoutInflater, ViewGroup, Bundle, View> view)
        {
            _fragmentList.Add (new GenericViewPagerFragment (view));
        }
    }

    public class GenericViewPagerFragment : Android.Support.V4.App.Fragment
    {
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

}