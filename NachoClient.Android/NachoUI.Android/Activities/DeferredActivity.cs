using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

using NachoCore;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoClient.AndroidClient
{
    [Activity (Label = "DeferredActivity")]            
    public class DeferredActivity : NcMessageListActivity
    {
        protected override INachoEmailMessages GetMessages (out List<int> adds, out List<int> deletes)
        {
            var messages = new NachoDeferredEmailMessages (NcApplication.Instance.Account.Id);
            messages.Refresh (out adds, out deletes);
            return messages;
        }
    }
}
