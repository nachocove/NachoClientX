//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using MonoTouch.UIKit;
using MonoTouch.Foundation;
using NachoCore.Utils;

namespace NachoClient.iOS
{
    public class NcUIViewController : UIViewController
    {
        private string ClassName;
        public event EventHandler ViewDisappearing;

        public NcUIViewController () : base()
        {
            Initialize ();
        }

        public NcUIViewController (IntPtr handle) : base (handle)
        {
            Initialize ();
        }

        public NcUIViewController (string nibName, NSBundle bundle) : base (nibName, bundle)
        {
            Initialize ();
        }

        private void Initialize ()
        {
            ClassName = this.GetType ().Name;
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            NachoClient.Util.HighPriority ();
        }
            
        public override void ViewWillAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR + "_BEGIN");
            base.ViewWillAppear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLAPPEAR + "_END");
        }

        public override void ViewDidAppear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_BEGIN");
            base.ViewDidAppear (animated);
            NachoClient.Util.RegularPriority ();
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDAPPEAR + "_END");
        }

        public override void ViewWillDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_BEGIN");
            base.ViewWillDisappear (animated);
            if (null != ViewDisappearing) {
                ViewDisappearing (this, EventArgs.Empty);
            }
            NachoClient.Util.RegularPriority ();
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_WILLDISAPPEAR + "_END");
        }

        public override void ViewDidDisappear (bool animated)
        {
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_BEGIN");
            base.ViewDidDisappear (animated);
            Telemetry.RecordUiViewController (ClassName, TelemetryEvent.UIVIEW_DIDDISAPPEAR + "_END");
        }
    }

    public abstract class NcUIViewControllerNoLeaks : NcUIViewController
    {
        public NcUIViewControllerNoLeaks ()
            : base()
        {
        }

        public NcUIViewControllerNoLeaks (IntPtr handle)
            : base (handle)
        {
        }

        public NcUIViewControllerNoLeaks (string nibName, NSBundle bundle)
            : base (nibName, bundle)
        {
        }

        protected abstract void CreateViewHierarchy ();

        protected abstract void ConfigureAndLayout ();

        protected abstract void Cleanup ();

        protected static void DisposeViewHierarchy (UIView view)
        {
            // This code comes from a StackOverflow question, and was provided by
            // Herman Schoenfeld.  I have made some slight modifications.
            // http://stackoverflow.com/questions/25532870/xamarin-ios-memory-leaks-everywhere
            try {
                if (null == view || IntPtr.Zero == view.Handle) {
                    return;
                }
                bool skipDispose = false;
                if (null != view.Subviews) {
                    foreach (var subview in view.Subviews) {
                        try {
                            DisposeViewHierarchy (subview);
                        } catch (Exception e) {
                            Log.Error(Log.LOG_UI, "Exception while disposing of view hierarchy: {0}", e.ToString());
                        }
                    }
                }
                if (view is UIActivityIndicatorView) {
                    var indicatorView = view as UIActivityIndicatorView;
                    if (indicatorView.IsAnimating) {
                        indicatorView.StopAnimating();
                    }
                } else if (view is UITableView) {
                    var tableView = view as UITableView;
                    if (null != tableView.DataSource) {
                        tableView.DataSource.Dispose();
                    }
                    tableView.Source = null;
                    tableView.Delegate = null;
                    tableView.DataSource = null;
                    tableView.WeakDelegate = null;
                    tableView.WeakDataSource = null;
                    if (null != tableView.VisibleCells) {
                        foreach (var cell in tableView.VisibleCells) {
                            DisposeViewHierarchy(cell);
                        }
                    }
                } else if (view is UICollectionView) {
                    // UICollectionViewController with throw if its view is disposed before the controller.
                    skipDispose = true;
                    var collectionView = view as UICollectionView;
                    if (null != collectionView.DataSource) {
                        collectionView.DataSource.Dispose();
                    }
                    collectionView.Source = null;
                    collectionView.Delegate = null;
                    collectionView.DataSource = null;
                    collectionView.WeakDelegate = null;
                    collectionView.WeakDataSource = null;
                    if (null != collectionView.VisibleCells) {
                        foreach (var cell in collectionView.VisibleCells) {
                            DisposeViewHierarchy(cell);
                        }
                    }
                } else if (view is UIWebView) {
                    var webView = view as UIWebView;
                    if (webView.IsLoading) {
                        webView.StopLoading();
                    }
                    webView.LoadHtmlString(string.Empty, null);
                    webView.Delegate = null;
                    webView.WeakDelegate = null;
                }
                if (null != view.Layer) {
                    view.Layer.RemoveAllAnimations();
                }
                if (!skipDispose) {
                    view.Dispose();
                }
            } catch (Exception e) {
                Log.Error (Log.LOG_UI, "Exception while disposing of view hierarchy: {0}", e.ToString ());
            }
        }

        public override void ViewDidLoad ()
        {
            base.ViewDidLoad ();
            CreateViewHierarchy ();
        }

        public override void ViewWillAppear (bool animated)
        {
            base.ViewWillAppear (animated);
            // Force the view hierarchy to be created by accessing the View property.
            this.View.GetHashCode ();
            ConfigureAndLayout ();
        }

        public override void ViewDidDisappear (bool animated)
        {
            base.ViewDidDisappear (animated);
            if (this.IsViewLoaded && null == this.NavigationController) {
                Cleanup ();
                DisposeViewHierarchy (View);
                View = null;
            }
        }
    }
}

