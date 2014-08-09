#!/usr/bin/env python

import sys
import xml.etree.ElementTree


class StoryboardScene:
    def __init__(self, root):
        self.root = root
        # Find the view controller
        self.objects = self.root.find('objects')
        self.vc = self.find_view_controller()

    def find_view_controller(self):
        for obj in self.objects:
            if obj.tag == 'viewController':
                return obj
            if obj.tag.endswith('ViewController'):
                return obj
        return None

    def vc_name(self):
        if self.vc is None or 'customClass' not in self.vc.attrib:
            return '[Scene %s]' % self.root.attrib['sceneID']
        return self.vc.attrib['customClass']

    def check(self, tags):
        num_errors = 0
        vc_name = self.vc_name()
        for tag in tags:
            for element in self.objects.iter(tag):
                label = self.get_accessibility_label(element)
                if label is None:
                    print 'ERROR: <%s> %s.%s' % (tag, vc_name, element.attrib['id'])
                    num_errors += 1
                else:
                    print '<%s> %s.%s: %s' % (tag, vc_name, element.attrib['id'], label)
        return num_errors

    @staticmethod
    def get_accessibility_label(element):
        accessibility = element.find('accessibility')
        if accessibility is None:
            return None
        return accessibility.attrib.get('label', None)


def main():
    ui_object_tags = ('button', )
    root = xml.etree.ElementTree.parse(sys.argv[1])
    scenes = root.find('scenes')
    num_errors = 0
    for scene in scenes.iter('scene'):
        scene_obj = StoryboardScene(scene)
        num_errors += scene_obj.check(ui_object_tags)
    if num_errors > 0:
        exit(1)

if __name__ == '__main__':
    main()