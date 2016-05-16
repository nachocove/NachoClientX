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
    # Select either all objects or the ones related to a given email id
    sync_info_query = session.query(model.McEmailMessageScoreSyncInfo)
    email_query = session.query(model.McEmailMessage)
    dep_query = session.query(model.McEmailMessageDependency)
    if options.email_id is None:
        si_objects = sync_info_query.all()
        em_objects = email_query.all()
        d_objects = dep_query.all()
    else:
        email_id = int(options.email_id)
        si_objects = sync_info_query.filter(model.McEmailMessageScoreSyncInfo.EmailMessageId == email_id)
        em_objects = email_query.filter(model.McEmailMessage.Id == email_id)
        d_objects = dep_query.filter(model.McEmailMessageDependency.EmailMessageId == email_id)

    # Reset McEmailMessageScoreSyncInfo
    if options.email_sync_info:
        for si in si_objects:
            print '[DELETE] email message score sync info %d' % si.Id
            session.delete(si)
        session.commit()

    # Reset McEmailMessage
    for em in em_objects:
        if options.email_update:
            print '[SET] email message %d update' % em.Id
            em.set_needupdate()
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

    # Reset McEmailMessageDependency
    if options.email_states:
        for d in d_objects:
            print '[DELETE] email message dependency %d' % d.Id
            session.delete(d)
        session.commit()


def reset_addresses():
    # Select either all objects or the ones related to a given address id
    sync_info_query = session.query(model.McEmailAddressScoreSyncInfo)
    address_query = session.query(model.McEmailAddress)
    if options.address_id is None:
        si_objects = sync_info_query.all()
        a_objects = address_query.all()
    else:
        address_id = int(options.address_id)
        si_objects = sync_info_query.filter(model.McEmailAddressScoreSyncInfo.EmailMessageId == address_id)
        a_objects = sync_info_query.filter(model.McEmailAddress.Id == address_id)

    # Reset McEmailAddressScoreSyncInfo
    if options.address_sync_info:
        for si in si_objects:
            print '[DELETE] address score sync info %d' % si.Id
            session.delete(si)
        session.commit()

    # Reset McContact
    if options.address:
        # Delete all addresses. Let gleaner to glean them
        for a in a_objects:
            print '[DELETE] email address %d' % a.Id
            session.delete(a)
        session.commit()
    else:
        for a in a_objects:
            if options.address_update:
                print '[SET] email address %d update' % a.Id
                a.set_needupdate()
            if options.address_states:
                print '[RESET] email address %d scoring states' % a.Id
                a.reset_score_states()
            else:
                if options.address_statistics:
                    print '[RESET] email address` %d statistics' % a.Id
                    a.reset_statistics()
                    a.NeedUpdate = True
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
    email_group.add_argument('--email-update', action='store_true',
                             help='Set McEmailMessage.NeedUpdate (to 1).'
                                  ' Use this option to force a re-scoring of emails')

    address_group = parser.add_argument_group(title='McEmailAddress Options')
    address_group.add_argument('--address', action='store_true',
                               help='Delete all McEmailAddress.')
    address_group.add_argument('--address-id',
                               help='McEmailAddress id to process. If omitted, all McEmailAddresses are processed.')
    address_group.add_argument('--address-states', action='store_true',
                               help='Reset all McEmailAddress scoring states')
    address_group.add_argument('--address-statistics', action='store_true',
                               help='Reset all McEmailAddress statistics. (Dangerous! Use with caution.)')
    address_group.add_argument('--address-sync-info', action='store_true',
                               help='Clear address sync info')
    address_group.add_argument('--address-update', action='store_true',
                               help='Set McEmailAddress.NeedUpdate (to 1).'
                                    ' Use this option to force a re-scoring of addresses')

    options_ = parser.parse_args()

    if options_.all:
        options_.email = True
        options_.address = True
    if options_.email:
        options_.email_gleaned = True
        options_.email_sync_info = True
        options_.email_states = True
        options_.email_time_variance = True
    if options_.address:
        options_.address_sync_info = True
    return options_

options = parse_arguments()
if not os.path.exists(options.db_file):
    print 'ERROR: %s does not exist.' % options.db_file
    exit(1)
model_db.ModelDb.initialize(options.db_file)
import model
session = sessionmaker(bind=model_db.ModelDb.engine)()
reset_emails()
reset_addresses()