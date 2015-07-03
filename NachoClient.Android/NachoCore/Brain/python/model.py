import os
from sqlalchemy import *
from sqlalchemy.orm import sessionmaker


class Model:
    db_path = None
    engine = None
    metadata = None
    email_messages = list()

    @classmethod
    def load(cls, db_path):
        if not os.path.exists(db_path):
            raise IOError('%s does not exist' % db_path)
        cls.db_path = db_path
        cls.engine = create_engine('sqlite:///' + os.path.realpath(cls.db_path))
        cls.metadata = MetaData(bind=cls.engine)

        # Load all emails. Note that we import after Model.metadata is initialized. This is
        # because I am using SQLAlchemy autoload feature that automatically import schema from
        # db file. This is very slick as it nicely sidesteps all db schema migration issues as long
        # as fields we use are not removed.
        import model_emailmessage
        session = sessionmaker(bind=cls.engine)()
        query = session.query(model_emailmessage.McEmailMessage)
        cls.email_messages = query.all()
