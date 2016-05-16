from analyzer import Analyzer
from analyzer_relation import RelationAnalyzer
from analyzer_content import ContentAnalyzer


class CombinedAnalyzer(Analyzer):
    def __init__(self):
        self.relation_analyzer = RelationAnalyzer()
        self.content_analyzer = ContentAnalyzer()
        self.combine = None

    def analyze(self, email_messages):
        self.relation_analyzer.analyze(email_messages)
        self.content_analyzer.analyze(email_messages)

    def classify(self, email_messages):
        relation_scores = self.relation_analyzer.classify(email_messages)
        content_scores = self.content_analyzer.classify(email_messages)
        score_len = len(relation_scores)
        assert score_len == len(content_scores)
        scores = list()
        for n in range(score_len):
            p = relation_scores[n]
            q = content_scores[n]
            scores.append(self.combine(p, q))
        return scores


class LinearCombinedAnalyzer(CombinedAnalyzer):
    def __init__(self):
        super(LinearCombinedAnalyzer, self).__init__()
        self.combine = LinearCombinedAnalyzer.combine_func

    @staticmethod
    def combine_func(p, q):
        return 0.5*p + 0.5*q


class ProductCombinedAnalyzer(CombinedAnalyzer):
    def __init__(self):
        super(ProductCombinedAnalyzer, self).__init__()
        self.combine = ProductCombinedAnalyzer.combine_func

    @staticmethod
    def combine_func(p, q):
        return p+q-(p*q)


class MaxCombinedAnalyzer(CombinedAnalyzer):
    def __init__(self):
        super(MaxCombinedAnalyzer, self).__init__()
        self.combine = MaxCombinedAnalyzer.combine_func

    @staticmethod
    def combine_func(p, q):
        return max(p, q)
