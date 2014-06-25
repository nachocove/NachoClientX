import os
from sqlalchemy import *


class ModelDb:
    db_path = None
    engine = None
    metadata = None

    @classmethod
    def initialize(cls, db_path):
        cls.db_path = db_path
        cls.engine = create_engine('sqlite:///' + os.path.realpath(cls.db_path))
        cls.metadata = MetaData(bind=cls.engine)
