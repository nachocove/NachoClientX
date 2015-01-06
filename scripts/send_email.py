# Copyright 2014, NachoCove, Inc
import argparse
from getpass import getpass
from random import randint
import smtplib
import sys


try:
    import loremipsum
except ImportError:
    print "ERROR: Need to have lorem ipsum installed. try 'sudo pip install loremipsum'"
    exit(1)

message_template = """From: From Person <from@fromdomain.com>
To: %(fromAddr)s <%(fromAddr)s>
Subject: %(subject)s

%(body)s
"""

def send_email(fromAddr, toAddrs, server, count):
    tls = server.get('tls', False)
    smtp_svr = smtplib.SMTP()
    smtp_svr.connect(server['host'], server['port'])
    smtp_svr.starttls()
    smtp_svr.login(server['username'], server['password'])
    for i in xrange(count):
        subject = loremipsum.get_sentence()
        lorem_generator = loremipsum.generate_paragraphs(randint(2, 10))
        body = "\n".join([x[2] for x in lorem_generator])
        message =  message_template % {'fromAddr': fromAddr, 'subject': subject, 'body': body}
        smtp_svr.sendmail(fromAddr, toAddrs, message)
    smtp_svr.quit()

if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    default_server = 'mail.d2.officeburrito.com'
    default_port = 25
    default_count = 5
    parser.add_argument('--hostname', help='Email server (default=%s)' % default_server, default=default_server)
    parser.add_argument('--port', help='Email server port (default %d)' % default_port, default=default_port, type=int)
    parser.add_argument('--count', '-c', help='How many messages to send (default=%d)' % default_count, default=default_count, type=int)
    parser.add_argument('--username', '-u', help='Email server username for auth')
    parser.add_argument('--password', '-p', help='Email server password for auth')
    parser.add_argument('--no-starttls', action='store_true', default=False)
    parser.add_argument('fromAddr', help='Recipient', action='append', default=[])
    parser.add_argument('recipient', help='Recipient', action='append', default=[])
    args = parser.parse_args()

    progname = sys.argv.pop(0)
    fromAddr = sys.argv.pop(0)

    if not args.username:
        args.username = raw_input("Username: ")
    if not args.password:
        args.password = getpass()

    server = {'host': args.hostname,
              'port': int(args.port),
              'tls': False if args.no_starttls else True,
              'username': args.username,
              'password': args.password}


    send_email(args.fromAddr, args.recipient, server, args.count)