#!/usr/bin/env python
import sys
import datetime


def db2datetime(value):
    milliseconds = value / 10000
    (days, milliseconds) = divmod(milliseconds, 86400 * 1000)
    date = datetime.date.fromordinal(days + 1)
    (hours, milliseconds) = divmod(milliseconds, 3600 * 1000)
    (minutes, milliseconds) = divmod(milliseconds, 60 * 1000)
    (seconds, milliseconds) = divmod(milliseconds, 1000)

    return date, datetime.time(hour=hours, minute=minutes, second=seconds, microsecond=milliseconds * 1000)


def usage():
    print 'USAGE: db_timestamp.py [value]'
    exit(1)

if __name__ == '__main__':
    if len(sys.argv) != 2:
        usage()
    try:
        value = int(sys.argv[1], 10)
    except ValueError:
        usage()
    (date_val, time_val) = db2datetime(value)
    time_str = time_val.strftime('%H:%M:%S') + ('.%03d' % (time_val.microsecond/1000))
    print 'Generic: %s %s' % (date_val.strftime('%m-%d-%Y'), time_str)
    print 'ISO-8601 UTC: %sT%sZ' % (date_val.strftime('%Y-%m-%d'), time_str)
