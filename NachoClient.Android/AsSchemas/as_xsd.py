import os
from as_xml import AsXmlElement
from as_xml import AsXmlParser
from output import Output
from argparse import ArgumentParser


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
        self.is_group = False

    def _ref_has_namespace(self):
        assert self.is_ref
        return self.name.find(':') >= 0

    @staticmethod
    def generate_xml_start(obj, indent_level, output, root):
        if obj.is_group:
            # xs:group is a macro. Generate nothing
            return False

        if obj.is_ref:
            # This is a reference. If it is in a different namespace, ignore it
            if obj._ref_has_namespace():
                return False
            new_obj = None

            # Resolve the reference. Use only global elements as per XML schema convention
            for child in root.children:
                if child.name == obj.name:
                    new_obj = child
                    break
            assert new_obj is not None
            new_obj.walk(indent_level, AsXsdElement.generate_xml_start,
                         AsXsdElement.generate_xml_end, output, root)
            return True

        if obj.name == 'xml':
            output.line(indent_level, '<%s namespace="%s">', obj.name, root.namespace)
            return True
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
        return True

    @staticmethod
    def generate_xml_end(obj, indent_level, output, root):
        if obj.is_group:
            return
        if obj.is_ref:
            return
        output.line(indent_level, '</%s>', obj.name)


class AsXsd(AsXmlParser):
    INDENTATION = '   '

    def __init__(self, xsd_fname=None):
        AsXmlParser.__init__(self)
        self.root = None
        self.start_handlers = {
            u'xs:schema': self.schema_start_handler,
            u'xs:element': self.element_start_handler,
            u'xs:include': self.include_start_handler,
            u'xs:group': self.group_start_handler,
            None: AsXsd.default_start_handler,
        }
        self.end_handlers = {
            u'xs:schema': self.schema_end_handler,
            u'xs:element': AsXsd.element_end_handler,
            u'xs:include': self.include_end_handler,
            u'xs:group': self.group_end_handler,
            None: AsXsd.default_end_handler,
        }
        self.included = []
        self.xsd_fname = xsd_fname
        if self.xsd_fname is not None:
            self.parse(xsd_fname)

    def parse(self, xsd_fname):
        self.xsd_fname = xsd_fname
        AsXmlParser.parse(self, self.xsd_fname)

    def schema_start_handler(self, name, attrs):
        assert name == u'xs:schema'
        if self.root is None:
            self.root = AsXsdElement('xml')
            assert u'xmlns' in attrs
            self.root.namespace = attrs[u'xmlns']
        else:
            assert self.root.namespace == attrs[u'xmlns']
        return self.root

    def schema_end_handler(self, obj, content):
        return

    def _element_handler(self, name, attrs):
        if u'ref' in attrs:
            obj = AsXsdElement(attrs[u'ref'], True)
            obj.is_ref = True
        else:
            assert u'name' in attrs
            obj = AsXsdElement(attrs[u'name'], False)
        self.current_element().add_child(obj)
        return obj

    def element_start_handler(self, name, attrs):
        assert name == u'xs:element'
        return self._element_handler(name, attrs)

    @staticmethod
    def element_end_handler(obj, content):
        # XML schema does not really have value for any element.
        pass

    def include_start_handler(self, name, attrs):
        # Make sure the file exists
        assert u'schemaLocation' in attrs
        xsd_fname = os.path.join(os.path.dirname(self.xsd_fname), attrs[u'schemaLocation'])
        assert os.path.exists(xsd_fname)

        # Create a new AsXsd object and glue it into the existing one
        xsd = AsXsd(xsd_fname)
        assert xsd.root.namespace == self.root.namespace
        if xsd_fname not in self.included:
            self.included.append(xsd_fname)
            for child in xsd.root.children:
                self.root.add_child(child)

        return xsd.root

    def include_end_handler(self, obj, content):
        return

    def group_start_handler(self, name, attrs):
        assert name == u'xs:group'
        obj = self._element_handler(name, attrs)
        obj.is_group = True
        return obj

    def group_end_handler(self, obj, content):
        return

    @staticmethod
    def default_start_handler(name, attrs):
        # Do not throw exception for unhandled elements. We ignore a
        # lot of elements in XML schema.
        return None

    @staticmethod
    def default_end_handler(obj, content):
        # Do not throw exception for unhandled elements. We ignore a
        # lot of elements in XML schema.
        return

    def generate_xml(self, xml_fname):
        output = Output(indent=3)
        self.root.walk(0, AsXsdElement.generate_xml_start,
                       AsXsdElement.generate_xml_end, output, self.root)
        output.write(xml_fname)


def main():
    parser = ArgumentParser()
    parser.add_argument('--out-file', help='Output file')
    parser.add_argument('xsd_files', nargs='+', help='ActiveSync .xsd files')
    options = parser.parse_args()

    xsd = AsXsd()
    for in_fname in options.xsd_files:
        print '%s ->' % in_fname
        xsd.parse(in_fname)

    print '    -> %s' % options.out_file
    xsd.generate_xml(options.out_file)

if __name__ == '__main__':
    main()