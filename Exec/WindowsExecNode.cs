#region usings
using System;
using System.ComponentModel.Composition;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Data;
using System.Diagnostics;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "Exec", Category = "Windows", AutoEvaluate=true, Help = "Executes a program (w/o parameters) and returns the output, errors and status", Tags = "execute, windows, process", Author="zeos")]
	#endregion PluginInfo
	public class WindowsExecNode : IPluginEvaluate, IDisposable
	{
		#region fields & pins & contructor & destructor & private members 
		[Input("Executable file", DefaultString = "CMD.EXE /c dir", StringType = StringType.Filename, IsSingle = true, Order = 0)]
		ISpread<string> FInput;
		
		[Input("Arguments", DefaultString = "", IsSingle = true, Order = 1)]
		ISpread<string>FArgs;
		
		[Input("Show Window", DefaultValue = 0,IsSingle = true, Order = 2)]
		ISpread<bool>FWindow;
		
		[Input("Block until finished", DefaultValue = 0, IsSingle = true, Order = 3)]
		ISpread<bool>FWait;
		
		[Input("Kill", DefaultValue = 0,IsBang=true, IsSingle = true, Order =4)]
		IDiffSpread<bool>FKill;
		
		[Input("Execute", IsBang=true, IsSingle = true, Order = 5)]
		IDiffSpread<bool> FExecute;

		///////////////////// OUTPUTS 
		[Output("Output")]
		ISpread<string> FOutput;
		
		[Output("Error")]
		ISpread<string> FError;
		
		[Output("PID", IsSingle = true, Visibility=PinVisibility.Hidden)]
		ISpread<string> FProcID;
		
		[Output("ExitCode", IsSingle = true)]
		ISpread<int> FExitCode;
		
		[Output("IsRunning", IsBang = true, IsSingle = true)]
		ISpread<bool> FRunning;
		
		[Output("Completed", IsBang = true, IsSingle = true)]
		ISpread<bool> FCompleted;
		
		private 	Process 			_p 						= null;
		private 	ProcessStartInfo 	_pStartInfo 			= null;
		private 	bool 				_bRunning 				= false;
		private 	bool				_bCompleted 			= false;
		private  	StreamReader 		_StandardOutputReader 	= null;
		private  	StreamReader 		_StandardErrorReader 	= null;
		private 	object 				l 						= new object();
		private 	bool 				FDisposed;
		private 	int 				_doBang 				= 0;
		[Import()]
		ILogger FLogger;
		
		[ImportingConstructor]
		public WindowsExecNode(IHDEHost host)
		{
			_p = new Process();
	        _p.EnableRaisingEvents 	= true;
	        _p.OutputDataReceived 	+= OnDataReceived;
	        _p.ErrorDataReceived 	+= OnErrorReceived;
			_p.Exited 				+= OnExited;	
			
			_p.StartInfo.RedirectStandardOutput = true;
	        _p.StartInfo.RedirectStandardError = true;
			_p.StartInfo.UseShellExecute = false;
		}
		
		~WindowsExecNode()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		protected void Dispose(bool disposing)
		{
			// Check to see if Dispose has already been called.
			if(!FDisposed)
			{
				if(disposing)
				{
					// Dispose managed resources.
					_p.OutputDataReceived 	-= OnDataReceived;
		       	 	_p.ErrorDataReceived 	-= OnErrorReceived;
					_p.Exited 				-= OnExited;		
				}
				// Release unmanaged resources. If disposing is false,
				// only the following code is executed.

			}
			FDisposed = true;
		}
		#endregion fields & pins

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if((FExecute.IsChanged && FExecute[0]) && (!string.IsNullOrEmpty(FInput[0])) && !_bRunning)
			{
				FOutput.SliceCount = 0;
				FError.SliceCount = 0;
				FProcID.SliceCount = 1;
				FExitCode.SliceCount = 1;
				FExitCode[0] = -1;
				FProcID[0] = string.Empty;
				FCompleted[0] = false;
				_bCompleted = false;
				_bRunning = false;
				
				try 
				{
					_p.CancelOutputRead();
					_p.CancelErrorRead();
				}
				catch 
				{
					
				}
				
				//set up process info
		        _p.StartInfo.CreateNoWindow = !FWindow[0];
				_p.StartInfo.FileName = FInput[0];
				_p.StartInfo.Arguments = FArgs[0];
				
				try
				{
					_bRunning = _p.Start();
					
					FProcID[0] = _p.Id.ToString();
					
					_p.BeginOutputReadLine();
					_p.BeginErrorReadLine();
					
					if(FWait[0]) 
					{
						_p.WaitForExit();
						_doBang = 2;
					}
				
				}
				catch (Exception e)
				{
					FError.Add("Error on start: " +e.Message); 
				}
			}
			
			
			if(_doBang>0) 
			{
				_doBang--;
				if(_doBang == 0)
				FRunning[0] = true;
			}
			else lock(l) { FCompleted[0] = _bCompleted; }
			
			lock(l) { FRunning[0] = _bRunning; }
			
			//kill the process?
			if(FKill.IsChanged && FKill[0] && _bRunning && !_bCompleted)
			{
				try
				{
					_p.Kill();	
				}
				catch (Exception e)
				{
					FError.Add("Error on kill: " + e.Message);
				}
			}
		}
		 
		internal void OnDataReceived(object sender, DataReceivedEventArgs e)
		{
			//FLogger.Log(LogType.Debug, "DataReceived.");
			if (e.Data != null)
		    {
		        string ln = (e.Data) + Environment.NewLine;
		    	//FOutputQueue.Enqueue(ln);
		    	FOutput.Add(ln);
		    
			}
		}
		
		internal void OnErrorReceived(object sender, DataReceivedEventArgs e)
		{
			//FLogger.Log(LogType.Debug, "ErrorReceived.{0}", e.Data);
		    if( e.Data != null ) 
			{
				string ln = (e.Data) + Environment.NewLine;
				FError.Add(ln);
			}
		}
		
		internal void OnExited(object sender, EventArgs e)
		{
			//FLogger.Log(LogType.Debug, "Done.");
			var p = sender as System.Diagnostics.Process;
			if(p != null) lock(l) 
			{
				FExitCode[0] = _p.ExitCode;
				FProcID[0] = string.Empty;
				
				_bRunning = false;
				_bCompleted = true;
				
			}
		}
	}
}
