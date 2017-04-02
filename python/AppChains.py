# encoding: utf-8
from __future__ import unicode_literals

import json
import time
import urllib

import urllib2


class ReportException(Exception):
    pass


class Result(object):
    """Class that represents single report result entity"""

    def __init__(self, name, result_value):
        self.name = name
        self.value = result_value

    def getValue(self):
        return self.value

    def getName(self):
        return self.name


class ResultValue(object):
    def __init__(self, type_data):
        self.type_data = type_data

    def getType(self):
        return self.type_data


class FileResultValue(ResultValue):
    """Class that represents result entity if it's file"""

    def __init__(self, chains, name, extension, url):
        super(FileResultValue, self).__init__('FILE')
        self.name = name
        self.extension = extension
        self.url = url
        self.chains = chains

    def getName(self):
        return self.name

    def getUrl(self):
        return self.url

    def getExtension(self):
        return self.extension

    def saveAs(self, full_path_with_name):
        self.chains.downloadFile(self.getUrl(), full_path_with_name)

    def saveTo(self, location):
        self.saveAs('{}/{}'.format(location, self.getName()))


class TextResultValue(ResultValue):
    """Class that represents result entity if plain text string"""

    def __init__(self, data):
        super(TextResultValue, self).__init__('TEXT')
        self.data = data

    def getData(self):
        return self.data


class Report(object):
    """Class that represents report available to the end client"""
    succeeded = None
    results = None

    def isSucceeded(self):
        return self.succeeded

    def setSucceeded(self, succeeded):
        self.succeeded = succeeded

    def getResults(self):
        return self.results

    def setResults(self, results):
        self.results = results


class Job(object):
    """Class that represents generic job identifier"""

    def __init__(self, job_id):
        self.job_id = job_id

    def getJobId(self):
        return self.job_id


class RawReportJobResult(object):
    """Class that represents unstructured job response"""
    job_id = None
    succeeded = None
    completed = None
    status = None
    source = None
    result_props = None

    def getResultProps(self):
        return self.result_props

    def setResultProps(self, result_props):
        self.result_props = result_props

    def getStatus(self):
        return self.status

    def setStatus(self, status):
        self.status = status

    def isCompleted(self):
        return self.completed

    def setCompleted(self, completed):
        self.completed = completed

    def isSucceeded(self):
        return self.succeeded

    def setSucceeded(self, succeeded):
        self.succeeded = succeeded

    def getJobId(self):
        return self.job_id

    def setJobId(self, job_id):
        self.job_id = job_id

    def getSource(self):
        return self.source

    def setSource(self, source):
        self.source = source


class HttpResponse(object):
    """Class that represents generic HTTP response"""

    def __init__(self, response_code, response_data):
        self.response_code = response_code
        self.response_data = response_data

    def getResponseCode(self):
        return self.response_code

    def getResponseData(self):
        return self.response_data


class AppChains(object):
    #: Schema to access remote API (http or https)
    DEFAULT_APPCHAINS_SCHEMA = 'https'
    #: Default hostname for Beacon requests
    BEACON_HOSTNAME = 'beacon.sequencing.com'
    #: Port to access remote API
    DEFAULT_APPCHAINS_PORT = 443
    #: Timeout to wait between tries to update Job status in seconds
    DEFAULT_REPORT_RETRY_TIMEOUT = 1
    #: Default AppChains protocol version
    PROTOCOL_VERSION = 'v2'

    #: Security token supplied by the client
    token = None
    #: Remote hostname to send requests to
    hostname = None

    def __init__(self, token=None, hostname=None):
        """
        :param token: OAuth security token
        :param hostname: hostname to call
        """
        if token:
            self.token = token
        if hostname:
            self.hostname = hostname

    #: High level public API
    def getReport(self, remote_method_name, app_method_name, source_id):
        """Requests report
        :param remote_method_name: REST endpoint name (i.e. StartApp)
        :param app_method_name: report/application specific identifier
        (i.e. MelanomaDsAppv)
        :param source_id: resource with data to use for report generation
        """
        raw_result = self.getRawJobResult(self.submitReportJob(
            remote_method_name, app_method_name, source_id
        ))
        return self.getReportImpl(raw_result)

    def getReportEx(self, remote_method_name, request_body):
        """Requests report
        :param remote_method_name: REST endpoint name (i.e. StartApp)
        :param request_body: jsonified request body to send to server
        """
        raw_result = self.getRawJobResult(self.submitReportJobImpl(remote_method_name, request_body))
        return self.getReportImpl(raw_result)

    def getReportBatch(self, remote_method_name, app_chain_param):
        """Requests report
        :param remote_method_name remote_method_name: REST endpoint name (i.e. StartAppBatch)
        :param appChainParam map of key app_method_name: report/application specific identifier
        (i.e. MelanomaDsAppv) value source_id: resource with data to use for report generation
        :return report_map of key app_method_name, value Report instance
        """
        requestBody = self.buildBatchReportRequestBody(app_chain_param)
        batchJobData = self.submitReportJobImpl(remote_method_name, requestBody)

        return  self.getBatchReportImpl(batchJobData)

    def getSequencingBeacon(self, chrom, pos, allele):
        """Returns sequencing beacon"""
        return self.getBeacon(
            'SequencingBeacon', self.getBeaconParameters(chrom, pos, allele)
        )

    def getPublicBeacon(self, chrom, pos, allele):
        """Returns public beacon"""
        return self.getBeacon(
            'PublicBeacons', self.getBeaconParameters(chrom, pos, allele)
        )

    #: Low level public API
    def getRawReport(self, remote_method_name, app_method_name, source_id):
        """Requests report in raw form it is sent from the server
        without parsing and transforming it
        :param remote_method_name: REST endpoint name (i.e. StartApp)
        :param app_method_name: report/application specific identifier
        (i.e. MelanomaDsAppv)
        :param source_id: resource with data to use for report generation
        """
        raw_result = self.getRawJobResult(self.submitReportJob(
            remote_method_name, app_method_name, source_id))
        return self.getRawReportImpl(raw_result).getSource()

    def getRawReportEx(self, remote_method_name, request_body):
        """Requests report in raw form it is sent from the server
        without parsing and transforming it
        :param remote_method_name: REST endpoint name (i.e. StartApp)
        :param request_body: jsonified request body to send to server
        """
        raw_result = self.getRawJobResult(self.submitReportJobImpl(remote_method_name, request_body))
        return self.getRawReportImpl(raw_result).getSource()

    def getBeacon(self, method_name, parameters):
        """Returns beacon
        :param method_name: REST endpoint name (i.e. PublicBeacons)
        :param parameters: map of request (GET) parameters (key->value)
        to append to the URL
        """
        return self.getBeaconEx(method_name, urllib.urlencode(parameters))

    def getBeaconEx(self, method_name, query_string):
        """Returns beacon
        :param method_name: REST endpoint name (i.e. PublicBeacons)
        :param query_string: query string
        """
        response = self.httpRequest(
            self.getBeaconUrl(method_name, query_string)
        )
        return response.getResponseData()

    #: Internal methods
    def getBeaconParameters(self, chrom, pos, allele):
        return {'chrom': chrom, 'pos': pos, 'allele': allele}

    def getReportImpl(self, raw_result):
        """Retrieves report data from the API server
        :param job: identifier to retrieve report
        :return: report
        """
        return self.processCompletedJob(self.getRawReportImpl(raw_result))

    def getBatchReportImpl(self, batchJobData):
        """Retrieves report data from the API server
        :param job: identifier to retrieve report
        :return: report
        """
        jobs = self.getBatchRawReportImpl(batchJobData)
        result = {}
        for job in jobs:
            result[job] = self.processCompletedJob(jobs[job])
        return result

    def getRawReportImpl(self, raw_result):
        """Retrieves report data from the API server
        :param job: identifier to retrieve report
        :return: report
        """
        while True:
            if raw_result.isCompleted():
                return raw_result
            time.sleep(self.DEFAULT_REPORT_RETRY_TIMEOUT)
            raw_result = self.getRawJobResult(self.getJobResponce(raw_result.getJobId()))

    def getBatchRawReportImpl(self, batchJobData):
        """Retrieves report data from the API server
        :param job: identifier to retrieve report
        :return: report
        """
        result = {}
        jobIdsPending = {}


        while True:
            for batchJobDataItem in batchJobData:
                job = self.getRawJobResult(batchJobDataItem.get('Value'))
                chainId = batchJobDataItem.get('Key')
                if job.isCompleted():
                    result[chainId] = job
                else:
                    jobIdsPending[job.getJobId()] = chainId

            if len(jobIdsPending) > 0 :
                batchJobData = self.getBatchJobResponse(jobIdsPending)
            else:
                return result

            jobIdsPending = {}
            time.sleep(self.DEFAULT_REPORT_RETRY_TIMEOUT)


    def processCompletedJob(self, raw_result):
        """Handles raw report result by transforming it to user friendly state"""
        results = []
        for result_prop in raw_result.getResultProps():
            types = result_prop.get('Type')
            result_prop_value = result_prop.get('Value')
            result_prop_name = result_prop.get('Name')
            #if not types or not result_prop_value or not result_prop_name:
            #    continue
            result_prop_type = types.lower()
            if result_prop_type == 'plaintext':
                results.append(
                    Result(
                        result_prop_name, TextResultValue(result_prop_value)))

        final_result = Report()
        final_result.setSucceeded(raw_result.isSucceeded())
        final_result.setResults(results)
        return final_result



    def buildReportRequestBody(self, application_method_name, datasource_id):
        """Builds request body used for report generation
        :param application_method_name:
        :param datasource_id:
        :return:
        """
        parameters = {'Name': 'dataSourceId', 'Value': datasource_id}
        return {'AppCode': application_method_name, 'Pars': [parameters]}

    def buildBatchReportRequestBody(self, app_chain_param):
        """Builds request body used for report generation
        :param app_chain_param application_method_name datasource_id:
        :return:
        """
        request_params = []
        for application_method_name in app_chain_param:
            request_params.append(
                self.buildReportRequestBody(application_method_name, app_chain_param[application_method_name]))
        request_body = json.dumps({'Pars': request_params})
        return request_body

    def submitReportJob(self, remote_method_name,
                        application_method_name, datasource_id):
        """Submits job to the API server
        :param remote_method_name: REST endpoint name (i.e. StartApp)
        :param application_method_name: report/application specific identifier
        (i.e. MelanomaDsAppv)
        :param datasource_id: resource with data to use for report generation
        :return: job identifier
        """
        return self.submitReportJobImpl(
            remote_method_name, json.dumps(self.buildReportRequestBody(
                application_method_name, datasource_id))
        )

    def getJobResponce(self, job_id):
        """Submits job to the API server
        :param job: job identifier
        :return: job responce
        """
        url = self.getJobResultsUrl(job_id)
        http_response = self.httpRequest(url)
        return json.loads(http_response.getResponseData())

    def getBatchJobResponse(self, jobs):
        """Submits job to the API server
        :param job: job identifier
        :return: job responce
        """

        url = self.getBatchJobResultsUrl()
        requestData = {"JobIds":list(jobs.keys())}

        httpResponse = self.httpRequest(url, json.dumps(requestData))
        decodedResponse = json.loads(httpResponse.getResponseData())
        result = [];
        for job in decodedResponse:
            jobData = {}
            jobData["Key"] = jobs[job["Status"]["IdJob"]]
            jobData["Value"] = job
            result.append(jobData)

        return result

    def submitReportJobImpl(self, remote_method_name, request_body):
        """Submits job to the API server
        :param remote_method_name: REST endpoint name (i.e. StartApp)
        :param request_body: jsonified request body to send to server
        """
        http_response = self.httpRequest(
            self.getJobSubmissionUrl(remote_method_name),
            request_body)
        response_code = http_response.getResponseCode()
        response_data = json.loads(http_response.getResponseData())

        if not response_code == 200:
            raise ReportException(
                'Appchains returned error HTTP code {} with message {}'.format(
                    response_code, response_data))
        return response_data

    def getRawJobResult(self, decoded_response):
        """Retrieves raw job results data
        :param job: job identifier
        :return: job results
        """
        result_props = decoded_response.get('ResultProps')
        status = decoded_response.get('Status')

        succeeded = False
        if status.get('CompletedSuccesfully'):
            succeeded = bool(status.get('CompletedSuccesfully'))
        job_status = status.get('Status')
        job_id = status.get("IdJob")
        result = RawReportJobResult()
        result.setSource(decoded_response)
        result.setJobId(job_id)
        result.setSucceeded(succeeded)
        result.setCompleted(
            job_status.lower() == 'completed' or job_status.lower() == 'cancelled')
        result.setResultProps(result_props)
        result.setStatus(job_status)
        return result

    def getReportFileUrl(self, file_id):
        """Constructs URL for getting report file
        :param file_id: file identifier
        :return: URL
        """
        return '{}/GetReportFile?id={}'.format(
            self.getBaseAppChainsUrl(), file_id)

    def getJobResultsUrl(self, job_id):
        """Constructs URL for getting job results
        :param job_id: job identifier
        :return: URL
        """
        return '{}/GetAppResults?idJob={}'.format(
            self.getBaseAppChainsUrl(), job_id)

    def getBatchJobResultsUrl(self):
        """Constructs URL for getting job results
        :return: URL
        """
        return '{}/{}/{}'.format(
            self.getBaseAppChainsUrl(), self.PROTOCOL_VERSION,  "GetAppResultsBatch")

    def getJobSubmissionUrl(self, application_method_name):
        """Constructs URL for job submission
        :param application_method_name: report/application specific identifier
         (i.e. MelanomaDsAppv)
        """
        return '{}/{}/{}'.format(
            self.getBaseAppChainsUrl(), self.PROTOCOL_VERSION,  application_method_name
        )

    def getBaseAppChainsUrl(self):
        """Constructs base Appchains URL"""
        return '{}://{}:{}'.format(
            self.DEFAULT_APPCHAINS_SCHEMA, self.hostname,
            self.DEFAULT_APPCHAINS_PORT)

    def getBeaconUrl(self, method_name, query_string):
        """Constructs URL for accessing beacon related remote endpoints
        :param method_name: report/application specific identifier
        (i.e. SequencingBeacon)
        :param query_string: query string
        """
        return '{}://{}:{}/{}?{}'.format(
            self.DEFAULT_APPCHAINS_SCHEMA, self.BEACON_HOSTNAME,
            self.DEFAULT_APPCHAINS_PORT, method_name, query_string
        )

    def downloadFile(self, url, file_name):
        """Downloads remote file
        :param url: URL to send request to
        :param file: path to local file to save file to
        """
        fp = open(file_name, 'w')
        self.httpRequest(url, None, fp)
        fp.close()

    def httpRequest(self, url, body=None, handler=None):
        """Executes HTTP request of the specified type
        :param url: URL to send request to
        :param body: request body (applicable for POST)
        :param curl_attributes: additional cURL attributes
        """
        headers = self.getHeaders()
        request = urllib2.Request(url=url, data=body, headers=headers)
        try:
            request = urllib2.urlopen(request)
        except urllib2.HTTPError as e:
            raise ReportException(e.msg)
        response_code = request.getcode()
        response_body = request.read()
        if handler:
            handler.write(response_body)
        return HttpResponse(response_code, response_body)

    def getHeaders(self):
        """Configures cURL handle by adding authorization headers"""
        return {'Authorization': 'Bearer {}'.format(self.token),
                'Content-Type': 'application/json'}


