from xml.sax.handler import ContentHandler
import xml.sax
from object_stack import ObjectStack
import sys
import os
from as_xml import AsXmlElement
from output import Output


class AsSchemaElement(AsXmlElement):
    INDENTATION = ' ' * 4

    @staticmethod
    def _indent(xml_str):
        lines = xml_str.split('\n')
        return '\n'.join([AsSchemaElement.INDENTATION + l for l in lines])

    def __init__(self, name):
        AsXmlElement.__init__(self, name)

    @classmethod
    def node(cls, level):
        return 'node%d' % level

    def generate_cs(self, output, level):
        # Create the node
        var_name = self.node(level)
        element_redaction = 'RedactionType.' + self.attrs['element_redaction'].upper()
        attribute_redaction = 'RedactionType.' + self.attrs['attribute_redaction'].upper()
        output.line(3, '// %s', self.name)
        output.line(3, '%s = new NcXmlFilterNode ("%s", %s, %s);', var_name, self.name,
                    element_redaction, attribute_redaction)

        # Add all children nodes
        for child in self.children:
            assert isinstance(child, AsSchemaElement)
            child.generate_cs(output, level+1)
            output.line(3, '%s.Add(%s); // %s -> %s', var_name, self.node(level+1), self.name, child.name)


class AsSchema(ContentHandler):
    REDACTION_TYPES = ['element_redaction', 'attribute_redaction']
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
        self.name_space = os.path.splitext(os.path.basename(xml_file))[0]
        self.value = ''
        xml.sax.parse(xml_file, self)

    def startElement(self, name, attrs):
        cur_element = self.stack.peek()
        child = AsSchemaElement(name)
        if name == 'xml':
            self.root = child
        if cur_element and name not in AsSchema.REDACTION_TYPES:
            cur_element.add_child(child)
        self.value = ''
        self.stack.push(child)

    def endElement(self, name):
        node = self.stack.pop()
        assert node.name == name
        if name in AsSchema.REDACTION_TYPES:
            cur_element = self.stack.peek()
            assert cur_element is not None
            cur_element.set_attr(name, self.value)

    def characters(self, content):
        self.value += content

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
        output.line(0, 'using NachoCore.Utils;')
        output.line(0, '')
        output.line(0, 'namespace NachoCore.Wbxml')
        output.line(0, '{')
        output.line(1, 'public class AsXmlFilter%s : NcXmlFilter', self.class_suffix)
        output.line(1, '{')
        output.line(2, 'public AsXmlFilter%s () : base ("%s")', self.class_suffix, self.name_space)
        output.line(2, '{')
        # Create all the variables
        for n in range(self.stack.max_depth()-1):  # -1 for redaction tag
            output.line(3, 'NcXmlFilterNode %s = null;', AsSchemaElement.node(n))
        output.line(0, '')
        output.line(3, 'node0 = new NcXmlFilterNode ("xml", RedactionType.NONE, RedactionType.NONE);')

        # Create the code that sets up the tree
        for child in self.root.children:
            child.generate_cs(output, 1)

        output.line(3, 'node0.Add(node1);')
        output.line(3, '')
        output.line(3, 'Root = node0;')
        output.line(2, '}')
        output.line(1, '}')
        output.line(0, '}')


def process(fname, class_suffix):
    as_schema = AsSchema(fname, class_suffix)
    output = Output('AsXmlFilter' + class_suffix + '.cs')
    as_schema.generate_xml_filter_cs(output)


def main():
    process(sys.argv[1], sys.argv[2])


if __name__ == '__main__':
    main()