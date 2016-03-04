﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DebugLogReader
{
    public class DebugLog
    {
        public DebugLog()
        {
            m_cameraNumber = -1;
            m_rows = new List<DebugLogRow>();
        }

        public DebugLog(int cameraNumber, String[] debugLogText, Regex r, List<DebugLogRowFilter> filters)
        {
            m_cameraNumber = cameraNumber;
            m_rows = new List<DebugLogRow>();
            DebugLogRow newRow = null;
            DateTime previousTimestamp = DateTime.MinValue;
            int rowCount = 0;

            foreach (String line in debugLogText)
            {
                newRow = new DebugLogRow(cameraNumber, line, r, previousTimestamp);
                AddRow(newRow, filters);

                if (newRow != null)
                {
                    previousTimestamp = newRow.Timestamp;
                }
                rowCount++;
            }

            if ((debugLogText.Length != m_rows.Count) && (filters == null))
            {
                throw new Exception("Ooops!");
            }
        }

        private void AddRow(DebugLogRow newRow, List<DebugLogRowFilter> filters)
        {
            bool conditionsMet = false;

            if (filters == null)
            {
                conditionsMet = true;
            }
            else
            {
                conditionsMet = (filters.Count == 0);

                if (filters.Count > 0)
                {
                    // Check first condition
                    conditionsMet = filters[0].MeetsConditions(newRow);

                    for (int i = 1; i < filters.Count; i++)
                    {
                        conditionsMet = conditionsMet && filters[i].MeetsConditions(newRow);
                    }
                }
            }

            if (conditionsMet)
            {
                m_rows.Add(newRow);
            }
        }

        public void AddLog(DebugLog newLog)
        {
            m_rows.AddRange(newLog.m_rows);
        }

        public void Sort()
        {
            m_rows.Sort(delegate (DebugLogRow log1, DebugLogRow log2) { return log1.Timestamp.CompareTo(log2.Timestamp); });
        }

        public int Count
        {
            get
            {
                return m_rows.Count;
            }
        }

        public int CameraNumber
        {
            get
            {
                return m_cameraNumber;
            }
        }

        public void Save(String filename)
        {
            StreamWriter sw = new StreamWriter(filename);
            foreach (DebugLogRow row in m_rows)
            {
                sw.WriteLine(row.ToString());
            }

            sw.Close();
        }

        int m_cameraNumber;
        List<DebugLogRow> m_rows;
    }
}
