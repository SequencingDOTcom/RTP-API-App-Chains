using System;
using Sequencing.AppChainsSample.SQAPI;

namespace Sequencing.AppChainsSample
{
    internal class Program
    {
        private static void Main(string[] args)

        {
            var chains0 = new AppChains("https://beacon.sequencing.com/");
            Console.WriteLine(chains0.GetPublicBeacon(1, 2, "A"));


            var chains = new AppChains("147fc0683b08e94c6c2835efba60b815eb501a13", "https://api.sequencing.com/v1",
                "https://beacon.sequencing.com/");


            //Low level method invocation example 
            AppResultsHolder rawReport = chains.GetRawReport("Chain9", "FILE:80599");
            printRawResponse(rawReport);


            //High level method invocation example
            Report result = chains.GetReport("Chain9", "FILE:80599");
            printReport("147fc0683b08e94c6c2835efba60b815eb501a13",result);
            Console.WriteLine("Press any key");
            Console.ReadKey();
        }

        private static void printRawResponse(AppResultsHolder rawReport)
        {
            Console.WriteLine(rawReport);
        }

        private static void printReport(string token, Report result)
        {
            if (result.Succeeded == false)
                Console.WriteLine("Request has failed");
            else
                Console.WriteLine("Request has succeeded");

            foreach (Result r in result.getResults())
            {
                ResultType type = r.getValue().getType();

                if (type == ResultType.TEXT)
                {
                    var v = (TextResultValue) r.getValue();
                    Console.WriteLine(" -> text result type {0} = {1}", r.getName(), v.Data);
                }

                if (type == ResultType.FILE)
                {
                    var v = (FileResultValue) r.getValue();
                    Console.WriteLine(" -> file result type {0} = {1}", r.getName(), v.Url);
                    v.saveTo(token, ".\\");
                }
            }
        }
    }
}