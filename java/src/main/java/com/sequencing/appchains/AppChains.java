package com.sequencing.appchains;

import java.io.*;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.nio.file.StandardCopyOption;
import java.util.*;
import java.util.Map.Entry;
import java.util.concurrent.TimeUnit;

import com.google.gson.GsonBuilder;
import com.google.gson.reflect.TypeToken;

public class AppChains
{
	/**
	 * Security token supplied by the client
	 */
	private String token;
	
	/**
	 * Remote hostname to send requests to
	 */
	private String chainsHostname;
	
	/**
	 * Schema to access remote API (http or https)
	 */
	private final static String DEFAULT_APPCHAINS_SCHEMA = "https";
	
	/**
	 * Port to access remote API
	 */
	private final static int DEFAULT_APPCHAINS_PORT = 443;
	
	/**
	 * Timeout to wait between tries to update Job status in seconds
	 */
	private final static int DEFAULT_REPORT_RETRY_TIMEOUT = 1;
	
	/**
	 * Default hostname for Beacon requests
	 */
	private final static String BEACON_HOSTNAME = "beacon.sequencing.com";

	/**
	 * Default AppChains protocol version
	 */
	private final static String PROTOCOL_VERSION = "v2";

	/**
	 * Constructor that should be called in order to work
	 * with methods that require authentication (i.e. getReport)
	 * @param token OAuth security token
	 * @param chainsHostname hostname to call
	 */
	public AppChains(String token, String chainsHostname)
	{
		this(chainsHostname);
		this.token = token;
	}
	
	/**
	 * Constructor that should be called in order to work
	 * with methods that doesn't require authentication (i.e. getBeacon)
	 * @param chainsHostname
	 */
	public AppChains(String chainsHostname)
	{
		this.chainsHostname = chainsHostname;
	}
	
	// High level public API
	
	/**
	 * Requests report
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
	 * @param datasourceId resource with data to use for report generation
	 * @return
	 */
	public Report getReport(String remoteMethodName, String applicationMethodName, String datasourceId)
	{
		return getReportImpl("POST", remoteMethodName, toJson(buildReportRequestBody(applicationMethodName, datasourceId)));
	}
	
	/**
	 * Requests report
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param requestBody jsonified request body to send to server
	 * @return
	 */
	public Report getReport(String remoteMethodName, String requestBody)
	{
		return getReportImpl("POST", remoteMethodName, requestBody);
	}

	public Map<String, Report> getReportBatch(String remoteMethodName, Map<String, String> appChainsParams)
	{
		List<Map<String, Object>> paramsList = new ArrayList<Map<String, Object>>();

		for (Map.Entry<String, String> appParameter : appChainsParams.entrySet()) {
			paramsList.add(buildReportRequestBody(appParameter.getKey(), appParameter.getValue()));
		}

		HttpResponse httpResponse = httpRequest("POST", getJobSubmissionUrl(remoteMethodName),  "{\"Pars\":" + toJson(paramsList) + "}");
		List<Map<String, Object>> decodedResponse = (List<Map<String, Object>>) fromJson(httpResponse.responseData);
		List<RawReportJobResult> rawReportJobResultList = getRawJobResultBatch(decodedResponse);
		rawReportJobResultList = getRawReportImpl(rawReportJobResultList);
		List<Report> reportList = getReportImplBatch(rawReportJobResultList);
		Map<String, Report> reportMap = new HashMap<String, Report>();

		for (int i = 0; i < decodedResponse.size(); i++) {
			reportMap.put((String) decodedResponse.get(i).get("Key"), reportList.get(i));
		}
		return reportMap;
	}

	private List<Report> getReportImplBatch(List<RawReportJobResult> rawReportJobResultList) {
		List<Report> reportList = new ArrayList<Report>();
		for(RawReportJobResult reportJobResult:rawReportJobResultList){
			reportList.add(processCompletedJob(reportJobResult));
		}
		return reportList;
	}

	/**
	 * Returns sequencing beacon
	 * @return
	 */
	public String getSequencingBeacon(int chrom, int pos, String allele)
	{
		return getBeacon("SequencingBeacon", getBeaconParameters(chrom, pos, allele));
	}

	/**
	 * Returns public beacon
	 * @return
	 */
	public String getPublicBeacon(int chrom, int pos, String allele)
	{
		return getBeacon("PublicBeacons", getBeaconParameters(chrom, pos, allele));
	}
	
	// Low level public API
	
	/**
	 * Requests report in raw form it is sent from the server
	 * without parsing and transforming it
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
	 * @param datasourceId resource with data to use for report generation
	 * @return
	 */
	public Map<String, Object> getRawReport(String remoteMethodName, String applicationMethodName, String datasourceId)
	{
		return getRawReportImpl("POST", remoteMethodName, toJson(buildReportRequestBody(applicationMethodName, datasourceId))).getSource();
	}
	
	/**
	 * Requests report in raw form it is sent from the server
	 * without parsing and transforming it
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param requestBody jsonified request body to send to server
	 * @return
	 */
	public Map<String, Object> getRawReport(String remoteMethodName, String requestBody)
	{
		return getRawReportImpl("POST", remoteMethodName, requestBody).getSource();
	}
	
	/**
	 * Returns beacon
	 * @param methodName REST endpoint name (i.e. PublicBeacons)
	 * @param parameters map of request (GET) parameters (key->value) to append to the URL
	 * @return
	 */
	public String getBeacon(String methodName, Map<String, String> parameters)
	{
		return getBeacon(methodName, getRequestString(parameters));
	}

	/**
	 * Returns beacon
	 * @param methodName REST endpoint name (i.e. PublicBeacons)
	 * @param queryString query string
	 * @return
	 */
	public String getBeacon(String methodName, String queryString)
	{
		HttpResponse response = httpRequest("GET", getBeaconUrl(methodName, queryString), "");
		return response.getResponseData();
	}

	// Internal methods

	private Map<String, String> getBeaconParameters(int chrom, int pos, String allele)
	{
		Map<String, String> parameters = new HashMap<String, String>();
		parameters.put("chrom", String.valueOf(chrom));
		parameters.put("pos", String.valueOf(pos));
		parameters.put("allele", String.valueOf(allele));
		return parameters;
	}
	
	/**
	 * Builds request body used for report generation
	 * @param applicationMethodName
	 * @param datasourceId
	 * @return
	 */
	protected Map<String, Object> buildReportRequestBody(String applicationMethodName, String datasourceId)
	{
		Map<String, Object> data = new HashMap<String, Object>(2);
		Map<String, String> parameters = new HashMap<String, String>(2);

		parameters.put("Name", "dataSourceId");
		parameters.put("Value", datasourceId);

		data.put("AppCode", applicationMethodName);
		data.put("Pars", Arrays.asList(parameters));

		return data;
	}

	/**
	 * Deserializes json
	 * @param data string with json data
	 * @return 
	 */
	protected Object fromJson(String data)
	{
		return new GsonBuilder().create().fromJson(data, new TypeToken<Object>(){}.getType());
	}
	
	/**
	 * Serializes object graph to json string
	 * @param data source object to serialize
	 * @return serialized data
	 */
	protected String toJson(Object data)
	{
		return new GsonBuilder().create().toJson(data);
	}

	/**
	 * Reads data from HTTP stream
	 * @param stream remote HTTP stream
	 * @return data read from the stream
	 */
	protected String getServerResponse(InputStream stream)
	{
		Scanner s = null;

		try
		{
			s = new Scanner(stream).useDelimiter("\\A");
			return s.hasNext() ? s.next() : "";
		}
		finally
		{
			s.close();
		}
	}
	
	/**
	 * Retrieves report data from the API server
	 * @param  rawResult RawReportJobResult
	 * @return report
	 */
	protected Report getReportImpl(RawReportJobResult rawResult)
	{
		return processCompletedJob(getRawReportImpl(rawResult));
	}
	/**
	 * Retrieves report data from the API server
	 * @param httpMethod httpMethod HTTP method to access API server
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param requestBody jsonified request body to send to server
	 * @return report
	 */
	protected Report getReportImpl(String httpMethod, String remoteMethodName, String requestBody)
	{
		return processCompletedJob(getRawReportImpl(httpMethod, remoteMethodName, requestBody));
	}

	/**
	 * Retrieves report data from the API server
	 * @param httpMethod httpMethod HTTP method to access API server
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param requestBody jsonified request body to send to server
	 * @return report
	 */
	protected RawReportJobResult getRawReportImpl(String httpMethod, String remoteMethodName, String requestBody)
	{
		HttpResponse httpResponse = httpRequest(httpMethod, getJobSubmissionUrl(remoteMethodName), requestBody);
		Map<String, Object> decodedResponse = (Map<String, Object>) fromJson(httpResponse.responseData);
		RawReportJobResult rawReportJobResult = getRawJobResult(decodedResponse);
		return getRawReportImpl(rawReportJobResult);
	}

	/**
	 * Retrieves raw report data from the API server
	 * @param rawResult RawReportJobResult
	 * @return report
	 */
	protected RawReportJobResult getRawReportImpl(RawReportJobResult rawResult)
	{
		while (true)
		{
			try
			{
				if (rawResult.isCompleted())
				{
					return rawResult;
				}
				
				TimeUnit.SECONDS.sleep(DEFAULT_REPORT_RETRY_TIMEOUT);

				rawResult = getRawJobResult(rawResult.getJobId());
			}
			catch (Exception e)
			{
				throw new RuntimeException(String.format(
						"Error processing job: %s", rawResult.getJobId(), e.getMessage()) , e);
			}
		}
	}

	/**
	 * Retrieves raw report data from the API server
	 * @param rawResultList RawReportJobResult list
	 * @return report
	 */
	protected List<RawReportJobResult> getRawReportImpl(List<RawReportJobResult> rawResultList)
	{
		while (true)
		{
			try
			{
				List<Integer> noneCompletedJobs = new ArrayList<Integer>();

				for(RawReportJobResult rawResult : rawResultList) {
					if (!rawResult.isCompleted()) {
						noneCompletedJobs.add(rawResult.getJobId());
					}
				}

				if(noneCompletedJobs.size() == 0){
					return rawResultList;
				}

				TimeUnit.SECONDS.sleep(DEFAULT_REPORT_RETRY_TIMEOUT);

				List<RawReportJobResult> updatedResList= getRawJobResultBatchList(noneCompletedJobs);
				for(RawReportJobResult updatedRes : updatedResList){
					if (updatedRes.isCompleted())
						for(RawReportJobResult rawResult : rawResultList)
							if(rawResult.getJobId().equals(updatedRes.getJobId()))
								rawResultList.set(rawResultList.indexOf(rawResult), updatedRes);
				}
			}
			catch (Exception e)
			{
				throw new RuntimeException("Error processing jobs" , e);
			}
		}
	}

	/**
	 * Handles raw report result by transforming it to user friendly state
	 * @param rawResult
	 * @return
	 */
	protected Report processCompletedJob(RawReportJobResult rawResult)
	{
		List<Result> results = new ArrayList<Result>(rawResult.getResultProps().size());
		
		for (Map<String, Object> resultProp : rawResult.getResultProps())
		{
			Object type = resultProp.get("Type"),
				   value = resultProp.get("Value"),
				   name = resultProp.get("Name");

			/*if (type == null || value == null || name == null)
				continue;*/

			String resultPropType = type.toString().toLowerCase(),
				   resultPropValue = (String) value,
				   resultPropName = (String) name;


			
			if (resultPropType.equals("plaintext"))
			{
					results.add(new Result(resultPropName, new TextResultValue(resultPropValue)));
			} 
			else if (resultPropType.equals("pdf"))
			{
					String filename = String.format("report_%d.%s", rawResult.getJobId(), resultPropType);
					URL reportFileUrl = getReportFileUrl(Integer.valueOf(resultPropValue));
					
					ResultValue resultValue = new FileResultValue(filename, resultPropType, reportFileUrl);
					results.add(new Result(resultPropName, resultValue));
			}
		}
		
		Report finalResult = new Report();
		finalResult.setSucceeded(rawResult.isSucceeded());
		finalResult.setResults(results);
		
		return finalResult;
	}

	/**
	 * Retrieves raw job results data
	 * @param jobId job id
	 * @return raw job results
	 */
	protected RawReportJobResult getRawJobResult(int jobId)
	{
		URL url = getJobResultsUrl(jobId);
		HttpResponse httpResponse = httpRequest("GET", url, "");
		Map<String, Object> decodedResponse = (Map<String, Object>) fromJson(httpResponse.responseData);

		return getRawJobResult(decodedResponse);
	}

	/**
	 * Retrieves raw job results data
	 * @param noneCompletedJobs job id
	 * @return raw job results
	 */
	private List<RawReportJobResult> getRawJobResultBatchList(List<Integer> noneCompletedJobs) {
		Map<String, List<Integer>> request = new HashMap<String, List<Integer>>();
		request.put("JobIds", noneCompletedJobs);
		String requestBody = toJson(request);
		HttpResponse httpResponse = httpRequest("POST", getJobSubmissionUrl("GetAppResultsBatch"), requestBody);
		List<Map<String, Object>> decodedResponse = (List<Map<String, Object>>) fromJson(httpResponse.responseData);
		List<RawReportJobResult> rawReportList = new ArrayList<RawReportJobResult>();
		for(Map<String, Object> response : decodedResponse){
			rawReportList.add(getRawJobResult(response));
		}
		return  rawReportList;
	}

	/**
	 * Retrieves raw job results data
	 * @param decodedResponse decoded response
	 * @return list raw job results
	 */
	protected List<RawReportJobResult> getRawJobResultBatch(List<Map<String, Object>> decodedResponse) {
		List<RawReportJobResult> rawReportList = new ArrayList<RawReportJobResult>();
		for(Map<String, Object> response : decodedResponse){
			rawReportList.add(getRawJobResult((Map<String, Object>)response.get("Value")));
		}
		return rawReportList;
	}
	/**
	 * Retrieves raw job results data
	 * @param decodedResponse decoded response
	 * @return raw job results
	 */
	@SuppressWarnings("unchecked")
	protected RawReportJobResult getRawJobResult(Map<String, Object> decodedResponse)
	{
		List<Map<String, Object>> resultProps = (List<Map<String, Object>>) decodedResponse.get("ResultProps");
		Map<String, Object> status = (Map<String, Object>) decodedResponse.get("Status");

		boolean succeeded;

		if (status.get("CompletedSuccesfully") == null)
			succeeded = false;
		else
			succeeded = (Boolean) status.get("CompletedSuccesfully");
		String jobStatus = status.get("Status").toString();
		int jobId;
		try {
			jobId = Float.valueOf(status.get("IdJob").toString()).intValue();
		} catch (Exception e) {
			throw new RuntimeException("Appchains returned invalid job identifier");
		}

		RawReportJobResult result = new RawReportJobResult();
		result.setSource(decodedResponse);
		result.setJobId(jobId);
		result.setSucceeded(succeeded);
		result.setCompleted(jobStatus.equalsIgnoreCase("completed") || jobStatus.equalsIgnoreCase("failed"));
		result.setResultProps(resultProps);
		result.setStatus(jobStatus);
		
		return result;
	}
	
	/**
	 * Submits job to the API server
	 * @param httpMethod HTTP method to access API server
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
	 * @param datasourceId resource with data to use for report generation
	 * @return job identifier
	 */
	protected Job submitReportJob(String httpMethod, String remoteMethodName, String applicationMethodName, String datasourceId)
	{
		return submitReportJob(httpMethod, remoteMethodName, toJson(buildReportRequestBody(applicationMethodName, datasourceId)));
	}

	/**
	 * Submits job to the API server
	 * @param httpMethod httpMethod HTTP method to access API server
	 * @param remoteMethodName REST endpoint name (i.e. StartApp)
	 * @param requestBody jsonified request body to send to server
	 * @return
	 */
	protected Job submitReportJob(String httpMethod, String remoteMethodName, String requestBody)
	{
		HttpResponse httpResponse = httpRequest(httpMethod, getJobSubmissionUrl(remoteMethodName), requestBody);
		
		if (httpResponse.getResponseCode() != 200)
			throw new RuntimeException(String.format("Appchains returned error HTTP code %d with message %s",
					httpResponse.getResponseCode(), httpResponse.getResponseData()));
		
		Map<String, Object> parsedResponse = (Map<String, Object>) fromJson(httpResponse.getResponseData());
		Integer jobId = null;
		
		try {
			jobId = Float.valueOf(parsedResponse.get("jobId").toString()).intValue();
		} catch (Exception e) {
			throw new RuntimeException("Appchains returned invalid job identifier");
		}
		
		return new Job(jobId);
	}

	
	/**
	 * Constructs URL for getting report file
	 * @param fileId file identifier
	 * @return URL
	 */
	protected URL getReportFileUrl(Integer fileId)
	{
		return getBaseAppChainsUrl(String.format("/%s/GetReportFile?idJob=%d", PROTOCOL_VERSION, fileId));
	}

	/**
	 * Constructs URL for getting job results
	 * @param jobId job identifier
	 * @return URL
	 */
	protected URL getJobResultsUrl(Integer jobId)
	{
		return getBaseAppChainsUrl(String.format("/GetAppResults?idJob=%d", jobId));
	}

	protected URL getAppChainsUrlWithVersion (String context) {
		return getBaseAppChainsUrl(String.format("/%s/%s", PROTOCOL_VERSION, context));
	}
	/**
	 * Constructs base URL for accessing sequencing backend
	 * @param context context identifier
	 * @return URL
	 */
	protected URL getBaseAppChainsUrl(String context)
	{
		URL remoteUrl = null;

		try
		{
			remoteUrl = new URL(DEFAULT_APPCHAINS_SCHEMA, this.chainsHostname, DEFAULT_APPCHAINS_PORT,
					context);
		}
		catch (Exception e)
		{
			throw new RuntimeException(String.format(
					"Invalid Appchains base AppChains URL %s", remoteUrl == null ? null : remoteUrl.toString()), e);
		}

		return remoteUrl;
	}
	
	/**
	 * Constructs URL for job submission
	 * @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
	 * @return
	 */
	protected URL getJobSubmissionUrl(String applicationMethodName)
	{
		return getAppChainsUrlWithVersion(applicationMethodName);
	}

	/**
	 * Constructs URL for accessing beacon related remote endpoints
	 * @param methodName report/application specific identifier (i.e. SequencingBeacon)
	 * @param queryString query string
	 * @return
	 */
	protected URL getBeaconUrl(String methodName, String queryString)
	{
		URL remoteUrl = null;

		try
		{
			remoteUrl = new URL(DEFAULT_APPCHAINS_SCHEMA, BEACON_HOSTNAME,
					DEFAULT_APPCHAINS_PORT,
					String.format("/%s/?%s", methodName, queryString));
		}
		catch (Exception e)
		{
			throw new RuntimeException(String.format(
					"Invalid Appchains job submission URL %s", remoteUrl == null ? null : remoteUrl.toString()), e);
		}

		return remoteUrl;
	}
	
	/**
	 * Generates query string by map of key=value pairs
	 * @param urlParameters map of key-value pairs
	 * @return
	 */
	protected String getRequestString(Map<String, String> urlParameters)
	{
		StringBuilder request = new StringBuilder();
		
		for (Entry<String, String> e : urlParameters.entrySet())
		{
			if (request.length() > 0)
				request.append("&");
			
			request.append(String.format("%s=%s", 
					urlEncode(e.getKey()), urlEncode(e.getValue())));
		}
		
		return request.toString();
	}
	
	/**
	 * URL encodes given string
	 * @param v source string to url encode
	 * @return
	 */
	private String urlEncode(String v)
	{
		try
		{
			return URLEncoder.encode(v, "UTF-8");
		}
		catch (Exception e)
		{
			return "";
		}
	}
	
	/**
	 * Opens and returns HTTP connection object using POST method
	 * @param url URL to send request to
	 * @param body request body (applicable for POST)
	 * @return HttpURLConnection instance
	 */
	protected HttpURLConnection openHttpPostConnection(URL url, String body)
	{
		HttpURLConnection connection;

		try
		{
			connection = openBaseOauthSecuredHttpConnection("POST", url);
			connection.setRequestProperty("Content-Length", String.valueOf(body.length()));
			connection.setRequestProperty("Content-Type", "application/json");
			connection.getOutputStream().write(body.getBytes());
		}
		catch (Exception e)
		{
			throw new RuntimeException(String.format(
					"Unable to connect to Appchains server: %s", e.getMessage()) ,e);
		}
		
		return connection;
	}
	
	/**
	 * Opens and returns HTTP connection object using GET method
	 * @param url URL to send request to
	 * @return HttpURLConnection instance
	 */
	protected HttpURLConnection openHttpGetConnection(URL url)
	{
		HttpURLConnection connection;

		try
		{
			connection = openBaseOauthSecuredHttpConnection("GET", url);
		}
		catch (Exception e)
		{
			throw new RuntimeException(String.format(
					"Unable to connect to Appchains server: %s", e.getMessage()) ,e);
		}
		
		return connection;
	}
	
	/**
	 * Opens and returns HTTP connection object
	 * @param method HTTP method to use
	 * @param url URL to send request to
	 * @return HttpURLConnection instance
	 * @throws IOException
	 */
	protected HttpURLConnection openBaseOauthSecuredHttpConnection(String method, URL url) throws IOException
	{
		HttpURLConnection connection = (HttpURLConnection) url.openConnection();
		connection.setRequestMethod(method);
		connection.setDoOutput(true);
		connection.setDoInput(true);
		connection.setRequestProperty("Authorization", String.format("Bearer %s", token));
		
		return connection;
	}
	
	/**
	 * Executes HTTP request of the specified type
	 * @param method HTTP method (GET/POST)
	 * @param url URL to send request to
	 * @param body request body (applicable for POST)
	 * @return
	 */
	protected HttpResponse httpRequest(String method, URL url, String body)
	{
		HttpURLConnection connection;
		
		if (method.equalsIgnoreCase("post"))
				connection = openHttpPostConnection(url, body);
		else if (method.equalsIgnoreCase("get"))
				connection = openHttpGetConnection(url);
		else
				throw new UnsupportedOperationException(String.format("HTTP method %s is not supported", method));
		
		Integer responseCode = 0;
		
		try
		{
			responseCode = connection.getResponseCode();
			String response = getServerResponse(connection.getInputStream());
			
			return new HttpResponse(responseCode, response);
		}
		catch (Exception e)
		{
			throw new RuntimeException(String.format(
					"Unable to read response from the Appchains server: %s", e.getMessage()), e);
		}
		finally
		{
			connection.disconnect();
		}
	}
	
	/**
	 * Checks whether specified character sequence represents a number
	 * @param data source data to check
	 * @return
	 */
	protected boolean isNumeric(String data)
	{
		return data.matches("^[0-9]+$");
	}

	/**
	 * Enumerates possible result entity types
	 */
	public enum ResultType
	{
		FILE, TEXT
	}
	
	class ResultValue
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
	
	/**
	 * Class that represents result entity if plain text string
	 */
	class TextResultValue extends ResultValue
	{
		private String data;

		public TextResultValue(String data)
		{
			super(ResultType.TEXT);
			this.data = data;
		}
		
		public String getData()
		{
			return data;
		}
	}
	
	/**
	 * Class that represents result entity if it's file
	 */
	class FileResultValue extends ResultValue
	{
		private String name;
		private String extension;
		private URL url;
		
		public FileResultValue(String name, String extension, URL url)
		{
			super(ResultType.FILE);
			
			this.name = name;
			this.extension = extension;
			this.url = url;
		}
		
		public String getName()
		{
			return name;
		}

		public URL getUrl()
		{
			return url;
		}

		public InputStream getStream() throws IOException
		{
			return openHttpGetConnection(url).getInputStream();
		}
		
		public void saveAs(String fullPathWithName) throws IOException {
			try {


			Path parentDir = (new File(fullPathWithName)).toPath().getParent();
			if (!Files.exists(parentDir))
				Files.createDirectories(parentDir);
			Files.copy(getStream(), Paths.get(fullPathWithName), StandardCopyOption.REPLACE_EXISTING);

			} catch (Exception e) {}
		}
		
		public void saveTo(String location) throws IOException
		{

			saveAs(String.format("%s/%s", location, getName()));
		}

		public String getExtension()
		{
			return extension;
		}
	}
	
	/**
	 * Class that represents single report result entity
	 */
	class Result
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
	
	/**
	 * Class that represents report available to
	 * the end client
	 */
	public class Report
	{
		private boolean succeeded;
		private List<Result> results;
		
		public boolean isSucceeded()
		{
			return succeeded;
		}
		
		void setSucceeded(boolean succeeded)
		{
			this.succeeded = succeeded;
		}

		public List<Result> getResults()
		{
			return results;
		}

		void setResults(List<Result> results)
		{
			this.results = results;
		}
	}
	
	/**
	 * Class that represents unstructured job response
	 */
	class RawReportJobResult
	{
		private Integer jobId;
		private boolean succeeded;
		private boolean completed;
		private String status;
		private Map<String, Object> source;
		private List<Map<String, Object>> resultProps;
		
		public List<Map<String, Object>> getResultProps()
		{
			return resultProps;
		}
		
		public void setResultProps(List<Map<String, Object>> resultProps)
		{
			this.resultProps = resultProps;
		}
		
		public String getStatus()
		{
			return status;
		}
		
		public void setStatus(String status)
		{
			this.status = status;
		}
		
		public boolean isCompleted()
		{
			return completed;
		}
		
		public void setCompleted(boolean completed)
		{
			this.completed = completed;
		}
		
		public boolean isSucceeded()
		{
			return succeeded;
		}
		
		public void setSucceeded(boolean succeeded)
		{
			this.succeeded = succeeded;
		}
		
		public Integer getJobId()
		{
			return jobId;
		}
		
		public void setJobId(Integer jobId)
		{
			this.jobId = jobId;
		}

		public Map<String, Object> getSource()
		{
			return source;
		}

		public void setSource(Map<String, Object> source)
		{
			this.source = source;
		}
	}
	
	/**
	 * Class that represents generic job identifier 
	 */
	class Job
	{
		private Integer jobId;
		
		public Job(Integer jobId)
		{
			this.jobId = jobId;
		}
		
		public Integer getJobId()
		{
			return jobId;
		}
	}
	
	/**
	 * Class that represents generic HTTP response
	 */
	class HttpResponse
	{
		private Integer responseCode;
		private String responseData;
		
		public HttpResponse(Integer responseCode, String responseData)
		{
			this.responseCode = responseCode;
			this.responseData = responseData;
		}
		
		public Integer getResponseCode()
		{
			return responseCode;
		}
		
		public String getResponseData()
		{
			return responseData;
		}
	}
}
