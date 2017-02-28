# encoding: utf-8
from __future__ import unicode_literals

from AppChains import AppChains


class UsageExample(object):
    token = '<your token goes here>'
    url = 'api.sequencing.com'

    def __init__(self):
        self.chains = AppChains(self.token, self.url)
        #print(self.get_public_beacon_test())
        print(self.get_raw_report_test())
        self.get_report_test()
        self.get_report_batch_test()


    def get_public_beacon_test(self):
        beacon_result = self.chains.getPublicBeacon(1, 2, 'A')
        return beacon_result

    def get_raw_report_test(self):
        chains_raw_result = self.chains.getRawReport(
            'StartApp', 'Chain12', '227680')
        return chains_raw_result

    def get_report_test(self):
        chains_result = self.chains.getReport(
            'StartApp', 'Chain87', '227680')
        if chains_result.isSucceeded():
            print('Request has succeeded')
        else:
            print('Request has failed')
        for r in chains_result.getResults():
            file_type = r.getValue().getType()
            v = r.getValue()
            if file_type == 'TEXT':
                print('-> text result type {} = {}'.format(
                    r.getName(), v.getData()))
            elif file_type == 'FILE':
                print(' -> file result type {} = {}'.format(
                    r.getName(), v.getUrl()
                ))
                v.saveTo('/tmp')

    def get_report_batch_test(self):
        chains_results = self.chains.getReportBatch(
            'StartAppBatch', {'Chain85': '227680', 'Chain88': '227680'})
        for chains_result in chains_results:
            if chains_results[chains_result].isSucceeded():
                print('Request has succeeded')
            else:
                print('Request has failed')
            for r in chains_results[chains_result].getResults():
                file_type = r.getValue().getType()
                v = r.getValue()
                if file_type == 'TEXT':
                    print('-> text result type {} = {}'.format(
                        r.getName(), v.getData()))
                elif file_type == 'FILE':
                    print(' -> file result type {} = {}'.format(
                        r.getName(), v.getUrl()
                    ))
                    v.saveTo('/tmp')

UsageExample()
