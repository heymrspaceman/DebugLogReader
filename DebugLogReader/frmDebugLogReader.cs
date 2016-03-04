﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DebugLogReader
{
    public partial class frmDebugLogReader : Form
    {
        public frmDebugLogReader()
        {
            InitializeComponent();

            txtLogDirectory.Text = @"C:\Users\tmackenzie01\Documents\Recorder testing\20160302\DebugLogs1";
        }

        private void btnReadLogs_Click(object sender, EventArgs e)
        {
            StartLogReads();
        }

        private void StartLogReads()
        {
            lstProgress.Items.Clear();
            btnReadLogs.Enabled = false;
            List<int> cameraNumbers = GetCameraNumbers();
            List<DebugLogRowFilter> filters = GetFilters();

            cameraNumbers.Sort();

            if (cameraNumbers.Count > 0)
            {
                m_logs = new List<DebugLog>();

                foreach (int cameraNumber in cameraNumbers)
                {
                    DebugLogReaderArgs args = new DebugLogReaderArgs(txtLogDirectory.Text, cameraNumber);
                    args.AddFilters(filters);
                    BackgroundWorker bgReadLog = new BackgroundWorker();
                    bgReadLog.DoWork += ReadLogs_DoWork;
                    bgReadLog.RunWorkerCompleted += ReadLogs_RunWorkerCompleted;
                    bgReadLog.RunWorkerAsync(args);
                    m_readLogsInProgress++;
                }
            }

            prgFiles.Maximum = cameraNumbers.Count;
        }

        private List<int> GetCameraNumbers()
        {
            String[] logDirectories = Directory.GetDirectories(txtLogDirectory.Text);
            List<int> cameraNumbers = new List<int>();
            int cameraNumber = -1;

            foreach (String logDirectory in logDirectories)
            {
                Regex r = new Regex("Cam.(?<cameraName>[0-9]+)_(?<cameraNumber>[0-9]+)$", RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

                if (r.IsMatch(logDirectory))
                {
                    Match match = r.Match(logDirectory);
                    String strCamName = match.Groups["cameraName"].Value;
                    String strCamNumber = match.Groups["cameraNumber"].Value;

                    if (strCamName.Equals(strCamNumber))
                    {
                        if (Int32.TryParse(strCamNumber, out cameraNumber))
                        {
                            cameraNumbers.Add(cameraNumber);
                        }
                    }
                }
            }

            return cameraNumbers;
        }

        List<DebugLogRowFilter> GetFilters()
        {
            List<DebugLogRowFilter> filters = null;
            StringBuilder filterDescription = new StringBuilder();

            if (chkQueueFilter.Checked)
            {
                if (!String.IsNullOrEmpty(txtQueueAbove.Text))
                {
                    int queueAbove = 0;
                    if (Int32.TryParse(txtQueueAbove.Text, out queueAbove))
                    {
                        DebugLogRowFilter filter = new DebugLogRowFilter(eFilterBy.QueueCount, queueAbove.ToString());
                        filterDescription.Append(filter.ToString());
                        if (filters == null)
                        {
                            filters = new List<DebugLogRowFilter>();
                            filters.Add(filter);
                        }
                    }
                }
            }

            if (chkStartAtSameTime.Checked)
            {
                // We cannot create the filter as we need to have the times for all the logs first
                // we do this just before the sort
                filterDescription.Append("_StartAtSameTime");
            }

            m_filterDescription = filterDescription.ToString();
            return filters;
        }

        private void ReadLogs_DoWork(object sender, DoWorkEventArgs e)
        {
            DebugLogReaderArgs args = (DebugLogReaderArgs)e.Argument;

            String[] logFiles = Directory.GetFiles(args.LogDirectory());
            String pushFile = "";
            String popFile = "";
            DebugLog pushLog = null;
            DebugLog popLog = null;
            int fileFoundCount = 0;

            foreach (String logFile in logFiles)
            {
                if (logFile.EndsWith("_Push.txt"))
                {
                    pushFile = logFile;
                    fileFoundCount++;
                }
                if (logFile.EndsWith("_Pop.txt"))
                {
                    popFile = logFile;
                    fileFoundCount++;
                }
            }

            if (fileFoundCount == 2)
            {
                if (!String.IsNullOrEmpty(pushFile))
                {
                    pushLog = new PushDebugLog(args.CameraNumber, args.Filters);
                    pushLog.Load(pushFile);
                }
                if (!String.IsNullOrEmpty(popFile))
                {
                    popLog = new PopDebugLog(args.CameraNumber, args.Filters);
                    popLog.Load(popFile);
                }

                e.Result = new DebugLogReadResult(args.CameraNumber, pushLog, popLog);
            }
            else
            {
                e.Result = new DebugLogReadResult(args.CameraNumber);
            }
        }

        private void AddMessage(String text)
        {
            ListViewItem newItem = new ListViewItem(text);
            lstProgress.Items.Add(newItem);
            lstProgress.Items[lstProgress.Items.Count - 1].EnsureVisible();
        }

        private void ReadLogs_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            DebugLogReadResult result = (DebugLogReadResult)e.Result;
            bool combineLogs = false;

            AddMessage(result.ToString());
            prgFiles.Value++;

            lock (m_logs)
            {
                m_logs.Add(result.PushLog);
                m_logs.Add(result.PopLog);
                m_readLogsInProgress--;

                combineLogs = (m_readLogsInProgress == 0);
            }

            if (combineLogs)
            {
                StartLogCombines();
            }
        }

        private void StartLogCombines()
        {
            prgFiles.Maximum = 100;
            prgFiles.Value = 0;

            BackgroundWorker bgCombineLogs = new BackgroundWorker();
            bgCombineLogs.WorkerReportsProgress = true;
            bgCombineLogs.DoWork += CombineLogs_DoWork;
            bgCombineLogs.ProgressChanged += CombineLogs_ProgressChanged;
            bgCombineLogs.RunWorkerCompleted += CombineLogs_RunWorkerCompleted;
            bgCombineLogs.RunWorkerAsync(m_logs);
        }

        private void CombineLogs_DoWork(object sender, DoWorkEventArgs e)
        {
            BackgroundWorker worker = sender as BackgroundWorker;
            List<DebugLog> logs = (List<DebugLog>)e.Argument;
            DebugLog giantLog = new DebugLog();
            int progressCount = 0;
            int progressFinish = logs.Count + 1;
            DateTime latestStartTime = DateTime.MinValue;

            foreach (DebugLog log in logs)
            {
                if (latestStartTime < log.GetStartTime())
                {
                    latestStartTime = log.GetStartTime();
                }
                giantLog.AddLog(log);
                progressCount++;

                worker.ReportProgress((progressCount * 100) / progressFinish, log.CameraNumber);
            }

            if (chkStartAtSameTime.Checked)
            {
                giantLog.Filter(new DebugLogRowFilter(eFilterBy.StartTime, latestStartTime.ToString(@"dd/MM/yyyy HH:mm:ss.fff")));
            }
            giantLog.Sort();
            progressCount++;

            worker.ReportProgress((progressCount * 100) / progressFinish, -1);
            e.Result = giantLog;
        }

        private void CombineLogs_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            int cameraNumber = (int)e.UserState;
            if (cameraNumber >= 0)
            {
                AddMessage($"Camera {cameraNumber} logs combined");
            }
            prgFiles.Value = e.ProgressPercentage;
        }

        private void CombineLogs_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            AddMessage("Logs combined");
            DebugLog giantLog = (DebugLog)e.Result;

            String giantLogFilename = Path.Combine(txtLogDirectory.Text, $"giantLog{m_filterDescription}.txt");
            if (File.Exists(giantLogFilename))
            {
                if (MessageBox.Show($"{giantLogFilename} exists\r\nYou want to overwrite it?", "", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    File.Delete(giantLogFilename);
                }
            }

            giantLog.Save(giantLogFilename);
            AddMessage($"File created {giantLogFilename}");
        }

        int m_readLogsInProgress;
        List<DebugLog> m_logs;
        String m_filterDescription;

        // Declare and intialise these Regex here as it's costly to keep creating them
        public static Regex m_pushedRegex = new Regex("Pushed...(?<timestamp>[0-9]+.[0-9]+.[0-9]+.[0-9]+.[0-9]+.[0-9]+.[0-9]+).(\\-\\-\\-...[0-9]+.[0-9]+.seconds..)*Q.(?<queueCount>[0-9]+).F..?[0-9]+,.[0-9]+,.[0-9]+$",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);

        public static Regex m_poppedRegex = new Regex("Popped...(?<timestamp>[0-9]+.[0-9]+.[0-9]+.[0-9]+.[0-9]+.[0-9]+.[0-9]+).(\\-\\-\\-..[0-9]+.[0-9]+.seconds..)*Q.(?<queueCount>[0-9]+).F..?[0-9]+,.[0-9]+,.[0-9]+$",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.Multiline);
    }
}
