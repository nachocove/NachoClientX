using System;
using System.Globalization;
using SQLite;
using NachoCore.Utils;

namespace NachoCore.Model
{
    public class NcEmailMessage : NcItem
    {
        private const string CrLf = "\r\n";
        private const string ColonSpace = ": ";

        public const string ClassName = "NcEmailMessage";

        [Indexed]
        public bool IsAwatingSend { get; set; }
        public string Body { set; get; }
        public string Encoding { set; get; }
        [Indexed]
        public string From { set; get; }
        [Indexed]
        public string To { set; get; }
        [Indexed]
        public string Subject { set; get; }
        public string ReplyTo { set; get; }
        public DateTime DateReceived { set; get; }
        public string DisplayTo { set; get; }
        [Indexed]
        public uint Importance { set; get; }
        [Indexed]
        public bool Read { set; get; }
        public string MessageClass { set; get; }

        public string ToMime () {
            string message = "";
            foreach (var propertyName in new [] {"From", "To", "Subject", "ReplyTo", "DisplayTo"}) {
                message = Append (message, propertyName);
            }
            string date = DateTime.UtcNow.ToString ("ddd, dd MMM yyyy HH:mm:ss K", DateTimeFormatInfo.InvariantInfo);
            message = message + CrLf + "Date" + ColonSpace + date;
            message = message + CrLf + CrLf + Body;
            return message;
        }

        private string Append(string message, string propertyName) {
            string propertyValue = (string)typeof(NcEmailMessage).GetProperty (propertyName).GetValue (this);
            if (null == propertyValue) {
                return message;
            }
            if ("" == message) {
                return propertyName + ColonSpace + propertyValue;
            }
            return message + CrLf + propertyName + ColonSpace + propertyValue;
        }
    }
}

