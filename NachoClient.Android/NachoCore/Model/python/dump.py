#!/usr/bin/env python
try:
    from sqlalchemy.orm import sessionmaker
except ImportError:
    print 'ERROR: SQLAlchemy package is not found. Please install SQLAlchemy first by:'
    print 'sudo easy_install SQLAlchemy'
    exit(1)
import argparse
import os
import cgi
from model_db import ModelDb


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


class HtmlTable:
    """
    HtmlTable builds on top of HtmlOutput. It takes a list of objects and
    creates a HTML table using a selected set of attributes from these
    objects. Each column can be individually formatted (e.g. precision on
    floating-point values). Customized HTML attributes can also be applied
    per column.
    """
    def __init__(self, columns, rows=None, column_attributes=None, column_formatters=None):
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
        self.table_attrs = {'style': 'border-collapse: collapse',
                            'border': 1,
                            'cellpadding': 2}

    def _add_row(self, output, tag, columns, attrs=None):
        output.add_open_tag('tr')
        assert len(columns) == len(self.columns)
        for n in range(len(self.columns)):
            col_name = self.columns[n]
            if attrs is not None and col_name in attrs:
                output.add_content(tag, columns[n], **attrs[col_name])
            else:
                output.add_content(tag, columns[n])
        output.add_close_tag('tr')

    def __str__(self):
        output = HtmlOutput()
        output.add_open_tag('html')
        output.add_open_tag('table', **self.table_attrs)
        self._add_row(output, 'th', self.columns)
        for row in self.rows:
            columns = []
            for col in self.columns:
                value = getattr(row, col)
                if col in self.column_formatters:
                    formatter = self.column_formatters[col]
                    columns.append(formatter.format(value))
                else:
                    columns.append(unicode(value))
            self._add_row(output, 'td', columns, self.column_attributes)
        output.add_close_tag('table')
        output.add_close_tag('html')
        return str(output)


class McEmailMessageDumper(HtmlTable):
    def __init__(self, objects):
        columns = ['Id',
                   'Score',
                   'TimeVarianceType',
                   'TimeVarianceState',
                   'DateReceived',
                   'From',
                   'Subject']
        column_formatters = {'Score': DoubleFormatter(7, 6)}
        align_right = {'align': 'right'}
        column_attributes = {'Score': align_right,
                             'TimeVarianceType': align_right,
                             'TimeVarianceState': align_right}
        HtmlTable.__init__(self, columns, rows=objects,
                           column_attributes=column_attributes,
                           column_formatters=column_formatters)


class McContactDumper(HtmlTable):
    def __init__(self, objects):
        columns = ['Id',
                   'Score',
                   'FirstName',
                   'MiddleName',
                   'LastName']
        column_formatters = {'Score': DoubleFormatter(7, 6)}
        align_right = {'align': 'right'}
        column_attributes = {'Score': align_right}
        HtmlTable.__init__(self, columns, rows=objects,
                           column_attributes=column_attributes,
                           column_formatters=column_formatters)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--db-file', '-f', help='SQLite database file')
    parser.add_argument('tables', nargs='*', help='Choices are: McEmailMessage')
    options = parser.parse_args()
    if not os.path.exists(options.db_file):
        print 'ERROR: %s does not exist.' % options.db_file
        exit(1)
    ModelDb.initialize(options.db_file)
    import model
    session = sessionmaker(bind=ModelDb.engine)()

    for table in options.tables:
        table = table.lower()
        if table == 'mcemailmessage':
            dumper_class = McEmailMessageDumper
            objects = session.query(model.McEmailMessage).all()
        elif table == 'mccontact':
            dumper_class = McContactDumper
            objects = session.query(model.McContact).all()
        else:
            raise ValueError('Unknown table %s' % table)
        filename = table + '.html'
        print 'Writing %s...' % filename
        with open(filename, 'w') as f:
            f.write(str(dumper_class(objects)))

if __name__ == '__main__':
    main()