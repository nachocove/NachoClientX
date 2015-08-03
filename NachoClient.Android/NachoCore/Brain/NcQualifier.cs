//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Text.RegularExpressions;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    /// <summary>
    /// A qualifoer / disqualifier is a discrete feature of an object that, if exists, immediately
    /// qualifies or disqualifies an object as hot.
    ///
    /// This is the base class of all discrete qualifiers and disqualifiers and it must not be
    /// used directly. 
    /// </summary>
    public class NcQualifier<T>
    {
        public string Description { get; protected set; }

        public double QualifiedFactor { get; set; }

        public double NonQualifiedFactor { get; set; }

        public NcQualifier (string description, double qualified, double notQualifed)
        {
            Description = description;
            QualifiedFactor = qualified;
            NonQualifiedFactor = notQualifed;
        }

        public virtual bool Analyze (T obj)
        {
            throw new NotImplementedException ();
        }

        public virtual bool ConditionMet (T obj)
        {
            throw new NotImplementedException ();
        }

        public double Classify (T obj)
        {
            return ConditionMet (obj) ? QualifiedFactor : NonQualifiedFactor;
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /// Qualifiers
    ////////////////////////////////////////////////////////////////////////////////////////////////
    public class NcVipQualifier : NcQualifier<McEmailMessage>
    {
        public NcVipQualifier () :
            base ("VipQualifer", 1.0, 0.0)
        {
        }

        public override bool Analyze (McEmailMessage emailMessage)
        {
            return false; // nothing to analyze / update.
        }

        public override bool ConditionMet (McEmailMessage emailMessage)
        {
            if (0 == emailMessage.FromEmailAddressId) {
                return false;
            }
            var emailAddress = McEmailAddress.QueryById<McEmailAddress> (emailMessage.FromEmailAddressId);
            if (null == emailAddress) {
                return false;
            }
            return emailAddress.IsVip;
        }
    }

    public class NcUserActionQualifier : NcQualifier<McEmailMessage>
    {
        public NcUserActionQualifier () :
            base ("UserActionQualifier", 1.0, 0.0)
        {
        }

        public override bool Analyze (McEmailMessage emailMessage)
        {
            return false; // nothing to analyze / update.
        }

        public override bool ConditionMet (McEmailMessage emailMessage)
        {
            return 1 == emailMessage.UserAction;
        }
    }

    public class NcRepliesToMyEmailsQualifier : NcQualifier<McEmailMessage>
    {
        public NcRepliesToMyEmailsQualifier () :
            base ("RepliesToMyEmailsQualifiier", 1.0, 0.0)
        {
        }

        public override bool Analyze (McEmailMessage emailMessage)
        {
            bool isReplied = false;
            var original = McEmailMessage.QueryByMessageId (emailMessage.AccountId, emailMessage.InReplyTo);
            if (null != original) {
                isReplied = original.IsFromMe ();
            }
            if (isReplied != emailMessage.IsReply) {
                emailMessage.IsReply = isReplied;
                return true;
            }
            return false;
        }

        public override bool ConditionMet (McEmailMessage emailMessage)
        {
            return emailMessage.IsReply;
        }
    }

    ////////////////////////////////////////////////////////////////////////////////////////////////
    /// Disqualifiers
    ////////////////////////////////////////////////////////////////////////////////////////////////
    public class NcUserActionDisqualifier : NcQualifier<McEmailMessage>
    {
        public NcUserActionDisqualifier () :
            base ("UserActionDisqualifier", 0.0, 1.0)
        {
        }

        public override bool Analyze (McEmailMessage emailMessage)
        {
            return false; // nothing to analyze / update
        }

        public override bool ConditionMet (McEmailMessage emailMessage)
        {
            return -1 == emailMessage.UserAction;
        }
    }


    public class NcMarketingMailDisqualifier : NcQualifier<McEmailMessage>
    {
        protected static Regex HeaderFilters = 
            new Regex (
                @"X-Campaign(.*):|" +
                @"List-Unsubscribe:",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public NcMarketingMailDisqualifier () :
            base ("MarketingMailDisqualifier", Scoring.HeaderFilteringPenalty, 1.0)
        {
        }

        public override bool Analyze (McEmailMessage emailMessage)
        {
            var isFiltered =
                String.IsNullOrEmpty (emailMessage.Headers) ? false : HeaderFilters.IsMatch (emailMessage.Headers);
            if (isFiltered != emailMessage.HeadersFiltered) {
                emailMessage.HeadersFiltered = isFiltered;
                return true;
            }
            return false;
        }

        public override bool ConditionMet (McEmailMessage emailMessage)
        {
            return emailMessage.HeadersFiltered;
        }
    }
}

