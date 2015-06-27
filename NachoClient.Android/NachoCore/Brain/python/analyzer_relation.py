from analyzer import Analyzer
from email_address import EmailAddress, EmailAddressTable


class RelationAnalyzer(Analyzer):
    def __init__(self):
        self._table = EmailAddressTable()
        # Default algorithm is bayes3
        self.analyze_to = True
        self.analyze_cc = True

    def _analyze_one(self, email_message):
        def update_address(email_address_stats):
            email_address_stats.num_received += 1
            if email_message.LastVerbExecuted in [1, 2]:
                email_address_stats.num_replied += 1
            elif email_message.IsRead:
                email_address_stats.num_read += 1

        for to_addr in EmailAddress.parse_address_string(email_message.To):
            if len(to_addr) > 60:
                continue
            email_address = self._table.add_or_get(to_addr)
            update_address(email_address.to_stats)

        for from_addr in EmailAddress.parse_address_string(email_message.From):
            if len(from_addr) > 60:
                continue
            email_address = self._table.add_or_get(from_addr)
            update_address(email_address.from_stats)

        for cc_addr in EmailAddress.parse_address_string(email_message.Cc):
            if len(cc_addr) > 60:
                continue
            email_address = self._table.add_or_get(cc_addr)
            update_address(email_address.cc_stats)

    def analyze(self, email_messages):
        if not isinstance(email_messages, list):
            self._analyze_one(email_messages)
        for email_message in email_messages:
            self._analyze_one(email_message)

    def _classify_one(self, email_message):
        top = 0.0
        bottom = 0.0

        email_addr = self._table.get(email_message.From)
        if email_addr is not None:
            top += email_addr.from_stats.num_read + email_addr.from_stats.num_replied
            bottom += email_addr.from_stats.num_received

        if self.analyze_to:
            to_addrs = EmailAddress.parse_address_string(email_message.To)
            for to_addr in to_addrs:
                email_addr = self._table.get(to_addr)
                if email_addr is not None:
                    top += email_addr.to_stats.num_read + email_addr.to_stats.num_replied
                    bottom += email_addr.to_stats.num_received

        if self.analyze_cc:
            cc_addrs = EmailAddress.parse_address_string(email_message.Cc)
            for cc_addr in cc_addrs:
                email_addr = self._table.get(cc_addr)
                if email_addr is not None:
                    top += email_addr.cc_stats.num_read + email_addr.cc_stats.num_replied
                    bottom += email_addr.cc_stats.num_received

        if bottom == 0:
            return 0.0

        return float(top)/float(bottom)

    def classify(self, email_messages):
        if not isinstance(email_messages, list):
            return self._classify_one(email_messages)
        scores = list()
        for email_message in email_messages:
            scores.append(self._classify_one(email_message))
        return scores
