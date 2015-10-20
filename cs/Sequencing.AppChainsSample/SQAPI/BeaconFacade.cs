using System;
using System.Net;
using System.Threading;
using RestSharp;

namespace Sequencing.AppChainsSample.SQAPI
{
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
}