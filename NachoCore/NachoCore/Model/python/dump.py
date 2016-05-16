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
import sys
import cgi
import copy
import datetime
import string
from model_db import ModelDb
import sqlalchemy


class Formatter:
    """
    The base class for all formatters. Must implement format()
    """
    def __init__(self):
        pass

    def format(self, value):
        """
        Return a string representation of 'value'.
        """
        raise NotImplementedError()


class DoubleFormatter(Formatter):
    """
    Double formatter converts double-precision floating point values to
    a string with fixed width and fixed number of decimal places.
    """
    def __init__(self, width, decimal_places):
        Formatter.__init__(self)
        assert width > decimal_places
        self._format = '%%%d.%df' % (width, decimal_places)

    def format(self, value):
        return self._format % value


class BooleanFormatter(Formatter):
    """
    Boolean formatter converts 1 / 0 to either True / False or T / F.
    """
    def __init__(self, short_form=False):
        Formatter.__init__(self)
        self.short_form = short_form

    def format(self, value):
        if self.short_form:
            if value:
                return 'T'
            else:
                return 'F'
        else:
            if value:
                return 'True'
            else:
                return 'False'


class DateTimeFormatter(Formatter):
    def __init__(self):
        Formatter.__init__(self)

    def format(self, value):
        milliseconds = value / 10000
        (days, milliseconds) = divmod(milliseconds, 86400 * 1000)
        date = datetime.date.fromordinal(days + 1)
        (hours, milliseconds) = divmod(milliseconds, 3600 * 1000)
        (minutes, milliseconds) = divmod(milliseconds, 60 * 1000)
        (seconds, milliseconds) = divmod(milliseconds, 1000)
        return '%d/%02d/%02d %02d:%02d:%02d.%03d' % (date.year, date.month, date.day,
                                                     hours, minutes, seconds, milliseconds)


class HtmlOutput:
    """
    This class represents a HTML document. Its methods help to construct
    a readable HTML document with automatic indentation of tags. It also
    helps to escape all element values and UTF-8 encoding.
    """
    def __init__(self):
        self.output = ''
        self.prefix = 0

    def __str__(self):
        return self.output

    def _open_tag(self, tag, **attrs):
        output = '<' + tag
        if len(attrs) > 0:
            output += ' ' + ' '.join(['%s="%s"' % (a, v) for (a, v) in attrs.items()])
        output += '>'
        self.prefix += 1
        return output

    def _close_tag(self, tag):
        self.prefix -= 1
        return '</' + tag + '>'

    def _prefix(self):
        return self.prefix * 2 * ' '

    def add_open_tag(self, tag, **attrs):
        self.output += self._prefix() + self._open_tag(tag, **attrs) + '\n'

    def add_close_tag(self, tag):
        self.output += self._prefix() + self._close_tag(tag) + '\n'

    def add_content(self, tag, content, **attrs):
        self.output += self._prefix() + self._open_tag(tag, **attrs)
        self.output += cgi.escape(content.encode('utf8'))
        self.output += self._close_tag(tag) + '\n'

    def add_comment(self, comment):
        self.output += '<!--\n'
        self.output += cgi.escape(comment.encode('utf8'))
        self.output += '\n-->\n'


class HtmlTable:
    """
    HtmlTable builds on top of HtmlOutput. It takes a list of objects and
    creates a HTML table using a selected set of attributes from these
    objects. Each column can be individually formatted (e.g. precision on
    floating-point values). Customized HTML attributes can also be applied
    per column.
    """
    def __init__(self, columns, rows=None, column_attributes=None, column_formatters=None, comment=None):
        """
        Constructor.
        :param columns: A list of attributes of the objects
        :param rows: A list of objects that will presented as rows of the table
        :param column_attributes:  A dictionary keyed by column names (in 'columns').
            Each keyed value is dictionary representing HTML attributes. For example,
            ALIGN="RIGHT" is expressed as {'ALIGN': 'RIGHT'}
        :param column_formatters: A dictionary of Formatter subclass objects keyed by
            column names.
        :return:
        """
        self.columns = columns
        self.rows = rows
        if column_attributes is None:
            self.column_attributes = dict()
        else:
            self.column_attributes = column_attributes
        if column_formatters is None:
            self.column_formatters = dict()
        else:
            self.column_formatters = column_formatters
        self.comment = comment
        self.table_attrs = {'style': 'border-collapse: collapse',
                            'border': 1,
                            'cellpadding': 2}
        self.num_rows = 0

    def _add_row(self, output, tag, columns, attrs=None):
        if attrs is None:
            attrs = dict()
        else:
            attrs = copy.copy(attrs)
        # determine background color
        num_rows_per_block = 1
        if 1 == ((self.num_rows / num_rows_per_block) % 2):
            attrs['bgcolor'] = 'lightcyan'
        output.add_open_tag('tr', **attrs)
        assert len(columns) == len(self.columns)
        for n in range(len(self.columns)):
            col_name = self.columns[n]
            if attrs is not None and col_name in attrs:
                output.add_content(tag, columns[n], **attrs[col_name])
            else:
                output.add_content(tag, columns[n])
        output.add_close_tag('tr')
        self.num_rows += 1

    def __str__(self):
        output = HtmlOutput()
        output.add_open_tag('html')
        if self.comment is not None:
            output.add_comment(self.comment)
        output.add_open_tag('table', **self.table_attrs)
        # Make the header font size smaller so some columns can be narrower
        header_attrs = dict()
        for col in self.columns:
            header_attrs[col] = {'style': 'font-size: 12px'}

        # Create the header
        def split_name(s):
            out = ''
            for c in s:
                if c in string.uppercase:
                    out += ' '
                out += c
            return out
        self._add_row(output, 'th', [split_name(x) for x in self.columns], header_attrs)
        # Reset the header count. This affects row highlighting
        self.num_rows = 0
        # Output all rows (objects)
        for row in self.rows:
            columns = []
            for col in self.columns:
                value = getattr(row, col)
                if col in self.column_formatters:
                    formatter = self.column_formatters[col]
                    columns.append(formatter.format(value))
                else:
                    if value is not None:
                        columns.append(unicode(value))
                    else:
                        columns.append('')
            self._add_row(output, 'td', columns, self.column_attributes)
        output.add_close_tag('table')
        output.add_close_tag('html')
        return str(output)


class McEmailMessageDumper(HtmlTable):
    def __init__(self, objects, comment=None):
        columns = ['Id',
                   'Score',
                   'ScoreVersion',
                   'TimeVarianceType',
                   'TimeVarianceState',
                   'NeedUpdate',
                   'DateReceived',
                   'From',
                   'Subject',
                   'IsRead',
                   'LastVerbExecuted',
                   'HasBeenGleaned',
                   'TimesRead',
                   'SecondsRead',
                   'ScoreIsRead',
                   'ScoreIsReplied']
        column_formatters = {'Score': DoubleFormatter(7, 6),
                             'NeedUpdate': BooleanFormatter(short_form=True),
                             'DateReceived': DateTimeFormatter(),
                             'IsRead': BooleanFormatter(short_form=True),
                             'HasBeenGleaned': BooleanFormatter(short_form=True),
                             'ScoreIsRead': BooleanFormatter(short_form=True),
                             'ScoreIsReplied': BooleanFormatter(short_form=True)}
        align_right = {'align': 'right'}
        align_center = {'align': 'center'}
        column_attributes = {'Score': align_right,
                             'ScoreVersion': align_center,
                             'TimeVarianceType': align_right,
                             'TimeVarianceState': align_right,
                             'NeedUpdate': align_center,
                             'TimesRead': align_right,
                             'SecondsRead': align_right,
                             'IsRead': align_center,
                             'LastVerbExecuted': align_center,
                             'HasBeenGleaned': align_center,
                             'ScoreIsRead': align_center,
                             'ScoreIsReplied': align_center}
        HtmlTable.__init__(self, columns, rows=objects,
                           column_attributes=column_attributes,
                           column_formatters=column_formatters,
                           comment=comment)


class McEmailMessageDependencyDumper(HtmlTable):
    def __init__(self, objects, comment=None):
        columns = ['Id',
                   'EmailAddressId',
                   'EmailAddressType',
                   'EmailMessageId']
        HtmlTable.__init__(self, columns, rows=objects, comment=comment)


class McEmailAddressDumper(HtmlTable):
    def __init__(self, objects, comment=None):
        columns = ['Id',
                   'Score',
                   'CanonicalEmailAddress',
                   'ScoreVersion',
                   'NeedUpdate',
                   'EmailsReceived',
                   'EmailsRead',
                   'EmailsReplied',
                   'EmailsArchived',
                   'EmailsSent',
                   'EmailsDeleted',
                   'IsHot']
        column_formatters = {'Score': DoubleFormatter(7, 6),
                             'NeedUpdate': BooleanFormatter(True),
                             'IsVip': BooleanFormatter(True)}
        align_right = {'align': 'right'}
        align_center = {'align': 'center'}
        column_attributes = {'Score': align_right,
                             'EmailsReceived': align_right,
                             'EmailsRead': align_right,
                             'EmailsReplied': align_right,
                             'EmailsArchived': align_right,
                             'EmailsSent': align_right,
                             'EmailsDeleted': align_right,
                             'ScoreVersion': align_center,
                             'NeedUpdate': align_center,
                             'IsVip': align_center}
        HtmlTable.__init__(self, columns, rows=objects,
                           column_attributes=column_attributes,
                           column_formatters=column_formatters,
                           comment=comment)


class McContactStringAttributeDumper(HtmlTable):
    def __init__(self, objects, comment=None):
        columns = ['Id',
                   'ContactId',
                   'Order',
                   'Name',
                   'Label',
                   'Type',
                   'Value']
        HtmlTable.__init__(self, columns, rows=objects,
                           comment=comment)


def main():
    # Parse options
    parser = argparse.ArgumentParser()
    parser.add_argument('--db-file', '-f', help='SQLite database file')
    order_group = parser.add_argument_group(title='Ordering Options').add_mutually_exclusive_group()
    order_group.add_argument('--date-received', action='store_true',
                             help='Sorted by DateReceived in ascending order [McEmailMessage only]')
    order_group.add_argument('--emails-received', action='store_true',
                             help='Sorted by EmailsReceived in descending order [McEmailAddress only]')
    order_group.add_argument('--score', action='store_true',
                             help='Sorted by Score in descending order')
    order_group.add_argument('--score-version', action='store_true',
                             help='Sorted by ScoreVersion in descending order')
    order_group.add_argument('--time-variance', action='store_true',
                             help='Sorted by TimeVarianceType, TimeVarianceState in descending order')

    parser.add_argument('tables', nargs='*', help='Choices are: McEmailMessage, McEmailAddress, '
                                                  'McEmailMessageDependency, McContactStringAttribute. '
                                                  '(Names are case insensitive.)')
    options = parser.parse_args()
    if len(options.tables) == 0:
        parser.print_help()
        exit(0)

    # Initialize SQLAlchemy
    if not os.path.exists(options.db_file):
        print 'ERROR: %s does not exist.' % options.db_file
        exit(1)
    ModelDb.initialize(options.db_file)
    import model
    session = sessionmaker(bind=ModelDb.engine)()

    # Generate a comment describing the command that creates these .html files
    now = datetime.datetime.now()
    comment = 'Command: %s\nCurrent working directory: %s\nDb file: %s\nTime: %s' % \
              (' '.join(sys.argv), os.getcwd(), options.db_file, now.strftime('%D %H:%M:%S'))

    # Process tables
    for table in options.tables:
        table = table.lower()

        table_to_classes = {
            'mcemailmessage': (McEmailMessageDumper, model.McEmailMessage),
            'mcemailaddress': (McEmailAddressDumper, model.McEmailAddress),
            'mcemailmessagedependency': (McEmailMessageDependencyDumper, model.McEmailMessageDependency),
            'mccontactstringattribute': (McContactStringAttributeDumper, model.McContactStringAttribute)
        }
        if table not in table_to_classes:
            raise ValueError('Unknown table %s' % table)
        (dumper_class, model_class) = table_to_classes[table]
        query = session.query(model_class)
        if options.date_received and table == 'mcemailmessage':
            objects = query.order_by(model_class.DateReceived)
        elif options.emails_received and table == 'mcemailaddress':
            objects = query.order_by(sqlalchemy.desc(model_class.EmailsReceived))
        elif options.score:
            objects = query.order_by(sqlalchemy.desc(model_class.Score))
        elif options.score_version:
            objects = query.order_by(model_class.ScoreVersion)
        else:
            objects = query.all()
        filename = table + '.html'
        print 'Writing %s...' % filename
        with open(filename, 'w') as f:
            f.write(str(dumper_class(objects, comment)))

if __name__ == '__main__':
    main()