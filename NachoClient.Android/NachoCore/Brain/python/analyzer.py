from abc import ABCMeta, abstractmethod


class Analyzer(object):
    __metaclass__ = ABCMeta
    """
    Template for all algorithms that analyze objects
    """
    @abstractmethod
    def analyze(self, objects):
        pass

    @abstractmethod
    def classify(self, objects):
        pass
