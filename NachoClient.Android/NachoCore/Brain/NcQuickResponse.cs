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

        protected List <QuickResponse> QuickReplyList = new List<QuickResponse> () {
            new QuickResponse (null, "Nice job!"),
            new QuickResponse (null, "Thanks."),
            new QuickResponse (null, "Approved."),
            new QuickResponse (null, "Please call me to discuss."),
            new QuickResponse (null, "Ok."),
            new QuickResponse (null, "Not at this time."),
        };

        protected List <QuickResponse> QuickForwardList = new List<QuickResponse> () {
            new QuickResponse (null, "FYI"),
            new QuickResponse (null, "Please Read and Respond by ... "),
            new QuickResponse (null, "Do you agree?"),
            new QuickResponse (null, "Please call me ASAP to discuss."),
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

        public void CreateQuickResponse (QuickResponse selectedResponse, ref McEmailMessage emailMessage)
        {
            McBody emailBody = McBody.QueryById<McBody> (emailMessage.BodyId);

            switch (whatType) {
            case QRTypeEnum.Compose:
                emailMessage.Subject = selectedResponse.subject;
                emailBody.UpdateData (selectedResponse.body);
                break;
            case QRTypeEnum.Reply:
                emailBody.UpdateData (selectedResponse.body + emailBody.GetContentsString ());
                break;
            case QRTypeEnum.Forward:
                emailBody.UpdateData (selectedResponse.body + emailBody.GetContentsString ());
                break;
            default:
                break;
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
