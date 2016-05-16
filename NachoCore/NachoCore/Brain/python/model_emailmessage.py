from sqlalchemy import *
from sqlalchemy.ext.declarative import declarative_base
from model import Model

Base = declarative_base()


def querable(function):
    function.is_querable = True
    return function


class McEmailMessage(Base):
    __table__ = Table('McEmailMessage', Model.metadata,
                      autoload=True,
                      autoload_with=Model.engine)

    @staticmethod
    def is_none_or_empty(s):
        return (s is not None) and (len(s) > 0)

    @querable
    def is_read(self):
        return self.IsRead

    @querable
    def has_to_addresses(self):
        return McEmailMessage.is_none_or_empty(self.To)

    @querable
    def has_cc_addresses(self):
        return McEmailMessage.is_none_or_empty(self.Cc)

    @querable
    def has_subject(self):
        return McEmailMessage.is_none_or_empty(self.Subject)
