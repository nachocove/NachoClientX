//  Copyright (C) 2014 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Collections.Generic;
using NachoCore.Model;
using NachoCore.Utils;

namespace NachoCore.Brain
{
    public class NcQuickResponse
    {
        protected List <QuickResponse> QuickComposeList = new List<QuickResponse> () {
            new QuickResponse ("Hey!", "Hey, haven't seen you in a while...", NcMessageIntent.NONE_INTENT),
            new QuickResponse ("Catch up?", "You want to catch up over coffee soon?", NcMessageIntent.NONE_INTENT),
            new QuickResponse ("How are you?", "How are you doing?", NcMessageIntent.NONE_INTENT),
            new QuickResponse ("Call me.", "Hey, Can you call me asap?", NcMessageIntent.RESPONSE_REQUIRED_INTENT),
            new QuickResponse ("Expense reports", "Can you approve my expense report?", NcMessageIntent.RESPONSE_REQUIRED_INTENT),
        };

        protected List <QuickResponse> QuickReplyList = new List<QuickResponse> () {
            new QuickResponse (null, "Nice job!", NcMessageIntent.NONE_INTENT),
            new QuickResponse (null, "Thanks.", NcMessageIntent.NONE_INTENT),
            new QuickResponse (null, "Approved.", NcMessageIntent.NONE_INTENT),
            new QuickResponse (null, "Please call me to discuss.", NcMessageIntent.RESPONSE_REQUIRED_INTENT),
            new QuickResponse (null, "Ok.", NcMessageIntent.NONE_INTENT),
            new QuickResponse (null, "Not at this time.", NcMessageIntent.NONE_INTENT),
        };

        protected List <QuickResponse> QuickForwardList = new List<QuickResponse> () {
            new QuickResponse (null, "FYI", NcMessageIntent.NONE_INTENT),
            new QuickResponse (null, "Please Read and Respond by ... ", NcMessageIntent.PLEASE_READ_INTENT),
            new QuickResponse (null, "Please call me ASAP to discuss.", NcMessageIntent.RESPONSE_REQUIRED_INTENT),
        };

        public enum QRTypeEnum
        {
            Compose,
            Reply,
            Forward,
            None,
        }

        public QRTypeEnum whatType { get; private set; }

        public NcQuickResponse (QRTypeEnum whatType)
        {
            this.whatType = whatType;
        }

        public List<QuickResponse> GetResponseList ()
        {
            switch (whatType) {
            case QRTypeEnum.Compose:
                return QuickComposeList;
            case QRTypeEnum.Reply:
                return QuickReplyList;
            case QRTypeEnum.Forward:
                return QuickForwardList;
            default:
                return null;
            }
        }

        public class QuickResponse
        {
            public string subject;
            public string body;
            public NcMessageIntent.MessageIntent intent;

            public QuickResponse (string subject, string body, NcMessageIntent.MessageIntent intent = null)
            {
                this.subject = subject;
                this.body = body;
                this.intent = intent;
            }
        }
    }
}
