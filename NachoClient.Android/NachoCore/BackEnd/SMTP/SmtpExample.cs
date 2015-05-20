//  Copyright (C) 2015 Nacho Cove, Inc. All rights reserved.
//
using System;
using System.Linq;
using System.Text;
using MailKit.Net.Smtp;
using MimeKit;
using MailKit;
using NachoCore.Utils;
using System.Security.Cryptography.X509Certificates;

namespace NachoCore.SMTP
{
    public class Smtp
    {
        private int AccountId { get; set; }

        private string m_hostname { get; set; }

        private int m_port { get; set; }

        private bool m_useSsl { get; set; }

        private string m_username { get; set; }

        private string m_password { get; set; }

        private SmtpClient m_smtpClient;

        private SmtpClient client {
            get {
                if (null == m_smtpClient) {
                    Log.Error (Log.LOG_SMTP, "no smtpClient set in getter");
                    GetAuthenticatedClient ();
                }
                return m_smtpClient;
            }
        }
        public Smtp (string hostname, int port, bool useSsl, string username, string password)
        {
            m_hostname = hostname;
            m_port = port;
            m_useSsl = useSsl;
            m_username = username;
            m_password = password;
            AccountId = 1;
            DoSmtp ();
        }

        public void DoSmtp()
        {
            GetAuthenticatedClient ();

            var message = new MimeMessage ();
            message.From.Add (new MailboxAddress ("Jan Vilhuber", "jan.vilhuber@gmail.com"));
            message.To.Add (new MailboxAddress ("Nacho Jan", "janv@nachocove.com"));
            message.Subject = "How you doin'?";
            message.Body = new TextPart ("plain") {
                Text = @"Hey Jan"
            };

            SendMessage (message);

            client.Disconnect (true);
        }

        public void SendMessage(MimeMessage message) {
            client.Send (message);
        }

        private void GetAuthenticatedClient ()
        {
            if (null == m_smtpClient) {
                SmtpProtocolLogger logger = new SmtpProtocolLogger ();
                m_smtpClient = new SmtpClient (logger);
                m_smtpClient.ClientCertificates = new X509CertificateCollection ();
                m_smtpClient.Connect (m_hostname, m_port, m_useSsl);

                // Note: since we don't have an OAuth2 token, disable
                // the XOAUTH2 authentication mechanism.
                m_smtpClient.AuthenticationMechanisms.Remove ("XOAUTH2");

                m_smtpClient.Authenticate (m_username, m_password);
            }
        }

        public class SmtpProtocolLogger : IProtocolLogger
        {
            public void LogConnect (Uri uri)
            {
                if (uri == null)
                    throw new ArgumentNullException ("uri");

                Log.Info (Log.LOG_SMTP, "Connected to {0}", uri);
            }

            private void logBuffer (string prefix, byte[] buffer, int offset, int count)
            {
                char[] delimiterChars = { '\n' };
                var lines = Encoding.UTF8.GetString (buffer.Skip (offset).Take (count).ToArray ()).Split (delimiterChars);

                Array.ForEach (lines, (line) => {
                    if (line.Length > 0) {
                        Log.Info (Log.LOG_SMTP, "{0}{1}", prefix, line);
                    }
                });
            }

            public void LogClient (byte[] buffer, int offset, int count)
            {
                logBuffer ("SMTP C: ", buffer, offset, count);
            }

            public void LogServer (byte[] buffer, int offset, int count)
            {
                logBuffer ("SMTP S: ", buffer, offset, count);
            }

            public void Dispose ()
            {
            }
        }
    }
}

