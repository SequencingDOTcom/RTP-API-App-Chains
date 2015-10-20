namespace Sequencing.AppChainsSample
{
    /// <summary>
    ///  Class that represents generic job identifier 
    /// </summary>
    public class Job
    {
        private readonly long jobId;

        public Job(long jobId)
        {
            this.jobId = jobId;
        }

        public long JobId
        {
            get { return jobId; }
        }
    }
}