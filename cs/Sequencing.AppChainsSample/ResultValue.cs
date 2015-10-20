namespace Sequencing.AppChainsSample
{
    /// <summary>
    /// Base class for result values
    /// </summary>
    public class ResultValue
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
}