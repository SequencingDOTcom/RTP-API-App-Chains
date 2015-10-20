using System.Collections.Generic;

namespace Sequencing.AppChainsSample
{
    /// <summary>
    /// Report class represents report available to the end client
    /// </summary>
    public class Report
    {
        public bool Succeeded { get; set; }
        private List<Result> results;

        public List<Result> getResults()
        {
            return results;
        }

        public void setResults(List<Result> results)
        {
            this.results = results;
        }
    }
}