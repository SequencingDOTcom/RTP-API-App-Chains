using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sequencing.AppChainsSample.SQAPI
{
    public class BatchAppStartParams
    {
        public BatchAppStartParams()
        {
            Pars = new List<AppStartParams>();
        }
        public List<AppStartParams> Pars { get; set; }
    }
}
