﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DebugLogReader
{
    public class DebugLogReaderArgs
    {
        public DebugLogReaderArgs(String directory, int cameraNumber)
        {
            m_directory = directory;
            m_cameraNumber = cameraNumber;
        }

        public String LogDirectory()
        {
            return $"{m_directory}\\Cam {m_cameraNumber.ToString()}_{m_cameraNumber.ToString()}";
        }

        public int CameraNumber
        {
            get
            {
                return m_cameraNumber;
            }
        }

        public String Directory
        {
            get
            {
                return m_directory;
            }
        }

        int m_cameraNumber;
        String m_directory;
    }
}
