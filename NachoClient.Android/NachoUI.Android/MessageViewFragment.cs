using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.Webkit;
using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;
using MimeKit;

namespace NachoClient.AndroidClient
{
    public class MessageViewFragment : Android.Support.V4.App.Fragment
    {
        int insertionPoint;
        ViewGroup messageView;

        public override void OnCreate (Bundle savedInstanceState)
        {
            base.OnCreate (savedInstanceState);
            this.HasOptionsMenu = true;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var rootView = inflater.Inflate (Resource.Layout.MessageViewFragment, container, false);

//            var folderId = this.Arguments.GetInt ("folderId", 0);
            var messageId = this.Arguments.GetInt ("messageId", 0);
//            var accountId = this.Arguments.GetInt ("accountId", 0);

            var message = NcModel.Instance.Db.Get<McEmailMessage> (messageId);

            MarkAsRead (message);

            insertionPoint = 0;
            messageView = rootView.FindViewById<ViewGroup> (Resource.Id.value);

            if (null != message.From) {
                AddHeader ("From: ", message.From);
            }
            if (null != message.Subject) {
                AddHeader ("Subject: ", message.Subject);
            }
            if (null != message.To) {
                string[] toList = message.To.Split (new Char [] { ',' });
                foreach (var s in toList) {
                    AddHeader ("To: ", s);
                }
            }
            if (null != message.DisplayTo) {
                string[] displayToList = message.DisplayTo.Split (new Char[] { ';' });
                foreach (var s in displayToList) {
                    AddHeader ("Display To: ", s);
                }
            }
            if (null != message.Cc) {
                string[] CcList = message.Cc.Split (new Char [] { ',' });
                foreach (var s in CcList) {
                    AddHeader ("Cc: ", s);
                }
            }

            var attachments = NcModel.Instance.Db.Table<McAttachment> ().Where (a => a.EmailMessageId == message.Id).ToList ();

            if (0 < attachments.Count) {
                foreach (var a in attachments) {
                    if (a.IsInline) {
                        AddHeader ("Inline attachment: ", a.DisplayName);
                    } else if (McAbstrFileDesc.FilePresenceEnum.Complete == a.FilePresence) {
                        AddHeader ("Downloaded attachment: ", a.DisplayName);
                    } else if (McAbstrFileDesc.FilePresenceEnum.Partial == a.FilePresence) {
                        AddHeader ("Downloading attachment: ", a.DisplayName);
                    } else {
                        AddHeader ("Attachment on server: ", a.DisplayName);
                    }
                }
            }

            var body = message.GetBody ();
            if (null != body) {
                var bodySource = new MemoryStream (Encoding.UTF8.GetBytes (body));
                var bodyParser = new MimeParser (bodySource, MimeFormat.Default);
                var parsedMessage = bodyParser.ParseMessage ();
                MimeHelpers.DumpMessage (parsedMessage, 0);
                RenderMessage (parsedMessage);
            }
                
            //set the actionbar to use the custom view (can also be done with a style)
            var ActionBarActivity = (Android.Support.V7.App.ActionBarActivity)Activity;
            ActionBarActivity.Title = "Message";
            var displayOptions = ActionBarDisplayOptions.ShowHome | ActionBarDisplayOptions.HomeAsUp | ActionBarDisplayOptions.ShowCustom;
            ActionBarActivity.SupportActionBar.SetDisplayOptions ((int)displayOptions, -1);
            ActionBarActivity.SupportActionBar.SetCustomView (Resource.Layout.MessageViewFragmentActionBar);

            return rootView;
        }

        protected void AddHeader (string tag, string value)
        {
            var lf = this.GetLayoutInflater (null);
            var view = lf.Inflate (Resource.Layout.MessageHeaderItem, null);
            var tagView = view.FindViewById<TextView> (Resource.Id.tag);
            var valueView = view.FindViewById<TextView> (Resource.Id.value);
            tagView.Text = tag;
            valueView.Text = value;
            messageView.AddView (view, insertionPoint);
            insertionPoint += 1;
            view.Click += (object sender, EventArgs e) => {
                Console.WriteLine ("beep beep!");
            };
        }

        protected string Prepend (string prefix, string suffix)
        {
            if (null == suffix) {
                return prefix;
            } else {
                return prefix + suffix;
            }
        }

        void RenderMessage (MimeMessage message)
        {
            RenderMimeEntity (message.Body);
        }

        void RenderMimeEntity (MimeEntity entity)
        {
            if (entity is MessagePart) {
                // This entity is an attached message/rfc822 mime part.
                var messagePart = (MessagePart)entity;
                // If you'd like to render this inline instead of treating
                // it as an attachment, you would just continue to recurse:
                RenderMessage (messagePart.Message);
                return;
            }
            if (entity is Multipart) {
                // This entity is a multipart container.
                var multipart = (Multipart)entity;

                if (multipart.ContentType.Matches ("multipart", "alternative")) {
                    RenderBestAlternative (multipart);
                    return;
                }

                foreach (var subpart in multipart) {
                    RenderMimeEntity (subpart);
                }
                return;
            }

            // Everything that isn't either a MessagePart or a Multipart is a MimePart
            var part = (MimePart)entity;

            // Don't render anything that is explicitly marked as an attachment.
            //            if (part.IsAttachment)
            //                return;

            if (part is TextPart) {
                // This is a mime part with textual content.
                var text = (TextPart)part;

                if (text.ContentType.Matches ("text", "html")) {
                    RenderHtml (text.Text);
                } else {
                    RenderText (text.Text);
                }
                return;
            }
            if (entity.ContentType.Matches ("image", "*")) {
                RenderImage (part);
                return;
            }

            if (entity.ContentType.Matches ("application", "ics")) {
                NachoCore.Utils.Log.Error (Log.LOG_RENDER, "Unhandled ics: {0}\n", part.ContentType);
                return;
            }
            if (entity.ContentType.Matches ("application", "octet-stream")) {
                NachoCore.Utils.Log.Error (Log.LOG_RENDER, "Unhandled octet-stream: {0}\n", part.ContentType);
                return;
            }

            NachoCore.Utils.Log.Error (Log.LOG_RENDER, "Unhandled Render: {0}\n", part.ContentType);
        }

        /// <summary>
        /// Renders the best alternative.
        /// http://en.wikipedia.org/wiki/MIME#Alternative
        /// </summary>
        void RenderBestAlternative (Multipart multipart)
        {
            var e = multipart.Last ();
            RenderMimeEntity (e);
        }

        void RenderHtml (string html)
        {
            NachoCore.Utils.Log.Info (Log.LOG_RENDER, "Html element string:\n{0}", html);
            var lf = this.GetLayoutInflater (null);
            var view = lf.Inflate (Resource.Layout.MimeHTML, null);
            var valueView = view.FindViewById<WebView> (Resource.Id.value);
            valueView.Settings.LoadsImagesAutomatically = true;
            valueView.SetWebViewClient (new RenderHtmlEmbeddedItems ());
            valueView.LoadDataWithBaseURL ("", html, "text/html", "UTF-8", "");
            messageView.AddView (view, insertionPoint);
            insertionPoint += 1;
        }

        public class RenderHtmlEmbeddedItems : WebViewClient
        {
            public override WebResourceResponse ShouldInterceptRequest (WebView view, string url)
            {
                Log.Info (Log.LOG_RENDER, "ShouldInterceptRequest: {0}", url);
                return base.ShouldInterceptRequest (view, url);
            }

            public override bool ShouldOverrideUrlLoading (WebView view, string url)
            {
                Log.Info (Log.LOG_RENDER, "ShouldOverrideUrlLoading: {0}", url);
                return base.ShouldOverrideUrlLoading (view, url);
            }

            public override void OnPageStarted (WebView view, string url, Android.Graphics.Bitmap favicon)
            {
                Log.Info (Log.LOG_RENDER, "OnPageStarted: {0}", url);
                base.OnPageStarted (view, url, favicon);
            }

            public override void OnLoadResource (WebView view, string url)
            {
                Log.Info (Log.LOG_RENDER, "OnLoadResource: {0}", url);
                base.OnLoadResource (view, url);
            }

            public override void OnReceivedError (WebView view, ClientError errorCode, string description, string failingUrl)
            {
                Log.Info (Log.LOG_RENDER, "OnReceivedError: {0}", failingUrl);
                base.OnReceivedError (view, errorCode, description, failingUrl);
            }
        }

        void RenderText (string text)
        {
            NachoCore.Utils.Log.Info (Log.LOG_RENDER, "Add multiline element: {0}", text);
            var lf = this.GetLayoutInflater (null);
            var view = lf.Inflate (Resource.Layout.MimePartText, null);
            var valueView = view.FindViewById<TextView> (Resource.Id.value);
            valueView.Text = text;
            messageView.AddView (view, insertionPoint);
            insertionPoint += 1;
        }

        void RenderImage (MimePart part)
        {
            NachoCore.Utils.Log.Info (Log.LOG_RENDER, "Add image element: {0}", part);
            var lf = this.GetLayoutInflater (null);
            var view = lf.Inflate (Resource.Layout.MimePartImage, null);
            var valueView = view.FindViewById<ImageView> (Resource.Id.value);

            using (var content = new MemoryStream ()) {
                // If the content is base64 encoded (which it probably is), decode it.
                part.ContentObject.DecodeTo (content);
                content.Seek (0, SeekOrigin.Begin);
                var d = new global::Android.Graphics.Drawables.BitmapDrawable (content);
                valueView.SetImageDrawable (d);
            }
            messageView.AddView (view, insertionPoint);
            insertionPoint += 1;
        }

        void DisplayAttachment (McAttachment attachment)
        {

        }

        void DownloadAttachment (McAttachment attachment)
        {
            if (McAbstrFileDesc.FilePresenceEnum.None == attachment.FilePresence) {
                var account = NcModel.Instance.Db.Table<McAccount> ().First ();
                // FIXME - first account only works for the moment...
                BackEnd.Instance.DnldAttCmd (account.Id, attachment.Id);
//                ReloadRoot ();
            }
        }

        void MarkAsRead (McEmailMessage message)
        {
            if (false == message.IsRead) {
                BackEnd.Instance.MarkEmailReadCmd (message.AccountId, message.Id);
            }
        }

        public override void OnCreateOptionsMenu (IMenu menu, MenuInflater inflater)
        {
            base.OnCreateOptionsMenu (menu, inflater);
            menu.Clear ();
            inflater.Inflate (Resource.Menu.MessageViewFragment, menu);
        }
    }
}

