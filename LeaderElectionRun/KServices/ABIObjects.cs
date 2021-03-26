using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LeaderElectionRun.KServices.ABIObjects
{
	public class ExecEventArgs
	{
		public string Id { get; set; }
		public string LeaderId { get; set; }
	}
}
