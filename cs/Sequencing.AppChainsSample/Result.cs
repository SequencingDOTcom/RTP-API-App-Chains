using System;

namespace Sequencing.AppChainsSample
{
    /// <summary>
    /// Class that represents single report result entity
    /// </summary>
    public class Result
    {
        private ResultValue value;
        private String name;
		
        public Result(String name, ResultValue resultValue)
        {
            this.name = name;
            this.value = resultValue;
        }
		
        public ResultValue getValue()
        {
            return value;
        }

        public String getName()
        {
            return name;
        }
    }
}