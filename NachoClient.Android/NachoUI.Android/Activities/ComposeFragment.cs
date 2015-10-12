
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

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

using MimeKit;
using Java.Interop;
using NachoPlatform;
using System.IO;

namespace NachoClient.AndroidClient
{
    public class ComposeFragment : Fragment, NachoJavascriptMessageHandler, MessageComposerDelegate, NachoWebClientDelegate
    {

        #region Properties

        MessageComposer Composer;
        Android.Webkit.WebView WebView;
        Android.Widget.ImageView SendButton;
        bool IsWebViewLoaded;
        bool FocusWebViewOnLoad;
        List<Tuple<string, JavascriptCallback>> JavaScriptQueue;

        #endregion

        #region Constructor/Factory

        public ComposeFragment () : base()
        {
            Composer = new MessageComposer (NcApplication.Instance.Account);
            Composer.Delegate = this;
            JavaScriptQueue = new List<Tuple<string, JavascriptCallback>> ();
        }

        public static ComposeFragment newInstance ()
        {
            var fragment = new ComposeFragment ();
            return fragment;
        }

        #endregion

        #region Lifecycle

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            // Use this to return your custom view for this Fragment
            // return inflater.Inflate(Resource.Layout.YourFragment, container, false);
            var view = inflater.Inflate (Resource.Layout.ComposeFragment, container, false);

            SendButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            SendButton.SetImageResource (Resource.Drawable.icn_send);
            SendButton.Visibility = Android.Views.ViewStates.Visible;
            SendButton.Click += SendButton_Click;

            WebView = view.FindViewById<Android.Webkit.WebView> (Resource.Id.message);
            WebView.Settings.JavaScriptEnabled = true;
            WebView.AddJavascriptInterface (new NachoJavascriptMessenger(this, "nacho"), "_android_messageHandlers_nacho");
            WebView.AddJavascriptInterface (new NachoJavascriptMessenger(this, "nachoCompose"), "_android_messageHandlers_nachoCompose");
            WebView.SetWebViewClient (new NachoWebClient (this));

            Composer.StartPreparingMessage ();

            return view;
        }

        #endregion

        #region User Actions

        void SendButton_Click (object sender, EventArgs e)
        {
            if (String.IsNullOrWhiteSpace (Composer.Message.Subject)) {
                // TODO: alert with empty subject warning; don't send
                SendWithoutSubject ();
            } else {
                CheckSizeBeforeSending ();
            }
        }

        void SendWithoutSubject ()
        {
            CheckSizeBeforeSending ();
        }

        void AddSubject ()
        {
            // TODO: focus subject field
        }

        void CheckSizeBeforeSending ()
        {
            SendButton.Enabled = false;
            GetHtmlContent ((string html) => {
                SendButton.Enabled = true;
                Composer.Save (html);
                if (Composer.IsOversize) {
                    if (Composer.CanResize) {
                        // TODO: show alert with resize options; don't send
                        Send ();
                    } else {
                        // TODO: show alert with size warning; don't send
                        Send ();
                    }
                } else {
                    Send ();
                }
            });
        }

        void AcknowlegeSizeWarning ()
        {
            Send ();
        }

        void ResizeImagesLarge ()
        {
            ResizeImagesAndSend (Composer.LargeImageLengths);
        }

        void ResizeImagesMedium ()
        {
            ResizeImagesAndSend (Composer.MediumImageLengths);
        }

        void ResizeImagesSmall ()
        {
            ResizeImagesAndSend (Composer.SmallImageLengths);
        }

        void ResizeImagesAndSend (Tuple<float, float> lengths)
        {
            Composer.ImageLengths = lengths;
            Send ();
        }

        void Send ()
        {
            Composer.Send ();
            Activity.Finish ();
        }

        #endregion

        #region Web View

        void EvaluateJavascript (string js, JavascriptCallback callback = null)
        {
            if (IsWebViewLoaded) {
                JavascriptResultHandler handler = null;
                if (callback != null) {
                    handler = new JavascriptResultHandler (callback);
                }
                WebView.EvaluateJavascript (js, handler);
            } else {
                JavaScriptQueue.Add (new Tuple<string, JavascriptCallback> (js, callback));
            }
        }

        public void HandleJavascriptMessage (NachoJavascriptMessage message)
        {
        }

        public void OnPageFinished (Android.Webkit.WebView view, string url)
        {
            IsWebViewLoaded = true;
            EnableEditingInWebView ();
            foreach (var args in JavaScriptQueue) {
                EvaluateJavascript (args.Item1, args.Item2);
            }
            JavaScriptQueue = null;
            if (FocusWebViewOnLoad) {
                FocusWebView ();
            }
        }

        private void EnableEditingInWebView ()
        {
            EvaluateJavascript ("Editor.Enable()");
        }

        private void FocusWebView ()
        {
            EvaluateJavascript ("Editor.defaultEditor.focus()");
        }

        #endregion

        #region Message Composer

        public void MessageComposerDidCompletePreparation (MessageComposer composer)
        {
            DisplayMessageBody ();
        }

        public void MessageComposerDidFailToLoadMessage (MessageComposer composer)
        {
            // TODO: show alert
//            var alertController = UIAlertController.Create ("Could not load message", "Sorry, we could not load your message.  Please try again.", UIAlertControllerStyle.Alert);
//            alertController.AddAction (UIAlertAction.Create ("OK", UIAlertActionStyle.Default, (UIAlertAction obj) => { 
//                DismissViewController (true, null);
//            }));
//            PresentViewController (alertController, true, null);
        }

        public PlatformImage ImageForMessageComposerAttachment (MessageComposer composer, Stream stream)
        {
            // TODO: return android image
//            return ImageiOS.FromStream (stream);
            return null;
        }

        void DisplayMessageBody ()
        {
            if (Composer.Bundle != null) {
                if (Composer.Bundle.FullHtmlUrl != null) {
                    WebView.LoadUrl (Composer.Bundle.FullHtmlUrl.AbsoluteUri);
                } else {
                    var html = Composer.Bundle.FullHtml;
                    WebView.LoadDataWithBaseURL (Composer.Bundle.BaseUrl.AbsoluteUri, html, "text/html", "utf-8", null);
                }
            }
        }

        #endregion

        #region Helpers

        delegate void HtmlContentCallback (string html);

        void GetHtmlContent (HtmlContentCallback callback)
        {
            EvaluateJavascript ("document.documentElement.outerHTML", (Java.Lang.Object result) => {
                var stringResult = result as Java.Lang.String;
                callback ("<!DOCTYPE html>\n" + stringResult.ToString ());
            });
        }

        #endregion

        #region Bride JS Result Interface to Delegate

        delegate void JavascriptCallback (Java.Lang.Object value);

        class JavascriptResultHandler : Java.Lang.Object, Android.Webkit.IValueCallback
        {

            JavascriptCallback Callback;

            public JavascriptResultHandler (JavascriptCallback callback) : base()
            {
                Callback = callback;
            }

            public void OnReceiveValue (Java.Lang.Object value)
            {
                Callback (value);
            }

        }

        #endregion

    }

    public interface NachoJavascriptMessageHandler {

        void HandleJavascriptMessage (NachoJavascriptMessage message);

    }

    public class NachoJavascriptMessage {

        object Body;
        string Name;

        public NachoJavascriptMessage (object body, string name)
        {
            Body = body;
            Name = name;
        }

    }

    public class NachoJavascriptMessenger : Java.Lang.Object
    {

        NachoJavascriptMessageHandler Handler;
        string Name;

        public NachoJavascriptMessenger (NachoJavascriptMessageHandler handler, string name)
        {
            Handler = handler;
            Name = name;
        }

        [Export ("postMessage")]
        [Android.Webkit.JavascriptInterface]
        void PostMessage (Java.Lang.Object o)
        {
            var message = new NachoJavascriptMessage (o, Name);
            Handler.HandleJavascriptMessage (message);
        }

    }

    interface NachoWebClientDelegate {

        void OnPageFinished  (Android.Webkit.WebView view, string url);

    }

    class NachoWebClient : Android.Webkit.WebViewClient 
    {

        NachoWebClientDelegate Delegate;

        public NachoWebClient (NachoWebClientDelegate del)
        {
            Delegate = del;
        }

        public override void OnPageFinished (Android.Webkit.WebView view, string url)
        {
            Delegate.OnPageFinished (view, url);
        }
    }
}

