import email.utils


class EmailAddressStatistics:
    def __init__(self):
        self.num_received = 0
        self.num_read = 0
        self.num_replied = 0

    def score(self):
        if self.num_received == 0:
            return 0.0
        return float(self.num_read + self.num_replied) / float(self.num_received)

    def __repr__(self):
        return '# replied: %d\n# read: %d\n# replied: %d\n' % (self.num_received, self.num_read, self.num_replied)

    def has_received(self):
        return self.num_received > 0

    def has_read(self):
        return self.num_read > 0

    def has_replied(self):
        return self.num_replied > 0


class EmailAddress:
    @staticmethod
    def get_canonical_address(address):
        return email.utils.parseaddr(address)[1]

    @staticmethod
    def parse_address_string(s):
        if s is None or len(s) == 0:
            return []
        return [x[1] for x in email.utils.getaddresses([s])]

    def __init__(self, address):
        self.original_address = address
        (self.name, self.canonical_address) = email.utils.parseaddr(address)
        self.from_stats = EmailAddressStatistics()
        self.to_stats = EmailAddressStatistics()
        self.cc_stats = EmailAddressStatistics()

    def bayes1_score(self):
        """
        'bayes1' refers to a 1-D Bayesian estimate (conditianal probability) of the email will be read given the
         sender statistics
        """
        return self.from_stats.score()

    def __repr__(self):
        msg = 'email address: %s\n' % self.original_address
        msg += 'canonical address: %s\n' % self.canonical_address
        msg += 'name: %s\n' % self.name
        stats = self.from_stats
        msg += 'from (received/read/replied): %d/%d/%d' % (stats.num_received, stats.num_read, stats.num_replied)
        stats = self.to_stats
        msg += 'to (received/read/replied): %d/%d/%d' % (stats.num_received, stats.num_read, stats.num_replied)
        stats = self.cc_stats
        msg += 'cc (received/read/replied): %d/%d/%d' % (stats.num_received, stats.num_read, stats.num_replied)
        msg += 'bayes1 score: %f' % self.bayes1_score()
        return msg


class EmailAddressTable:
    def __init__(self):
        self._table = dict()

    def count(self):
        return len(self._table)

    def count_from(self):
        return reduce(lambda x: x.from_stats.has_received() and 1 or 0, self._table.values(), 0)

    def count_to(self):
        return reduce(lambda x: x.to_stats.has_received() and 1 or 0, self._table.values(), 0)

    def count_cc(self):
        return reduce(lambda x: x.cc_stats.has_received() and 1 or 0, self._table.values(), 0)

    def add(self, address):
        canonical_address = EmailAddress.get_canonical_address(address)
        assert canonical_address not in self._table
        email_address = EmailAddress(address)
        self._table[canonical_address] = email_address
        return email_address

    def add_or_get(self, address):
        canonical_address = EmailAddress.get_canonical_address(address)
        email_address = self._table.get(canonical_address, None)
        if email_address is not None:
            return email_address
        return self.add(address)

    def get(self, address):
        canonical_address = EmailAddress.get_canonical_address(address)
        return self._table.get(canonical_address, None)

    def get_all(self):
        return self._table.values()

    def get_has_from(self):
        return filter(lambda x: x.has_from(), self.get_all())

    def get_has_to(self):
        return filter(lambda x: x.has_to(), self.get_all())

    def get_has_cc(self):
        return filter(lambda x: x.has_to(), self.get_all())

    def __repr__(self):
        msg = ''
        for canonical_address in sorted(self._table.keys()):
            email_address = self._table[canonical_address]
            from_ = email_address.from_stats
            to = email_address.to_stats
            cc = email_address.cc_stats
            msg += '%s: from=%d/%d/%d  to=%d/%d/%d   cc=%d/%d/%d' % \
                   (email_address.canonical_address, from_.num_received, from_.num_read, from_.num_replied,
                    to.num_received, to.num_read, to.num_replied,
                    cc.num_received, cc.num_read, cc.num_replied)
            return msg
