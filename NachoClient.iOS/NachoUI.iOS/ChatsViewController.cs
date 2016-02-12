//  Copyright (C) 2016 Nacho Cove, Inc. All rights reserved.
//
using System;
using UIKit;
using NachoCore;
using NachoCore.Utils;
using NachoCore.Model;
using System.Collections.Generic;

namespace NachoClient.iOS
{
    [Foundation.Register ("ChatsViewController")]
    public class ChatsViewController : NcUIViewControllerNoLeaks
    {
        public ChatsViewController (IntPtr ptr) : base(ptr)
        {
        }

        protected override void CreateViewHierarchy ()
        {
            var account = NcApplication.Instance.DefaultEmailAccount;
            var addresses = new List<McEmailAddress> (1);
            McEmailAddress address;
            McEmailAddress.Get (account.Id, "owens@d3.officeburrito.com", out address);
            addresses.Add (address);
            var chat = McChat.ChatForAddresses (account.Id, addresses);
            ChatMessageComposer.SendChatMessage (chat, "hello chat!", null);
        }

        protected override void ConfigureAndLayout ()
        {
        }

        protected override void Cleanup ()
        {
        }
    }
}

