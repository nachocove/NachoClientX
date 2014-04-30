import sys
import os
from as_xml import AsXmlElement
from as_xml import AsXmlParser
from output import Output


class AsXsdOutput:
    """
    A class for handling output the generated XML file.
    """
    INDENTATION = '   '

    def __init__(self):
        self.output = ''

    def line(self, indent_level, fmt, *params):
        indent = indent_level * AsXsdOutput.INDENTATION
        self.output += indent + fmt % tuple(params) + '\n'

    def write_xml(self, fname):
        with open(fname, 'w') as f:
            f.write(self.output)
            f.close()


class AsXsdElement(AsXmlElement):
    def __init__(self, name, is_ref=False):
        AsXmlElement.__init__(self, name)
        assert isinstance(is_ref, bool)
        self.is_ref = is_ref

    def _ref_has_namespace(self):
        assert self.is_ref
        return self.name.find(':') >= 0

    @staticmethod
    def generate_xml_start(obj, indent_level, output, root):
        if obj.is_ref:
            # This is a reference. If it is in a different namespace, ignore it
            if obj._ref_has_namespace():
                return
            new_obj = None

            # Resolve the reference. Use only global elements as per XML schema convention
            for child in root.children:
                if child.name == obj.name:
                    new_obj = child
                    break
            assert new_obj is not None
            new_obj.walk(indent_level, AsXsdElement.generate_xml_start,
                         AsXsdElement.generate_xml_end, output, root)
            return

        if obj.name == 'xml':
            output.line(indent_level, '<%s namespace="%s">', obj.name, root.namespace)
            return
        output.line(indent_level, '<%s>', obj.name)
        if obj.has_children():
            # Complex type is never redacted
            element_redaction = 'none'
            attribute_redaction = 'none'
        else:
            element_redaction = 'full'
            attribute_redaction = 'full'
        output.line(indent_level+1, '<element_redaction>%s</element_redaction>', element_redaction)
        output.line(indent_level+1, '<attribute_redaction>%s</attribute_redaction>', attribute_redaction)

    @staticmethod
    def generate_xml_end(obj, indent_level, output, root):
        if obj.is_ref:
            return
        output.line(indent_level, '</%s>', obj.name)


class AsXsd(AsXmlParser):
    INDENTATION = '   '

    def __init__(self, xsd_fname):
        AsXmlParser.__init__(self)
        self.root = None
        self.start_handlers = {
            u'xs:schema': self.schema_start_handler,
            u'xs:element': self.element_start_handler,
            None: AsXsd.default_start_handler,
        }
        self.end_handlers = {
            u'xs:schema': self.schema_end_handler,
            u'xs:element': AsXsd.element_end_handler,
            None: AsXsd.default_end_handler,
        }
        self.parse(xsd_fname)

    def schema_start_handler(self, name, attrs):
        assert name == u'xs:schema'
        self.root = AsXsdElement('xml')
        assert u'xmlns' in attrs
        self.root.namespace = attrs[u'xmlns']
        return self.root

    def schema_end_handler(self, obj, content):
        return

    def element_start_handler(self, name, attrs):
        assert name == u'xs:element'
        if u'ref' in attrs:
            obj = AsXsdElement(attrs[u'ref'], True)
            obj.is_ref = True
        else:
            assert u'name' in attrs
            obj = AsXsdElement(attrs[u'name'], False)
        self.current_element().add_child(obj)
        return obj

    @staticmethod
    def element_end_handler(obj, content):
        # XML schema does not really have value for any element.
        pass

    @staticmethod
    def default_start_handler(name, attrs):
        # Do not throw exception for unhandled elements. We ignore a
        # lot of elements in XML schema.
        return None

    @staticmethod
    def default_end_handler(name, content):
        # Do not throw exception for unhandled elements. We ignore a
        # lot of elements in XML schema.
        return

    def generate_xml(self, xml_fname):
        output = Output(indent=3)
        self.root.walk(0, AsXsdElement.generate_xml_start,
                       AsXsdElement.generate_xml_end, output, self.root)
        output.write(xml_fname)


def main():
    for in_fname in sys.argv[1:]:
        (base, ext) = os.path.splitext(in_fname)
        out_fname = base + '.xml'
        print '%s -> %s' % (in_fname, out_fname)
        AsXsd(in_fname).generate_xml(out_fname)

if __name__ == '__main__':
    main()