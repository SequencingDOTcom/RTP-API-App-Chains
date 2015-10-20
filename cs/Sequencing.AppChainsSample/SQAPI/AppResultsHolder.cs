using System.Collections.Generic;
using System.Linq;

namespace Sequencing.AppChainsSample.SQAPI
{
    /// <summary>
    /// App results data holder
    /// </summary>
    public class AppResultsHolder
    {
        private readonly List<ItemDataValue> resultProps = new List<ItemDataValue>();

        public List<ItemDataValue> ResultProps
        {
            get { return resultProps; }
        }

        /// <summary>
        /// Status of application job
        /// </summary>
        public AppStatus Status { get; set; }

        public override string ToString()
        {
            return string.Format("ResultProps: {0}, Status: {1}",
                resultProps.Aggregate("", (s, value) => s + "," + value.ToString()), Status);
        }
    }
}