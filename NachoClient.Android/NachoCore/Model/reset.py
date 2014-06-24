try:
    from sqlalchemy.orm import sessionmaker
except ImportError:
    print 'ERROR: SQLAlchemy package is not found. Please install SQLAlchemy first by:'
    print 'sudo easy_install SQLAlchemy'
    exit(1)
import sys
import os
import model_db
path = sys.argv[1]
if not os.path.exists(path):
    print 'ERROR: %s does not exist.' % path
    exit(1)
model_db.ModelDb.initialize(path)
from model import *


def reset():
    Session = sessionmaker(bind=model_db.ModelDb.engine)
    session = Session()

    # Reset all email messages
    for em in session.query(McEmailMessage).all():
        print '[RESET] email message %d' % em.Id
        em.reset_score_states()
    session.commit()

    # Clear all sync info
    for si in session.query(McContactScoreSyncInfo).all():
        print '[DELETE] contact score sync info %d' % si.Id
        session.delete(si)
    for si in session.query(McEmailMessageScoreSyncInfo).all():
        print '[DELETE] email message score sync info %d' % si.Id
    session.commit()

    # Clear all dependencies
    for d in session.query(McEmailMessageDependency).all():
        print '[DELETE] email message dependency %d' % d.Id
        session.delete(d)
    session.commit()

    # Clear all contacts. Let gleaner to glean them
    for c in session.query(McContact).all():
        print '[DELETE] contact %d' % c.Id
        session.delete(c)
    session.commit()

if __name__ == '__main__':
    reset()