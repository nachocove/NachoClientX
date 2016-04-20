//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore
{
    public interface IStatusIndEvent
    {
        event EventHandler StatusIndEvent;
    }

    public class StatusIndEventArgs : EventArgs
    {
        public McAccount Account;
        public NcResult Status;
        public string[] Tokens;
        public DateTime Stamp;

        public bool AppliesToAccount (McAccount account)
        {
            if (null == Account) {
                return true; // applies to all accounts
            }
            if (null == account) {
                return false;
            }
            return account.ContainsAccount (Account.Id);
        }
    }
}
