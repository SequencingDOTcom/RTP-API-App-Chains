using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Sequencing.AppChainsSample.SQAPI
{
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
}