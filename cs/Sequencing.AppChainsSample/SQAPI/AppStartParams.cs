using System.Collections.Generic;

namespace Sequencing.AppChainsSample.SQAPI
{
    /// <summary>
    /// Application start parameters
    /// </summary>
    public class AppStartParams
    {
        public AppStartParams()
        {
            Pars = new List<NewJobParameter>();
        }

        public string AppCode { get; set; }
        public List<NewJobParameter> Pars { get; set; }
    }
}