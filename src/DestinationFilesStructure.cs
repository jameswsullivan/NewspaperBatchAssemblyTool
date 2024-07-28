﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NewspaperBatchAssemblyTool.src
{
    public class DestinationFilesStructure
    {
        public string SOURCE_FILE_PATH { get; set; }
        public string OUTPUT_FOLDER_PATH { get; set; }
        public string BATCH_FOLDER_NAME { get; set; }
        public string DATA_FOLDER_NAME { get; set; }
        public string LCCN_FOLDER_NAME { get; set; }
        public string BATCH_XML_FILE_PATH { get; set; }
        public string PRINT_FOLDER_NAME { get; set; }
        public string ISSUE_FOLDER_NAME { get; set; }
        public string DESTINATION_FILE_PATH { get; set; }
        public string ISSUE_XML_FILE_PATH { get; set; }
    }
}
