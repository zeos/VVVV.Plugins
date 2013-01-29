#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using System.Drawing;
using System.Drawing.Printing;
using VVVV.Core.Logging;
using System.Threading;
using System.Threading.Tasks;

#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "Printer", Category = "Devices", Help = "Basic template with one string in/out", Tags = "")]
	#endregion PluginInfo
	public class DevicesPrinterNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input", DefaultString = "")]
		IDiffSpread<string> FInput;
		
		[Input("Update", IsSingle=true, IsBang=true)]
		IDiffSpread<bool> FUpdate;

		[Output("PrinterName")]
		ISpread<string> FPrinterName;
		
		[Output("Is Valid")]
		ISpread<bool> FIsValid;
		
		[Output("Can Duplex")]
		ISpread<bool> FCanDuplex;
		
		[Output("Is Default Printer")]
		ISpread<bool>FIsDefaultPrinter;
		
		[Output("Supports Color")]
		ISpread<bool>FSupportsColor;
		
		[Output("Paper Sizes")]
		ISpread<ISpread<string>>FPaperSizes;
		
		[Output("Done", IsBang = true)]
		ISpread<bool>FDone;
		
		private Task t = null;

		[Import()]
		ILogger FLogger;
		
		[ImportingConstructor]
		public DevicesPrinterNode(IHDEHost host)
		{
			t = new Task(new Action(taskEval));
		}
		#endregion fields & pins

		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if( (FUpdate.IsChanged && FUpdate[0]) && (t.Status != TaskStatus.Running) )
			{
				//list & process all available printers if FPrinterName is empty
				if(FInput[0] == "") 
				{
					int i = 0;
					FPrinterName.SliceCount = System.Drawing.Printing.PrinterSettings.InstalledPrinters.Count;
					foreach (var printerNameItem in System.Drawing.Printing.PrinterSettings.InstalledPrinters)
					{
						//if(printerNameItem  == "") continue;
						FPrinterName[i] = printerNameItem.ToString();
						i++;
		       		}
				} 
				else FPrinterName.AssignFrom(FInput);
				
				FDone[0] = false;
				if(t.IsCompleted) //run it again
				{
					t.Dispose();
					t = new Task(new Action(taskEval));
				}
				
				t.Start();
				t.ContinueWith(result => {FDone[0] = true;}, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);
	
			}
		}
		
		//Task
		internal void taskEval()
		{

			FIsValid.SliceCount = FPrinterName.SliceCount;
			FCanDuplex.SliceCount = FPrinterName.SliceCount;
			FIsDefaultPrinter.SliceCount = FPrinterName.SliceCount;
			FSupportsColor.SliceCount = FPrinterName.SliceCount;
			FPaperSizes.SliceCount = FPrinterName.SliceCount;
			
			for (int i = 0; i<FPrinterName.SliceCount; i++)
			{
				PrinterSettings ps = new PrinterSettings();
				ps.PrinterName = FPrinterName[i]; // Load the appropriate printer's setting
				FIsValid[i] = ps.IsValid;
				FCanDuplex[i] = ps.CanDuplex;
				FIsDefaultPrinter[i] =  ps.IsDefaultPrinter;
				FSupportsColor[i] = ps.SupportsColor;
				int j = 0;
				FPaperSizes[i].SliceCount = ps.PaperSizes.Count*4;
				for (int paperSizeIdx = 0; paperSizeIdx < ps.PaperSizes.Count; paperSizeIdx++)
				{
	            	PaperSize pkSize = ps.PaperSizes[paperSizeIdx];
				
					FPaperSizes[i][j] 	 = pkSize.Kind.ToString() + " (" + pkSize.RawKind + ")";
					FPaperSizes[i][j+1]  = pkSize.PaperName .ToString();
					FPaperSizes[i][j+2]  = pkSize.Width.ToString();
					FPaperSizes[i][j+3]  = pkSize.Height.ToString();
					
					j = j + 4;
				}
				ps = null;
			}
		}
	}
}
