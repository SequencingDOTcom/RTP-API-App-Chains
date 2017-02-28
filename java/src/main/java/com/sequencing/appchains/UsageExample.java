package com.sequencing.appchains;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;

import com.sequencing.appchains.AppChains.FileResultValue;
import com.sequencing.appchains.AppChains.Report;
import com.sequencing.appchains.AppChains.Result;
import com.sequencing.appchains.AppChains.ResultType;
import com.sequencing.appchains.AppChains.TextResultValue;

public class UsageExample
{
	// Main method for testing purposes
	
	public static void main(String[] args) throws IOException
	{
		AppChains chains = new AppChains("<your token goes here>", "api.sequencing.com");
		
		/**
		 * Low level method invocation example 
		 */
		
		Map<String, Object> rawReport = chains.getRawReport("StartApp", "Chain9", "227680");
		printRawResponse(rawReport);
		
		/**
		 * High level method invocation example
		 */
		
		Report result = chains.getReport("StartApp", "Chain11", "227680");
		printReport(result);

		Map<String, String> appChainsParams = new HashMap<String, String>();
		appChainsParams.put("Chain9",  "227680");
		appChainsParams.put("Chain88",  "227680");
		Map<String, Report> reportMap = chains.getReportBatch("StartAppBatch", appChainsParams);
		printReport(reportMap.get("Chain9"));
		printReport(reportMap.get("Chain88"));

	}
	
	private static void printRawResponse(Map<String, Object> rawReport)
	{
		System.out.println(rawReport);
	}
	
	private static void printReport(Report result) throws IOException
	{
		if (result.isSucceeded() == false)
			System.out.println("Request has failed");
		else
			System.out.println("Request has succeeded");
		
		for (Result r : result.getResults())
		{
			ResultType type = r.getValue().getType();
			
			if (type == ResultType.TEXT)
			{
				TextResultValue v = (TextResultValue) r.getValue();
				System.out.println(String.format(" -> text result type %s = %s", r.getName(), v.getData()));
			}
			
			if (type == ResultType.FILE)
			{
				FileResultValue v = (FileResultValue) r.getValue();
				System.out.println(String.format(" -> file result type %s = %s", r.getName(), v.getUrl()));
				v.saveTo("/tmp");
			}
		}
	}
}
