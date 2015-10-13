
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
using NachoCore.Utils;
using NachoCore;
using NachoCore.Model;
using NachoPlatform;
using Android.Views.InputMethods;

namespace NachoClient.AndroidClient
{
    public class SupportMessageFragment : Fragment
    {
        protected NcTimer sendMessageTimer;
        protected bool hasDisplayedStatusMessage = false;
        ProgressBar activityIndicatorView;

        public static SupportMessageFragment newInstance ()
        {
            var fragment = new SupportMessageFragment ();
            return fragment;
        }

        public override View OnCreateView (LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            var view = inflater.Inflate (Resource.Layout.SupportMessageFragment, container, false);

            var sendButton = view.FindViewById<Android.Widget.ImageView> (Resource.Id.right_button1);
            sendButton.SetImageResource (Resource.Drawable.icn_send);
            sendButton.Visibility = Android.Views.ViewStates.Visible;
            sendButton.Click += SendButton_Click;

            var titleView = view.FindViewById<TextView> (Resource.Id.title);
            titleView.SetText (Resource.String.message_support);

            activityIndicatorView = view.FindViewById<ProgressBar> (Resource.Id.spinner);
            activityIndicatorView.Visibility = ViewStates.Invisible;

            return view;
        }

        public override void OnResume ()
        {
            base.OnResume ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        public override void OnPause ()
        {
            base.OnPause ();
            NcApplication.Instance.StatusIndEvent += StatusIndicatorCallback;
        }

        void SendButton_Click (object sender, EventArgs e)
        {
            InputMethodManager imm = (InputMethodManager)Activity.GetSystemService (Activity.InputMethodService);
            imm.HideSoftInputFromWindow (View.WindowToken, HideSoftInputFlags.NotAlways);

            if (!NachoCore.Utils.Network_Helpers.HasNetworkConnection ()) {
                NcAlertView.ShowMessage (Activity, "Network Error",
                    "A networking issue prevents this message from being sent. Please try again when you have a network connection.");
            } else {
                sendMessageTimer = new NcTimer ("support", MessageSendTimeout, null, 12 * 1000, 0); 

                var contactInfoTextField = View.FindViewById<EditText> (Resource.Id.reach);
                var messageInfoTextView = View.FindViewById<EditText> (Resource.Id.assist);

                Dictionary<string,string> supportInfo = new Dictionary<string, string> ();
                supportInfo.Add ("ContactInfo", contactInfoTextField.Text);
                supportInfo.Add ("Message", messageInfoTextView.Text);
                supportInfo.Add ("BuildVersion", Build.BuildInfo.Version);
                supportInfo.Add ("BuildNumber", Build.BuildInfo.BuildNumber);

                Telemetry.StartService ();
                // Close all JSON files so they can be immediately uploaded while the user enters the
                Telemetry.JsonFileTable.FinalizeAll ();
                Telemetry.RecordSupport (supportInfo, () => {
                    NcApplication.Instance.InvokeStatusIndEvent (new StatusIndEventArgs () { 
                        Status = NachoCore.Utils.NcResult.Info (NcResult.SubKindEnum.Info_TelemetrySupportMessageReceived),
                        Account = ConstMcAccount.NotAccountSpecific,
                    });
                });
                activityIndicatorView.Visibility = ViewStates.Visible;
            }
        }

        protected void StatusIndicatorCallback (object sender, EventArgs e)
        {
            var s = (StatusIndEventArgs)e;

            if (NcResult.SubKindEnum.Info_TelemetrySupportMessageReceived == s.Status.SubKind) {
                MessageReceived (true);
            }
        }

        protected string GetEmailAddress ()
        {
            if (LoginHelpers.IsCurrentAccountSet ()) {
                McAccount Account = McAccount.QueryById<McAccount> (LoginHelpers.GetCurrentAccountId ());
                return Account.EmailAddr;
            } else {
                return "";
            }
        }

        void MessageSendTimeout (object sender)
        {
            InvokeOnUIThread.Instance.Invoke (delegate () {
                MessageReceived (false);
            });
        }

        void MessageReceived (bool didSend)
        {
            if (!hasDisplayedStatusMessage) {
                hasDisplayedStatusMessage = true;

                activityIndicatorView.Visibility = ViewStates.Invisible;

                if (null != sendMessageTimer) {
                    sendMessageTimer.Dispose ();
                    sendMessageTimer = null;
                }

                if (didSend) {
                    NcAlertView.Show (Activity, "Message Sent",
                        "We have received your message and will respond shortly. Thank you for your feedback.",
                        () => {
                            MessageSent ();
                        });
                } else {
                    NcAlertView.Show (Activity, "Message Not Sent",
                        "There was a delay while sending the message. We will continue trying to send the message in the background.",
                        () => {
                            MessageSent ();
                        });
                }
            }
        }

        void MessageSent ()
        {
            var parent = (SupportActivity)Activity;
            parent.MessageSentCallback ();
        }

    }
}

