#!/usr/bin/env python

import ConfigParser
import argparse
import os.path
from email_util import error, EmailServer, EmailAccount, Email, RcFile


class TestCase:
    def __init__(self, config, section):
        self.name = section
        self.config = config
        self.description = self.config.get(self.name, 'description')
        if not self.config.has_section(section):
            raise ValueError('Section %s not found' % section)

        self.subject = self._try_getstr('subject')
        self.plain_text = self._read_content('plaintext', 'Cannot read plain text file %s')
        self.text = self._read_content('text', 'Cannot read text file %s')
        self.html = self._read_content('html', 'Cannot read HTML file %s')
        self.to = self._try_getlist('to')
        self.cc = self._try_getlist('cc')
        self.attachments = self._try_getlist('attachments')

    def _try_getstr(self, field):
        if not self.config.has_option(self.name, field):
            return None
        return self.config.get(self.name, field)

    def _try_getlist(self, field):
        if not self.config.has_option(self.name, field):
            return list()
        return [x.strip() for x in self.config.get(self.name, field).split(',')]

    def _read_content(self, field, err_mesg):
        path = self._try_getstr(field)
        if not path:
            return None
        if not os.path.exists(path):
            error(err_mesg % path)
        with open(path, 'r') as f:
            return f.read()

    def run(self, server, account):
        print 'Sending "%s"' % self.name
        assert isinstance(server, EmailServer)
        assert isinstance(account, EmailAccount)
        email_ = Email()
        email_.from_address = account.username
        if self.text:
            email_.text = self.text
        else:
            if self.plain_text:
                email_.plain = self.plain_text
            if self.html:
                email_.html = self.html
        if self.attachments:
            email_.attachments = self.attachments
        if self.to:
            email_.to_addresses = self.to
        else:
            # Default to send to oneself
            email_.to_addresses = [account.username]
        if self.cc:
            # TODO - Add cc support
            pass
        if self.subject:
            email_.subject = self.subject
        email_.send(server)


class TestCasesFile:
    def __init__(self, path):
        self.testcases = list()

        cfg = ConfigParser.SafeConfigParser()
        cfg.read(path)
        for section in cfg.sections():
            if section == 'Globals':
                continue
            self.testcases.append(TestCase(cfg, section))

    def summary(self):
        return [{'name': x.name, 'description': x.description} for x in self.testcases]

    def num_testcases(self):
        return len(self.testcases)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--account', '-A', help='Email account configuration file')
    parser.add_argument('--server', '-S', help='Email server configuration file')
    parser.add_argument('--username', '-u', help='Email account username')
    parser.add_argument('--list', '-l', help='List all test cases', action='store_true')
    parser.add_argument('testcases', type=int, nargs='*', help='List of test case indices')
    options = parser.parse_args()

    # Read test cases
    testcases_config = TestCasesFile('emails.cfg')

    # If --list, just print the test cases and quit
    if options.list:
        n = 1
        for tc in testcases_config.summary():
            print '%d: %s - %s' % (n, tc['name'], tc['description'])
            n += 1
        exit(0)

    # Use rc file to fill in missing parameters
    rcfile = RcFile(os.path.expanduser('~/.email_tests_rc'))
    if not options.username:
        options.username = rcfile.get('username')
    if not options.server:
        options.server = rcfile.get('server_config')

    # Set up account
    if not options.server:
        error('No server configuration')
    account = None
    if options.account:
        account = EmailAccount.init_from_config(options.account)
    elif options.username:
        account = EmailAccount(username=options.account)
    else:
        error('No account configuration or username')

    # Run selected test cases
    for tc_index in options.testcases:
        if not (1 <= tc_index <= testcases_config.num_testcases()):
            print 'WARN: Index %d is out of range (%d-%d)' % (tc_index, 1, testcases_config.num_testcases())
            continue
        server = EmailServer(server_cfg=options.server, account=account)
        testcases_config.testcases[tc_index-1].run(server, account)


if __name__ == '__main__':
    main()