from analyzer import Analyzer
import numpy
from sklearn import linear_model
from sklearn.feature_extraction.text import CountVectorizer


class ContentAnalyzer(Analyzer):
    def __init__(self):
        self.vectorizer = CountVectorizer(min_df=5)
        self.logreg = linear_model.LogisticRegression(C=1e5)

    @staticmethod
    def is_none_or_empty(s):
        return (s is None) or (len(s) == 0)

    def analyze(self, email_messages):
        subjects = list()
        is_read = list()
        for email_message in email_messages:
            if ContentAnalyzer.is_none_or_empty(email_message.Subject):
                continue
            subjects.append(email_message.Subject)
            is_read.append(email_message.IsRead)

        x = self.vectorizer.fit_transform(subjects)
        y = numpy.array(is_read)

        self.logreg.fit(x, y)

    def classify(self, email_messages):
        scores = list()
        for email_message in email_messages:
            if ContentAnalyzer.is_none_or_empty(email_message.Subject):
                scores.append(0.5)
                continue
            x = self.vectorizer.transform([email_message.Subject])
            y_est = self.logreg.predict_proba(x)
            scores.append(y_est[0, 1])
        return scores
