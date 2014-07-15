#!/usr/bin/env python

try:
    from sqlalchemy.orm import sessionmaker
except ImportError:
    sessionmaker = None  # to get rid of a PyCharm warning
    print 'ERROR: SQLAlchemy package is not found. Please install SQLAlchemy first by:'
    print 'sudo easy_install SQLAlchemy'
    exit(1)
import argparse
import os
import model_db


def reset_emails():
    if options.email_sync_info:
        # Clear all sync info if scoring states are
        for si in session.query(model.McEmailMessageScoreSyncInfo).all():
            print '[DELETE] email message score sync info %d' % si.Id
            session.delete(si)
        session.commit()

    for em in session.query(model.McEmailMessage).all():
        if options.email_gleaned:
            print '[RESET] email message %d glean state' % em.Id
            em.reset_glean_state()
        if options.email_states:
            print '[RESET] email message %d email states' % em.Id
            em.reset_score_states()
        else:
            if options.email_time_variance:
                print '[RESET] email message %d time variance' % em.Id
                em.reset_time_variance()
                em.NeedUpdate = True
            if options.email_statistics:
                print '[RESET] email message %d statistics' % em.Id
                em.reset_statistics()
                em.NeedUpdate = True
    session.commit()

    if options.email_states:
        for d in session.query(model.McEmailMessageDependency).all():
            print '[DELETE] email message dependency %d' % d.Id
            session.delete(d)
        session.commit()


def reset_contacts():
    # Clear all sync info
    if options.contact_sync_info:
        for si in session.query(model.McContactScoreSyncInfo).all():
            print '[DELETE] contact score sync info %d' % si.Id
            session.delete(si)
        session.commit()

    if options.contact:
        # Delete all contacts. Let gleaner to glean them
        for c in session.query(model.McContact).all():
            print '[DELETE] contact %d' % c.Id
            session.delete(c)
        for cs in session.query(model.McContactStringAttribute).all():
            print '[DELETE] contact string %d' % cs.Id
            session.delete(cs)
        session.commit()
    else:
        for c in session.query(model.McContact).all():
            if options.contact_states:
                print '[RESET] contact %d scoring states' % c.Id
                c.reset_score_states()
            else:
                if options.contact_statistics:
                    print '[RESET] contact %d statistics' % c.Id
                    c.reset_statistics()
                    c.NeedUpdate = True
        session.commit()


def parse_arguments():
    parser = argparse.ArgumentParser()
    parser.add_argument('--all', action='store_true',
                        help='Reset everything')
    parser.add_argument('--db-file', '-f', help='SQLite database file')
    email_group = parser.add_argument_group(title='McEmailMessage Options')
    email_group.add_argument('--email', action='store_true',
                             help='Reset all McEmailMessage scoring and glean states'
                                  ' [--email-gleaned + --email-states]')
    email_group.add_argument('--email-gleaned', action='store_true',
                             help='Reset gleaned state')
    email_group.add_argument('--email-id',
                             help='McEmailMessage id to process. If omitted, all McEmailMessages are processed')
    email_group.add_argument('--email-states', action='store_true',
                             help='Reset all McEmailMessage scoring states [include --email-sync-info,'
                                  ' --email-time-variance, --email-statistics]')
    email_group.add_argument('--email-statistics', action='store_true',
                             help='Reset McEmailMessage statistics. (Dangerous! Use with caution.)')
    email_group.add_argument('--email-sync-info', action='store_true',
                             help='Clear email sync info')
    email_group.add_argument('--email-time-variance', action='store_true',
                             help='Reset time variance states')

    contact_group = parser.add_argument_group(title='McContact Options')
    contact_group.add_argument('--contact', action='store_true',
                               help='Delete all McContact and McContactStringAttribute')
    contact_group.add_argument('--contact-id',
                               help='McContact id to process. If omitted, all McContacts are processed.')
    contact_group.add_argument('--contact-states', action='store_true',
                               help='Reset all McContact scoring states')
    contact_group.add_argument('--contact-statistics', action='store_true',
                               help='Reset all McContact statistics. (Dangerous! Use with caution.)')
    contact_group.add_argument('--contact-sync-info', action='store_true',
                               help='Clear contact sync info')

    options_ = parser.parse_args()

    if options_.all:
        options_.email = True
        options_.contact = True
    if options_.email:
        options_.email_gleaned = True
        options_.email_sync_info = True
        options_.email_states = True
        options_.email_time_variance = True
    if options_.contact:
        options_.contact_sync_info = True
    return options_

options = parse_arguments()
if not os.path.exists(options.db_file):
    print 'ERROR: %s does not exist.' % options.db_file
    exit(1)
model_db.ModelDb.initialize(options.db_file)
import model
session = sessionmaker(bind=model_db.ModelDb.engine)()
reset_emails()
reset_contacts()