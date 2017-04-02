require LWP::UserAgent;
require JSON;
require URI::Escape;

package AppChains;

use constant
{
	# Schema to access remote API (http or https)
	DEFAULT_APPCHAINS_SCHEMA => "https",
	
	# Port to access remote API
	DEFAULT_APPCHAINS_PORT => 443,
	
	# Timeout to wait between tries to update Job status in seconds
	DEFAULT_REPORT_RETRY_TIMEOUT => 1,
	
	# Default hostname for Beacon requests
	BEACON_HOSTNAME => "beacon.sequencing.com",
	
	# Default AppChains protocol version
	PROTOCOL_VERSION => "v2"
};

sub new
{
	my $class = shift;
	my ($token, $chainsHostname) = @_;
	
	my $self = {
		token  => $token,
		chainsHostname => $chainsHostname
	};
	
	return bless $self, $class;
}

###
### High level public API
###

# Requests report
# @param remoteMethodName REST endpoint name (i.e. StartApp)
# @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
# @param datasourceId resource with data to use for report generation
# @return
sub getReport
{
	my ($self, $remoteMethodName, $applicationMethodName, $datasourceId) = @_;
	my $jobData = $self->submitReportJob("POST", $remoteMethodName, $applicationMethodName, $datasourceId);
	return return $self->getReportImpl($jobData);
}

# Requests batch report
# @param remoteMethodName REST endpoint name (i.e. StartApp)
# @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
# @param datasourceId resource with data to use for report generation
# @return
sub getBatchReport
{
	my ($self, $remoteMethodName, $chainSpec) = @_;
	
	my $requestBody = JSON::encode_json($self->buildBatchReportRequestBody($remoteMethodName, $chainSpec));
	print $requestBody;
	my $batchJobData = $self->submitReportJobImpl("POST", $remoteMethodName, $requestBody);
	
	return $self->getBatchReportImpl($batchJobData);
}

# Requests report
# @param remoteMethodName REST endpoint name (i.e. StartApp)
# @param requestBody jsonified request body to send to server
# @return
sub getReportEx
{
	my ($self, $remoteMethodName, $requestBody) = @_;
	my $jobData = $self->submitReportJobImpl("POST", $remoteMethodName, $requestBody);
	return $self->getReportImpl($jobData);
}

# Returns sequencing beacon
# @return
sub getSequencingBeacon
{
	my ($self, $chrom, $pos, $allele) = @_;
	return $self->getBeacon("SequencingBeacon", $self->getBeaconParameters($chrom, $pos, $allele));
}

# Returns public beacon
# @return
sub getPublicBeacon
{
	my ($self, $chrom, $pos, $allele) = @_;
	return $self->getBeacon("PublicBeacons", $self->getBeaconParameters($chrom, $pos, $allele));
}

###
### Low level public API
###

# Requests report in raw form it is sent from the server
# without parsing and transforming it
# @param remoteMethodName REST endpoint name (i.e. StartApp)
# @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
# @param datasourceId resource with data to use for report generation
# @return
sub getRawReport
{
	my ($self, $remoteMethodName, $applicationMethodName, $datasourceId) = @_;
	my $jobData = $self->submitReportJob("POST", $remoteMethodName, $applicationMethodName, $datasourceId);
	return $self->getRawReportImpl($jobData)->getSource();
}

# Returns beacon
# @param methodName REST endpoint name (i.e. PublicBeacons)
# @param parameters map of request (GET) parameters (key->value) to append to the URL
# @return
sub getBeacon
{
	my ($self, $methodName, $parameters) = @_;
	return $self->getBeaconEx($methodName, $self->getRequestString($parameters));
}

# Requests report in raw form it is sent from the server
# without parsing and transforming it
# @param remoteMethodName REST endpoint name (i.e. StartApp)
# @param requestBody jsonified request body to send to server
# @return
sub getRawReportEx
{
	my ($self, $remoteMethodName, $requestBody) = @_;
	my $jobData = $self->submitReportJobImpl("POST", $remoteMethodName, $requestBody);
	return $self->getRawReportImpl($jobData)->getSource();
}

# Returns beacon
# @param methodName REST endpoint name (i.e. PublicBeacons)
# @param parameters query string
# @return
sub getBeaconEx
{
	my ($self, $methodName, $queryString) = @_;
	my $response = $self->httpRequest("GET", $self->getBeaconUrl($methodName, $queryString));
	return $response->getResponseData();
}

###
### Internal methods
###

sub getBeaconParameters
{
	my ($self, $chrom, $pos, $allele) = @_;
	my %parameters = ("chrom" => $chrom, "pos" => $pos, "allele" => $allele);
	return \%parameters;
}

# Retrieves report data from the API server using batch approach
# @param job identifier to retrieve report
# @return report
sub getBatchReportImpl
{
	my ($self, $batchJobData) = @_;
	my $jobs = $self->getBatchRawReportImpl($batchJobData);
	my %result = ();
	
	foreach (keys %$jobs) {
		$result{$_} = $self->processCompletedJob($jobs->{$_});
	}
	
	return \%result;
}

# Retrieves report data from the API server
# @param job identifier to retrieve report
# @return report
sub getReportImpl
{
	my ($self, $job) = @_;
	return $self->processCompletedJob($self->getRawReportImpl($job));
}

# Handles raw report result by transforming it to user friendly state
# @param rawResult
# @return
sub processCompletedJob
{
	my ($self, $rawResult) = @_;
	
	my @results = ();
		
	foreach (@{$rawResult->getResultProps()})
	{
		my $type = $_->{Type},
		   $resultPropValue = $_->{Value},
		   $resultPropName = $_->{Name};
		   
		next unless (defined $type && defined $resultPropValue && defined $resultPropName);
		
		my $resultPropType = lc($type);
		
		if ($resultPropType eq "plaintext")
		{
			push(@results, Result->new($resultPropName, TextResultValue->new($resultPropValue)));
		}
		elsif ($resultPropType eq "pdf")
		{
			my $filename = sprintf("report_%d.%s", $rawResult->getJobId(), $resultPropType);
			my $reportFileUrl = $self->getReportFileUrl($resultPropValue);
			
			my $resultValue = FileResultValue->new($self, $filename, $resultPropType, $reportFileUrl);
			push(@results, Result->new($resultPropName, $resultValue));
		}
	}
	
	my $finalResult = Report->new();
	$finalResult->setSucceeded($rawResult->isSucceeded() ? 1 : 0);
	$finalResult->setResults(\@results);
	
	return $finalResult;
}

# Retrieves report data from the API server
# @param job identifier to retrieve report
# @return report
sub getRawReportImpl
{
	my ($self, $jobData) = @_;
	
	while (1)
	{
		my $rawResult = $self->getRawJobResult($jobData);
		
		if ($rawResult->isCompleted()) {
			return $rawResult;
		}
		
		sleep(DEFAULT_REPORT_RETRY_TIMEOUT);

		$jobData = $self->getJobResponse($jobData->getJobId());
	}
}

# Retrieves report data from the API server
# @param job identifier to retrieve report
# @return report
sub getBatchRawReportImpl
{
	my ($self, $batchJobData) = @_;
	my %result = ();
	my %jobIdsPending = ();
	my %jobIdsCompleted = ();
	
	while (1)
	{
		foreach (@$batchJobData)
		{
			my $job = $self->getRawJobResult($_ ->{Value});
			
			if ($job->isCompleted()) {
				$jobIdsCompleted{$job->getJobId()} = $_ ->{Key};
				$result{$_ ->{Key}} = $job;
			} else {
				$jobIdsPending{$job->getJobId()} = $_ ->{Key};
			}
		}
		
		#my @pendingJobs = keys %jobIdsPending;
		#my @completedJobs = keys %jobIdsCompleted;
		
		if (scalar (keys %jobIdsPending) > 0) {
			$batchJobData = $self->getBatchJobResponse(\%jobIdsPending);
		} else {
			return \%result;
		}
		
		%jobIdsPending = ();
		sleep(DEFAULT_REPORT_RETRY_TIMEOUT);
	}
}

sub getJobResponse
{
	my ($self, $job) = @_;
	
	my $url = $self->getJobResultsUrl($job);
	my $httpResponse = $self->httpRequest("GET", $url);
	return JSON::decode_json($httpResponse->getResponseData());
}

sub getBatchJobResponse
{
	my ($self, $jobs) = @_;

	my @jobIds = keys %$jobs;	
	my $url = $self->getBatchJobResultsUrl();
	my %requestData = ("JobIds" => \@jobIds);
	
	my $httpResponse = $self->httpRequest("POST", $url, JSON::encode_json(\%requestData));
	my $decodedResponse = (JSON::decode_json($httpResponse->getResponseData()));

	my @result = ();
	foreach my $k (@$decodedResponse)
	{
		my %h = ();
		my $jobId = $k->{Status}->{IdJob};
		$h{Key} = $jobs->{$jobId};
		$h{Value} = $k;
		push(@result, \%h);
	}
	
	return \@result;
}


# Retrieves raw job results data
# @param job job identifier
# @return raw job results
sub getRawJobResult
{
	my ($self, $decodedResponse) = @_;
	
	my $status = $decodedResponse->{Status},
	   $succeeded = 0;
	
	unless (defined($status->{CompletedSuccesfully})) {
		$succeeded = 0;
	} else {
		$succeeded = $status->{CompletedSuccesfully} ? 1 : 0;
	}
	
	my $jobStatus = $status->{Status};
	
	my $result = RawReportJobResult->new;
	$result->setSource($decodedResponse);
	$result->setJobId($decodedResponse->{Status}->{IdJob});
	$result->setSucceeded($succeeded);
	$result->setCompleted(lc($jobStatus) eq "completed" || lc($jobStatus) eq "cancelled");
	$result->setResultProps($decodedResponse->{ResultProps});
	$result->setStatus($jobStatus);
	
	return $result;
}

# Builds request body used for report generation
# @param applicationMethodName
# @param datasourceId
# @return
sub buildReportRequestBody
{
	my ($self, $applicationMethodName, $datasourceId) = @_;

	my %parameters = (
			"Name" => "dataSourceId", 
			"Value" => $datasourceId);

	my @pars = ();
	push(@pars, \%parameters);
	
	my %data = (
			"AppCode" => $applicationMethodName,
			"Pars" => \@pars);

	return \%data;
}

# Builds request body used for batch report generation
# @param applicationMethodName
# @param chainSpec
# @return
sub buildBatchReportRequestBody
{
	my ($self, $applicationMethodName, $chainSpec) = @_;

	my @result = ();
	for my $c (keys %{$chainSpec})
	{
		my @pars = ();
		my %data = (
				"AppCode" => $c,
				"Pars" => \@pars);
				
		my %parameters = (
				"Name" => "dataSourceId", 
				"Value" => $chainSpec->{$c});
				
		push(@pars, \%parameters);
		push(@result, \%data);
	}

	my %data = (
			"Pars" => \@result);

	return \%data;
}

# Submits job to the API server
# @param httpMethod HTTP method to access API server
# @param remoteMethodName REST endpoint name (i.e. StartApp)
# @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
# @param datasourceId resource with data to use for report generation
# @return job identifier
sub submitReportJob
{
	my ($self, $httpMethod, $remoteMethodName, $applicationMethodName, $datasourceId) = @_;
	return $self->submitReportJobImpl($httpMethod, $remoteMethodName,
		JSON::encode_json($self->buildReportRequestBody($applicationMethodName, $datasourceId)));
}

# Submits job to the API server
# @param httpMethod httpMethod HTTP method to access API server
# @param remoteMethodName REST endpoint name (i.e. StartApp)
# @param requestBody jsonified request body to send to server
# @return
sub submitReportJobImpl
{
	my ($self, $httpMethod, $remoteMethodName, $requestBody) = @_;
	
	my $httpResponse = $self->httpRequest($httpMethod, $self->getJobSubmissionUrl($remoteMethodName), $requestBody);
	my $responseCode = $httpResponse->getResponseCode(),
	   $responseData = $httpResponse->getResponseData();

	if ($responseCode != 200)
	{
		warn(sprintf("Appchains returned error HTTP code %d with message %s", $responseCode, $responseData));
		return;
	}

	return $decodedResponse = JSON::decode_json($responseData);
}

# Constructs URL for getting report file
# @param fileId file identifier
# @return URL
sub getReportFileUrl
{
	my ($self, $fileId) = @_;
	return sprintf("%s/GetReportFile?id=%d", $self->getBaseAppChainsUrl(), $fileId);
}

# Constructs URL for getting job results
# @param jobId job identifier
# @return URL
sub getJobResultsUrl
{
	my ($self, $jobId) = @_;
	return return sprintf("%s/GetAppResults?idJob=%d", $self->getBaseAppChainsUrl(), $jobId);
}

sub getBatchJobResultsUrl
{
	my ($self) = @_;
	return return sprintf("%s/GetAppResultsBatch", $self->getBaseAppChainsUrl());
}


# Constructs URL for job submission
# @param applicationMethodName report/application specific identifier (i.e. MelanomaDsAppv)
# @return
sub getJobSubmissionUrl
{
	my ($self, $applicationMethodName) = @_;
	return sprintf("%s/%s", $self->getBaseAppChainsUrl(), $applicationMethodName);
}

# Constructs URL for accessing beacon related remote endpoints
# @param methodName report/application specific identifier (i.e. SequencingBeacon)
# @param queryString query string
# @return
sub getBeaconUrl
{
	my ($self, $methodName, $queryString) = @_;
	return sprintf("%s://%s:%d/%s/?%s", DEFAULT_APPCHAINS_SCHEMA,
		BEACON_HOSTNAME, DEFAULT_APPCHAINS_PORT, $methodName, $queryString);
}

# Constructs base Appchains URL
# @return
sub getBaseAppChainsUrl
{
	my $self = shift;
	return sprintf("%s://%s:%d/%s", DEFAULT_APPCHAINS_SCHEMA,
			$self->{chainsHostname}, DEFAULT_APPCHAINS_PORT, PROTOCOL_VERSION);
}

# Executes HTTP request of the specified type
# @param method HTTP method (GET/POST)
# @param url URL to send request to
# @param body request body (applicable for POST)
# @param curlAttributes additional cURL attributes
# @return
sub httpRequest
{
	my ($self, $method, $url, $body, $saveTo) = @_;
	
	my $httpResponse = $self->httpRequestImpl($method, $url, $body, $saveTo);
	my $responseBody = $httpResponse->content,
	   $responseCode = $httpResponse->code;
	   
	if ($responseCode != 200) {
		warn("Error retrieving data from $url ($method, '$body'): $responseBody");
		return;
	}
	
	return HttpResponse->new($responseCode, $responseBody);
}

# Executes HTTP request of the specified type and returns cURL handle
# @param method HTTP method (GET/POST)
# @param url URL to send request to
# @param body request body (applicable for POST)
# @param curlAttributes additional cURL attributes
# @return
sub httpRequestImpl
{
	my ($self, $method, $url, $body, $saveTo) = @_;
	
	$method = uc($method);
	unless ($method ~~ ['POST', 'GET']) {
		warn("Unsupported  HTTP method '$method'");
		return;
	}
	
	my $httpRequest;
	
	if ($method eq "GET") {
		$httpRequest = $self->createHttpGetConnection($url);
	} else {
		$httpRequest = $self->createHttpPostConnection($url, $body);
	}
	
	my $userAgent = LWP::UserAgent->new;
	$userAgent->ssl_opts( verify_hostname => 0 );
	
	if (defined($saveTo)) {
		return $userAgent->request($httpRequest, $saveTo);
	} else {
		return $userAgent->request($httpRequest);
	}
}

# Creates and returns HTTP connection cURL object using POST method
# @param url URL to send request to
# @param body request body (applicable for POST)
# @return cURL handle
sub createHttpPostConnection
{
	my ($self, $url, $body) = @_;
	
	my $request = HTTP::Request->new('POST', $url);
	$request->header("Content-Type" => "application/json");
	$request->header("Content-Length" => length($body));
	
	my %headers = $self->getOauthHeaders();
	foreach my $k (keys %headers) {
		$request->header($k, $headers{$k});	
	}
	
	$request->content($body);
	
	return $request;
}


# Creates and returns HTTP connection cURL object using GET method
# @param url URL to send request to
# @return cURL handle
sub createHttpGetConnection
{
	my ($self, $url) = @_;
	
	my $request = HTTP::Request->new('GET', $url);

	my %headers = $self->getOauthHeaders();
	foreach my $k (keys %headers) {
		$request->header($k, $headers{$k});	
	}
	
	return $request;
}

# Configures cURL handle by adding authorization headers
# @param curlHandle cURL handle
sub getOauthHeaders
{
	my $self = shift;
	
	if (!$self->{token}) {
		return ();
	} else {
		return ("Authorization" => "Bearer " . $self->{token});
	}
}

# Generates query string by map of key=value pairs
# @param urlParameters map of key-value pairs
# @return
sub getRequestString
{
	my ($self, $urlParameters) = @_;
	
	my $request = "";
	
	foreach (keys %{$urlParameters})
	{
		$request .= "&" if (length($request) > 0);
		$request .= sprintf("%s=%s", $_, URI::Escape::uri_escape($urlParameters->{$_}));
	}
	
	return $request;
}

# Downloads remote file
# @param method HTTP method (GET/POST)
# @param url URL to send request to
# @param file path to local file to save file to
# @return
sub downloadFile
{
	my ($self, $method, $url, $file) = @_;
	$self->httpRequest($method, $url);
}

###
### Class that represents generic HTTP response
###

package HttpResponse;

sub new
{
	my $class = shift;
	my ($responseCode, $responseData) = @_;
	
	my $self = {
		responseCode  => $responseCode,
		responseData => $responseData
	};
	
	return bless $self, $class;
}

sub getResponseCode
{
	my $self = shift;
	return $self->{responseCode};
}

sub getResponseData
{
	my $self = shift;
	return $self->{responseData};
}

###
### Class that represents generic job identifier 
###

package Job;

sub new
{
	my ($class, $jobId) = @_;
	
	my $self = {
		jobId  => $jobId
	};
	
	return bless $self, $class;
}

sub getJobId
{
	my $self = shift;
	return $self->{jobId}; 
}

###
### Class that represents unstructured job response
###

package RawReportJobResult;

sub new
{
	my ($class) = @_;
	
	my $self = {};
	
	return bless $self, $class;
}

sub getResultProps
{
	my $self = shift;
	return $self->{resultProps};
}

sub setResultProps
{
	my ($self, $resultProps) = @_;
	$self->{resultProps} = $resultProps;
}

sub getStatus
{
	my $self = shift;
	return $self->{status};
}

sub setStatus
{
	my ($self, $status) = @_;
	$self->{status} = $status;
}

sub isCompleted
{
	my $self = shift;
	return $self->{completed};
}

sub setCompleted
{
	my ($self, $completed) = @_;
	$self->{completed} = $completed;
}

sub isSucceeded
{
	my $self = shift;
	return $self->{succeeded};
}

sub setSucceeded
{
	my ($self, $succeeded) = @_;
	$self->{succeeded} = $succeeded;
}

sub getJobId
{
	my $self = shift;
	return $self->{jobId};
}

sub setJobId
{
	my ($self, $jobId) = @_;
	$self->{jobId} = $jobId;
}

sub getSource
{
	my $self = shift;
	return $self->{source};
}

sub setSource
{
	my ($self, $source) = @_;
	$self->{source} = $source;
}

###
### Class that represents report available to
### the end client
###

package Report;

sub new
{
	my ($class) = @_;
	
	my $self = {};
	
	return bless $self, $class;
}

sub isSucceeded
{
	my $self = shift;
	return $self->{succeeded};
}

sub setSucceeded
{
	my ($self, $succeeded) = @_;
	$self->{succeeded} = $succeeded;
}

sub getResults
{
	my $self = shift;
	return $self->{results};
}

sub setResults
{
	my ($self, $results) = @_;
	$self->{results} = $results;
}

###
### Enumerates possible result entity types
###

package ResultType;

use constant {
	FILE => 0,
	TEXT => 1
};

package ResultValue;

sub new
{
	my ($class, $type) = @_;

	my $self = {
		type  => $type
	};
	
	return bless $self, $class;
}

sub getType
{
	my $self = shift;
	return $self->{type};
}

###
### Class that represents result entity if plain text string
###

package TextResultValue;
use parent -norequire, ResultValue;

sub new
{
	my ($class, $data) = @_;

	my $self = {
		type  => ResultType->TEXT,
		data => $data
	};
	
	return bless $self, $class;
}

sub getData
{
	my $self = shift;
	return $self->{data};
}

###
### Class that represents result entity if it's file
###

package FileResultValue;
use parent -norequire, ResultValue;

sub new
{
	my ($class, $chains, $name, $extension, $url) = @_;

	my $self = {
		type  => ResultType->FILE,
		chains => $chains,
		name => $name,
		extension => $extension,
		url => $url
	};
	
	return bless $self, $class;
}

sub getName
{
	my $self = shift;
	return $self->{name};
}

sub getUrl
{
	my $self = shift;
	return $self->{url};
}

sub getExtension
{
	my $self = shift;
	return $self->{extension};
}

sub saveAs
{
	my ($self, $fullPathWithName) = @_;
	$self->{chains}->httpRequest("GET", $self->{url}, undef, $fullPathWithName);
}

sub saveTo
{
	my ($self, $location) = @_;
	$self->saveAs(sprintf("%s/%s", $location, $self->getName()));
}

###
### Class that represents single report result entity
###

package Result;

sub new
{
	my ($class, $name, $resultValue) = @_;

	my $self = {
		name  => $name,
		value => $resultValue
	};
	
	return bless $self, $class;
}

sub getValue
{
	my $self = shift;
	return $self->{value};
}

sub getName
{
	my $self = shift;
	return $self->{name};
}

1;
