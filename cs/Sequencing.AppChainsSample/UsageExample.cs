using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequencing.AppChainsSample
{
    public class UsageExample
    {
        private static void Main(string[] args)

        {          
            var chains = new AppChains("<your token goes here>", "https://api.sequencing.com/v2",
                "https://beacon.sequencing.com/");

            //Low level method invocation example 
            AppResultsHolder rawReport = chains.GetRawReport("Chain9", "80599");
            PrintRawResponse(rawReport);

            //High level method invocation example
            Report result = chains.GetReport("Chain9", "80599");
            PrintReport("<your token goes here>", result);
            Console.WriteLine("Press any key");
            Console.ReadKey();
        }

        private static void PrintRawResponse(AppResultsHolder rawReport)
        {
            Console.WriteLine(rawReport);
        }

        private static void PrintReport(string token, Report result)
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
                    var v = (TextResultValue)r.getValue();
                    Console.WriteLine(" -> text result type {0} = {1}", r.getName(), v.Data);
                }

                if (type == ResultType.FILE)
                {
                    var v = (FileResultValue)r.getValue();
                    Console.WriteLine(" -> file result type {0} = {1}", r.getName(), v.Url);
                    v.saveTo(token, ".\\");
                }
            }
        }
    }
}
