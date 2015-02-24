#!/usr/bin/env python
import sys
import datetime
from dateutil import parser


def db2datetime(value):
    milliseconds = value / 10000
    (days, milliseconds) = divmod(milliseconds, 86400 * 1000)
    date = datetime.date.fromordinal(days + 1)
    (hours, milliseconds) = divmod(milliseconds, 3600 * 1000)
    (minutes, milliseconds) = divmod(milliseconds, 60 * 1000)
    (seconds, milliseconds) = divmod(milliseconds, 1000)

    return date, datetime.time(hour=hours, minute=minutes, second=seconds, microsecond=milliseconds * 1000)

def toticks(dt):
    days = datetime.date.toordinal(dt.date()) - 1
    ticks = days * 86400
    ticks += dt.hour * 3600
    ticks += dt.minute * 60
    ticks += dt.second
    ticks = (ticks * 1000000) + (dt.microsecond - (dt.microsecond % 1000))
    return ticks * 10  # convert to ticks

def usage():
    print 'USAGE: db_timestamp.py [value]'
    exit(1)

if __name__ == '__main__':
    if len(sys.argv) != 2:
        usage()
    db_val = None
    try:
        db_val = int(sys.argv[1], 10)
        (date_val, time_val) = db2datetime(db_val)
        time_str = time_val.strftime('%H:%M:%S') + ('.%03d' % (time_val.microsecond/1000))
        print 'Generic: %s %s' % (date_val.strftime('%m-%d-%Y'), time_str)
        print 'ISO-8601 UTC: %sT%sZ' % (date_val.strftime('%Y-%m-%d'), time_str)
    except ValueError:
        try:
            dt = parser.parse(sys.argv[1])
            print "DynamoDB format: %s" % toticks(dt)
        except:
            usage()
