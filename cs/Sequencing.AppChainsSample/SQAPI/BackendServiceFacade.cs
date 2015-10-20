using System;
using System.Net;
using System.Threading;
using Newtonsoft.Json;
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
        
        /// <summary>
        /// Starts app chain execution
        /// </summary>
        /// <param name="pars">app parameters</param>
        /// <returns>appchain job id</returns>
        public long StartApp(AppStartParams pars)
        {
            var _restRequest = CreateRq("StartApp", Method.POST);
            _restRequest.AddParameter(new Parameter
                                      {
                                          Name = "application/json",
                                          Value = JsonConvert.SerializeObject(pars),
                                          Type = ParameterType.RequestBody
                                      });
            var _startAppRs = ExecuteRq<StartAppRs>(_restRequest);
            return _startAppRs.jobId;
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