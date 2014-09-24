#!/usr/bin/env python
import argparse
import getpass
import ConfigParser
import smtplib
import email.mime.base
import email.mime.text
import email.mime.multipart
from email import encoders
import os
import os.path


class EmailAccount:
    def __init__(self, username, password=None):
        self.username = username
        if password is None:
            self.password = getpass.getpass()
        else:
            self.password = password

    @staticmethod
    def init_from_config(config):
        cfg = ConfigParser.ConfigParser()
        cfg.read(config)
        section = 'Account'
        return EmailAccount(username=cfg.get(section, 'username'),
                            password=cfg.get(section, 'password'))


class EmailServer:
    def __init__(self, server_cfg, account):
        self.account = account
        cfg = ConfigParser.ConfigParser()
        cfg.read(server_cfg)
        section = 'Server'
        self.server = cfg.get(section, 'host')
        self.port = cfg.get(section, 'port')
        self.start_tls = cfg.getboolean(section, 'start_tls')
        self.tls = cfg.getboolean(section, 'tls')

    def send(self, from_address, to_addresses, email_):
        if self.tls:
            smtp_svr = smtplib.SMTP_SSL()
        else:
            smtp_svr = smtplib.SMTP()
        smtp_svr.connect(self.server, self.port)
        if self.start_tls:
            smtp_svr.starttls()
        smtp_svr.login(self.account.username, self.account.password)
        smtp_svr.sendmail(from_address, to_addresses, email_)
        smtp_svr.quit()


class Email:
    def __init__(self):
        self.subject = ''
        self.text = None
        self.plain = None
        self.html = None
        self.from_address = None
        self.to_addresses = []
        # A list of file paths of attachments
        self.attachments = []

    def to_addresses_str(self):
        return ','.join(self.to_addresses)

    def send(self, server):
        plain = None
        html = None
        if self.text is not None:
            # This is not MIME-encoded plain text. It is just unencoded text. Note that no
            # attachment or other part will be sent.
            if not self.subject:
                subject_line = '\n'
            else:
                subject_line = 'Subject: ' + self.subject + '\n\n'
            server.send(self.from_address, self.to_addresses_str(), subject_line + self.text)
            return
        if self.plain is not None:
            plain = email.mime.text.MIMEText(self.plain, 'plain')
        if self.html is not None:
            html = email.mime.text.MIMEText(self.html, 'html')

        email_ = None
        if plain is not None and html is not None:
            # HTML email with plain text fallback
            email_ = email.mime.multipart.MIMEMultipart('alternative')
            email_.attach(plain)
            email_.attach(html)
        elif plain is not None:
            email_ = plain
        elif html is not None:
            email_ = html
        else:
            assert 'Does not have neither plain text nor HTML email message!'

        if len(self.attachments) > 0:
            outer_email = email.mime.multipart.MIMEMultipart()
            outer_email.attach(email_)

            def encode_attachment(path):
                print '  Attaching %s...' % path
                part = email.mime.base.MIMEBase('application', "octet-stream")
                part.set_payload(open(path, "rb").read())
                encoders.encode_base64(part)
                part.add_header('Content-Disposition', 'attachment; filename="%s"' % os.path.basename(path))
                return part

            for attachment in self.attachments:
                outer_email.attach(encode_attachment(attachment))
            email_ = outer_email

        email_['From'] = self.from_address
        email_['To'] = self.to_addresses_str()
        email_['Subject'] = self.subject

        server.send(self.from_address, self.to_addresses_str(), email_.as_string())


class RcFile:
    SECTION = 'Config'

    def __init__(self, path):
        self.config = ConfigParser.ConfigParser()
        self.config.read(path)

    def get(self, key, default_value=None):
        if not self.config.has_section(RcFile.SECTION):
            return default_value
        if not self.config.has_option(RcFile.SECTION, key):
            return default_value
        return self.config.get(RcFile.SECTION, key)


def error(msg):
    print 'ERROR: ' + msg
    exit(1)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--account', '-A', help='Email account configuration file')
    parser.add_argument('--attachment', '-a', help='Email attachments', action='append', default=[])
    parser.add_argument('--from', '-f', dest='from_', help='From address')
    parser.add_argument('--html', '-H', help='HTML file')
    parser.add_argument('--password', '-P', help='Email account password')
    parser.add_argument('--server', '-S', help='Email server configuration file')
    parser.add_argument('--subject', '-s', help='Email subject')
    parser.add_argument('--username', '-u', help='Email account username')
    parser.add_argument('--to', '-T', help='Email recipient', action='append', default=[])
    parser.add_argument('--text', '-t', help='Text file to be non-MIME encoded as text')
    parser.add_argument('--plain-text', '-p', help='Text file to be MIME encoded as plain-text')

    options = parser.parse_args()

    rcfile = RcFile(os.path.expanduser('~/.email_util_rc'))

    # Set up the server
    if not options.server:
        server_config = rcfile.get('server_config')
        if server_config is None:
            error('No server configuration')
        else:
            options.server = server_config
    elif not os.path.exists(options.server):
        error('Cannot file server configuration file %s' % options.server)

    # Set up the account
    account = None
    if options.account:
        if not os.path.exists(options.account):
            error('Cannot find account configuration file %s' % options.account)
        account = EmailAccount.init_from_config(options.account)
    elif options.username:
        account = EmailAccount(username=options.username)
    else:
        username = rcfile.get('username')
        if not username:
            error('No account configuration or username')
        else:
            account = EmailAccount(username=username)

    # Make sure the text / HTML / attachment files are there
    for attachment in options.attachment:
        if not os.path.exists(attachment):
            error('Cannot find attachment %s' % attachment)

    # Create the email object
    email_ = Email()

    def try_read(path, err_mesg):
        if not os.path.exists(path):
            error(err_mesg % path)
        with open(path, 'r') as f:
            return f.read()

    if options.text:
        email_.text = try_read(options.text, 'Cannot find text file %s')
    if options.plain_text:
        email_.plain = try_read(options.plain_text, 'Cannot find plain text file %s')
    if options.html:
        email_.html = try_read(options.html, 'Cannot find HTML file %s')
    email_.subject = options.subject
    if options.from_ is None:
        email_.from_address = account.username
    else:
        email_.from_address = options.from_
    if not options.to:
        email_.to_addresses = [account.username]
    else:
        email_.to_addresses = options.to

    # Create the server object
    server = EmailServer(server_cfg=options.server, account=account)

    # Send it
    email_.send(server)

if __name__ == '__main__':
    main()
