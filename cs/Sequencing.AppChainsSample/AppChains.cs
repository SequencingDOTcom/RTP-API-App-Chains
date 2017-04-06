using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using Sequencing.AppChainsSample.SQAPI;

namespace Sequencing.AppChainsSample
{
    /// <summary>
    /// High level wrapper for accessing appchains functionality
    /// </summary>
    /// <summary>
    /// High level wrapper for accessing appchains functionality
    /// </summary>
    public class AppChains
    {
        private const string API_VERSION = "/v2/";

        /// <summary>
        /// Security token supplied by the client
        /// </summary>
        private string token;

        /// <summary>
        /// Service facade for SQAPI
        /// </summary>
        private readonly BackendServiceFacade backendFacade;

        /// <summary>
        /// Service facade for beacons
        /// </summary>
        private readonly BeaconFacade beaconFacade;

        /// <summary>
        /// Timeout to wait between tries to update Job status in seconds
        /// </summary>
        private const int DEFAULT_REPORT_RETRY_TIMEOUT = 1;

        /// <summary>
        /// Constructor that should be called in order to work  with methods that require authentication (i.e. GetReport)
        /// </summary>
        /// <param name="token">OAuth security token</param>
        /// <param name="chainsUrl">hostname to call</param>
        /// <param name="beaconsUrl"></param>
        public AppChains(string token, string chainsUrl, string beaconsUrl) : this(beaconsUrl)
        {
            backendFacade = new BackendServiceFacade(token, chainsUrl + API_VERSION);
        }

        /// <summary>
        /// Constructor that should be called in order to work with methods that doesn't require authentication (i.e. GetBeacon)
        /// </summary>
        /// <param name="beaconsUrl"></param>
        public AppChains(string beaconsUrl)
        {
            beaconFacade = new BeaconFacade(beaconsUrl);
        }

        /// <summary>
        /// High level public API. Requests report.
        /// </summary>
        /// <param name="applicationMethodName">report/application specific identifier (i.e. MelanomaDsAppv)</param>
        /// <param name="datasourceId">resource with data to use for report generation</param>
        /// <returns></returns>
        public Report GetReport(string applicationMethodName, string datasourceId)
        {
            var _startApp = backendFacade.StartApp(new AppStartParams
            {
                AppCode = applicationMethodName,
                Pars = { new NewJobParameter("dataSourceId", datasourceId) }
            });
            return GetReportImpl(_startApp);
        }

        public Dictionary<string, Report> GetReportBatch(Dictionary<string, string> appChainsParams)
        {
            List<AppStartParams> paramsList = new List<AppStartParams>(appChainsParams.Count);

            foreach (var appParameter in appChainsParams)
                paramsList.Add(new AppStartParams
                {
                    AppCode = appParameter.Key,
                    Pars = { new NewJobParameter("dataSourceId", appParameter.Value) }
                });
            var startAppBatch = backendFacade.StartAppBatch(new BatchAppStartParams() { Pars = paramsList });
            return GetReportImplBatch(startAppBatch);
        }

        /// <summary>
        /// Returns sequencing beacon
        /// </summary>
        /// <param name="chrom"></param>
        /// <param name="pos"></param>
        /// <param name="allele"></param>
        /// <returns></returns>
        public string GetSequencingBeacon(int chrom, int pos, string allele)
        {
            return beaconFacade.GetSequencingBeacon(chrom, pos, allele);
        }

        /// <summary>
        /// Returns public beacon
        /// </summary>
        /// <param name="chrom"></param>
        /// <param name="pos"></param>
        /// <param name="allele"></param>
        /// <returns></returns>
        public string GetPublicBeacon(int chrom, int pos, string allele)
        {
            return beaconFacade.GetPublicBeacon(chrom, pos, allele);
        }

        // Low level public API

        /// <summary>
        ///   Requests report in raw form it is sent from the server
        /// without parsing and transforming it
        /// </summary>
        /// <param name="remoteMethodName">REST endpoint name (i.e. StartApp)</param>
        /// <param name="applicationMethodName">report/application specific identifier (i.e. MelanomaDsAppv)</param>
        /// <param name="datasourceId">resource with data to use for report generation</param>
        /// <returns></returns>
        public AppResultsHolder GetRawReport(string applicationMethodName, string datasourceId)
        {
            var _startApp = backendFacade.StartApp(new AppStartParams
            {
                AppCode = applicationMethodName,
                Pars = { new NewJobParameter("dataSourceId", datasourceId) }
            });
            return GetRawReportImpl(_startApp);
        }

        protected Dictionary<string, Report> GetReportImplBatch(Dictionary<string, AppResultsHolder> resultHolder)
        {
            var reportList = new Dictionary<string, Report>();
            var appChainsresult = GetRawReportImplBatch(resultHolder);
            foreach (var res in appChainsresult)
                reportList[res.Key] = ProcessCompletedJob(res.Value);
            return reportList;
        }

        /// <summary>
        /// Retrieves raw report data from the API server
        /// </summary>
        /// <param name="job">job id</param>
        /// <returns></returns>
        protected Dictionary<string, AppResultsHolder> GetRawReportImplBatch(Dictionary<string, AppResultsHolder> appResult)
        {
            while (true)
            {
                try
                {
                    List<long> noneCompletedJobs = new List<long>();

                    foreach (var result in appResult)
                    {
                        if (result.Value.Status.Status == "Cancelled")
                            throw new ApplicationException("Error processing jobs");
                        if (result.Value.Status.Status != "Completed")
                            noneCompletedJobs.Add(result.Value.Status.IdJob);
                    }
                        
                    if (noneCompletedJobs.Count == 0)
                        return appResult;

                    Task.Delay(DEFAULT_REPORT_RETRY_TIMEOUT).Wait();

                    var appChainsRes = backendFacade.GetAppResultsBatch(new { JobIds = noneCompletedJobs });
                    foreach (var chainRes in appResult.ToList())
                    {
                        var updateResult = appChainsRes.Select(r => r).Where(i => i.Status.IdJob == chainRes.Value.Status.IdJob).FirstOrDefault();
                        if (!updateResult.Equals(default(KeyValuePair<string, AppResultsHolder>)))
                        {
                            var updatedEntry = new KeyValuePair<string, AppResultsHolder>(chainRes.Key, updateResult);
                            appResult[chainRes.Key] = updatedEntry.Value;
                        }
                    }
                }
                catch (Exception e)
                {
                    throw new ApplicationException(e.Message);
                }
            }
        }


        /// <summary>
        /// Retrieves report data from the API server
        /// </summary>
        /// <param name="job">identifier to retrieve report</param>
        /// <returns></returns>
        protected Report GetReportImpl(AppResultsHolder resultHolder)
        {
            return ProcessCompletedJob(GetRawReportImpl(resultHolder));
        }

        /// <summary>
        /// Retrieves raw report data from the API server
        /// </summary>
        /// <param name="job">job id</param>
        /// <returns></returns>
        protected AppResultsHolder GetRawReportImpl(AppResultsHolder appResult)
        {
            while (true)
            {
                try
                {
                    if (appResult.Status.Status == "Completed")
                        return appResult;

                    Task.Delay(DEFAULT_REPORT_RETRY_TIMEOUT).Wait();

                    appResult = backendFacade.GetAppResults(appResult.Status.IdJob);
                }
                catch (Exception e)
                {
                    throw new ApplicationException("Error processing job:" + appResult.Status.IdJob, e);
                }
            }
        }


        /// <summary>
        /// Handles raw report result by transforming it to user friendly state
        /// </summary>
        /// <param name="rawResult"></param>
        /// <returns></returns>
        protected Report ProcessCompletedJob(AppResultsHolder rawResult)
        {
            var results = new List<Result>();
            foreach (var prop in rawResult.ResultProps)
            {
                string resultPropType = prop.Type.ToLower();

                switch (resultPropType)
                {
                    case "plaintext":
                        results.Add(new Result(prop.Name, new TextResultValue(prop.Value)));
                        break;
                    case "pdf":
                        string filename = string.Format("report_{0}.{1}", rawResult.Status.IdJob, resultPropType);
                        var reportFileUrl = GetReportFileUrl(prop.Value);

                        ResultValue resultValue = new FileResultValue(filename, resultPropType, reportFileUrl);
                        results.Add(new Result(prop.Name, resultValue));
                        break;
                }
            }

            Report finalResult = new Report();
            finalResult.Succeeded = rawResult.Status.CompletedSuccesfully ?? false;
            finalResult.setResults(results);

            return finalResult;
        }

        /// <summary>
        /// Constructs URL for getting report file
	    /// </summary>
        /// <param name="fileId">file identifier</param>
        /// <returns></returns>
        protected Uri GetReportFileUrl(string fileId)
        {
            var _uri = new Uri(backendFacade.ServiceUrl);
            return new Uri(_uri, string.Format("GetReportFile?idJob={0}", fileId));
        }
    }

    /// <summary>
    /// App results data holder
    /// </summary>
    public class AppResultsHolder
    {
        private readonly List<ItemDataValue> resultProps = new List<ItemDataValue>();

        public List<ItemDataValue> ResultProps
        {
            get { return resultProps; }
        }

        /// <summary>
        /// Status of application job
        /// </summary>
        public AppStatus Status { get; set; }

        public override string ToString()
        {
            return string.Format("ResultProps: {0}, Status: {1}",
                resultProps.Aggregate("", (s, value) => s + "," + value.ToString()), Status);
        }
    }

    /// <summary>
    /// Application start parameters
    /// </summary>
    public class AppStartParams
    {
        public AppStartParams()
        {
            Pars = new List<NewJobParameter>();
        }

        public string AppCode { get; set; }
        public List<NewJobParameter> Pars { get; set; }
    }

    /// <summary>
    /// AppStatus class describe the state of the executing app job
    /// </summary>
    public class AppStatus
    {
        public long IdJob { get; set; }
        public string Status { get; set; }
        public bool? CompletedSuccesfully { get; set; }
        public DateTime? FinishDt { get; set; }

        public override string ToString()
        {
            return string.Format("IdJob: {0}, Status: {1}, CompletedSuccesfully: {2}, FinishDt: {3}", IdJob, Status, CompletedSuccesfully, FinishDt);
        }
    }

    /// <summary>
    /// Access facade for SQAPI endpoint
    /// </summary>
    public class BackendServiceFacade
    {
        private int ATTEMPTS_COUNT = 30;
        private int RETRY_TIMEOUT = 5000;
        private readonly string token;
        private readonly string serviceUrl;

        public string ServiceUrl
        {
            get { return serviceUrl; }
        }

        public BackendServiceFacade(string token, string serviceUrl)
        {
            this.token = token;
            this.serviceUrl = serviceUrl;
        }

        private T ExecuteRq<T>(RestRequest rq) where T : new()
        {
            var _cl = CreateClient();
            for (int _idx = 0; _idx < ATTEMPTS_COUNT; _idx++)
            {
                var _execute = _cl.Execute<T>(rq);
                if (_execute.StatusCode != HttpStatusCode.OK)
                {
                    Thread.Sleep(RETRY_TIMEOUT);
                    continue;
                }

                return JsonConvert.DeserializeObject<T>(_execute.Content);
            }
            throw new Exception("Unable to call service, last response was:");
        }

        private Dictionary<string, AppResultsHolder> ExecuteRqExtended(RestRequest rq)
        {
            var _cl = CreateClient();
            for (int _idx = 0; _idx < ATTEMPTS_COUNT; _idx++)
            {
                var _execute = _cl.Execute(rq);
                if (_execute.StatusCode != HttpStatusCode.OK)
                {
                    Thread.Sleep(RETRY_TIMEOUT);
                    continue;
                }
                return JArray.Parse(_execute.Content)
                    .Select(x => x.ToObject<KeyValuePair<string, AppResultsHolder>>())
                    .ToDictionary(x => x.Key, x => x.Value);
            }
            throw new Exception("Unable to call service, last response was:");
        }

        private RestRequest CreateRq(string opName, Method method)
        {
            var _restRequest = new RestRequest(opName, method);
            return _restRequest;
        }

        private RestClient CreateClient()
        {
            var _restClient = new RestClient(serviceUrl);
            _restClient.Authenticator = new OAuth2AuthorizationRequestHeaderAuthenticator(token, "Bearer");
            return _restClient;
        }

        /// <summary>
        /// Retrieves app results for previously executed appchain job
        /// </summary>
        /// <param name="idJob"></param>
        /// <returns></returns>
        public AppResultsHolder GetAppResults(long idJob)
        {
            var _restRequest = CreateRq("GetAppResults", Method.GET);
            _restRequest.AddParameter("idJob", idJob, ParameterType.QueryString);
            return ExecuteRq<AppResultsHolder>(_restRequest);
        }

        public List<AppResultsHolder> GetAppResultsBatch(dynamic idJob)
        {
            var _restRequest = CreateRq("GetAppResultsBatch", Method.POST);
            _restRequest.AddParameter(new Parameter
            {
                Name = "application/json",
                Value = JsonConvert.SerializeObject(idJob),
                Type = ParameterType.RequestBody
            });
            return ExecuteRq<List<AppResultsHolder>>(_restRequest);
        }

        /// <summary>
        /// Starts app chain execution
        /// </summary>
        /// <param name="pars">app parameters</param>
        /// <returns>appchain job id</returns>
        public AppResultsHolder StartApp(AppStartParams pars)
        {
            var _restRequest = CreateRq("StartApp", Method.POST);
            _restRequest.AddParameter(new Parameter
            {
                Name = "application/json",
                Value = JsonConvert.SerializeObject(pars),
                Type = ParameterType.RequestBody
            });
            return ExecuteRq<AppResultsHolder>(_restRequest);
        }

        public Dictionary<string, AppResultsHolder> StartAppBatch(BatchAppStartParams pars)
        {
            var restRequest = CreateRq("StartAppBatch", Method.POST);
            restRequest.AddParameter(new Parameter
            {
                Name = "application/json",
                Value = JsonConvert.SerializeObject(pars),
                Type = ParameterType.RequestBody
            });
            return ExecuteRqExtended(restRequest);
        }

        private IRestResponse ExecuteRestRq(RestRequest rq)
        {
            RestClient _restClient = CreateClient();
            while (true)
            {
                int _attemptIdx = 0;
                {
                    IRestResponse _restResponse = _restClient.Execute(rq);
                    if (_restResponse.StatusCode == HttpStatusCode.OK)
                        return _restResponse;
                    if (_attemptIdx < ATTEMPTS_COUNT)
                    {
                        Thread.Sleep(RETRY_TIMEOUT);
                        _attemptIdx++;
                    }
                    else
                        throw new Exception("Error while request to web-server:" + _restResponse.StatusCode +
                                            Environment.NewLine + _restResponse.Content);
                }
            }
        }
    }

    /// <summary>
    /// Access facade for beacon endpoints
    /// </summary>
    public class BeaconFacade
    {
        private int ATTEMPTS_COUNT = 30;
        private int RETRY_TIMEOUT = 5000;
        private readonly string serviceUrl;

        public BeaconFacade(string serviceUrl)
        {
            this.serviceUrl = serviceUrl;
        }

        private string ExecuteRq(RestRequest rq)
        {
            var _cl = CreateClient();
            for (int _idx = 0; _idx < ATTEMPTS_COUNT; _idx++)
            {
                var _execute = _cl.Execute(rq);
                if (_execute.StatusCode != HttpStatusCode.OK)
                {
                    Thread.Sleep(RETRY_TIMEOUT);
                    continue;
                }
                return _execute.Content;
            }
            throw new Exception("Unable to call service, last response was:");
        }

        private RestRequest CreateRq(string opName, Method method)
        {
            var _restRequest = new RestRequest(opName, method);
            return _restRequest;
        }

        private RestClient CreateClient()
        {
            var _restClient = new RestClient(serviceUrl);
            return _restClient;
        }

        public string GetSequencingBeacon(int chrom, int pos, string allele)
        {
            var _restRequest = CreateRq("SequencingBeacon", Method.GET);
            _restRequest.AddParameter("chrom", chrom, ParameterType.QueryString);
            _restRequest.AddParameter("pos", pos, ParameterType.QueryString);
            _restRequest.AddParameter("allele", allele, ParameterType.QueryString);
            return ExecuteRq(_restRequest);
        }

        public string GetPublicBeacon(int chrom, int pos, string allele)
        {
            var _restRequest = CreateRq("PublicBeacons", Method.GET);
            _restRequest.AddParameter("chrom", chrom, ParameterType.QueryString);
            _restRequest.AddParameter("pos", pos, ParameterType.QueryString);
            _restRequest.AddParameter("allele", allele, ParameterType.QueryString);
            return ExecuteRq(_restRequest);
        }
    }

    /// <summary>
    /// Represents result property value
    /// </summary>
    public class ItemDataValue
    {
        public string Name { get; set; }
        public string Title { get; set; }
        public string SubTitle { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
        public string SubType { get; set; }
        public string Value { get; set; }

        public override string ToString()
        {
            return string.Format("Name: {0}, Title: {1}, SubTitle: {2}, Description: {3}, Type: {4}, SubType: {5}, Value: {6}", Name, Title, SubTitle, Description, Type, SubType, Value);
        }
    }


    /// <summary>
    /// Appchain job parameter holder
    /// </summary>
    public class NewJobParameter
    {
        public NewJobParameter(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public NewJobParameter(string value)
        {
            Value = value;
        }

        public NewJobParameter(long? val)
        {
            ValueLong = val;
        }


        public NewJobParameter()
        {
        }

        public string Name { get; set; }
        public string Value { get; set; }
        public long? ValueLong { get; set; }
    }

    /// <summary>
    /// Appchain execution response
    /// </summary>
    public class StartAppRs
    {
        public long jobId { get; set; }
    }

    public class BatchAppStartParams
    {
        public BatchAppStartParams()
        {
            Pars = new List<AppStartParams>();
        }
        public List<AppStartParams> Pars { get; set; }
    }

    /// <summary>
    ///  Class that represents result entity if it's file	
    /// </summary>
    public class FileResultValue : ResultValue
    {
        private readonly string name;
        private readonly string extension;
        private readonly Uri url;

        public FileResultValue(string name, string extension, Uri url) : base(ResultType.FILE)
        {
            this.name = name;
            this.extension = extension;
            this.url = url;
        }

        public string Name
        {
            get { return name; }
        }
        
        public Uri Url
        {
            get { return url; }
        }

        public void saveTo(string token, string fullPathWithName)
        {
            var path = Path.Combine(fullPathWithName, name);
            new SqApiWebClient(token).DownloadFile(url, path);
        }

        public string getExtension()
        {
            return extension;
        }
    }

    /// <summary>
    /// Report class represents report available to the end client
    /// </summary>
    public class Report
    {
        public bool Succeeded { get; set; }
        private List<Result> results;

        public List<Result> getResults()
        {
            return results;
        }

        public void setResults(List<Result> results)
        {
            this.results = results;
        }
    }

    /// <summary>
    /// Class that represents single report result entity
    /// </summary>
    public class Result
    {
        private ResultValue value;
        private String name;

        public Result(String name, ResultValue resultValue)
        {
            this.name = name;
            this.value = resultValue;
        }

        public ResultValue getValue()
        {
            return value;
        }

        public String getName()
        {
            return name;
        }
    }

    /// <summary>
    /// Enumerates possible result entity types
    /// </summary>
    public enum ResultType
    {
        FILE,
        TEXT
    }
    /// <summary>
    /// Base class for result values
    /// </summary>
    public class ResultValue
    {
        private ResultType type;

        public ResultValue(ResultType type)
        {
            this.type = type;
        }

        public ResultType getType()
        {
            return type;
        }
    }

    /// <summary>
    /// Class that represents result entity if plain text string
    /// </summary>
    public class TextResultValue : ResultValue
    {
        private string data;

        public TextResultValue(string data) : base(ResultType.TEXT)
        {
            this.data = data;
        }

        public string Data
        {
            get { return data; }
        }
    }
}