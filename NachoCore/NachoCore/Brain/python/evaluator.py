class EvaluatorResult:
    def __init__(self):
        self.total = 0
        self.read = 0
        self.unread = 0

        self.hot = 0
        self.not_hot = 0
        self.misses = 0
        self.false_alarms = 0

        self.miss_indices = list()
        self.false_alarm_indices = list()

        self.miss_rate = 0.0
        self.false_alarms_rate = 0.0
        self.error_rate = 0.0
        self.hot_error_rate = 0.0
        self.not_hot_error_rate = 0.0

    def update_rate(self):
        def rate(top, bottom):
            if bottom == 0:
                return 0.0
            return float(top)/float(bottom)

        self.miss_rate = rate(self.misses, self.total)
        self.false_alarms_rate = rate(self.false_alarms, self.total)
        self.hot_error_rate = rate(self.false_alarms, self.hot)
        self.not_hot_error_rate = rate(self.misses, self.not_hot)
        self.error_rate = rate(self.misses + self.false_alarms, self.total)

    def summary(self):
        out = 'total: %d\n' % self.total
        out += 'read: %d\n' % self.read
        out += 'unread: %d\n\n' % self.unread

        out += 'hot: %d\n' % self.hot
        out += 'not hot: %d\n' % self.not_hot
        out += 'misses: %d\n' % self.misses
        out += 'false alarms: %d\n\n' % self.false_alarms

        out += 'error rate: %.3f%%\n' % (self.error_rate * 100.0)
        out += 'miss rate: %.3f%%\n' % (self.miss_rate * 100.0)
        out += 'false alarm rate: %.3f%%\n' % (self.false_alarms_rate * 100.0)
        out += 'hot error rate: %.3f%%\n' % (self.hot_error_rate * 100.0)
        out += 'not-hot error rate: %.3f%%\n' % (self.not_hot_error_rate * 100.0)

        return out


class Evaluator:
    """
    This takes scores from an algorithm for a set of email message. It returns a dictionary of information
    """
    def __init__(self):
        self.hot_threshold = 0.5

    def evaluate(self, email_messages, scores):
        results = EvaluatorResult()

        score_len = len(scores)
        assert len(email_messages) == score_len

        for n in range(score_len):
            email_message = email_messages[n]
            score = scores[n]

            results.total += 1
            if email_message.IsRead:
                results.read += 1
            else:
                results.unread += 1

            if score >= self.hot_threshold:
                results.hot += 1
                if not email_message.IsRead:
                    results.false_alarms += 1
                    results.false_alarm_indices.append(n)
            else:
                results.not_hot += 1
                if email_message.IsRead:
                    results.misses += 1
                    results.miss_indices.append(n)

        results.update_rate()
        return results
