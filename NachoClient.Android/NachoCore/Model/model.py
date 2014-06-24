from sqlalchemy import Table
from sqlalchemy.ext.declarative import declarative_base
import model_db


Base = declarative_base()


class McContact(Base):
    __table__ = Table('McContact', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)


class McContactScoreSyncInfo(Base):
    __table__ = Table('McContactScoreSyncInfo', model_db.ModelDb.metadata,
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

    def reset_score_states(self):
        # Scorable states
        self.ScoreVersion = 0
        self.TimeVarianceType = 0
        self.TimeVarianceState = 0
        self.Score = 0.0
        self.NeedUpdate = 0
        # Statistics
        self.TimesRead = 0
        self.SecondsRead = 0
        # Contact glean state
        self.HasBeenGleaned = 0


class McEmailMessageDependency(Base):
    __table__ = Table('McEmailMessageDependency', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)


class McEmailMessageScoreSyncInfo(Base):
    __table__ = Table('McEmailMessageScoreSyncInfo', model_db.ModelDb.metadata,
                      autoload=True,
                      autoload_with=model_db.ModelDb.engine)