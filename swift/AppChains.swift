import Foundation

struct AppChainsConfig
{
    /**
    * Schema to access remote API (http or https)
    */
    static let DEFAULT_APPCHAINS_SCHEMA = "https"
    
    /**
    * Port to access remote API
    */
    static let DEFAULT_APPCHAINS_PORT = 443
    
    /**
    * Timeout to wait between tries to update Job status in seconds
    */
    static let DEFAULT_REPORT_RETRY_TIMEOUT:NSTimeInterval = 1
    
    /**
    * Default hostname for Beacon requests
    */
    static let BEACON_HOSTNAME = "beacon.sequencing.com"
    
    /**
    * Default AppChains protocol version
    */
    static let PROTOCOL_VERSION = "v1"
}

class AppChains
{
    /**
    * Security token supplied by the client
    */
    var token: String = ""
    
    /**
    * Remote hostname to send requests to
    */
    var chainsHostname: String = ""
    
    /**
    * Constructor that should be called in order to work
    * with methods that require authentication (i.e. getReport)
    * @param token OAuth security token
    * @param chainsHostname hostname to call
    */
    init(token: String, chainsHostname: String)
    {
        self.token = token
        self.chainsHostname = chainsHostname
    }
    
    /**
    * Constructor that should be called in order to work
    * with methods that doesn't require authentication (i.e. getBeacon)
    * @param chainsHostname
    */
    init(chainsHostname: String)
    {
        self.chainsHostname = chainsHostname
    }
    
    init()
    {
    }
    
    // High level public API
    
    /**
    * Requests report
    * @param remoteMethodName REST endpoint name (i.e. StartApp)
    * @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
    * @param datasourceId resource with data to use for report generation
    * @return
    */
    func getReport(remoteMethodName: String, applicationMethodName: String, datasourceId: String) -> ReturnValue<Report>
    {
        let job:ReturnValue<Job> = submitReportJob("POST", remoteMethodName: remoteMethodName,
            applicationMethodName: applicationMethodName, datasourceId: datasourceId);
        
        return getReportInternal(job);
    }
    
    /**
    * Requests report
    * @param remoteMethodName REST endpoint name (i.e. StartApp)
    * @param requestBody jsonified request body to send to server
    * @return
    */
    func getReport(remoteMethodName:String, requestBody:String) -> ReturnValue<Report>
    {
        let job:ReturnValue<Job> = submitReportJob("POST", remoteMethodName: remoteMethodName, requestBody: requestBody)
        return getReportInternal(job);
    }
    
    /**
    * Returns sequencing beacon
    * @return
    */
    func getSequencingBeacon(chrom: Int, pos: Int, allele: String) -> ReturnValue<String>
    {
        return getBeacon("SequencingBeacon", parameters: getBeaconParameters(chrom, pos: pos, allele: allele));
    }
    
    /**
    * Returns public beacon
    * @return
    */
    func getPublicBeacon(chrom: Int, pos: Int, allele: String) -> ReturnValue<String>
    {
        return getBeacon("PublicBeacons", parameters: getBeaconParameters(chrom, pos: pos, allele: allele));
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
    func getRawReport(remoteMethodName: String, applicationMethodName: String, datasourceId: String) -> ReturnValue<Dictionary<String, AnyObject>>
    {
        let job:ReturnValue<Job> = submitReportJob("POST", remoteMethodName: remoteMethodName,
                applicationMethodName: applicationMethodName, datasourceId: datasourceId);
        
        return getRawReportInternal(job);
    }
    
    /**
    * Requests report in raw form it is sent from the server
    * without parsing and transforming it
    * @param remoteMethodName REST endpoint name (i.e. StartApp)
    * @param requestBody jsonified request body to send to server
    * @return
    */
    func getRawReport(remoteMethodName:String, requestBody:String) -> ReturnValue<Dictionary<String, AnyObject>>
    {
        let job:ReturnValue<Job> = submitReportJob("POST", remoteMethodName: remoteMethodName, requestBody: requestBody)
        return getRawReportInternal(job);
    }
    
    /**
    * Returns beacon
    * @param methodName REST endpoint name (i.e. PublicBeacons)
    * @param parameters query string
    * @return
    */
    func getBeacon(methodName: String, parameters: Dictionary<String, String>) -> ReturnValue<String>
    {
        let response:ReturnValue<HttpResponse> = httpRequest("GET", url: getBeaconUrl(methodName, queryString: getRequestString(parameters)), body: "")
        
        if let r = response.value
        {
            return ReturnValue<String>.Success(r.responseData)
        }
        else
        {
            return ReturnValue.Failure(response.error!)
        }
    }
    
    // Internal methods

    func getBeaconParameters(chrom: Int, pos: Int, allele: String) -> Dictionary<String, String>
    {
        return ["chrom": String(chrom), "pos": String(pos), "allele": allele];
    }
    
    func getRawReportInternal(job:ReturnValue<Job>) -> ReturnValue<Dictionary<String, AnyObject>>
    {
        if (job.value == nil) {
            return ReturnValue.Failure(job.error!)
        }
        
        let rawResult:ReturnValue<RawReportJobResult> = getRawReportImpl(job.value!)
        if (rawResult.value == nil)
        {
            return ReturnValue.Failure(rawResult.error!)
        }
        
        return ReturnValue<Dictionary<String, AnyObject>>.Success(rawResult.value!.source)
    }

    /**
    * Handles raw report result by transforming it to user friendly state
    * @param rawResult
    * @return
    */
    func processCompletedJob(rawResult:RawReportJobResult) -> Report
    {
        var results:Array<Result> = Array<Result>()
        
        for resultProp: Dictionary<String, AnyObject> in rawResult.resultProps
        {
            let type = resultProp["Type"] as? String
            let value = resultProp["Value"] as? String
            let name = resultProp["Name"] as? String
            
            if (type == nil || value == nil || name == nil) {
                continue
            }
            
            let resultPropType:String = type!.lowercaseString
            let resultPropValue:String = value! as String
            let resultPropName:String = name! as String
            
            switch resultPropType
            {
                case "plaintext":
                    results.append(Result(value: TextResultValue(data: resultPropValue), name: resultPropName))
                    break;
                case "pdf":
                    let filename:String = String(format: "report_%d.%@", rawResult.jobId, resultPropType)
                    let reportFileUrl: NSURL = getReportFileUrl(resultPropValue.toInt()!)
                    
                    let resultValue:ResultValue = FileResultValue(chains: self, name: filename, ext: resultPropType, url: reportFileUrl)
                    results.append(Result(value: resultValue, name: resultPropName))
                    break;
                default:
                    break;
            }
        }
        
        let finalResult:Report = Report()
        finalResult.results = results
        finalResult.succeeded = rawResult.succeeded
        
        return finalResult
    }
    
    /**
    * Submits job to the API server
    * @param httpMethod HTTP method to access API server
    * @param remoteMethodName REST endpoint name (i.e. StartApp)
    * @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
    * @param datasourceId resource with data to use for report generation
    * @return job identifier
    */
    func submitReportJob(httpMethod: String, remoteMethodName: String, applicationMethodName: String, datasourceId: String) -> ReturnValue<Job>
    {
        return submitReportJob(httpMethod, remoteMethodName: remoteMethodName, requestBody: toJson(buildReportRequestBody(applicationMethodName, datasourceId: datasourceId)))
    }
    
    /**
    * Builds request body used for report generation
    * @param applicationMethodName
    * @param datasourceId
    * @return
    */
    func buildReportRequestBody(applicationMethodName: String, datasourceId: String) -> Dictionary<String, AnyObject>
    {
        return ["AppCode": applicationMethodName, "Pars": [["Name": "dataSourceId", "Value": datasourceId]]];
    }
    
    /**
    * Serializes object graph to json string
    * @param data source object to serialize
    * @return serialized data
    */
    func toJson(data: Dictionary<String, AnyObject>) -> String
    {
        var error: NSError?
        let jsonData:NSData = NSJSONSerialization.dataWithJSONObject((data as AnyObject), options: nil, error: &error)!;
        return NSString(data: jsonData, encoding: NSUTF8StringEncoding)!;
    }
    
    /**
    * Deserializes json
    * @param data string with json data
    * @return
    */
    func fromJson(data: String) -> Dictionary<String, AnyObject>
    {
        return NSJSONSerialization.JSONObjectWithData((data as NSString).dataUsingEncoding(NSUTF8StringEncoding)!, options: NSJSONReadingOptions.MutableContainers, error: nil) as Dictionary<String, AnyObject>
    }
    
    /**
    * Retrieves report data from the API server
    * @param job identifier to retrieve report
    * @return report
    */
    func getReportInternal(job:ReturnValue<Job>) -> ReturnValue<Report>
    {
        if (job.value == nil) {
            return ReturnValue.Failure(job.error!)
        }
        
        let rawResult:ReturnValue<RawReportJobResult> = getRawReportImpl(job.value!)
        if (rawResult.value == nil)
        {
            return ReturnValue.Failure(rawResult.error!)
        }
        
        return ReturnValue<Report>.Success(processCompletedJob(rawResult.value!))
    }
    
    /**
    * Retrieves raw report data from the API server
    * @param job identifier to retrieve report
    * @return report
    */
    func getRawReportImpl(job:Job) -> ReturnValue<RawReportJobResult>
    {
        while (true)
        {
            var rawResult = getRawJobResult(job)
            
            if let data = rawResult.value
            {
                if (data.completed) {
                    return ReturnValue<RawReportJobResult>.Success(data)
                }
            }
            else
            {
                return ReturnValue<RawReportJobResult>(rawResult.error!)
            }
            
            NSThread.sleepForTimeInterval(AppChainsConfig.DEFAULT_REPORT_RETRY_TIMEOUT)
        }
    }
    
    /**
    * Retrieves raw job results data
    * @param job job identifier
    * @return raw job results
    */
    func getRawJobResult(job: Job) -> ReturnValue<RawReportJobResult>
    {
        let url:NSURL = getJobResultsUrl(job.jobId)
        let response:ReturnValue<HttpResponse> = httpRequest("GET", url: url, body: "")
        
        if let v = response.value
        {
            var decodedResponse:Dictionary<String, AnyObject> = fromJson(v.responseData)
            var resultProps = decodedResponse["ResultProps"] as Array<Dictionary<String, AnyObject>>
            var status = decodedResponse["Status"] as Dictionary<String, AnyObject>

            var succeeded:Bool = false
            
            if let s = status["CompletedSuccesfully"] as? Bool {
                succeeded = s;
            }
            
            let jobStatus:String = status["Status"] as String
            
            var result: RawReportJobResult = RawReportJobResult(jobId: job.jobId)
            result.source = decodedResponse
            result.succeeded = succeeded
            result.completed = jobStatus.lowercaseString == "completed" || jobStatus.lowercaseString == "failed"
            result.resultProps = resultProps
            result.status = jobStatus
            
            return ReturnValue<RawReportJobResult>.Success(result)
        }
        else
        {
            return ReturnValue<RawReportJobResult>.Failure(response.error!)
        }
    }
    
    /**
    * Submits job to the API server
    * @param httpMethod httpMethod HTTP method to access API server
    * @param remoteMethodName REST endpoint name (i.e. StartApp)
    * @param requestBody jsonified request body to send to server
    * @return
    */
    func submitReportJob(httpMethod: String, remoteMethodName: String, requestBody: String) -> ReturnValue<Job>
    {
        let response: ReturnValue<HttpResponse> = httpRequest(httpMethod, url: getJobSubmissionUrl(remoteMethodName), body: requestBody);

        if let data = response.value
        {
            if (data.responseCode != 200) {
                return ReturnValue.Failure(String(format: "Appchains returned error HTTP code %d with message %@",
                    data.responseCode, data.responseData))
            }
            
            let responseObject:Dictionary<String, AnyObject?> = fromJson(data.responseData)
            
            if let jobId = responseObject["jobId"] as? Int
            {
                return ReturnValue.Success(Job(jobId: jobId))
            }
            else
            {
                return ReturnValue.Failure("Appchains returned invalid job identifier")
            }
        }
        
        return ReturnValue.Failure(response.error!)
    }
    
    /**
    * Executes HTTP request of the specified type
    * @param method HTTP method (GET/POST)
    * @param url URL to send request to
    * @param body request body (applicable for POST)
    * @return
    */
    func httpRequest(method: String, url: NSURL, body: String) -> ReturnValue<HttpResponse>
    {
        var connection: NSURLRequest? = nil
        
        switch method.lowercaseString
        {
            case "post":
                connection = openHttpPostConnection(url, body: body)
                break
            case "get":
                connection = openHttpGetConnection(url)
                break
            default:
                return ReturnValue.Failure("Unsupported method " + method)
        }
        
        var rawResponse: NSURLResponse?
        var error: NSErrorPointer = nil
        
        if let data = NSURLConnection.sendSynchronousRequest(connection!, returningResponse: &rawResponse, error: error)
        {
            let response: NSHTTPURLResponse = rawResponse as NSHTTPURLResponse
            var reply = NSString(data: data, encoding: NSUTF8StringEncoding)
            
            return ReturnValue<HttpResponse>.Success(HttpResponse(responseCode: response.statusCode, responseData: reply!));
        }

        return ReturnValue.Failure("Unable to read response from the Appchains server")
    }
    
    func downloadFile(url: NSURL, path:String) -> ReturnValue<Bool>
    {
        var connection: NSURLRequest = openHttpGetConnection(url);
        
        if let data = NSURLConnection.sendSynchronousRequest(connection, returningResponse: nil, error: nil)
        {
            data.writeToFile(path, atomically: true);
            return ReturnValue<Bool>.Success(true);
        }
        
        return ReturnValue.Failure("Unable to read data from the Appchains server")
    }
    
    /**
    * Constructs URL for getting report file
    * @param fileId file identifier
    * @return URL
    */
    func getReportFileUrl(fileId: Int) -> NSURL
    {
        return NSURL(string: NSString(format: "%@/GetReportFile?id=%d", getBaseAppChainsUrl(), fileId))!;
    }
    
    /**
    * Constructs URL for getting job results
    * @param jobId job identifier
    * @return URL
    */
    func getJobResultsUrl(jobId: Int) -> NSURL
    {
        return NSURL(string: NSString(format: "%@/GetAppResults?idJob=%d", getBaseAppChainsUrl(), jobId))!;
    }
    
    /**
    * Constructs URL for job submission
    * @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
    * @return
    */
    func getJobSubmissionUrl(applicationMethodName: String) -> NSURL
    {
        return NSURL(string: NSString(format: "%@/%@", getBaseAppChainsUrl(), applicationMethodName))!;
    }
    
    /**
    * Constructs base Appchains URL
    * @return
    */
    func getBaseAppChainsUrl() -> String
    {
        return NSString(format: "%@://%@:%d/%@", AppChainsConfig.DEFAULT_APPCHAINS_SCHEMA,
            self.chainsHostname, AppChainsConfig.DEFAULT_APPCHAINS_PORT, AppChainsConfig.PROTOCOL_VERSION);
    }
    
    /**
    * Constructs URL for accessing beacon related remote endpoints
    * @param methodName report/application specific identifier (i.e. SequencingBeacon)
    * @param queryString query string
    * @return
    */
    func getBeaconUrl(methodName: String, queryString: String) -> NSURL
    {
        return NSURL(string: NSString(format: "%@://%@:%d/%@/?%@",
            AppChainsConfig.DEFAULT_APPCHAINS_SCHEMA, AppChainsConfig.BEACON_HOSTNAME, AppChainsConfig.DEFAULT_APPCHAINS_PORT,
            methodName, queryString))!;
    }
    
    /**
    * Generates query string by map of key=value pairs
    * @param urlParameters map of key-value pairs
    * @return
    */
    func getRequestString(urlParameters: Dictionary<String, String>) -> String
    {
        var result = "";
        
        for (k, v) in urlParameters
        {
            if (result.lengthOfBytesUsingEncoding(NSUTF8StringEncoding) > 0) {
                result += "&";
            }

            result += String(format: "%@=%@", urlEncode(k), urlEncode(v))
        }
        
        return result;
    }
    
    /**
    * URL encodes given string
    * @param v source string to url encode
    * @return
    */
    func urlEncode(v: String) -> String
    {
        var encodedMessage = v.stringByAddingPercentEncodingWithAllowedCharacters(
            NSCharacterSet.URLHostAllowedCharacterSet())
        
        return encodedMessage!
    }
    
    /**
    * Opens and returns HTTP connection object using POST method
    * @param url URL to send request to
    * @param body request body (applicable for POST)
    * @return HttpURLConnection instance
    */
    func openHttpPostConnection(url: NSURL, body: String) -> NSURLRequest
    {
        let connection = openBaseOauthSecuredHttpConnection("POST", url: url);
        connection.addValue("application/json", forHTTPHeaderField: "Content-Type");
        connection.addValue(String(body.lengthOfBytesUsingEncoding(NSUTF8StringEncoding)), forHTTPHeaderField: "Content-Length");
        connection.HTTPBody = body.dataUsingEncoding(NSUTF8StringEncoding)
        return connection;
    }
    
    /**
    * Opens and returns HTTP connection object using GET method
    * @param url URL to send request to
    * @return HttpURLConnection instance
    */
    func openHttpGetConnection(url: NSURL) -> NSMutableURLRequest
    {
        return openBaseOauthSecuredHttpConnection("GET", url: url);
    }
    
    /**
    * Opens and returns HTTP connection object
    * @param method HTTP method to use
    * @param url URL to send request to
    * @return HttpURLConnection instance
    * @throws IOException
    */
    func openBaseOauthSecuredHttpConnection(method: String, url: NSURL) -> NSMutableURLRequest
    {
        let request = NSMutableURLRequest(URL: url)
        request.HTTPMethod = method
        request.addValue(String(format: "Bearer %@", self.token), forHTTPHeaderField: "Authorization")
        return request
    }
    
    /**
    * Checks whether specified character sequence represents a number
    * @param data source data to check
    * @return
    */
    func isNumeric(data: String) -> Bool
    {
        return data.toInt() != nil
    }
}

/**
* Enumerates possible result entity types
*/
enum ResultType: Int {
    case FILE = 0
    case TEXT = 1
}

class ResultValue
{
    var type: ResultType
    init (type: ResultType)
    {
        self.type = type
    }
}

/**
* Class that represents single report result entity
*/
class Result
{
    var value: ResultValue
    var name: String
    
    init (value: ResultValue, name: String)
    {
        self.value = value
        self.name = name
    }
}

/**
* Class that represents result entity if plain text string
*/
class TextResultValue: ResultValue
{
    var data: String
    
    init (data: String)
    {
        self.data = data
        super.init(type: ResultType.TEXT)
    }
}

/**
* Class that represents result entity if it's file
*/
class FileResultValue: ResultValue
{
    var name: String
    var ext: String
    var url: NSURL
    var chains: AppChains
    
    init (chains:AppChains, name: String, ext: String, url: NSURL)
    {
        self.chains = chains
        self.name = name
        self.ext = ext
        self.url = url
        super.init(type: ResultType.FILE)
    }
    
    func saveAs(fullPathWithName: String) -> ReturnValue<Bool>
    {
        return chains.downloadFile(self.url, path: fullPathWithName)
    }
    
    func saveTo(location: String) -> ReturnValue<Bool>
    {
        return saveAs(NSString(format: "%@/%@", location, self.name));
    }
}

/**
* Class that represents report available to
* the end client
*/
class Report
{
    var succeeded: Bool = false
    var results: Array<Result> = [Result]()
}

/**
* Class that represents unstructured job response
*/
class RawReportJobResult
{
    var jobId: Int
    var succeeded: Bool = false
    var completed: Bool = false
    var status: String = ""
    var source: Dictionary<String, AnyObject> = [String:AnyObject]()
    var resultProps: Array<Dictionary<String, AnyObject>> = [Dictionary<String, AnyObject>]()
    
    init(jobId:Int)
    {
        self.jobId = jobId;
    }
}

/**
* Class that represents generic job identifier
*/
class Job
{
    var jobId: Int

    init (jobId: Int)
    {
        self.jobId = jobId
    }
}

/**
* Class that represents generic HTTP response
*/
class HttpResponse
{
    var responseCode: Int
    var responseData: String
    
    init (responseCode: Int, responseData: String)
    {
        self.responseCode = responseCode
        self.responseData = responseData
    }
}

enum ReturnValue<T>
{
    case Success(@autoclosure() -> T)
    case Failure(String)

    var error: String? {
        switch self {
        case .Failure(let error):
            return error
            
        default:
            return nil
        }
    }
    
    var value: T? {
        switch self {
        case .Success(let value):
            return value()
            
        default:
            return nil
        }
    }
    
    init(_ value: T) {
        self = .Success(value)
    }
    
    init(_ error: String) {
        self = .Failure(error)
    }
}
