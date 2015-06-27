#!/usr/bin/env python

import sys
from model import Model
from analyzer_relation import AnalyzerRelation
from evaluator import Evaluator
import pprint


def get_analyzer(name):
    if name == 'bayes1':
        analyzer = AnalyzerRelation()
        analyzer.analyze_to = False
        analyzer.analyze_cc = False
    elif name == 'bayes3':
        analyzer = AnalyzerRelation()
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
