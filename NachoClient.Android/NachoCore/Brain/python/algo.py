#!/usr/bin/env python

import sys
from model import Model
from analyzer_relation import RelationAnalyzer
from analyzer_content import ContentAnalyzer
from analyzer_combined import LinearCombinedAnalyzer, ProductCombinedAnalyzer, MaxCombinedAnalyzer
from evaluator import Evaluator


def get_analyzer(name):
    if name == 'bayes1':
        analyzer = RelationAnalyzer()
        analyzer.analyze_to = False
        analyzer.analyze_cc = False
    elif name == 'bayes3':
        analyzer = RelationAnalyzer()
    elif name == 'logistic':
        analyzer = ContentAnalyzer()
    elif name == 'linear':
        analyzer = LinearCombinedAnalyzer()
    elif name == 'product':
        analyzer = ProductCombinedAnalyzer()
    elif name == 'max':
        analyzer = MaxCombinedAnalyzer()
    else:
        raise ValueError('unknown analyzer type %s' % name)
    return analyzer


def main():
    Model.load(sys.argv[1])

    for name in sys.argv[2:]:
        print '-' * 10, name, '-' * 10
        analyzer = get_analyzer(name)
        analyzer.analyze(Model.email_messages)
        scores = analyzer.classify(Model.email_messages)
        evaluator = Evaluator()
        results = evaluator.evaluate(Model.email_messages, scores)
        print results.summary()


if __name__ == '__main__':
    main()
