using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sequencing.AppChainsSample.SQAPI;

namespace Sequencing.AppChainsSample
{
    /// <summary>
    /// High level wrapper for accessing appchains functionality
    /// </summary>
    public class AppChains
    {
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
            backendFacade = new BackendServiceFacade(token, chainsUrl);
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

            Dictionary<string, Report> reportAppResults = new Dictionary<string, Report>(startAppBatch.Count);
            var reportsList = GetReportImplBatch(startAppBatch.Values.ToList());

            for (int i = 0; i < startAppBatch.Count; i++)  
                reportAppResults[startAppBatch.ElementAt(i).Key] = reportsList[i];

            return reportAppResults;
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

        protected List<Report> GetReportImplBatch(List<AppResultsHolder> resultHolder)
        {
            var reportList = new List<Report>();
            var appChainsresult = GetRawReportImplBatch(resultHolder);
            foreach (var res in appChainsresult)
            {
                reportList.Add(ProcessCompletedJob(res));
            }
            return reportList;
        }

        /// <summary>
        /// Retrieves raw report data from the API server
        /// </summary>
        /// <param name="job">job id</param>
        /// <returns></returns>
        protected List<AppResultsHolder> GetRawReportImplBatch(List<AppResultsHolder> appResult)
        {
            while (true)
            {
                try
                {
                    List<long> noneCompletedJobs = new List<long>();

                    foreach (var result in appResult)
                        if(result.Status.Status != "Completed")
                            noneCompletedJobs.Add(result.Status.IdJob);

                    if (noneCompletedJobs.Count == 0)
                        return appResult;

                    Task.Delay(DEFAULT_REPORT_RETRY_TIMEOUT).Wait();

                    var appChainsRes = backendFacade.GetAppResultsBatch(new { JobIds = noneCompletedJobs });
                    for (int k = 0; k < appResult.Count; k++)
                    {
                        var updateResult = appChainsRes.Select(r => r).Where(i => i.Status.IdJob == appResult[k].Status.IdJob).FirstOrDefault();
                        if (updateResult != null)
                            appResult[k] = updateResult;
                    }
 
                    //appResult.Select(r =>r.Status.IdJob).ToList().Contains(res.Select(i =>i.Status.IdJob));
                    //var jobsResult = appResult.Where(r => r.Status.IdJob != res.Contains());
                }
                catch (Exception e)
                {
                    throw new ApplicationException("Error processing jobs", e);
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
            return new Uri(_uri, string.Format("/GetReportFile?idJob={0}", fileId));
        }
    }
}