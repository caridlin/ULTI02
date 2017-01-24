// ==============================================================================
//
//  File:                         ULTI02.CS
//
//  Library Call Demonstrated:    MccDaq.MccBoard.TInScan()
//
//  Purpose:                      Scans temperature input channels.
//
//  Demonstration:                Displays the temperature inputs on a
//                                range of channels.
//
//  Other Library Calls:          MccDaq.MccService.ErrHandling()
//
//  Special Requirements:         Unless the board at BoardNum(=0) does not use
//                                EXP boards for temperature measurements(the
//                                CIO-DAS-TC or USB-2001-TC for example), it must
//                                have an A/D converter with an attached EXP
//                                board.  Thermocouples must be wired to EXP
//                                channels selected.
// ==============================================================================
using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms.DataVisualization.Charting;

using DigitalIO;
using MccDaq;

namespace ULTI02
{
	public class frmDataDisplay : System.Windows.Forms.Form
	{

        private MccDaq.MccBoard DaqBoard = new MccDaq.MccBoard(1);
        
        private MccDaq.ErrorInfo ULStat;
		private int UsesEXPs=0;
        private float[] DataBuffer = new float[16]; // array to hold  the temperatures

        // for DIO
        int NumPorts, NumBits, FirstBit;
        int PortType, ProgAbility;

        MccDaq.DigitalPortType PortNum;
        MccDaq.DigitalPortDirection Direction;
        public Label lblInstruct;
        public Label lblValueSet;
        
        DigitalIO.clsDigitalIO DioProps = new DigitalIO.clsDigitalIO();

        System.Windows.Forms.DataVisualization.Charting.Chart chart1;

        double timeElapsed = 0;

        private void frmDataDisplay_Load(object sender, EventArgs e)
        {

            InitUL();

            // Determine if the board uses EXP boards for temperature measurements
            UsesEXPs = 0;
            ULStat = DaqBoard.BoardConfig.GetUsesExps(out UsesEXPs);

            string PortName;

            //determine if digital port exists, its capabilities, etc
            PortType = clsDigitalIO.PORTOUT;
            NumPorts = DioProps.FindPortsOfType(DaqBoard, PortType, out ProgAbility,
                out PortNum, out NumBits, out FirstBit);
            if (NumBits > 8) NumBits = 8;

            // if programmable, set direction of port to output
            // configure the first port for digital output
            //  Parameters:
            //    PortNum        :the output port
            //    Direction      :sets the port for input or output
            if (ProgAbility == clsDigitalIO.PROGPORT)
            {
                Direction = MccDaq.DigitalPortDirection.DigitalOut;
                ULStat = DaqBoard.DConfigPort(PortNum, Direction);
            }
            PortName = PortNum.ToString();
            for (int i = 0; i <= 7; ++i)
            {
                chart1.Series.Add("Ch" + i.ToString("0"));
                chart1.Series["Ch" + i.ToString("0")].ChartType = SeriesChartType.Line;
            }


        }

        private void cmdStartConvert_Click(object eventSender, System.EventArgs eventArgs)
        {
            cmdStartConvert.Visible = false;
            cmdStopConvert.Visible = true;
            tmrConvert.Enabled = true;
        }

        private void tmrConvert_Tick(object eventSender, System.EventArgs eventArgs)
        {

            tmrConvert.Stop();

            //  Collect the data with MccDaq.MccBoard.TInScan()
            //   Input values will be collected from a range of thermocouples.
            //   The data value will be updated and displayed until a key is pressed.
            //   Parameters:
            //     LowChan       :the starting channel of the scan
            //     HighChan      :the ending channel of the scan
            //     MccScale      :temperature scale (Celsius, Fahrenheit, Kelvin)
            //     DataBuffer()  :the array where the temperature values are collected

            MccDaq.TempScale MccScale = MccDaq.TempScale.Celsius;

            int ADChan = 0; // the channel on the A/D
            int LowMux = hsbLoChan.Value, LowChan = 0;
            int HighMux = hsbHiChan.Value, HighChan = 0;
            int LowTemp = hsbLTChan.Value;
            int HighTemp = hsbHTChan.Value;

            MccDaq.ThermocoupleOptions Options = 0;
            if (UsesEXPs > 0)
            {
                LowChan = (ADChan + 1) * 16 + LowMux;
                HighChan = (ADChan + 1) * 16 + HighMux;
            }
            else
            {
                LowChan = LowMux;
                HighChan = HighMux;
            }


            MccDaq.ErrorInfo ULStat = DaqBoard.TInScan(LowChan, HighChan, MccScale, DataBuffer, Options);

            for (int j = 0; j <= LowMux - 1; ++j)
            {
                lblShowData[j].Text = "";
                lblBitStatus[j].Text = "";
            }

            for (int j = HighMux + 1; j < 8; ++j)
            {
                lblShowData[j].Text = "";
                lblBitStatus[j].Text = "";
            }

            bool flag = File.Exists(FilePathChan.Text);


            using (StreamWriter sw = File.AppendText(FilePathChan.Text))
            {
                if (!flag)
                {
                    for (int i = LowMux, Element = 0; i <= HighMux; ++i, ++Element)
                    {
                        sw.Write("C"+ Element.ToString("0") + "(°C)" + "\t");
                    }
                    sw.Write("\r\n");
                }
                for (int i = LowMux, Element = 0; i <= HighMux; ++i, ++Element)
                {
                    lblShowData[i].Text = DataBuffer[Element].ToString("00.000") + "°C"; //  print the value
                    sw.Write(DataBuffer[Element] + "\t");


                    chart1.Series["Ch" + Element.ToString("0")].Points.AddXY(timeElapsed, DataBuffer[Element]);
                    chart1.Series["Ch" + Element.ToString("0")].ChartArea = "ChartArea1";
                    chart1.ChartAreas[0].AxisY.IsStartedFromZero = false;
                    chart1.ChartAreas[0].AxisX.IsMarginVisible = false;


                    int BitNum = Element;
                    MccDaq.DigitalPortType BitPort;
                    MccDaq.DigitalLogicState BitValue = MccDaq.DigitalLogicState.Low;

                    if (DataBuffer[Element] < LowTemp || DataBuffer[Element] > HighTemp)
                    {
                        BitValue = MccDaq.DigitalLogicState.High;
                        lblBitStatus[i].Text = "High";
                    }
                    else
                    {
                        lblBitStatus[i].Text = "Low";
                    }

                    //the port must be AuxPort or FirstPortA for bit output
                    BitPort = MccDaq.DigitalPortType.AuxPort;
                    if (PortNum > MccDaq.DigitalPortType.AuxPort)
                        BitPort = MccDaq.DigitalPortType.FirstPortA;

                    MccDaq.ErrorInfo ULStat1 = DaqBoard.DBitOut(BitPort, FirstBit + BitNum, BitValue);
                }
                sw.Write("\r\n");
            }

            if (ULStat.Value == MccDaq.ErrorInfo.ErrorCode.NoErrors)
            {
                tmrConvert.Start();
                timeElapsed += 0.001 * tmrConvert.Interval;
            }
            
        }

        private void hsbHiChan_Change(int newScrollValue)
        {
            if (newScrollValue < hsbLoChan.Value)
            {
                hsbLoChan.Value = newScrollValue;
                lblLoChan.Text = newScrollValue.ToString("0");
            }

            lblHiChan.Text = newScrollValue.ToString("0");

        }

        private void hsbHTChan_Change(int newScrollValue)
        {
            if (newScrollValue < hsbLoChan.Value)
            {
                hsbLTChan.Value = newScrollValue;
                lblLTChan.Text = newScrollValue.ToString("0");
            }

            lblHTChan.Text = newScrollValue.ToString("0");
        }

        private void hsbLoChan_Change(int newScrollValue)
        {
            if (hsbHiChan.Value < newScrollValue)
            {
                hsbHiChan.Value = newScrollValue;
                lblHiChan.Text = newScrollValue.ToString("0");
            }

            lblLoChan.Text = newScrollValue.ToString("0");

        }

        private void hsbLTChan_Change(int newScrollValue)
        {
            if (hsbHTChan.Value < newScrollValue)
            {
                hsbHTChan.Value = newScrollValue;
                lblHTChan.Text = newScrollValue.ToString("0");
            }

            lblLTChan.Text = newScrollValue.ToString("0");

        }

        private void hsbHiChan_Scroll(object eventSender, System.Windows.Forms.ScrollEventArgs eventArgs)
        {

            if (eventArgs.Type == System.Windows.Forms.ScrollEventType.EndScroll)
                hsbHiChan_Change(eventArgs.NewValue);

        }

        private void hsbHTChan_Scroll(object eventSender, System.Windows.Forms.ScrollEventArgs eventArgs)
        {

            if (eventArgs.Type == System.Windows.Forms.ScrollEventType.EndScroll)
                hsbHTChan_Change(eventArgs.NewValue);

        }

        private void hsbLoChan_Scroll(object eventSender, System.Windows.Forms.ScrollEventArgs eventArgs)
        {
            if (eventArgs.Type == System.Windows.Forms.ScrollEventType.EndScroll)
                hsbLoChan_Change(eventArgs.NewValue);
        }

        private void hsbLTChan_Scroll(object eventSender, System.Windows.Forms.ScrollEventArgs eventArgs)
        {
            if (eventArgs.Type == System.Windows.Forms.ScrollEventType.EndScroll)
                hsbLTChan_Change(eventArgs.NewValue);
        }

        private void cmdStopConvert_Click(object eventSender, System.EventArgs eventArgs)
        {
            ushort DataValue = 0;

            if (ProgAbility == clsDigitalIO.PROGPORT)
            {
                MccDaq.ErrorInfo ULStat = DaqBoard.DOut(PortNum, DataValue);

                Direction = MccDaq.DigitalPortDirection.DigitalIn;
                ULStat = DaqBoard.DConfigPort(PortNum, Direction);
            }
            Application.Exit();
        }

        private void InitUL()
        {

            //  Initiate error handling
            //   activating error handling will trap errors like
            //   bad channel numbers and non-configured conditions.
            //   Parameters:
            //     MccDaq.ErrorReporting.PrintAll :all warnings and errors encountered will be printed
            //     MccDaq.ErrorHandling.StopAll   :if an error is encountered, the program will stop

            MccDaq.ErrorInfo ULStat = MccDaq.MccService.ErrHandling
                (MccDaq.ErrorReporting.DontPrint, MccDaq.ErrorHandling.StopAll);

            lblShowData = (new Label[]{_lblShowData_0, _lblShowData_1,
                _lblShowData_2, _lblShowData_3, _lblShowData_4, _lblShowData_5,
                _lblShowData_6, _lblShowData_7 });

            lblBitStatus = (new Label[]{_lblBitStatus_0, _lblBitStatus_1,
                _lblBitStatus_2, _lblBitStatus_3, _lblBitStatus_4, _lblBitStatus_5, 
                _lblBitStatus_6, _lblBitStatus_7});

        }
	    
		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
	    
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            this.ToolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.cmdStartConvert = new System.Windows.Forms.Button();
            this.cmdStopConvert = new System.Windows.Forms.Button();
            this.hsbHiChan = new System.Windows.Forms.HScrollBar();
            this.hsbHTChan = new System.Windows.Forms.HScrollBar();
            this.tmrConvert = new System.Windows.Forms.Timer(this.components);
            this.hsbLoChan = new System.Windows.Forms.HScrollBar();
            this.hsbLTChan = new System.Windows.Forms.HScrollBar();
            this._lblBitStatus_7 = new System.Windows.Forms.Label();
            this._lblDIO_7 = new System.Windows.Forms.Label();
            this._lblShowData_7 = new System.Windows.Forms.Label();
            this._lblChanNum_7 = new System.Windows.Forms.Label();
            this._lblBitStatus_6 = new System.Windows.Forms.Label();
            this._lblDIO_6 = new System.Windows.Forms.Label();
            this._lblShowData_6 = new System.Windows.Forms.Label();
            this._lblChanNum_6 = new System.Windows.Forms.Label();
            this._lblBitStatus_5 = new System.Windows.Forms.Label();
            this._lblDIO_5 = new System.Windows.Forms.Label();
            this._lblShowData_5 = new System.Windows.Forms.Label();
            this._lblChanNum_5 = new System.Windows.Forms.Label();
            this._lblBitStatus_4 = new System.Windows.Forms.Label();
            this._lblDIO_4 = new System.Windows.Forms.Label();
            this._lblShowData_4 = new System.Windows.Forms.Label();
            this._lblChanNum_4 = new System.Windows.Forms.Label();
            this._lblBitStatus_3 = new System.Windows.Forms.Label();
            this._lblShowData_3 = new System.Windows.Forms.Label();
            this._lblChanNum_3 = new System.Windows.Forms.Label();
            this._lblBitStatus_2 = new System.Windows.Forms.Label();
            this._lblShowData_2 = new System.Windows.Forms.Label();
            this._lblChanNum_2 = new System.Windows.Forms.Label();
            this._lblBitStatus_1 = new System.Windows.Forms.Label();
            this._lblShowData_1 = new System.Windows.Forms.Label();
            this._lblChanNum_1 = new System.Windows.Forms.Label();
            this._lblBitStatus_0 = new System.Windows.Forms.Label();
            this._lblDIO_0 = new System.Windows.Forms.Label();
            this._lblShowData_0 = new System.Windows.Forms.Label();
            this._lblChanNum_0 = new System.Windows.Forms.Label();
            this.lblTemp2 = new System.Windows.Forms.Label();
            this.lblChan2 = new System.Windows.Forms.Label();
            this.lblTemp1 = new System.Windows.Forms.Label();
            this.lblChan1 = new System.Windows.Forms.Label();
            this.lblLastChan = new System.Windows.Forms.Label();
            this.lblHiChan = new System.Windows.Forms.Label();
            this.lblHT = new System.Windows.Forms.Label();
            this.lblHTChan = new System.Windows.Forms.Label();
            this.lblFirstChan = new System.Windows.Forms.Label();
            this.lblLoChan = new System.Windows.Forms.Label();
            this.lblLT = new System.Windows.Forms.Label();
            this.lblLTChan = new System.Windows.Forms.Label();
            this.lblChanPrompt = new System.Windows.Forms.Label();
            this.lblDemoFunction = new System.Windows.Forms.Label();
            this.FilePath = new System.Windows.Forms.Label();
            this.FilePathChan = new System.Windows.Forms.TextBox();
            this._lblDIO_1 = new System.Windows.Forms.Label();
            this._lblDIO_2 = new System.Windows.Forms.Label();
            this._lblDIO_3 = new System.Windows.Forms.Label();
            this.chart1 = new System.Windows.Forms.DataVisualization.Charting.Chart();
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).BeginInit();
            this.SuspendLayout();
            // 
            // cmdStartConvert
            // 
            this.cmdStartConvert.BackColor = System.Drawing.SystemColors.Control;
            this.cmdStartConvert.Cursor = System.Windows.Forms.Cursors.Default;
            this.cmdStartConvert.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdStartConvert.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cmdStartConvert.Location = new System.Drawing.Point(417, 467);
            this.cmdStartConvert.Name = "cmdStartConvert";
            this.cmdStartConvert.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.cmdStartConvert.Size = new System.Drawing.Size(66, 31);
            this.cmdStartConvert.TabIndex = 3;
            this.cmdStartConvert.Text = "Start";
            this.cmdStartConvert.UseVisualStyleBackColor = false;
            this.cmdStartConvert.Click += new System.EventHandler(this.cmdStartConvert_Click);
            // 
            // cmdStopConvert
            // 
            this.cmdStopConvert.BackColor = System.Drawing.SystemColors.Control;
            this.cmdStopConvert.Cursor = System.Windows.Forms.Cursors.Default;
            this.cmdStopConvert.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.cmdStopConvert.ForeColor = System.Drawing.SystemColors.ControlText;
            this.cmdStopConvert.Location = new System.Drawing.Point(417, 467);
            this.cmdStopConvert.Name = "cmdStopConvert";
            this.cmdStopConvert.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.cmdStopConvert.Size = new System.Drawing.Size(66, 31);
            this.cmdStopConvert.TabIndex = 4;
            this.cmdStopConvert.Text = "Quit";
            this.cmdStopConvert.UseVisualStyleBackColor = false;
            this.cmdStopConvert.Visible = false;
            this.cmdStopConvert.Click += new System.EventHandler(this.cmdStopConvert_Click);
            // 
            // hsbHiChan
            // 
            this.hsbHiChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.hsbHiChan.LargeChange = 1;
            this.hsbHiChan.Location = new System.Drawing.Point(59, 108);
            this.hsbHiChan.Maximum = 7;
            this.hsbHiChan.Name = "hsbHiChan";
            this.hsbHiChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.hsbHiChan.Size = new System.Drawing.Size(197, 21);
            this.hsbHiChan.TabIndex = 6;
            this.hsbHiChan.TabStop = true;
            this.hsbHiChan.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hsbHiChan_Scroll);
            // 
            // hsbHTChan
            // 
            this.hsbHTChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.hsbHTChan.LargeChange = 1;
            this.hsbHTChan.Location = new System.Drawing.Point(59, 416);
            this.hsbHTChan.Name = "hsbHTChan";
            this.hsbHTChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.hsbHTChan.Size = new System.Drawing.Size(197, 21);
            this.hsbHTChan.TabIndex = 6;
            this.hsbHTChan.TabStop = true;
            this.hsbHTChan.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hsbHTChan_Scroll);
            // 
            // tmrConvert
            // 
            this.tmrConvert.Interval = 250;
            this.tmrConvert.Tick += new System.EventHandler(this.tmrConvert_Tick);
            // 
            // hsbLoChan
            // 
            this.hsbLoChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.hsbLoChan.LargeChange = 1;
            this.hsbLoChan.Location = new System.Drawing.Point(59, 78);
            this.hsbLoChan.Maximum = 7;
            this.hsbLoChan.Name = "hsbLoChan";
            this.hsbLoChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.hsbLoChan.Size = new System.Drawing.Size(197, 21);
            this.hsbLoChan.TabIndex = 5;
            this.hsbLoChan.TabStop = true;
            this.hsbLoChan.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hsbLoChan_Scroll);
            // 
            // hsbLTChan
            // 
            this.hsbLTChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.hsbLTChan.LargeChange = 1;
            this.hsbLTChan.Location = new System.Drawing.Point(59, 384);
            this.hsbLTChan.Name = "hsbLTChan";
            this.hsbLTChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.hsbLTChan.Size = new System.Drawing.Size(197, 21);
            this.hsbLTChan.TabIndex = 5;
            this.hsbLTChan.TabStop = true;
            this.hsbLTChan.Scroll += new System.Windows.Forms.ScrollEventHandler(this.hsbLTChan_Scroll);
            // 
            // _lblBitStatus_7
            // 
            this._lblBitStatus_7.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_7.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_7.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_7.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_7.Location = new System.Drawing.Point(327, 345);
            this._lblBitStatus_7.Name = "_lblBitStatus_7";
            this._lblBitStatus_7.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_7.Size = new System.Drawing.Size(75, 21);
            this._lblBitStatus_7.TabIndex = 23;
            this._lblBitStatus_7.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblDIO_7
            // 
            this._lblDIO_7.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_7.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_7.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_7.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_7.Location = new System.Drawing.Point(261, 345);
            this._lblDIO_7.Name = "_lblDIO_7";
            this._lblDIO_7.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_7.Size = new System.Drawing.Size(48, 21);
            this._lblDIO_7.TabIndex = 43;
            this._lblDIO_7.Text = "7";
            this._lblDIO_7.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_7
            // 
            this._lblShowData_7.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_7.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_7.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_7.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_7.Location = new System.Drawing.Point(131, 345);
            this._lblShowData_7.Name = "_lblShowData_7";
            this._lblShowData_7.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_7.Size = new System.Drawing.Size(75, 21);
            this._lblShowData_7.TabIndex = 15;
            this._lblShowData_7.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_7
            // 
            this._lblChanNum_7.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_7.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_7.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_7.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_7.Location = new System.Drawing.Point(65, 345);
            this._lblChanNum_7.Name = "_lblChanNum_7";
            this._lblChanNum_7.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_7.Size = new System.Drawing.Size(48, 21);
            this._lblChanNum_7.TabIndex = 35;
            this._lblChanNum_7.Text = "7";
            this._lblChanNum_7.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblBitStatus_6
            // 
            this._lblBitStatus_6.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_6.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_6.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_6.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_6.Location = new System.Drawing.Point(327, 325);
            this._lblBitStatus_6.Name = "_lblBitStatus_6";
            this._lblBitStatus_6.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_6.Size = new System.Drawing.Size(75, 21);
            this._lblBitStatus_6.TabIndex = 22;
            this._lblBitStatus_6.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblDIO_6
            // 
            this._lblDIO_6.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_6.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_6.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_6.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_6.Location = new System.Drawing.Point(261, 325);
            this._lblDIO_6.Name = "_lblDIO_6";
            this._lblDIO_6.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_6.Size = new System.Drawing.Size(48, 21);
            this._lblDIO_6.TabIndex = 42;
            this._lblDIO_6.Text = "6";
            this._lblDIO_6.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_6
            // 
            this._lblShowData_6.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_6.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_6.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_6.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_6.Location = new System.Drawing.Point(131, 325);
            this._lblShowData_6.Name = "_lblShowData_6";
            this._lblShowData_6.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_6.Size = new System.Drawing.Size(75, 21);
            this._lblShowData_6.TabIndex = 14;
            this._lblShowData_6.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_6
            // 
            this._lblChanNum_6.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_6.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_6.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_6.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_6.Location = new System.Drawing.Point(65, 325);
            this._lblChanNum_6.Name = "_lblChanNum_6";
            this._lblChanNum_6.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_6.Size = new System.Drawing.Size(48, 21);
            this._lblChanNum_6.TabIndex = 34;
            this._lblChanNum_6.Text = "6";
            this._lblChanNum_6.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblBitStatus_5
            // 
            this._lblBitStatus_5.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_5.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_5.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_5.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_5.Location = new System.Drawing.Point(327, 305);
            this._lblBitStatus_5.Name = "_lblBitStatus_5";
            this._lblBitStatus_5.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_5.Size = new System.Drawing.Size(75, 21);
            this._lblBitStatus_5.TabIndex = 21;
            this._lblBitStatus_5.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblDIO_5
            // 
            this._lblDIO_5.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_5.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_5.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_5.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_5.Location = new System.Drawing.Point(261, 305);
            this._lblDIO_5.Name = "_lblDIO_5";
            this._lblDIO_5.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_5.Size = new System.Drawing.Size(48, 21);
            this._lblDIO_5.TabIndex = 41;
            this._lblDIO_5.Text = "5";
            this._lblDIO_5.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_5
            // 
            this._lblShowData_5.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_5.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_5.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_5.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_5.Location = new System.Drawing.Point(131, 305);
            this._lblShowData_5.Name = "_lblShowData_5";
            this._lblShowData_5.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_5.Size = new System.Drawing.Size(75, 21);
            this._lblShowData_5.TabIndex = 13;
            this._lblShowData_5.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_5
            // 
            this._lblChanNum_5.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_5.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_5.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_5.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_5.Location = new System.Drawing.Point(65, 305);
            this._lblChanNum_5.Name = "_lblChanNum_5";
            this._lblChanNum_5.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_5.Size = new System.Drawing.Size(48, 21);
            this._lblChanNum_5.TabIndex = 33;
            this._lblChanNum_5.Text = "5";
            this._lblChanNum_5.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblBitStatus_4
            // 
            this._lblBitStatus_4.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_4.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_4.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_4.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_4.Location = new System.Drawing.Point(327, 286);
            this._lblBitStatus_4.Name = "_lblBitStatus_4";
            this._lblBitStatus_4.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_4.Size = new System.Drawing.Size(75, 20);
            this._lblBitStatus_4.TabIndex = 20;
            this._lblBitStatus_4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblDIO_4
            // 
            this._lblDIO_4.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_4.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_4.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_4.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_4.Location = new System.Drawing.Point(261, 286);
            this._lblDIO_4.Name = "_lblDIO_4";
            this._lblDIO_4.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_4.Size = new System.Drawing.Size(48, 20);
            this._lblDIO_4.TabIndex = 40;
            this._lblDIO_4.Text = "4";
            this._lblDIO_4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_4
            // 
            this._lblShowData_4.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_4.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_4.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_4.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_4.Location = new System.Drawing.Point(131, 286);
            this._lblShowData_4.Name = "_lblShowData_4";
            this._lblShowData_4.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_4.Size = new System.Drawing.Size(75, 20);
            this._lblShowData_4.TabIndex = 12;
            this._lblShowData_4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_4
            // 
            this._lblChanNum_4.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_4.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_4.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_4.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_4.Location = new System.Drawing.Point(65, 286);
            this._lblChanNum_4.Name = "_lblChanNum_4";
            this._lblChanNum_4.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_4.Size = new System.Drawing.Size(48, 20);
            this._lblChanNum_4.TabIndex = 32;
            this._lblChanNum_4.Text = "4";
            this._lblChanNum_4.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblBitStatus_3
            // 
            this._lblBitStatus_3.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_3.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_3.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_3.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_3.Location = new System.Drawing.Point(327, 266);
            this._lblBitStatus_3.Name = "_lblBitStatus_3";
            this._lblBitStatus_3.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_3.Size = new System.Drawing.Size(75, 21);
            this._lblBitStatus_3.TabIndex = 19;
            this._lblBitStatus_3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_3
            // 
            this._lblShowData_3.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_3.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_3.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_3.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_3.Location = new System.Drawing.Point(131, 266);
            this._lblShowData_3.Name = "_lblShowData_3";
            this._lblShowData_3.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_3.Size = new System.Drawing.Size(75, 21);
            this._lblShowData_3.TabIndex = 11;
            this._lblShowData_3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_3
            // 
            this._lblChanNum_3.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_3.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_3.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_3.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_3.Location = new System.Drawing.Point(65, 266);
            this._lblChanNum_3.Name = "_lblChanNum_3";
            this._lblChanNum_3.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_3.Size = new System.Drawing.Size(48, 21);
            this._lblChanNum_3.TabIndex = 31;
            this._lblChanNum_3.Text = "3";
            this._lblChanNum_3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblBitStatus_2
            // 
            this._lblBitStatus_2.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_2.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_2.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_2.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_2.Location = new System.Drawing.Point(327, 246);
            this._lblBitStatus_2.Name = "_lblBitStatus_2";
            this._lblBitStatus_2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_2.Size = new System.Drawing.Size(75, 21);
            this._lblBitStatus_2.TabIndex = 18;
            this._lblBitStatus_2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_2
            // 
            this._lblShowData_2.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_2.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_2.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_2.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_2.Location = new System.Drawing.Point(131, 246);
            this._lblShowData_2.Name = "_lblShowData_2";
            this._lblShowData_2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_2.Size = new System.Drawing.Size(75, 21);
            this._lblShowData_2.TabIndex = 10;
            this._lblShowData_2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_2
            // 
            this._lblChanNum_2.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_2.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_2.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_2.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_2.Location = new System.Drawing.Point(65, 246);
            this._lblChanNum_2.Name = "_lblChanNum_2";
            this._lblChanNum_2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_2.Size = new System.Drawing.Size(48, 21);
            this._lblChanNum_2.TabIndex = 30;
            this._lblChanNum_2.Text = "2";
            this._lblChanNum_2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblBitStatus_1
            // 
            this._lblBitStatus_1.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_1.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_1.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_1.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_1.Location = new System.Drawing.Point(327, 226);
            this._lblBitStatus_1.Name = "_lblBitStatus_1";
            this._lblBitStatus_1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_1.Size = new System.Drawing.Size(75, 21);
            this._lblBitStatus_1.TabIndex = 17;
            this._lblBitStatus_1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_1
            // 
            this._lblShowData_1.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_1.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_1.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_1.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_1.Location = new System.Drawing.Point(131, 226);
            this._lblShowData_1.Name = "_lblShowData_1";
            this._lblShowData_1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_1.Size = new System.Drawing.Size(75, 21);
            this._lblShowData_1.TabIndex = 9;
            this._lblShowData_1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_1
            // 
            this._lblChanNum_1.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_1.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_1.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_1.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_1.Location = new System.Drawing.Point(65, 226);
            this._lblChanNum_1.Name = "_lblChanNum_1";
            this._lblChanNum_1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_1.Size = new System.Drawing.Size(48, 21);
            this._lblChanNum_1.TabIndex = 29;
            this._lblChanNum_1.Text = "1";
            this._lblChanNum_1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblBitStatus_0
            // 
            this._lblBitStatus_0.BackColor = System.Drawing.SystemColors.Window;
            this._lblBitStatus_0.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblBitStatus_0.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblBitStatus_0.ForeColor = System.Drawing.Color.Blue;
            this._lblBitStatus_0.Location = new System.Drawing.Point(327, 207);
            this._lblBitStatus_0.Name = "_lblBitStatus_0";
            this._lblBitStatus_0.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblBitStatus_0.Size = new System.Drawing.Size(75, 21);
            this._lblBitStatus_0.TabIndex = 16;
            this._lblBitStatus_0.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblDIO_0
            // 
            this._lblDIO_0.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_0.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_0.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_0.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_0.Location = new System.Drawing.Point(261, 207);
            this._lblDIO_0.Name = "_lblDIO_0";
            this._lblDIO_0.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_0.Size = new System.Drawing.Size(48, 21);
            this._lblDIO_0.TabIndex = 36;
            this._lblDIO_0.Text = "0";
            this._lblDIO_0.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblShowData_0
            // 
            this._lblShowData_0.BackColor = System.Drawing.SystemColors.Window;
            this._lblShowData_0.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblShowData_0.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblShowData_0.ForeColor = System.Drawing.Color.Blue;
            this._lblShowData_0.Location = new System.Drawing.Point(131, 207);
            this._lblShowData_0.Name = "_lblShowData_0";
            this._lblShowData_0.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblShowData_0.Size = new System.Drawing.Size(75, 21);
            this._lblShowData_0.TabIndex = 2;
            this._lblShowData_0.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblChanNum_0
            // 
            this._lblChanNum_0.BackColor = System.Drawing.SystemColors.Window;
            this._lblChanNum_0.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblChanNum_0.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblChanNum_0.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblChanNum_0.Location = new System.Drawing.Point(65, 207);
            this._lblChanNum_0.Name = "_lblChanNum_0";
            this._lblChanNum_0.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblChanNum_0.Size = new System.Drawing.Size(48, 21);
            this._lblChanNum_0.TabIndex = 28;
            this._lblChanNum_0.Text = "0";
            this._lblChanNum_0.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // lblTemp2
            // 
            this.lblTemp2.BackColor = System.Drawing.SystemColors.Window;
            this.lblTemp2.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblTemp2.Font = new System.Drawing.Font("Arial", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTemp2.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblTemp2.Location = new System.Drawing.Point(317, 167);
            this.lblTemp2.Name = "lblTemp2";
            this.lblTemp2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblTemp2.Size = new System.Drawing.Size(104, 21);
            this.lblTemp2.TabIndex = 25;
            this.lblTemp2.Text = "Status";
            this.lblTemp2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // lblChan2
            // 
            this.lblChan2.BackColor = System.Drawing.SystemColors.Window;
            this.lblChan2.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblChan2.Font = new System.Drawing.Font("Arial", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblChan2.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblChan2.Location = new System.Drawing.Point(252, 167);
            this.lblChan2.Name = "lblChan2";
            this.lblChan2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblChan2.Size = new System.Drawing.Size(72, 21);
            this.lblChan2.TabIndex = 27;
            this.lblChan2.Text = "DIO";
            this.lblChan2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // lblTemp1
            // 
            this.lblTemp1.BackColor = System.Drawing.SystemColors.Window;
            this.lblTemp1.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblTemp1.Font = new System.Drawing.Font("Arial", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblTemp1.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblTemp1.Location = new System.Drawing.Point(121, 167);
            this.lblTemp1.Name = "lblTemp1";
            this.lblTemp1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblTemp1.Size = new System.Drawing.Size(113, 21);
            this.lblTemp1.TabIndex = 24;
            this.lblTemp1.Text = "Temperature";
            this.lblTemp1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // lblChan1
            // 
            this.lblChan1.BackColor = System.Drawing.SystemColors.Window;
            this.lblChan1.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblChan1.Font = new System.Drawing.Font("Arial", 8.25F, ((System.Drawing.FontStyle)((System.Drawing.FontStyle.Bold | System.Drawing.FontStyle.Underline))), System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblChan1.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblChan1.Location = new System.Drawing.Point(56, 167);
            this.lblChan1.Name = "lblChan1";
            this.lblChan1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblChan1.Size = new System.Drawing.Size(66, 21);
            this.lblChan1.TabIndex = 26;
            this.lblChan1.Text = "Channel";
            this.lblChan1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // lblLastChan
            // 
            this.lblLastChan.BackColor = System.Drawing.SystemColors.Window;
            this.lblLastChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblLastChan.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLastChan.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblLastChan.Location = new System.Drawing.Point(317, 108);
            this.lblLastChan.Name = "lblLastChan";
            this.lblLastChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblLastChan.Size = new System.Drawing.Size(104, 21);
            this.lblLastChan.TabIndex = 45;
            this.lblLastChan.Text = "Last Channel";
            // 
            // lblHiChan
            // 
            this.lblHiChan.BackColor = System.Drawing.SystemColors.Window;
            this.lblHiChan.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblHiChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblHiChan.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHiChan.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblHiChan.Location = new System.Drawing.Point(271, 107);
            this.lblHiChan.Name = "lblHiChan";
            this.lblHiChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblHiChan.Size = new System.Drawing.Size(38, 21);
            this.lblHiChan.TabIndex = 8;
            this.lblHiChan.Text = "0";
            // 
            // lblHT
            // 
            this.lblHT.BackColor = System.Drawing.SystemColors.Window;
            this.lblHT.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblHT.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHT.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblHT.Location = new System.Drawing.Point(317, 417);
            this.lblHT.Name = "lblHT";
            this.lblHT.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblHT.Size = new System.Drawing.Size(181, 20);
            this.lblHT.TabIndex = 45;
            this.lblHT.Text = "High Temperture Limit (°C)";
            // 
            // lblHTChan
            // 
            this.lblHTChan.BackColor = System.Drawing.SystemColors.Window;
            this.lblHTChan.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblHTChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblHTChan.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblHTChan.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblHTChan.Location = new System.Drawing.Point(271, 416);
            this.lblHTChan.Name = "lblHTChan";
            this.lblHTChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblHTChan.Size = new System.Drawing.Size(38, 21);
            this.lblHTChan.TabIndex = 8;
            this.lblHTChan.Text = "0";
            // 
            // lblFirstChan
            // 
            this.lblFirstChan.BackColor = System.Drawing.SystemColors.Window;
            this.lblFirstChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblFirstChan.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblFirstChan.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblFirstChan.Location = new System.Drawing.Point(317, 79);
            this.lblFirstChan.Name = "lblFirstChan";
            this.lblFirstChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblFirstChan.Size = new System.Drawing.Size(104, 21);
            this.lblFirstChan.TabIndex = 44;
            this.lblFirstChan.Text = "First Channel";
            // 
            // lblLoChan
            // 
            this.lblLoChan.BackColor = System.Drawing.SystemColors.Window;
            this.lblLoChan.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblLoChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblLoChan.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLoChan.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblLoChan.Location = new System.Drawing.Point(271, 78);
            this.lblLoChan.Name = "lblLoChan";
            this.lblLoChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblLoChan.Size = new System.Drawing.Size(38, 21);
            this.lblLoChan.TabIndex = 7;
            this.lblLoChan.Text = "0";
            // 
            // lblLT
            // 
            this.lblLT.BackColor = System.Drawing.SystemColors.Window;
            this.lblLT.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblLT.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLT.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblLT.Location = new System.Drawing.Point(317, 384);
            this.lblLT.Name = "lblLT";
            this.lblLT.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblLT.Size = new System.Drawing.Size(181, 21);
            this.lblLT.TabIndex = 44;
            this.lblLT.Text = "Low Temperture Limit (°C)";
            // 
            // lblLTChan
            // 
            this.lblLTChan.BackColor = System.Drawing.SystemColors.Window;
            this.lblLTChan.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblLTChan.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblLTChan.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblLTChan.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblLTChan.Location = new System.Drawing.Point(271, 384);
            this.lblLTChan.Name = "lblLTChan";
            this.lblLTChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblLTChan.Size = new System.Drawing.Size(38, 21);
            this.lblLTChan.TabIndex = 7;
            this.lblLTChan.Text = "0";
            // 
            // lblChanPrompt
            // 
            this.lblChanPrompt.BackColor = System.Drawing.SystemColors.Window;
            this.lblChanPrompt.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblChanPrompt.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblChanPrompt.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblChanPrompt.Location = new System.Drawing.Point(56, 49);
            this.lblChanPrompt.Name = "lblChanPrompt";
            this.lblChanPrompt.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblChanPrompt.Size = new System.Drawing.Size(325, 21);
            this.lblChanPrompt.TabIndex = 0;
            this.lblChanPrompt.Text = "Select the range of multiplexor channels to display";
            // 
            // lblDemoFunction
            // 
            this.lblDemoFunction.BackColor = System.Drawing.SystemColors.Window;
            this.lblDemoFunction.Cursor = System.Windows.Forms.Cursors.Default;
            this.lblDemoFunction.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblDemoFunction.ForeColor = System.Drawing.SystemColors.WindowText;
            this.lblDemoFunction.Location = new System.Drawing.Point(9, 10);
            this.lblDemoFunction.Name = "lblDemoFunction";
            this.lblDemoFunction.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.lblDemoFunction.Size = new System.Drawing.Size(403, 31);
            this.lblDemoFunction.TabIndex = 1;
            this.lblDemoFunction.Text = "OM_USB_TC v2.0";
            this.lblDemoFunction.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // FilePath
            // 
            this.FilePath.BackColor = System.Drawing.SystemColors.Window;
            this.FilePath.Cursor = System.Windows.Forms.Cursors.Default;
            this.FilePath.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FilePath.ForeColor = System.Drawing.SystemColors.WindowText;
            this.FilePath.Location = new System.Drawing.Point(56, 481);
            this.FilePath.Name = "FilePath";
            this.FilePath.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.FilePath.Size = new System.Drawing.Size(297, 17);
            this.FilePath.TabIndex = 7;
            this.FilePath.Text = "Enter the file path to save: ";
            // 
            // FilePathChan
            // 
            this.FilePathChan.AcceptsReturn = true;
            this.FilePathChan.BackColor = System.Drawing.SystemColors.Window;
            this.FilePathChan.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.FilePathChan.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.FilePathChan.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.FilePathChan.ForeColor = System.Drawing.SystemColors.WindowText;
            this.FilePathChan.Location = new System.Drawing.Point(59, 516);
            this.FilePathChan.MaxLength = 0;
            this.FilePathChan.Name = "FilePathChan";
            this.FilePathChan.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this.FilePathChan.Size = new System.Drawing.Size(424, 23);
            this.FilePathChan.TabIndex = 8;
            this.FilePathChan.Text = "C:\\Users\\Manuel\\Documents\\Visual Studio 2015\\Projects\\ULTI02\\1.txt";
            // 
            // _lblDIO_1
            // 
            this._lblDIO_1.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_1.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_1.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_1.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_1.Location = new System.Drawing.Point(261, 226);
            this._lblDIO_1.Name = "_lblDIO_1";
            this._lblDIO_1.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_1.Size = new System.Drawing.Size(48, 21);
            this._lblDIO_1.TabIndex = 37;
            this._lblDIO_1.Text = "1";
            this._lblDIO_1.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblDIO_2
            // 
            this._lblDIO_2.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_2.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_2.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_2.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_2.Location = new System.Drawing.Point(261, 246);
            this._lblDIO_2.Name = "_lblDIO_2";
            this._lblDIO_2.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_2.Size = new System.Drawing.Size(48, 21);
            this._lblDIO_2.TabIndex = 38;
            this._lblDIO_2.Text = "2";
            this._lblDIO_2.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // _lblDIO_3
            // 
            this._lblDIO_3.BackColor = System.Drawing.SystemColors.Window;
            this._lblDIO_3.Cursor = System.Windows.Forms.Cursors.Default;
            this._lblDIO_3.Font = new System.Drawing.Font("Arial", 8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this._lblDIO_3.ForeColor = System.Drawing.SystemColors.WindowText;
            this._lblDIO_3.Location = new System.Drawing.Point(261, 266);
            this._lblDIO_3.Name = "_lblDIO_3";
            this._lblDIO_3.RightToLeft = System.Windows.Forms.RightToLeft.No;
            this._lblDIO_3.Size = new System.Drawing.Size(48, 21);
            this._lblDIO_3.TabIndex = 39;
            this._lblDIO_3.Text = "3";
            this._lblDIO_3.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // chart1
            // 
            chartArea1.AxisX.Title = "Times (s)";
            chartArea1.AxisY.Title = "Temperature (°C)";
            chartArea1.BorderDashStyle = System.Windows.Forms.DataVisualization.Charting.ChartDashStyle.Solid;
            chartArea1.Name = "ChartArea1";
            this.chart1.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend1";
            this.chart1.Legends.Add(legend1);
            this.chart1.Location = new System.Drawing.Point(554, 127);
            this.chart1.Name = "chart1";
            series1.ChartArea = "ChartArea1";
            series1.IsVisibleInLegend = false;
            series1.Legend = "Legend1";
            series1.Name = "Temperature (°C)";
            this.chart1.Series.Add(series1);
            this.chart1.Size = new System.Drawing.Size(596, 340);
            this.chart1.TabIndex = 46;
            this.chart1.Text = "chart1";
            // 
            // frmDataDisplay
            // 
            this.AcceptButton = this.cmdStartConvert;
            this.AutoScaleBaseSize = new System.Drawing.Size(7, 16);
            this.BackColor = System.Drawing.SystemColors.Window;
            this.ClientSize = new System.Drawing.Size(1348, 630);
            this.Controls.Add(this.chart1);
            this.Controls.Add(this.cmdStartConvert);
            this.Controls.Add(this.cmdStopConvert);
            this.Controls.Add(this.hsbHiChan);
            this.Controls.Add(this.hsbLoChan);
            this.Controls.Add(this.hsbHTChan);
            this.Controls.Add(this.hsbLTChan);
            this.Controls.Add(this._lblBitStatus_7);
            this.Controls.Add(this._lblDIO_7);
            this.Controls.Add(this._lblShowData_7);
            this.Controls.Add(this._lblChanNum_7);
            this.Controls.Add(this._lblBitStatus_6);
            this.Controls.Add(this._lblDIO_6);
            this.Controls.Add(this._lblShowData_6);
            this.Controls.Add(this._lblChanNum_6);
            this.Controls.Add(this._lblBitStatus_5);
            this.Controls.Add(this._lblDIO_5);
            this.Controls.Add(this._lblShowData_5);
            this.Controls.Add(this._lblChanNum_5);
            this.Controls.Add(this._lblBitStatus_4);
            this.Controls.Add(this._lblDIO_4);
            this.Controls.Add(this._lblShowData_4);
            this.Controls.Add(this._lblChanNum_4);
            this.Controls.Add(this._lblBitStatus_3);
            this.Controls.Add(this._lblDIO_3);
            this.Controls.Add(this._lblShowData_3);
            this.Controls.Add(this._lblChanNum_3);
            this.Controls.Add(this._lblBitStatus_2);
            this.Controls.Add(this._lblDIO_2);
            this.Controls.Add(this._lblShowData_2);
            this.Controls.Add(this._lblChanNum_2);
            this.Controls.Add(this._lblBitStatus_1);
            this.Controls.Add(this._lblDIO_1);
            this.Controls.Add(this._lblShowData_1);
            this.Controls.Add(this._lblChanNum_1);
            this.Controls.Add(this._lblBitStatus_0);
            this.Controls.Add(this._lblDIO_0);
            this.Controls.Add(this._lblShowData_0);
            this.Controls.Add(this._lblChanNum_0);
            this.Controls.Add(this.lblTemp2);
            this.Controls.Add(this.lblChan2);
            this.Controls.Add(this.lblTemp1);
            this.Controls.Add(this.lblChan1);
            this.Controls.Add(this.lblLastChan);
            this.Controls.Add(this.lblHiChan);
            this.Controls.Add(this.lblHT);
            this.Controls.Add(this.lblHTChan);
            this.Controls.Add(this.lblFirstChan);
            this.Controls.Add(this.lblLoChan);
            this.Controls.Add(this.lblLT);
            this.Controls.Add(this.lblLTChan);
            this.Controls.Add(this.lblChanPrompt);
            this.Controls.Add(this.lblDemoFunction);
            this.Controls.Add(this.FilePathChan);
            this.Controls.Add(this.FilePath);
            this.Font = new System.Drawing.Font("Arial", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ForeColor = System.Drawing.SystemColors.WindowText;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Location = new System.Drawing.Point(7, 103);
            this.Name = "frmDataDisplay";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Universal Library Temperature Measurement";
            this.Load += new System.EventHandler(this.frmDataDisplay_Load);
            ((System.ComponentModel.ISupportInitialize)(this.chart1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

	#endregion

        #region Form initialization, variables, and entry point

        /// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main() 
		{
			Application.Run(new frmDataDisplay());
		}

        public frmDataDisplay()
        {
            // This call is required by the Windows Form Designer.
            InitializeComponent();

        }

        // Form overrides dispose to clean up the component list.
        protected override void Dispose(bool Disposing)
        {
            if (Disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(Disposing);
        }

        // Required by the Windows Form Designer
        private System.ComponentModel.IContainer components;

        public ToolTip ToolTip1;
        public Button cmdStartConvert;
        public Button cmdStopConvert;
        public HScrollBar hsbHiChan;
        public HScrollBar hsbHTChan;
        public Timer tmrConvert;
        public HScrollBar hsbLoChan;
        public HScrollBar hsbLTChan;
        public Label _lblBitStatus_7;
        public Label _lblDIO_7;
        public Label _lblShowData_7;
        public Label _lblChanNum_7;
        public Label _lblBitStatus_6;
        public Label _lblDIO_6;
        public Label _lblShowData_6;
        public Label _lblChanNum_6;
        public Label _lblBitStatus_5;
        public Label _lblDIO_5;
        public Label _lblShowData_5;
        public Label _lblChanNum_5;
        public Label _lblBitStatus_4;
        public Label _lblDIO_4;
        public Label _lblShowData_4;
        public Label _lblChanNum_4;
        public Label _lblBitStatus_3;
        public Label _lblShowData_3;
        public Label _lblChanNum_3;
        public Label _lblBitStatus_2;
        public Label _lblShowData_2;
        public Label _lblChanNum_2;
        public Label _lblBitStatus_1;
        public Label _lblShowData_1;
        public Label _lblChanNum_1;
        public Label _lblBitStatus_0;
        public Label _lblDIO_0;
        public Label _lblShowData_0;
        public Label _lblChanNum_0;
        public Label _lblDIO_1;
        public Label _lblDIO_2;
        public Label _lblDIO_3;
        public Label lblTemp2;
        public Label lblChan2;
        public Label lblTemp1;
        public Label lblChan1;
        public Label lblLastChan;
        public Label lblHiChan;
        public Label lblHT;
        public Label lblHTChan;
        public Label lblFirstChan;
        public Label lblLoChan;
        public Label lblLT;
        public Label lblLTChan;
        public Label lblChanPrompt;
        public Label lblDemoFunction;
        public TextBox FilePathChan;
        public Label FilePath;

        public Label[] lblShowData;
        public Label[] lblBitStatus;

        #endregion
    }
}