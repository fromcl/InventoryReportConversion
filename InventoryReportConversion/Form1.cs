﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Data.OleDb;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;

namespace InventoryReportConversion
{
    struct DataFilename
    {
        public int index;
        public string fileName;
        public DataGridView dataGrid;
    }

    public partial class Form1 : Form
    {
        private string filename = "Untitled";  //选中的文件路径
        private DataTable data_source = null;  //要处理的表放在这里，程序中最重要的变量

        private delegate void SetDtCallback(DataTable dt);  //创建一个委托，用于解决从不是创建控件的线程访问问题

        public Form1()
        {
            InitializeComponent();

            string dir = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);  //用Environment.SpecialFolder枚举一个特定的系统文件夹
            openFileDialog1.InitialDirectory = dir;  //初始化openFileDialog1的初始打开位置
            //openFileDialog1.InitialDirectory = @"C:\Users\Administrator\Desktop\InventoryReportConversion\原表";  //不用理会，测试时使用的路径
            openFileDialog1.Filter = "Excel 2007 工作簿(*.xlsx)|*.xlsx|Excel 2003 工作簿(*.xls)|*.xls|所有文件|*.*";
            openFileDialog1.FilterIndex = 3;
            openFileDialog1.FileName = "";

            saveFileDialog1.Filter = "Excel 97 工作簿(*.xls)|*.xls|Excel 2007 工作簿(*.xlsx)|*.xlsx|CSV文件(*.csv)|*.csv|文本文件(*.txt)|*.txt";
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dataGridView1.ReadOnly = true;
            dataGridView1.AllowUserToAddRows = false;
            dataGridView1.AllowUserToDeleteRows = false;
            dataGridView1.AllowUserToResizeRows = false;
            dataGridView1.EnableHeadersVisualStyles = false;

            #region 屏蔽按钮功能，防止误操作
            foreach (System.Windows.Forms.Control bt in this.panel1.Controls)
            {
                if (bt is System.Windows.Forms.Button)
                    bt.Enabled = false;
            }
            exportFile.Enabled = false;
            importFile.Enabled = true;
            environmentalDetection.Enabled = true;
            #endregion
        }

        #region 被事件处理函数调用的一些方法

        /// <summary>
        /// 用于跨线程将数据表传递到dataGridView中
        /// </summary>
        private void SetDT(DataTable dt)
        {
            // InvokeRequired需要比较调用线程ID和创建线程ID不相同则返回true
            if (this.dataGridView1.InvokeRequired)
            {
                SetDtCallback d = new SetDtCallback(SetDT);
                this.Invoke(d, new object[] { dt });  //在拥有控件的基础窗口句柄的线程上，用指定的参数列表执行指定委托。
            }
            else
            {
                this.dataGridView1.DataSource = null;
                this.dataGridView1.DataSource = dt;
                if (data_source.Rows.Count >= 65535)
                    MessageBox.Show("目前表格有：" + data_source.Rows.Count + "行、" + data_source.Columns.Count + "列。Excel97单个Sheet最多存储65536行、256列数据，超过这个数据请不要使用本程序！", "警告");
                this.numberRows.Visible = true;
                this.numberRows.Text = "当前表格记录条数：" + data_source.Rows.Count;
            }
        }

        /// <summary>
        /// 把EXCEL转换成一个数据源来进行数据的 读取 操作
        /// </summary>
        private void ExcelToDS(object Path)
        {
            DataSet ds = new DataSet();
            //string strConn = "Provider=Microsoft.Jet.OleDb.4.0;" + "data source=" + (string)Path + ";Extended Properties='Excel 8.0; HDR=NO; IMEX=1'"; //此连接只能操作Excel 2007之前(.xls)文件
            string strConn = "Provider=Microsoft.Ace.OleDb.12.0;" + "data source=" + (string)Path + ";Extended Properties='Excel 12.0; HDR=NO; IMEX=1'";  //此连接可以操作.xls与.xlsx文件
            //"Provider=Microsoft.Jet.OLEDB.4.0;Data Source=D:/公司网站/gys_center/kucun/20170708库存日报表.xls;Extended Properties='Excel 8.0;HDR=Yes;IMEX=1;'"  //网站使用的连接字符串

            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Visible = true; });
            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Value = 0; });
            OleDbConnection conn = new OleDbConnection(strConn);
            conn.Open();
            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Value = 10; });
            string strExcel = "select * from [sheet1$]";
            OleDbDataAdapter myCommand = null;
            myCommand = new OleDbDataAdapter(strExcel, strConn);
            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Value = 100; });
            myCommand.Fill(ds, "table1");
            data_source = ds.Tables["table1"];
            conn.Close();

            for (int i = 0; i < data_source.Columns.Count; i++)  //为每一列设置列名
            {
                data_source.Columns[i].ColumnName = data_source.Rows[0][i].ToString() + "(" + i.ToString() + ")";
            }

            //dataGridView1.DataSource = data_souce.Tables[0];  //将跨线程传递数据的代码换成使用委托，即下一行
            SetDT(data_source);
            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Visible = false; });
            /*导入键使能*/this.importFile.BeginInvoke((Action)delegate { this.importFile.Enabled = true; });
        }

        /// <summary>
        /// 把DataTable转换成Excel文件，且只有一个sheet
        /// </summary>
        /// <param name="df">包含选中索引、文件路径、数据表控件的一个结构体</param>
        public void ExportToExcel(object df)
        {
            DataFilename dataFilename = (DataFilename)df;

            int index = dataFilename.index;
            string fileName = dataFilename.fileName;
            DataGridView dataGrid = dataFilename.dataGrid;

            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Visible = true; });
            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Value = 0; });

            try
            {
                if (index == 1 || index == 2)
                {
                    try
                    {
                        #region 导出Excel文件

                        Microsoft.Office.Interop.Excel.ApplicationClass app = new Microsoft.Office.Interop.Excel.ApplicationClass();

                        System.Globalization.CultureInfo CurrentCI = System.Threading.Thread.CurrentThread.CurrentCulture;
                        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
                        Microsoft.Office.Interop.Excel.Workbooks workbooks = app.Workbooks;
                        Microsoft.Office.Interop.Excel.Workbook workbook = workbooks.Add(Microsoft.Office.Interop.Excel.XlWBATemplate.xlWBATWorksheet);
                        Microsoft.Office.Interop.Excel.Worksheet worksheet = (Microsoft.Office.Interop.Excel.Worksheet)workbook.Worksheets[1];

                        app.Visible = false;
                        app.DisplayAlerts = false;
                        app.AlertBeforeOverwriting = false;



                        //标题
                        int ColCnt = 0;
                        for (int indexCol = 0; indexCol < dataGrid.ColumnCount; indexCol++)
                        {
                            if (dataGrid.Columns[indexCol].Visible == true)
                            {
                                //worksheet.Cells[1, indexCol + 1] = datagrid.Columns[indexCol].HeaderText;  //开启本行，将DataGridView中的表头做进表里
                                ColCnt++;
                            }
                        }

                        //object[,] data = new object[datagrid.RowCount, ColCnt];  //使用object类型的数组保存的excel会出现科学计数法的问题
                        string[,] data = new string[dataGrid.RowCount, ColCnt];

                        //明细
                        for (int curIndexRow = 0; curIndexRow < dataGrid.RowCount; curIndexRow++)
                        {
                            int curIndex = 0;

                            for (int curIndexCol = 0; curIndexCol < dataGrid.ColumnCount; curIndexCol++)
                            {
                                try
                                {
                                    if (dataGrid.Columns[curIndexCol].Visible == true)
                                    {
                                        data[curIndexRow, curIndex] = dataGrid.Rows[curIndexRow].Cells[curIndexCol].Value.ToString();
                                        curIndex++;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("导出失败，错误信息：" + ex.ToString());
                                }
                            }
                        }

                        //worksheet.get_Range("A2", worksheet.Cells[datagrid.RowCount + 1, ColCnt]).set_Value(Type.Missing, data);
                        worksheet.get_Range("A1", worksheet.Cells[dataGrid.RowCount, ColCnt]).set_Value(Type.Missing, data);  //本行设置为A2，将DataGridView中的表头做进表里
                        
                        workbook.Saved = true;  //保存工作表 

                        /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Value = 100; });
                        if (index == 2)
                            workbook.SaveAs(fileName);  //会保存为当前Excel版本的文档
                        else if (index == 1)
                            workbook.SaveAs(fileName, Microsoft.Office.Interop.Excel.XlFileFormat.xlExcel8);  //Excel 97文档

                        app.Workbooks.Close();
                        app.Quit();
                        worksheet = null;
                        workbook = null;
                        app = null;

                        GC.Collect();

                        #endregion
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("导出失败，" + ex.Message);
                        return;
                    }
                }
                else if (index == 3)
                {
                    try
                    {
                        #region 导出CSV文件

                        StreamWriter write = new StreamWriter(fileName, false, Encoding.Default);

                        //标题
                        //for (int t = 0; t < datagrid.ColumnCount; t++)
                        //{
                        //    if (datagrid.Columns[t].Visible == true)
                        //    {
                        //        write.Write(datagrid.Columns[t].HeaderText + ",");
                        //    }
                        //}

                        //write.WriteLine();

                        //明细
                        for (int Lin = 2; Lin <= dataGrid.RowCount + 1; Lin++)
                        {
                            if (dataGrid.Rows[Lin - 2].Visible == true)
                            {
                                string Tem = "";

                                for (int k = 0; k < dataGrid.ColumnCount; k++)
                                {
                                    if (dataGrid.Columns[k].Visible == true)
                                    {
                                        if (dataGrid.Rows[Lin - 2].Cells[k].Value != null)
                                        {
                                            string TemString = dataGrid.Rows[Lin - 2].Cells[k].Value.ToString().Trim().Replace(',', '.');

                                            Tem += TemString;
                                            Tem += ",";
                                        }
                                        else
                                        {
                                            string TemString = " ";

                                            Tem += TemString;
                                            Tem += ",";
                                        }
                                    }
                                }

                                /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Value = 100; });
                                write.WriteLine(Tem);
                            }
                        }

                        write.Flush();
                        write.Close();

                        #endregion
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message);
                    }
                }
                else if (index == 4)
                {
                    #region 导出文本文件
                    StreamWriter write = new StreamWriter(fileName, false, Encoding.Default);
                    //标题行
                    //for (int t = 0; t < datagrid.ColumnCount; t++)
                    //{
                    //    if (datagrid.Columns[t].Visible == true)
                    //    {
                    //        write.Write(datagrid.Columns[t].HeaderText + "\t");
                    //    }
                    //}
                    //write.WriteLine();
                    //明细行
                    for (int Lin = 2; Lin <= dataGrid.RowCount + 1; Lin++)
                    {

                        if (dataGrid.Rows[Lin - 2].Visible == true)
                        {
                            string Tem = "";
                            for (int k = 0; k < dataGrid.ColumnCount; k++)
                            {
                                if (dataGrid.Columns[k].Visible)
                                {
                                    if (dataGrid.Rows[Lin - 2].Cells[k].Value != null)
                                    {
                                        Tem += dataGrid.Rows[Lin - 2].Cells[k].Value.ToString();
                                        Tem += "\t";
                                    }
                                    else
                                    {
                                        string TemString = "";

                                        Tem += TemString;
                                        Tem += "\t";
                                    }
                                }

                            }
                            /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Value = 100; });
                            write.WriteLine(Tem);
                        }
                    }
                    write.Flush();
                    write.Close();
                    #endregion
                }

                MessageBox.Show("导出完毕！");

                /*进度条*/this.progressBar1.BeginInvoke((Action)delegate { this.progressBar1.Visible = false; });
                /*导出键使能*/this.exportFile.BeginInvoke((Action)delegate { this.exportFile.Enabled = true; });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        /// <summary>
        /// 打印当前导入表格DataTable中的最大长度，并在超过指定长度时返回false，出现科学计数法但不超长返回true，无任何错误返回true
        /// </summary>
        /// <param name="target_str">所查询列的列名</param>
        /// <param name="notice_length">用来做警告的最大长度</param> 
        private bool GetMaxLength(string target_str, int notice_length)
        {
            //查找关键字：优化
            int max_length = 0;
            bool only_once = true, only_once2 = true;
            string error_str = null, error_str2 = null;
            for (int i = 1; i < data_source.Rows.Count; i++)
            {
                if (data_source.Rows[i][target_str].ToString().Length > max_length)
                {
                    max_length = data_source.Rows[i][target_str].ToString().Length;
                    if (max_length > notice_length && only_once == true)
                    {
                        error_str = data_source.Rows[i][target_str].ToString();
                        only_once = false;
                    }
                }

                if (Regex.IsMatch(data_source.Rows[i][target_str].ToString(), @"([+-]?)((?<!0)\d\.\d{1,})[eE]([+-]?)(\d+)") && only_once2 == true)  //检查是否有科学计数法的行，有则报错
                {
                    error_str2 = data_source.Rows[i][target_str].ToString();
                    only_once2 = false;
                }
            }

            if (error_str == null && error_str2 == null)  //无任何错误
            {
                numberRows.Text = target_str + " 列最长长度为：" + max_length;
                return true;
            }
            else if (error_str != null && error_str2 == null)  //超长了
            {
                MessageBox.Show(target_str + " 列最长长度为：" + max_length + "。警告！第一个超长的单元格内容为：" + error_str, "警告");
                return false;
            }
            else if (error_str == null && error_str2 != null)  //出现科学计数法了
            {
                MessageBox.Show(target_str + " 列出现科学计数法。第一个出错的单元格内容为：" + error_str2, "警告");
                return true;  //没超长不需要裁剪，所以返回true
            }
            else  //两者都出现了
            {
                MessageBox.Show(target_str + " 出现列超长和科学计数法，两种错误发生！", "警告");
                return false;
            }
        }

        /// <summary>
        /// 按设定的长度裁剪一列，超长部分舍弃
        /// </summary>
        /// <param name="sub_length">要求的列长度</param>
        private void SubColumn(string target_str, int sub_length)
        {
            //查找关键字：优化
            for (int i = 0; i < data_source.Rows.Count; i++)
            {
                if (data_source.Rows[i][target_str].ToString().Length > sub_length)
                    data_source.Rows[i][target_str] = data_source.Rows[i][target_str].ToString().Substring(0, sub_length);
            }

            dataGridView1.DataSource = null;
            dataGridView1.DataSource = data_source;
        }

        #endregion

        //环境检测
        private void environmentalDetection_Click(object sender, EventArgs e)
        {
            Microsoft.Office.Interop.Excel.Application test = new Microsoft.Office.Interop.Excel.Application();
            if (Convert.ToDouble(test.Version) >= 12.0)
                MessageBox.Show("您的电脑装有对应Office软件，可以使用此程序", "提示：" + test.Version);
            else
                MessageBox.Show("您的电脑未安装Office 2007或更高版本的Office，无法使用此程序", "提示：" + test.Version);
            test.Quit();
            test = null;
        }

        //导入需要更改的文件
        private void importFile_Click(object sender, EventArgs e)
        {
            #region 屏蔽按钮功能，防止误操作
            foreach (System.Windows.Forms.Control bt in this.panel1.Controls)
            {
                if (bt is System.Windows.Forms.Button)
                    bt.Enabled = false;
            }
            exportFile.Enabled = false;
            importFile.Enabled = true;
            environmentalDetection.Enabled = true;
            #endregion

            string fs = null;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                filename = openFileDialog1.FileName;
                fs = new FileInfo(filename).Name;
                this.Text = fs + " - 库存日报表数据格式转换";  //修改一下标题栏
                if (filename.IndexOf(".xlsx", filename.Length - 5, 5) == -1 && filename.IndexOf(".xls", filename.Length - 4, 4) == -1)
                {
                    MessageBox.Show("错误：导入的文件非Excel类型的文件！");
                    return;
                }

                if (fs.IndexOf("库存日报表") != -1)
                    deleteUnusedColumns_1.Enabled = true;
                else if (fs.IndexOf("出库明细") != -1)
                    deleteUnusedColumns_2.Enabled = true;
                else if (fs.IndexOf("入库明细") != -1)
                    deleteUnusedColumns_3.Enabled = true;
                else
                {
                    MessageBox.Show("表文件名不正确，请检查您导入表的文件名！");
                    return;
                }

                txtDaoru.Text = filename;

                importFile.Enabled = false;

                Thread ds_thread = new Thread(new ParameterizedThreadStart(ExcelToDS));  //使用一个线程调用ExcelToDS方法
                ds_thread.IsBackground = true;  //设置为后台线程，关闭主线程后，会自动关闭不会占用系统资源
                ds_thread.Name = "ExcelToDSThread";
                //CheckForIllegalCrossThreadCalls = false;  //为从不是创建控件的线程访问，关闭非法线程交叉调用检查，这不是标准做法，只有调试时才可能使用！
                ds_thread.Start(filename);
            }
        }

        //导出已处理好的表格
        private void exportFile_Click(object sender, EventArgs e)
        {
            DataFilename df = new DataFilename();

            saveFileDialog1.FileName = Path.GetFileNameWithoutExtension(filename);

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                exportFile.Enabled = false;

                df.index = saveFileDialog1.FilterIndex;
                df.fileName = saveFileDialog1.FileName;
                df.dataGrid = dataGridView1;

                Thread te_thread = new Thread(new ParameterizedThreadStart(ExportToExcel));
                te_thread.IsBackground = true;
                te_thread.Name = "ExportToExcelThread";
                //CheckForIllegalCrossThreadCalls = false;  //为从不是创建控件的线程访问，关闭非法线程交叉调用检查，这不是标准做法，只有调试时才可能使用！
                te_thread.Start(df);
            }

        }

        #region 处理库存日报表部分

        //删除无用列
        private void deleteUnusedColumns_1_Click(object sender, EventArgs e)
        {
            if (data_source != null)  //查找关键字：优化
            {
                data_source.Columns.Remove("物资类别名称(0)");  //设置要删除的列名
                data_source.Columns.Remove("结存时间(6)");
                data_source.Columns.Remove("库管员(7)");
                data_source.Columns.Remove("零件类别(8)");
                data_source.Columns.Remove("库位(9)");
                data_source.Columns.Remove("标准包装(10)");
                data_source.Columns.Remove("标准包装单位(11)");
                data_source.Columns.Remove("有效期(12)");

                dataGridView1.DataSource = null;  //没有这一行，dataGridView的列在重新导入时，将无法恢复，每次给DataSource修改数据时最好都写上
                dataGridView1.DataSource = data_source;

                deleteUnusedColumns_1.Enabled = false;
                renameSort_1.Enabled = true;
            }
            else
                MessageBox.Show("请先等待库存日报表文件导入完毕！");
        }

        //更名与排序
        private void renameSort_1_Click(object sender, EventArgs e)
        {
            data_source.Rows[0]["零件编号(1)"] = "零件图号";
            data_source.Rows[0]["供应商编号(3)"] = "厂家代码";
            data_source.Rows[0]["供应商名称(4)"] = "厂家名称";
            data_source.Rows[0]["库存(5)"] = "数量";

            dataGridView1.DataSource = null;
            dataGridView1.DataSource = data_source;

            renameSort_1.Enabled = false;
            partNumberMid_1.Enabled = true;
        }

        //零件图号(mid)
        private void partNumberMid_1_Click(object sender, EventArgs e)
        {
            int overlength_length = 30;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "零件编号(1)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            partNumberMid_1.Enabled = false;
            manufacturerCodeMid_1.Enabled = true;
        }

        //厂家代码(mid)
        private void manufacturerCodeMid_1_Click(object sender, EventArgs e)
        {
            int overlength_length = 9;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "供应商编号(3)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            manufacturerCodeMid_1.Enabled = false;
            exportFile.Enabled = true;
        }

        #endregion

        #region 处理出库明细部分

        //删除无用列
        private void deleteUnusedColumns_2_Click(object sender, EventArgs e)
        {
            if (data_source != null)  //查找关键字：优化
            {
                data_source.Columns.Remove("序号(1)");  //设置要删除的列名
                data_source.Columns.Remove("订单序号(2)");
                data_source.Columns.Remove("领料单位(4)");
                data_source.Columns.Remove("单位名称(5)");
                data_source.Columns.Remove("领料部门(6)");
                data_source.Columns.Remove("部门名称(7)");
                data_source.Columns.Remove("单位(10)");
                data_source.Columns.Remove("规格(11)");
                data_source.Columns.Remove("库区(16)");
                data_source.Columns.Remove("出库时间(17)");
                data_source.Columns.Remove("操作员(19)");
                data_source.Columns.Remove("门点(20)");
                data_source.Columns.Remove("接收单号(21)");
                data_source.Columns.Remove("客户接收单号(22)");
                data_source.Columns.Remove("物资类别编号(23)");
                data_source.Columns.Remove("物资类别名称(24)");
                data_source.Columns.Remove("采购单位编号(25)");
                data_source.Columns.Remove("采购单位名称(26)");
                data_source.Columns.Remove("配送单位编号(27)");
                data_source.Columns.Remove("配送单位名称(28)");
                data_source.Columns.Remove("任务号(29)");
                data_source.Columns.Remove("流水号(30)");
                data_source.Columns.Remove("备注(31)");
                data_source.Columns.Remove("说明(32)");
                data_source.Columns.Remove("内部编号(33)");
                data_source.Columns.Remove("库管员(34)");
                data_source.Columns.Remove("扫描批次(35)");
                data_source.Columns.Remove("零件类别(36)");
                data_source.Columns.Remove("客户单号(37)");
                data_source.Columns.Remove("计生号(38)");

                dataGridView1.DataSource = null;  //没有这一行，dataGridView的列在重新导入时，将无法恢复，每次给DataSource修改数据时最好都写上
                dataGridView1.DataSource = data_source;

                deleteUnusedColumns_2.Enabled = false;
                renameSort_2.Enabled = true;
            }
            else
                MessageBox.Show("请先等待出库明细文件导入完毕！");

            //此方法虽减少代码量，但过于缓慢
            //if (data_source != null)  
            //{
            //    data_source = data_source.DefaultView.ToTable("table1", true, "零件编号(8)", "零件名称(9)", "出库数量(12)", "供应商编号(13)", "供应商名称(14)", "出库类别(15)", "出库单号(0)", "操作时间(18)", "客户单号(37)");
            //    dataGridView1.DataSource = null;  //没有这一行，dataGridView的列在重新导入时，将无法恢复，每次给DataSource修改数据时最好都写上
            //    dataGridView1.DataSource = data_source;
            //}
            //else
            //    MessageBox.Show("请先导入出库明细文件！");
        }

        //更名与排序
        private void renameSort_2_Click(object sender, EventArgs e)
        {
            data_source.Rows[0]["零件编号(8)"] = "零件图号";
            data_source.Rows[0]["出库数量(12)"] = "数量";
            data_source.Rows[0]["供应商编号(13)"] = "厂家代码";
            data_source.Rows[0]["供应商名称(14)"] = "厂家名称";
            data_source.Rows[0]["出库类别(15)"] = "票据类型";
            data_source.Rows[0]["出库单号(0)"] = "出库编号";
            data_source.Rows[0]["操作时间(18)"] = "时间";
            data_source.Rows[0]["客户单号(3)"] = "订单号";

            data_source.Columns["出库单号(0)"].SetOrdinal(7);
            data_source.Columns["客户单号(3)"].SetOrdinal(8);

            dataGridView1.DataSource = null;
            dataGridView1.DataSource = data_source;

            renameSort_2.Enabled = false;
            partNumberMid_2.Enabled = true;
        }

        //零件图号(mid)
        private void partNumberMid_2_Click(object sender, EventArgs e)
        {
            int overlength_length = 30;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "零件编号(8)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            partNumberMid_2.Enabled = false;
            manufacturerCodeMid_2.Enabled = true;
        }

        //厂家代码(mid)
        private void manufacturerCodeMid_2_Click(object sender, EventArgs e)
        {
            int overlength_length = 9;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "供应商编号(13)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            manufacturerCodeMid_2.Enabled = false;
            orderNumber_2.Enabled = true;
        }

        //订单号(mid)
        private void orderNumber_2_Click(object sender, EventArgs e)
        {
            int overlength_length = 30;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "客户单号(3)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            orderNumber_2.Enabled = false;
            time_2.Enabled = true;
        }

        //时间(DATE)
        private void time_2_Click(object sender, EventArgs e)
        {
            string times = Convert.ToDateTime(data_source.Rows[1]["操作时间(18)"].ToString()).ToString("yyyy/MM/dd");

            Form2 frm = new Form2();
            frm.StartPosition = FormStartPosition.CenterScreen;
            frm.OldTime = times;
            frm.NewTime = times;
            frm.ShowDialog();

            if (frm.DialogResult == DialogResult.OK)
            {
                for (int i = 1; i < data_source.Rows.Count; i++)
                {
                    if (frm.NewTime != null && frm.NewTime.Trim() != "")  //有些时候，导入的表会出现多一行空白行，导致报错，这里把它取消掉
                        data_source.Rows[i]["操作时间(18)"] = frm.NewTime;
                    else
                    {
                        MessageBox.Show("输入的时间不能为空！");
                        return;
                    }
                }
            }
            else if (frm.DialogResult == DialogResult.No)
                return;

            time_2.Enabled = false;
            exportFile.Enabled = true;
        }

        #endregion

        #region 处理入库明细部分

        //删除无用列
        private void deleteUnusedColumns_3_Click(object sender, EventArgs e)
        {
            if (data_source != null)  //查找关键字：优化
            {
                data_source.Columns.Remove("入库单号(0)");  //设置要删除的列名
                data_source.Columns.Remove("库区(1)");
                data_source.Columns.Remove("入库类别(2)");
                data_source.Columns.Remove("门点(4)");
                data_source.Columns.Remove("入库时间(5)");
                data_source.Columns.Remove("操作员(6)");
                data_source.Columns.Remove("操作时间(7)");
                data_source.Columns.Remove("状态(8)");
                data_source.Columns.Remove("备注(9)");
                data_source.Columns.Remove("序号(10)");
                data_source.Columns.Remove("内部编号(12)");
                data_source.Columns.Remove("规格(14)");
                data_source.Columns.Remove("单位(17)");
                data_source.Columns.Remove("包装(18)");
                data_source.Columns.Remove("单价(19)");
                data_source.Columns.Remove("物资类别编号(21)");
                data_source.Columns.Remove("物资类别名称(22)");
                data_source.Columns.Remove("接收单号(23)");
                data_source.Columns.Remove("采购单位编号(25)");
                data_source.Columns.Remove("采购单位名称(26)");
                data_source.Columns.Remove("配送单位编号(27)");
                data_source.Columns.Remove("配送单位名称(28)");
                data_source.Columns.Remove("源单号(29)");
                data_source.Columns.Remove("源单序号(30)");
                data_source.Columns.Remove("操作员(31)");
                data_source.Columns.Remove("状态(33)");
                data_source.Columns.Remove("明细备注(34)");
                data_source.Columns.Remove("说明(35)");
                data_source.Columns.Remove("配送类别(36)");
                data_source.Columns.Remove("零件类别(37)");
                data_source.Columns.Remove("库管员(38)");
                
                dataGridView1.DataSource = null;  //没有这一行，dataGridView的列在重新导入时，将无法恢复，每次给DataSource修改数据时最好都写上
                dataGridView1.DataSource = data_source;

                deleteUnusedColumns_3.Enabled = false;
                renameSort_3.Enabled = true;
            }
            else
                MessageBox.Show("请先等待出库明细文件导入完毕");
        }

        //更名与排序
        private void renameSort_3_Click(object sender, EventArgs e)
        {
            data_source.Rows[0]["零件编号(11)"] = "零件图号";
            //零件名称(13)
            data_source.Rows[0]["入库数(20)"] = "数量";
            data_source.Rows[0]["供应商编号(15)"] = "厂家代码";
            data_source.Rows[0]["供应商名称(16)"] = "厂家名称";
            data_source.Rows[0]["接收客户单号(24)"] = "入库单号";
            data_source.Rows[0]["操作时间(32)"] = "DATE";
            data_source.Rows[0]["客户单号(3)"] = "PO_NO";

            data_source.Columns["零件编号(11)"].SetOrdinal(0);
            data_source.Columns["零件名称(13)"].SetOrdinal(1);
            data_source.Columns["入库数(20)"].SetOrdinal(2);
            data_source.Columns["供应商编号(15)"].SetOrdinal(3);
            data_source.Columns["供应商名称(16)"].SetOrdinal(4);
            data_source.Columns["接收客户单号(24)"].SetOrdinal(5);
            data_source.Columns["操作时间(32)"].SetOrdinal(6);
            data_source.Columns["客户单号(3)"].SetOrdinal(7);

            dataGridView1.DataSource = null;
            dataGridView1.DataSource = data_source;

            renameSort_3.Enabled = false;
            partNumberMid_3.Enabled = true;
        }

        //零件图号(mid)
        private void partNumberMid_3_Click(object sender, EventArgs e)
        {
            int overlength_length = 30;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "零件编号(11)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            partNumberMid_3.Enabled = false;
            manufacturerCodeMid_3.Enabled = true;
        }

        //厂家代码(mid)
        private void manufacturerCodeMid_3_Click(object sender, EventArgs e)
        {
            int overlength_length = 9;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "供应商编号(15)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            manufacturerCodeMid_3.Enabled = false;
            receiveCustomerNumber_3.Enabled = true;
        }

        //入库单号(mid)
        private void receiveCustomerNumber_3_Click(object sender, EventArgs e)
        {
            int overlength_length = 30;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "接收客户单号(24)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            receiveCustomerNumber_3.Enabled = false;
            CustomerNumber_3.Enabled = true;
        }

        //PO_NO(mid)
        private void CustomerNumber_3_Click(object sender, EventArgs e)
        {
            int overlength_length = 30;  //此处设定列超长时的长度，查找关键字：参数
            string target_str = "客户单号(3)";

            if (GetMaxLength(target_str, overlength_length) == false)
                SubColumn(target_str, overlength_length);

            CustomerNumber_3.Enabled = false;
            time_3.Enabled = true;
        }

        //DATE(DATE)
        private void time_3_Click(object sender, EventArgs e)
        {
            string times = Convert.ToDateTime(data_source.Rows[1]["操作时间(32)"].ToString()).ToString("yyyy/MM/dd");

            Form2 frm = new Form2();
            frm.StartPosition = FormStartPosition.CenterScreen;
            frm.OldTime = times;
            frm.NewTime = times;
            frm.ShowDialog();

            if (frm.DialogResult == DialogResult.OK)
            {
                for (int i = 1; i < data_source.Rows.Count; i++)
                {
                    if (frm.NewTime != null && frm.NewTime.Trim() != "")  //有些时候，导入的表会出现多一行空白行，导致报错，这里把它取消掉
                        data_source.Rows[i]["操作时间(32)"] = frm.NewTime;
                    else
                    {
                        MessageBox.Show("输入的时间不能为空！");
                        return;
                    }
                }
            }
            else if (frm.DialogResult == DialogResult.No)
                return;

            time_3.Enabled = false;
            exportFile.Enabled = true;
        }

        #endregion
    }
}