from sqlalchemy import Table
from sqlalchemy.ext.declarative import declarative_base
import model_db


Base = declarative_base()


class McEmailAddress(Base):
    __table__ = Table('McEmailAddress', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)

    def reset_time_variance(self):
        self.TimeVarianceType = 0
        self.TimeVarianceState = 0

    def reset_statistics(self):
        self.EmailsReceived = 0
        self.EmailsRead = 0
        self.EmailsReplied = 0
        self.EmailsArchived = 0
        self.EmailsDeleted = 0

    def reset_score_states(self):
        # Scorable states
        self.ScoreVersion = 0
        self.Score = 0.0
        self.NeedUpdate = 0
        self.reset_time_variance()
        self.reset_statistics()

    def set_needupdate(self):
        self.NeedUpdate = 1


class McEmailAddressScoreSyncInfo(Base):
    __table__ = Table('McEmailAddressScoreSyncInfo', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)


class McContactStringAttribute(Base):
    __table__ = Table('McContactStringAttribute', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)


class McEmailMessage(Base):
    __table__ = Table('McEmailMessage', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)

    def reset_time_variance(self):
        self.TimeVarianceType = 0
        self.TimeVarianceState = 0

    def reset_statistics(self):
        self.TimesRead = 0
        self.SecondsRead = 0

    def reset_score_states(self):
        # Scorable states
        self.ScoreVersion = 0
        self.Score = 0.0
        self.NeedUpdate = 0
        self.reset_time_variance()
        self.reset_statistics()

    def reset_glean_state(self):
        self.HasBeenGleaned = 0

    def set_needupdate(self):
        self.NeedUpdate = 1


class McEmailMessageDependency(Base):
    __table__ = Table('McEmailMessageDependency', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)


class McEmailMessageScoreSyncInfo(Base):
    __table__ = Table('McEmailMessageScoreSyncInfo', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)