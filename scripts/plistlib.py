r"""plistlib.py -- a tool to generate and parse MacOSX .plist files.

The PropertyList (.plist) file format is a simple XML pickle supporting
basic object types, like dictionaries, lists, numbers and strings.
Usually the top level object is a dictionary.

To write out a plist file, use the writePlist(rootObject, pathOrFile)
function. 'rootObject' is the top level object, 'pathOrFile' is a
filename or a (writable) file object.

To parse a plist from a file, use the readPlist(pathOrFile) function,
with a file name or a (readable) file object as the only argument. It
returns the top level object (again, usually a dictionary).

To work with plist data in strings, you can use readPlistFromString()
and writePlistToString().

Values can be strings, integers, floats, booleans, tuples, lists,
dictionaries, Data or datetime.datetime objects. String values (including
dictionary keys) may be unicode strings -- they will be written out as
UTF-8.

The <data> plist type is supported through the Data class. This is a
thin wrapper around a Python string.

Generate Plist example:

    pl = dict(
        aString="Doodah",
        aList=["A", "B", 12, 32.1, [1, 2, 3]],
        aFloat=0.1,
        anInt=728,
        aDict=dict(
            anotherString="<hello & hi there!>",
            aUnicodeValue=u'M\xe4ssig, Ma\xdf',
            aTrueValue=True,
            aFalseValue=False,
        ),
        someData=Data("<binary gunk>"),
        someMoreData=Data("<lots of binary gunk>" * 10),
        aDate=datetime.datetime.fromtimestamp(time.mktime(time.gmtime())),
    )
    # unicode keys are possible, but a little awkward to use:
    pl[u'\xc5benraa'] = "That was a unicode key."
    writePlist(pl, fileName)

Parse Plist example:

    pl = readPlist(pathOrFile)
    print pl["aKey"]
"""


__all__ = [
    "readPlist", "writePlist", "readPlistFromString", "writePlistToString",
    "readPlistFromResource", "writePlistToResource",
    "Plist", "Data", "Dict",
    "InvalidFileException",
]
# Note: the Plist and Dict classes have been deprecated.

import binascii
import datetime
import json
from cStringIO import StringIO
import os
import re
import struct
import warnings


def readPlist(pathOrFile):
    """Read a .plist file. 'pathOrFile' may either be a file name or a
    (readable) file object. Return the unpacked root object (which
    usually is a dictionary).
    """
    didOpen = 0
    if isinstance(pathOrFile, (str, unicode)):
        pathOrFile = open(pathOrFile)
        didOpen = 1
    header = pathOrFile.read(8)
    pathOrFile.seek(0)
    if header == '<?xml ve' or header[2:] == '<?xml ': #XML plist file, without or with BOM 
        p = PlistParser()
        rootObject = p.parse(pathOrFile)
    elif header == 'bplist00': #binary plist file
        rootObject =  readBinaryPlistFile(pathOrFile)
    else: #json plist file
        rootObject = json.load(pathOrFile)
    if didOpen:
        pathOrFile.close()
    return rootObject


def writePlist(rootObject, pathOrFile):
    """Write 'rootObject' to a .plist file. 'pathOrFile' may either be a
    file name or a (writable) file object.
    """
    didOpen = 0
    if isinstance(pathOrFile, (str, unicode)):
        pathOrFile = open(pathOrFile, "w")
        didOpen = 1
    writer = PlistWriter(pathOrFile)
    writer.writeln("<plist version=\"1.0\">")
    writer.writeValue(rootObject)
    writer.writeln("</plist>")
    if didOpen:
        pathOrFile.close()


def readPlistFromString(data):
    """Read a plist data from a string. Return the root object.
    """
    return readPlist(StringIO(data))


def writePlistToString(rootObject):
    """Return 'rootObject' as a plist-formatted string.
    """
    f = StringIO()
    writePlist(rootObject, f)
    return f.getvalue()


def readPlistFromResource(path, restype='plst', resid=0):
    """Read plst resource from the resource fork of path.
    """
    warnings.warnpy3k("In 3.x, readPlistFromResource is removed.",
                      stacklevel=2)
    from Carbon.File import FSRef, FSGetResourceForkName
    from Carbon.Files import fsRdPerm
    from Carbon import Res
    fsRef = FSRef(path)
    resNum = Res.FSOpenResourceFile(fsRef, FSGetResourceForkName(), fsRdPerm)
    Res.UseResFile(resNum)
    plistData = Res.Get1Resource(restype, resid).data
    Res.CloseResFile(resNum)
    return readPlistFromString(plistData)


def writePlistToResource(rootObject, path, restype='plst', resid=0):
    """Write 'rootObject' as a plst resource to the resource fork of path.
    """
    warnings.warnpy3k("In 3.x, writePlistToResource is removed.", stacklevel=2)
    from Carbon.File import FSRef, FSGetResourceForkName
    from Carbon.Files import fsRdWrPerm
    from Carbon import Res
    plistData = writePlistToString(rootObject)
    fsRef = FSRef(path)
    resNum = Res.FSOpenResourceFile(fsRef, FSGetResourceForkName(), fsRdWrPerm)
    Res.UseResFile(resNum)
    try:
        Res.Get1Resource(restype, resid).RemoveResource()
    except Res.Error:
        pass
    res = Res.Resource(plistData)
    res.AddResource(restype, resid, '')
    res.WriteResource()
    Res.CloseResFile(resNum)


class DumbXMLWriter:

    def __init__(self, file, indentLevel=0, indent="\t"):
        self.file = file
        self.stack = []
        self.indentLevel = indentLevel
        self.indent = indent

    def beginElement(self, element):
        self.stack.append(element)
        self.writeln("<%s>" % element)
        self.indentLevel += 1

    def endElement(self, element):
        assert self.indentLevel > 0
        assert self.stack.pop() == element
        self.indentLevel -= 1
        self.writeln("</%s>" % element)

    def simpleElement(self, element, value=None):
        if value is not None:
            value = _escapeAndEncode(value)
            self.writeln("<%s>%s</%s>" % (element, value, element))
        else:
            self.writeln("<%s/>" % element)

    def writeln(self, line):
        if line:
            self.file.write(self.indentLevel * self.indent + line + "\n")
        else:
            self.file.write("\n")



# timestamp 0 of binary plists corresponds to 1/1/2001 (year of Mac OS X 10.0), instead of 1/1/1970.
MAC_OS_X_TIME_OFFSET = (31 * 365 + 8) * 86400

class InvalidFileException(ValueError):
    def __str__(self):
        return "Invalid file"
    def __unicode__(self):
        return "Invalid file"

def readBinaryPlistFile(in_file):
    """
    Read a binary plist file, following the description of the binary format: http://opensource.apple.com/source/CF/CF-550/CFBinaryPList.c
    Raise InvalidFileException in case of error, otherwise return the root object, as usual
    """
    in_file.seek(-32, os.SEEK_END)
    trailer = in_file.read(32)
    if len(trailer) != 32:
        return InvalidFileException()
    offset_size, ref_size, num_objects, top_object, offset_table_offset = struct.unpack('>6xBB4xL4xL4xL', trailer)
    in_file.seek(offset_table_offset)
    object_offsets = []
    offset_format = '>' + {1: 'B', 2: 'H', 4: 'L', 8: 'Q', }[offset_size] * num_objects
    ref_format = {1: 'B', 2: 'H', 4: 'L', 8: 'Q', }[ref_size]
    int_format = {0: (1, '>B'), 1: (2, '>H'), 2: (4, '>L'), 3: (8, '>Q'), }
    object_offsets = struct.unpack(offset_format, in_file.read(offset_size * num_objects))
    def getSize(token_l):
        """ return the size of the next object."""
        if token_l == 0xF:
            m = ord(in_file.read(1)) & 0x3
            s, f = int_format[m]
            return struct.unpack(f, in_file.read(s))[0]
        return token_l
    def readNextObject(offset):
        """ read the object at offset. May recursively read sub-objects (content of an array/dict/set) """
        in_file.seek(offset)
        token = in_file.read(1)
        token_h, token_l = ord(token) & 0xF0, ord(token) & 0x0F #high and low parts 
        if token == '\x00':
            return None
        elif token == '\x08':
            return False
        elif token == '\x09':
            return True
        elif token == '\x0f':
            return ''
        elif token_h == 0x10: #int
            result = 0
            for k in xrange((2 << token_l) - 1):
                result = (result << 8) + ord(in_file.read(1))
            return result
        elif token_h == 0x20: #real
            if token_l == 2:
                return struct.unpack('>f', in_file.read(4))[0]
            elif token_l == 3:
                return struct.unpack('>d', in_file.read(8))[0]
        elif token_h == 0x30: #date
            f = struct.unpack('>d', in_file.read(8))[0]
            return datetime.datetime.utcfromtimestamp(f + MAC_OS_X_TIME_OFFSET)
        elif token_h == 0x80: #data
            s = getSize(token_l)
            return in_file.read(s)
        elif token_h == 0x50: #ascii string
            s = getSize(token_l)
            return in_file.read(s)
        elif token_h == 0x60: #unicode string
            s = getSize(token_l)
            return in_file.read(s * 2).decode('utf-16be')
        elif token_h == 0x80: #uid
            return in_file.read(token_l + 1)
        elif token_h == 0xA0: #array
            s = getSize(token_l)
            obj_refs = struct.unpack('>' + ref_format * s, in_file.read(s * ref_size))
            return map(lambda x: readNextObject(object_offsets[x]), obj_refs)
        elif token_h == 0xC0: #set
            s = getSize(token_l)
            obj_refs = struct.unpack('>' + ref_format * s, in_file.read(s * ref_size))
            return set(map(lambda x: readNextObject(object_offsets[x]), obj_refs))
        elif token_h == 0xD0: #dict
            result = {}
            s = getSize(token_l)
            key_refs = struct.unpack('>' + ref_format * s, in_file.read(s * ref_size))
            obj_refs = struct.unpack('>' + ref_format * s, in_file.read(s * ref_size))
            for k, o in zip(key_refs, obj_refs):
                key = readNextObject(object_offsets[k])
                obj = readNextObject(object_offsets[o])
                result[key] = obj
            return result
        raise InvalidFileException()
    return readNextObject(object_offsets[top_object])

# Contents should conform to a subset of ISO 8601
# (in particular, YYYY '-' MM '-' DD 'T' HH ':' MM ':' SS 'Z'.  Smaller units may be omitted with
#  a loss of precision)
_dateParser = re.compile(r"(?P<year>\d\d\d\d)(?:-(?P<month>\d\d)(?:-(?P<day>\d\d)(?:T(?P<hour>\d\d)(?::(?P<minute>\d\d)(?::(?P<second>\d\d))?)?)?)?)?Z")

def _dateFromString(s):
    order = ('year', 'month', 'day', 'hour', 'minute', 'second')
    gd = _dateParser.match(s).groupdict()
    lst = []
    for key in order:
        val = gd[key]
        if val is None:
            break
        lst.append(int(val))
    return datetime.datetime(*lst)

def _dateToString(d):
    return '%04d-%02d-%02dT%02d:%02d:%02dZ' % (
        d.year, d.month, d.day,
        d.hour, d.minute, d.second
    )


# Regex to find any control chars, except for \t \n and \r
_controlCharPat = re.compile(
    r"[\x00\x01\x02\x03\x04\x05\x06\x07\x08\x0b\x0c\x0e\x0f"
    r"\x10\x11\x12\x13\x14\x15\x16\x17\x18\x19\x1a\x1b\x1c\x1d\x1e\x1f]")

def _escapeAndEncode(text):
    m = _controlCharPat.search(text)
    if m is not None:
        raise ValueError("strings can't contains control characters; "
                         "use plistlib.Data instead")
    text = text.replace("\r\n", "\n")       # convert DOS line endings
    text = text.replace("\r", "\n")         # convert Mac line endings
    text = text.replace("&", "&amp;")       # escape '&'
    text = text.replace("<", "&lt;")        # escape '<'
    text = text.replace(">", "&gt;")        # escape '>'
    return text.encode("utf-8")             # encode as UTF-8


PLISTHEADER = """\
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
"""

class PlistWriter(DumbXMLWriter):

    def __init__(self, file, indentLevel=0, indent="\t", writeHeader=1):
        if writeHeader:
            file.write(PLISTHEADER)
        DumbXMLWriter.__init__(self, file, indentLevel, indent)

    def writeValue(self, value):
        if isinstance(value, (str, unicode)):
            self.simpleElement("string", value)
        elif isinstance(value, bool):
            # must switch for bool before int, as bool is a
            # subclass of int...
            if value:
                self.simpleElement("true")
            else:
                self.simpleElement("false")
        elif isinstance(value, (int, long)):
            self.simpleElement("integer", "%d" % value)
        elif isinstance(value, float):
            self.simpleElement("real", repr(value))
        elif isinstance(value, dict):
            self.writeDict(value)
        elif isinstance(value, Data):
            self.writeData(value)
        elif isinstance(value, datetime.datetime):
            self.simpleElement("date", _dateToString(value))
        elif isinstance(value, (tuple, list)):
            self.writeArray(value)
        else:
            raise TypeError("unsuported type: %s" % type(value))

    def writeData(self, data):
        self.beginElement("data")
        self.indentLevel -= 1
        maxlinelength = 76 - len(self.indent.replace("\t", " " * 8) *
                                 self.indentLevel)
        for line in data.asBase64(maxlinelength).split("\n"):
            if line:
                self.writeln(line)
        self.indentLevel += 1
        self.endElement("data")

    def writeDict(self, d):
        self.beginElement("dict")
        items = d.items()
        items.sort()
        for key, value in items:
            if not isinstance(key, (str, unicode)):
                raise TypeError("keys must be strings")
            self.simpleElement("key", key)
            self.writeValue(value)
        self.endElement("dict")

    def writeArray(self, array):
        self.beginElement("array")
        for value in array:
            self.writeValue(value)
        self.endElement("array")


class _InternalDict(dict):

    # This class is needed while Dict is scheduled for deprecation:
    # we only need to warn when a *user* instantiates Dict or when
    # the "attribute notation for dict keys" is used.

    def __getattr__(self, attr):
        try:
            value = self[attr]
        except KeyError:
            raise AttributeError, attr
        from warnings import warn
        warn("Attribute access from plist dicts is deprecated, use d[key] "
             "notation instead", PendingDeprecationWarning, 2)
        return value

    def __setattr__(self, attr, value):
        from warnings import warn
        warn("Attribute access from plist dicts is deprecated, use d[key] "
             "notation instead", PendingDeprecationWarning, 2)
        self[attr] = value

    def __delattr__(self, attr):
        try:
            del self[attr]
        except KeyError:
            raise AttributeError, attr
        from warnings import warn
        warn("Attribute access from plist dicts is deprecated, use d[key] "
             "notation instead", PendingDeprecationWarning, 2)

class Dict(_InternalDict):

    def __init__(self, **kwargs):
        from warnings import warn
        warn("The plistlib.Dict class is deprecated, use builtin dict instead",
             PendingDeprecationWarning, 2)
        super(Dict, self).__init__(**kwargs)


class Plist(_InternalDict):

    """This class has been deprecated. Use readPlist() and writePlist()
    functions instead, together with regular dict objects.
    """

    def __init__(self, **kwargs):
        from warnings import warn
        warn("The Plist class is deprecated, use the readPlist() and "
             "writePlist() functions instead", PendingDeprecationWarning, 2)
        super(Plist, self).__init__(**kwargs)

    def fromFile(cls, pathOrFile):
        """Deprecated. Use the readPlist() function instead."""
        rootObject = readPlist(pathOrFile)
        plist = cls()
        plist.update(rootObject)
        return plist
    fromFile = classmethod(fromFile)

    def write(self, pathOrFile):
        """Deprecated. Use the writePlist() function instead."""
        writePlist(self, pathOrFile)


def _encodeBase64(s, maxlinelength=76):
    # copied from base64.encodestring(), with added maxlinelength argument
    maxbinsize = (maxlinelength//4)*3
    pieces = []
    for i in range(0, len(s), maxbinsize):
        chunk = s[i : i + maxbinsize]
        pieces.append(binascii.b2a_base64(chunk))
    return "".join(pieces)

class Data:

    """Wrapper for binary data."""

    def __init__(self, data):
        self.data = data

    def fromBase64(cls, data):
        # base64.decodestring just calls binascii.a2b_base64;
        # it seems overkill to use both base64 and binascii.
        return cls(binascii.a2b_base64(data))
    fromBase64 = classmethod(fromBase64)

    def asBase64(self, maxlinelength=76):
        return _encodeBase64(self.data, maxlinelength)

    def __cmp__(self, other):
        if isinstance(other, self.__class__):
            return cmp(self.data, other.data)
        elif isinstance(other, str):
            return cmp(self.data, other)
        else:
            return cmp(id(self), id(other))

    def __repr__(self):
        return "%s(%s)" % (self.__class__.__name__, repr(self.data))


class PlistParser:

    def __init__(self):
        self.stack = []
        self.currentKey = None
        self.root = None

    def parse(self, fileobj):
        from xml.parsers.expat import ParserCreate
        parser = ParserCreate()
        parser.StartElementHandler = self.handleBeginElement
        parser.EndElementHandler = self.handleEndElement
        parser.CharacterDataHandler = self.handleData
        parser.ParseFile(fileobj)
        return self.root

    def handleBeginElement(self, element, attrs):
        self.data = []
        handler = getattr(self, "begin_" + element, None)
        if handler is not None:
            handler(attrs)

    def handleEndElement(self, element):
        handler = getattr(self, "end_" + element, None)
        if handler is not None:
            handler()

    def handleData(self, data):
        self.data.append(data)

    def addObject(self, value):
        if self.currentKey is not None:
            self.stack[-1][self.currentKey] = value
            self.currentKey = None
        elif not self.stack:
            # this is the root object
            self.root = value
        else:
            self.stack[-1].append(value)

    def getData(self):
        data = "".join(self.data)
        try:
            data = data.encode("ascii")
        except UnicodeError:
            pass
        self.data = []
        return data

    # element handlers

    def begin_dict(self, attrs):
        d = _InternalDict()
        self.addObject(d)
        self.stack.append(d)
    def end_dict(self):
        self.stack.pop()

    def end_key(self):
        self.currentKey = self.getData()

    def begin_array(self, attrs):
        a = []
        self.addObject(a)
        self.stack.append(a)
    def end_array(self):
        self.stack.pop()

    def end_true(self):
        self.addObject(True)
    def end_false(self):
        self.addObject(False)
    def end_integer(self):
        self.addObject(int(self.getData()))
    def end_real(self):
        self.addObject(float(self.getData()))
    def end_string(self):
        self.addObject(self.getData())
    def end_data(self):
        self.addObject(Data.fromBase64(self.getData()))
    def end_date(self):
        self.addObject(_dateFromString(self.getData()))
