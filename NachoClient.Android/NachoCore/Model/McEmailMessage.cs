using System;
using System.Globalization;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class McEmailMessage : McItem
    {
        private const string CrLf = "\r\n";
        private const string ColonSpace = ": ";

        [Indexed]
        public int BodyId { set; get; }
        public string Summary {set; get; }
        public string Encoding { set; get; }
        [Indexed]
        public string From { set; get; }
        [Indexed]
        public string To { set; get; }
        public string Cc { set; get; }
        [Indexed]
        public string Subject { set; get; }
        public string ReplyTo { set; get; }
        public DateTime DateReceived { set; get; }
        public string DisplayTo { set; get; }
        [Indexed]
        public uint Importance { set; get; }
        [Indexed]
        public bool IsRead { set; get; }
        public string MessageClass { set; get; }

        public string ToMime (SQLiteConnection db) {
            string message = "";
            foreach (var propertyName in new [] {"From", "To", "Subject", "ReplyTo", "DisplayTo"}) {
                message = Append (message, propertyName);
            }
            string date = DateTime.UtcNow.ToString ("ddd, dd MMM yyyy HH:mm:ss K", DateTimeFormatInfo.InvariantInfo);
            message = message + CrLf + "Date" + ColonSpace + date;
            message = message + CrLf + CrLf + GetBody(db);
            return message;
        }

        private string Append(string message, string propertyName) {
            string propertyValue = (string)typeof(McEmailMessage).GetProperty (propertyName).GetValue (this);
            if (null == propertyValue) {
                return message;
            }
            if ("" == message) {
                return propertyName + ColonSpace + propertyValue;
            }
            return message + CrLf + propertyName + ColonSpace + propertyValue;
        }

        public string GetBody(SQLiteConnection db)
        {
            var body = db.Get<McBody> (BodyId);
            if (null == body) {
                return null;
            } else {
                return body.Body;
            }
        }

        public void DeleteBody(SQLiteConnection db)
        {
            if (0 != BodyId) {
                var body = new McBody ();
                body.Id = BodyId;
                db.Delete (body);
                BodyId = 0;
                db.Update (this);
            }
        }
    }

    public class McBody : McObject
    {
        public string Body { get; set; }
    }

}

