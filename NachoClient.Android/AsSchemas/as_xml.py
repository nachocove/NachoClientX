from object_stack import ObjectStack
from xml.sax import ContentHandler
import xml.sax


class AsXmlElement:
    """
    This class provides a base class for creating tree nodes.
    When AsXmlParser is parsing, start handlers should be
    returning objects derived from this base class. It is
    a generic tree node that provides a walk function and
    auxilliary attribute dictionary.
    """
    def __init__(self, name, value=None):
        self.name = name
        self.attrs = {}
        self.children = []
        self.value = value

    def set_attr(self, attr, value):
        self.attrs[attr] = value

    def set_attrs(self, attr_dict):
        assert isinstance(attr_dict, dict)
        if len(attr_dict) == 0:
            return
        for (key, value) in attr_dict:
            self.set_attr(key, value)

    def get_attr(self, attr):
        return self.attrs[attr]

    def add_child(self, child):
        self.children.append(child)

    def has_children(self):
        return len(self.children) > 0

    def walk(self, level, start_fn, end_fn, *params):
        assert callable(start_fn) and callable(end_fn)
        start_fn(self, level, *params)
        for child in self.children:
            child.walk(level+1, start_fn, end_fn, *params)
        end_fn(self, level, *params)


class AsXmlParser(ContentHandler):
    def __init__(self):
        ContentHandler.__init__(self)
        self.stack = ObjectStack()
        self.attributes = {}  # attributes of the current element
        self.value = ''  # value of the current element
        self.start_handlers = {}
        self.end_handlers = {}

    def parse(self, xml_fname):
        if None not in self.start_handlers:
            # Install default handler to catch unknown tag if one has
            # not be installed already.
            self.start_handlers[None] = AsXmlParser.default_start_handler
            self.end_handlers[None] = AsXmlParser.default_end_handler
        xml.sax.parse(xml_fname, self)

    @staticmethod
    def default_start_handler(name, attrs):
        # By default seeing an unexpected tag results in an exception
        raise ValueError()

    @staticmethod
    def default_end_handler(obj, content):
        # By default seeing an unexpected tag results in an exception
        raise ValueError()

    def current_element(self):
        tos = self.stack.peek()
        if tos is None:
            return None
        return tos

    def startElement(self, name, attrs):
        self.attributes = {}
        self.value = ''
        if name in self.start_handlers:
            obj = self.start_handlers[name](str(name), attrs)
        else:
            # Call the default handler if it exists
            if None not in self.start_handlers:
                raise ValueError()
            obj = self.start_handlers[None](str(name), attrs)
        if obj is None:
            return  # ignoring this element
        #print 'START[%d]: %s %s' % (self.stack.depth(), name, attrs.items())
        self.stack.push(obj)

    def endElement(self, name):
        obj = self.stack.peek()
        assert obj is not None
        if name not in self.end_handlers:
            if None not in self.end_handlers:
                raise ValueError()
            obj = self.stack.pop()
            if not self.end_handlers[None](name, self.value):
                self.stack.push(obj)  # this element was ignored in startElement
            return
        obj = self.stack.pop()
        #print 'END[%d]: %s' % (self.stack.depth(), name)
        self.end_handlers[name](obj, self.value)

    def characters(self, content):
        self.value += content
