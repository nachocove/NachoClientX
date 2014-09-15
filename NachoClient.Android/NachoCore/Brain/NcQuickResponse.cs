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
            new QuickResponse ("Hey!", "Hey, haven't seen you in a while..."),
            new QuickResponse ("Catch up?", "You want to catch up over coffee soon?"),
            new QuickResponse ("How are you?", "How are you doing?"),
            new QuickResponse ("Call me.", "Hey, Can you call me asap?"),
            new QuickResponse ("Expense reports", "Can you approve my expense report?"),
        };

        public enum QRTypeEnum
        {
            Compose,
            Reply,
            Forward,
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
            default:
                return null;
            }
        }

        public void CreateQuickResponse (QuickResponse whichOne, ref McEmailMessage emailMessage)
        {
            switch (whatType) {
            case QRTypeEnum.Compose:
                emailMessage.Subject = whichOne.subject;
                McBody emailBody = McBody.QueryById<McBody> (emailMessage.BodyId);
                emailBody.UpdateData (whichOne.body);
                break;
            default:
                return;
            }
        }

        public class QuickResponse
        {
            public string subject;
            public string body;

            public QuickResponse (string subject, string body)
            {
                this.subject = subject;
                this.body = body;
            }
        }
    }
}
