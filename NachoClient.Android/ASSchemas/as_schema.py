from xml.sax.handler import ContentHandler
import xml.sax
from object_stack import ObjectStack
import sys
import os

class Output:
    def __init__(self, output):
        self.output = output

    def line(self, indent, fmt, *arg):
        print >> self.output, (' ' * 4 * indent) + fmt % arg


class AsSchemaElement:
    INDENTATION = ' ' * 4

    @staticmethod
    def _indent(xml_str):
        lines = xml_str.split('\n')
        return '\n'.join([AsSchemaElement.INDENTATION + l for l in lines])

    def __init__(self, name):
        self.name = name
        self.children = []
        self.attrs = {}
        self.value = None

    def set_attr(self, attr, value):
        self.attrs[attr] = value

    def add_child(self, child):
        self.children.append(child)

    def __str__(self):
        s = '<element name="%s">\n' % self.name
        for key in sorted(self.attrs.keys()):
            s += AsSchemaElement.INDENTATION + '<%s>%s</%s>\n' % (key, self.attrs[key], key)
        for child in self.children:
            s += AsSchemaElement._indent(str(child))
        s += '</element>\n'
        return s

    @classmethod
    def node(cls, level):
        return 'node%d' % level

    def generate_cs(self, output, level):
        # Create the node
        var_name = self.node(level)
        output.line(2, '// %s', self.name)
        if self.value is None:
            output.line(2, '%s = new XElement ("%s");', var_name, self.name)
        else:
            output.line(2, '%s = new XElement ("%s", "%s");', var_name, self.name, self.value)

        # Add all attributes
        for (key, value) in self.attrs.items():
            if key == 'redaction':
                value = value.upper()
            else:
                value = '"%s"' % value
            output.line(2, '%s.Add(new Attribute("%s", %s);', var_name, key, value)

        # Add all children nodes
        for child in self.children:
            assert isinstance(child, AsSchemaElement)
            child.generate_cs(output, level+1)
            output.line(2, '%s.Add(%s); // %s -> %s', var_name, self.node(level+1), self.name, child.name)


class AsSchema(ContentHandler):
    REDACTION_TYPES = ['no_redaction', 'full_redaction', 'redact_with_length']
    """
    This class represents an ActiveSync schema XML file. An AS schema XML
    file is a XML file that describes attributes of various ActiveSync XML
    tag. It is created from AS .xsd file.

    Currently, we only support redaction filtering. But we can extend it to
    support other features in the future.
    """
    def __init__(self, xml_file, class_suffix):
        ContentHandler.__init__(self)
        self.stack = ObjectStack()
        self.root = None
        self.class_suffix = class_suffix
        xml.sax.parse(xml_file, self)

    def startElement(self, name, attrs):
        cur_element = self.stack.peek()
        if name in AsSchema.REDACTION_TYPES:
            # This is an attribute of the element
            assert cur_element is not None
            cur_element.set_attr('redaction', name)
        else:
            # This is a child element
            child = AsSchemaElement(name)
            if name == 'xml':
                self.root = child
            if cur_element:
                cur_element.add_child(child)
            self.stack.push(child)

    def endElement(self, name):
        if name not in AsSchema.REDACTION_TYPES:
            node = self.stack.pop()
            assert node.name == name

    @classmethod
    def line(cls, output, indent, fmt, *args):
        """
        Generate one line of C# code.
        """
        print >> output, (' ' * indent) + fmt % args

    def generate_xml_filter_cs(self, output):
        """
        Generate a C# class that provides XML filtering
        """
        output.line(0, 'using System.Xml.Linq;')
        output.line(0, '')
        output.line(0, 'namespace NachoCore.Wbxml')
        output.line(0, '{')
        output.line(1, 'class AsXmlFilter%s : XmlFilter', self.class_suffix)
        output.line(1, '{')
        # Create all the variables
        for n in range(self.stack.max_depth()-1):
            output.line(2, 'XElement %s = null;', AsSchemaElement.node(n))
        output.line(2, 'XAttribute attribute = null;\n')

        # Create the code that sets up the tree
        for child in self.root.children:
            child.generate_cs(output, 0)

        output.line(2, '')
        output.line(2, 'Root = node0;')
        output.line(1, '}')
        output.line(0, '}')


def process(fname):
    (base, ext) = os.path.splitext(os.path.basename(fname))
    as_schema = AsSchema(fname, base)
    output = Output(open(base + '.cs', 'w'))
    as_schema.generate_xml_filter_cs(output)


def main():
    for fname in sys.argv[1:]:
        process(fname)


if __name__ == '__main__':
    main()