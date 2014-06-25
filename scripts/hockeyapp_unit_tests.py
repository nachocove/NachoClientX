import unittest
import hockeyapp


class TestHockeyApp(unittest.TestCase):
    def setUp(self):
        self.ha_obj = hockeyapp.HockeyApp(api_token='86697957b3f54a8e86c8473e2003b810')

    @staticmethod
    def remove_nachomail_app(app_list):
        for app in app_list:
            if app.title != 'NachoMail':
                continue
            app_list.remove(app)

    def test_app(self):
        app_list = self.ha_obj.apps()
        TestHockeyApp.remove_nachomail_app(app_list)
        self.assertEqual(app_list, [])

        self.app = hockeyapp.App(self.ha_obj,
                                 title='TestApp',
                                 bundle_id='com.nachocove.testapp',
                                 platform='iOS',
                                 release_type='alpha')
        self.app.create()

        app_list = self.ha_obj.apps()
        self.remove_nachomail_app(app_list)
        self.assertEqual(len(app_list), 1)
        self.assertEqual(app_list[0], self.app)

        self.app.delete()

        app_list = self.ha_obj.apps()
        TestHockeyApp.remove_nachomail_app(app_list)
        self.assertEqual(app_list, [])

    def test_version(self):
        self.app = hockeyapp.App(self.ha_obj,
                                 title='TestApp',
                                 bundle_id='com.nachocove.testapp',
                                 platform='iOS',
                                 release_type='alpha')
        self.app.create()

        version_list = self.app.versions()
        self.assertEqual(version_list, [])

        self.version = hockeyapp.Version(self.app,
                                         version='4.1',
                                         short_version='123')
        self.version.create()

        version_list = self.app.versions()
        self.assertEqual(len(version_list), 1)
        self.assertEqual(version_list[0], self.version)

        self.version.delete()

        version_list = self.app.versions()
        self.assertEqual(version_list, [])

        self.app.delete()

if __name__ == '__main__':
    unittest.main()