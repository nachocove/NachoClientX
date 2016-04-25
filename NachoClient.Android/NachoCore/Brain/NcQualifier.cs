//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Text.RegularExpressions;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    /// <summary>
    /// A qualifier / disqualifier is a discrete feature of an object that, if exists, immediately
    /// qualifies or disqualifies an object as hot.
    ///
    /// This is the base class of all discrete qualifiers and disqualifiers and it must not be
    /// used directly. 
    /// </summary>
    public class NcQualifierBase<T>
    {
        public string Description { get; protected set; }

        public double QualifiedFactor { get; protected set; }

        public double NonQualifiedFactor { get; protected set; }

        public NcQualifierBase (string description, double qualified, double notQualifed)
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
    public class NcQualifier<T> : NcQualifierBase<T>
    {
        public double Weight {
            get {
                return QualifiedFactor;
            }
            set {
                QualifiedFactor = value;
            }
        }

        public NcQualifier (string description, double weight) :
            base (description, weight, Scoring.Min)
        {
        }
    }

    public class NcVipQualifier : NcQualifier<McEmailMessage>
    {
        public NcVipQualifier () :
            base ("VipQualifer", 1.0)
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
            base ("UserActionQualifier", Scoring.MarkedHotWeight)
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
            base ("RepliesToMyEmailsQualifiier", 1.0)
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
    public class NcDisqualifier<T> : NcQualifierBase<T>
    {
        public double Penalty {
            get {
                return QualifiedFactor;
            }
            set {
                QualifiedFactor = value;
            }
        }

        public NcDisqualifier (string description, double penalty) :
            base (description, penalty, Scoring.Max)
        {
        }
    }

    public class NcUserActionDisqualifier : NcDisqualifier<McEmailMessage>
    {
        public NcUserActionDisqualifier () :
            base ("UserActionDisqualifier", Scoring.MarkedNotHotPenalty)
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


    public class NcMarketingEmailDisqualifier : NcDisqualifier<McEmailMessage>
    {
        protected static Regex HeaderFilters = 
            new Regex (
                @"X-Campaign(.*):|" +
                @"List-Unsubscribe:",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public NcMarketingEmailDisqualifier () :
            base ("MarketingMailDisqualifier", Scoring.HeadersFilteringPenalty)
        {
        }

        public override bool Analyze (McEmailMessage emailMessage)
        {
            if (emailMessage.HeadersFiltered) {
                // Already disqualified. No further computation needed.
                return true;
            }
            bool isMarketing = !string.IsNullOrEmpty (emailMessage.Headers) && HeaderFilters.IsMatch (emailMessage.Headers);
            emailMessage.HeadersFiltered = isMarketing;
            return isMarketing;
        }

        public override bool ConditionMet (McEmailMessage emailMessage)
        {
            return emailMessage.HeadersFiltered;
        }
    }

    public class NcYahooBulkEmailDisqualifier : NcDisqualifier<McEmailMessage>
    {
        protected static Regex HeaderFilters =
            new Regex (
                @"X-YahooFilteredBulk:",
                RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        public NcYahooBulkEmailDisqualifier () :
            base ("BulkEmailDisqualifier", Scoring.HeadersFilteringPenalty)
        {
        }

        public override bool Analyze (McEmailMessage emailMessage)
        {
            if (emailMessage.HeadersFiltered) {
                // Already disqualified. No further computation needed.
                return true;
            }
            bool isBulk = !string.IsNullOrEmpty (emailMessage.Headers) && HeaderFilters.IsMatch (emailMessage.Headers);
            emailMessage.HeadersFiltered = isBulk;
            return isBulk;
        }

        public override bool ConditionMet (McEmailMessage emailMessage)
        {
            return emailMessage.HeadersFiltered;
        }

    }
}

