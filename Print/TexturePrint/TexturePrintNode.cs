#region usings
using System;
using System.ComponentModel.Composition;

using VVVV.PluginInterfaces.V1;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils.VColor;
using VVVV.Utils.VMath;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Printing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using VVVV.Core.Logging;
#endregion usings

namespace VVVV.Nodes
{
	#region PluginInfo
	[PluginInfo(Name = "Print", Category = "Texture", Help = "Prints a texture", Tags = "print")]
	#endregion PluginInfo
	public class TexturePrintNode : IPluginEvaluate
	{
		#region fields & pins
		[Input("Input", DefaultString = "", IsSingle = true)]
		ISpread<string> FInput;
		
		[Input("Printer Name", DefaultString = "", IsSingle = true)]
		ISpread<string> FPrinterName;
		
		[Input("X", DefaultValue = 0, IsSingle = true)]
		ISpread<int> FXpos;
		
		[Input("Y", DefaultValue = 0, IsSingle = true)]
		ISpread<int> FYpos;
		
		[Input("Fit to Page", IsSingle = true)]
		ISpread<bool> FFit;
		
		[Input("Landscape", IsSingle = true)]
		ISpread<bool> FLandscape;
		
		[Input("Paper Size", IsSingle = true)]
		ISpread<string> FPaperSize;
		
		[Input("Color", IsSingle = true)]
		ISpread<bool> FColor;
		
		[Input("Margins", IsSingle = true)]
		ISpread<Vector4D> FMargins;

		[Input("Print", IsSingle=true, IsBang=true)]
		IDiffSpread<bool> FPrint;

		//-----------------------------

		[Output("Done", IsBang=true)]
		ISpread<bool> FDone;
		
		private Task t = null;
		private PrintDocument pd = null;
		private Image img = null;
		private bool bStatus = false;
		
		[Import()]
		ILogger FLogger;
		#endregion fields & pins
		
		[ImportingConstructor]
		public TexturePrintNode(IHDEHost host)
		{
			pd = new PrintDocument();
			pd.EndPrint  += (sender, e) =>
	     	{	
	     		bStatus = true;
	     	};
			
	     	pd.PrintPage += (sender, e) =>
	     	{	
	     		if(img == null) return;
	     		float w;
	     		float h;
	     		
	     		if(FFit[0])
	     		{
	     			float quotient = 1;
					float page_w = e.PageBounds.Width - FXpos[0] - e.PageSettings.Margins.Left - e.PageSettings.Margins.Right;
					float page_h = e.PageBounds.Height - FYpos[0] - e.PageSettings.Margins.Top - e.PageSettings.Margins.Bottom;
				
					if (img.Width >= img.Height)
					{
						quotient = page_w / img.Width;
					}
	     			
					if (img.Width < img.Height)
					{
						quotient =  img.Height / page_h;
					}
				
					w = page_w;
					h = img.Height * quotient;
					
	     			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
					e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
					
	     			//strech
	     			//e.Graphics.DrawImage(img, 
	     			//	e.Graphics.VisibleClipBounds.Location.X, 
	     			//	e.Graphics.VisibleClipBounds.Location.Y,
       				//	e.Graphics.VisibleClipBounds.Size.Width, 
	     			//	e.Graphics.VisibleClipBounds.Size.Height);
	     		} 
	     		else
	     		{
	     			w = img.Width;
	     			h = img.Height;
	     		}
	     		
	     		e.Graphics.DrawImage(img, FXpos[0], FYpos[0], w, h);
	     		e.HasMorePages = false;
	     	};
			
			t = new Task(new Action(taskEval));
		}
		
		//called when data for any output pin is requested
		public void Evaluate(int SpreadMax)
		{
			if((FPrint.IsChanged && (FPrint[0]))  && (t.Status != TaskStatus.Running))
			{			
				FDone[0] = false;
	
				//restart the task
				if(t.IsCompleted) 
				{
					t.Dispose();
					t = new Task(new Action(taskEval));
				}
				t.Start();
				t.ContinueWith(result => {FDone[0] = (true && bStatus);}, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.AttachedToParent);		
			}
		}
		
		internal void taskEval()
		{	 	
			
			if(!string.IsNullOrEmpty(FPrinterName[0])) 
				pd.PrinterSettings.PrinterName = FPrinterName[0];
		        	
			if (pd.PrinterSettings.IsValid) 
			{
				img = null;
				byte[] byteArray = System.Text.ASCIIEncoding.Default.GetBytes(FInput[0]);
				try
				{	
					//convert the string into image
					img = Image.FromStream(new MemoryStream(byteArray));
					
					//settings
					pd.DocumentName = "vvv";
					pd.OriginAtMargins = true;
					pd.DefaultPageSettings.Landscape = FLandscape[0];
					pd.DefaultPageSettings.Color = FColor[0];
					pd.DefaultPageSettings.Margins = new Margins(Convert.ToInt32(FMargins[0].x), Convert.ToInt32(FMargins[0].y), Convert.ToInt32(FMargins[0].z), Convert.ToInt32(FMargins[0].w));
		        	//set paper size
					foreach (PaperSize p in pd.PrinterSettings.PaperSizes)
					{
						if(p.PaperName == FPaperSize[0]) {
							pd.DefaultPageSettings.PaperSize = p;
							break;
						}
					}
         			
					pd.Print();
				}	
				catch (Exception ex)
				{
					FLogger.Log(LogType.Debug, "Can not load image data: " + ex.Message.ToString());
				}
      		} 
			else 
			 	FLogger.Log(LogType.Debug, "Selected printer is invalid. ");
				
		}
	}
}
