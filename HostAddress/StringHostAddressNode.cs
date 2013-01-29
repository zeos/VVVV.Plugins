#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
#endregion usings
#region usings
using System;
using System.ComponentModel.Composition;
using System.Net;
using System.Collections;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using VVVV.Utils.Concurrent;
using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "HostAddress", Category = "Network", Help = "Returns Host By given name", Tags = "Network", Author="Zeos")]
	#endregion PluginInfo
	public class StringHostAddressNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Host Name", DefaultString = "vvvv.org")]
		IDiffSpread<string> FInput;

		[Output("IP Address")]
		ISpread<ISpread<string>> FOutput;

		[Import()]
		ILogger FLogger;
		
		private class TRes {
			
			public string host;
			public int idx;
			public bool Resolved;
			public string[] ips;
			
		}
		
		private readonly ConcurrentQueue<List<string>> FResultQueue = new ConcurrentQueue<List<string>>();
		
		#endregion fields & pins

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if(FInput.IsChanged)
			{
				FOutput.SliceCount = FInput.SliceCount;
	
				for (int i = 0; i < FInput.SliceCount; i++) {
					
					var r = new TRes();
					r.host = FInput[i];
					r.idx = i;
					r.Resolved = false;
					var t = new Task<TRes>(() => _GetHostByName(r));
					var c = t.ContinueWith((res) =>
					{
					   if(res.Result.ips != null)
					   {
							FOutput[res.Result.idx].SliceCount = res.Result.ips.Length;
							for(int j = 0; j<res.Result.ips.Length;j++)
							{
								FOutput[res.Result.idx][j] = res.Result.ips[j];
							}
						} else FOutput[res.Result.idx].SliceCount = 0;
					},TaskContinuationOptions.OnlyOnRanToCompletion);
					t.Start();
					
					
				}
				
			}
			
			
			//FLogger.Log(LogType.Debug, "Logging to Renderer (TTY)");
		}
		
		private TRes _GetHostByName(TRes r)
		{
			r.ips = null;
			try
            {
                IPHostEntry hostname = Dns.GetHostEntry(r.host);
            	if(hostname != null)
            	{
                	IPAddress[] ip = hostname.AddressList;
            		if(ip.Length>0)
                	{
                		r.ips = new string[ip.Length];
                		for (int i = 0; i< ip.Length; i++)
                		{
                			r.ips[i] = (ip[i].ToString());
                		}
                		//FLogger.Log(LogType.Debug, ip.Length.ToString());
                	}
            	}
            }
            catch (Exception ex)
            {
            	FLogger.Log(LogType.Debug,"_GetHostByName: " + ex.ToString());
            
            	
            }
			return r;
		}
	}
}

