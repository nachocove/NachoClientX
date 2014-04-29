import StringIO


class Output:
    """
    This class is used for generating output to either a file
    or a string buffer. It provides a C printf like method
    for formatting with a per-level indentation.
    """
    def __init__(self, dst=None, indent=4):
        if isinstance(dst, str):
            # This is a filename
            self.output = open(dst, 'w')
        elif isinstance(dst, file):
            # This is a file stream
            self.output = dst
        elif dst is None:
            self.output = StringIO.StringIO()
        else:
            raise TypeError()

        assert isinstance(indent, int)
        self.indent = ' ' * indent

    def line(self, indent_level, fmt, *args):
        indent = indent_level * self.indent
        print >> self.output, indent + fmt % tuple(args)

    def write(self, fname):
        if not isinstance(self.output, StringIO.StringIO):
            raise TypeError('write() can only be used for StringIO destination')
        with open(fname, 'w') as f:
            f.write(self.output.getvalue())
            f.close()

