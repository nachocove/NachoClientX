import os
import sys
from xml.sax.handler import ContentHandler
from object_stack import ObjectStack
from as_xml import AsXmlElement
from as_xml import AsXmlParser
from output import Output
from argparse import ArgumentParser


class AsSchemaElement(AsXmlElement):
    def __init__(self, name):
        AsXmlElement.__init__(self, name)

    @classmethod
    def node(cls, level):
        return 'node%d' % level

    def generate_cs(self, output, level):
        # Create the node
        var_name = self.node(level)
        output.line(3, '// %s', self.name)
        element_redaction = 'RedactionType.' + self.attrs['element_redaction'].upper()
        attribute_redaction = 'RedactionType.' + self.attrs['attribute_redaction'].upper()
        output.line(3, '%s = new NcXmlFilterNode ("%s", %s, %s);', var_name, self.name,
                    element_redaction, attribute_redaction)

        # Add all children nodes
        for child in self.children:
            assert isinstance(child, AsSchemaElement)
            child.generate_cs(output, level+1)
            output.line(3, '%s.Add(%s); // %s -> %s', var_name, self.node(level+1), self.name, child.name)


class AsSchema(AsXmlParser):
    REDACTION_TYPES = ['element_redaction', 'attribute_redaction']
    # This list must match NcXmlFilter.cs:RedactionType
    REDACTION_OPTIONS = (u'none', u'length', u'short_hash', u'full_hash', u'full')

    """
    This class represents an ActiveSync schema XML file. An AS schema XML
    file is a XML file that describes attributes of various ActiveSync XML
    tag. It is created from AS .xsd file.

    Currently, we only support redaction filtering. But we can extend it to
    support other features in the future.
    """
    def __init__(self, xml_file, class_suffix=None):
        ContentHandler.__init__(self)
        self.stack = ObjectStack()
        self.root = None
        self.class_suffix = class_suffix
        self.namespace = ''
        self.value = ''
        self.start_handlers = {
            u'xml': self.xml_start_handler,
            u'element_redaction': self.redaction_start_handler,
            u'attribute_redaction': self.redaction_start_handler,
            None: self.default_start_handler
        }
        self.end_handlers = {
            u'xml': self.xml_end_handler,
            u'element_redaction': self.redaction_end_handler,
            u'attribute_redaction': self.redaction_end_handler,
            None: self.default_end_handler
        }
        self.parse(xml_file)

    def xml_start_handler(self, name, attrs):
        obj = AsSchemaElement(name)
        self.root = obj
        obj.set_attr(u'element_redaction', u'none')
        obj.set_attr(u'attribute_redaction', u'none')
        assert u'namespace' in attrs
        self.namespace = attrs[u'namespace']
        if self.class_suffix is None:
            self.class_suffix = str(self.namespace)
        return obj

    def redaction_start_handler(self, name, attrs):
        obj = AsSchemaElement(name)
        return obj

    def default_start_handler(self, name, attrs):
        obj = AsSchemaElement(name)
        self.current_element().add_child(obj)
        return obj

    def xml_end_handler(self, obj, content):
        return

    def redaction_end_handler(self, obj, content):
        # Sanity check the value
        if obj.name == u'element_redaction':
            if content not in AsSchema.REDACTION_OPTIONS:
                print 'ERROR: "%s" is not a valid element redaction type.' % content
                sys.exit(1)
        self.current_element().set_attr(obj.name, content)

    def default_end_handler(self, name, attrs):
        return True

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
        output.line(2, 'public AsXmlFilter%s () : base ("%s")', self.class_suffix, self.namespace)
        output.line(2, '{')
        # Create all the variables
        for n in range(max(1, self.stack.max_depth()-1)):  # -1 for redaction tag
            output.line(3, 'NcXmlFilterNode %s = null;', AsSchemaElement.node(n))
        output.line(0, '')

        # Create the code that sets up the tree
        self.root.generate_cs(output, 0)

        output.line(3, '')
        output.line(3, 'Root = node0;')
        output.line(2, '}')
        output.line(1, '}')
        output.line(0, '}')


def process(fname, class_suffix=None, out_dir='.'):
    as_schema = AsSchema(fname, class_suffix)
    output = Output(os.path.join(out_dir, 'AsXmlFilter' + as_schema.class_suffix + '.cs'))
    as_schema.generate_xml_filter_cs(output)


def main():
    parser = ArgumentParser()
    parser.add_argument('--class-suffix',
                        help='Specify the generated C# class suffix. [Default to namespace]',
                        default=None)
    parser.add_argument('--out-dir', help='Output directory', default='.')
    parser.add_argument('xml_file', nargs=1, help='XML configuration file')
    options = parser.parse_args()
    process(fname=options.xml_file[0], class_suffix=options.class_suffix, out_dir=options.out_dir)


if __name__ == '__main__':
    main()