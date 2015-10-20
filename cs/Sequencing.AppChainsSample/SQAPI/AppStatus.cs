using System;

namespace Sequencing.AppChainsSample.SQAPI
{
    /// <summary>
    /// AppStatus class describe the state of the executing app job
    /// </summary>
    public class AppStatus
    {
        public long IdJob { get; set; }
        public string Status { get; set; }
        public bool? CompletedSuccesfully { get; set; }
        public DateTime? FinishDt { get; set; }

        public override string ToString()
        {
            return string.Format("IdJob: {0}, Status: {1}, CompletedSuccesfully: {2}, FinishDt: {3}", IdJob, Status, CompletedSuccesfully, FinishDt);
        }
    }
}