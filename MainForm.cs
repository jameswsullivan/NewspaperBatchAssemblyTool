using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using ImageMagick;
using NewspaperBatchAssemblyTool.src;
using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace NewspaperBatchAssemblyTool
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
            CustomInitialization();
        }

        #region Custom Methods
        private void loadLccnComboBoxItems()
        {
            foreach (var lccn in LCCN.LCCN_ITEMS)
            {
                selectLccnComboBox.Items.Add($"{lccn.Key} - {lccn.Value.TITLE}");
            }
        }

        private void lccnComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (selectLccnComboBox.SelectedIndex != -1)
            {
                Properties.Settings.Default.SeletedLccn = selectLccnComboBox.SelectedItem.ToString().Substring(0, 10);
                Properties.Settings.Default.Save();

                //Automatically populate the BATCH_PREFIX based on selected LCCN:
                LCCN_PROPERTIES lccnProperties;
                LCCN.LCCN_ITEMS.TryGetValue(Properties.Settings.Default.SeletedLccn, out lccnProperties);
                batchNamePrefixTextBox.Text = lccnProperties.BATCH_PREFIX;

                //Set award year based on selected LCCN:
                switch (Properties.Settings.Default.SeletedLccn)
                {
                    case "sn37020267":
                        Properties.Settings.Default.AwardYear = "2020";
                        Properties.Settings.Default.Save();
                        logForm.appendTextsToLog($"AwardYear for \"{selectLccnComboBox.SelectedItem}\" is set to {Properties.Settings.Default.AwardYear}.", logForm.LOG_TYPE_INFO);
                        break;
                    case "sn86088544":
                        Properties.Settings.Default.AwardYear = "2015";
                        Properties.Settings.Default.Save();
                        logForm.appendTextsToLog($"AwardYear for \"{selectLccnComboBox.SelectedItem}\" is set to {Properties.Settings.Default.AwardYear}.", logForm.LOG_TYPE_INFO);
                        break;
                    case "sn88037063":
                        Properties.Settings.Default.AwardYear = "2020";
                        Properties.Settings.Default.Save();
                        logForm.appendTextsToLog($"AwardYear for \"{selectLccnComboBox.SelectedItem}\" is set to {Properties.Settings.Default.AwardYear}.", logForm.LOG_TYPE_INFO);
                        break;
                    default:
                        Properties.Settings.Default.AwardYear = String.Empty;
                        Properties.Settings.Default.Save();
                        logForm.appendTextsToLog($"No newspaper is selected. AwardYear is set to String.Empty.", logForm.LOG_TYPE_INFO);
                        break;
                }

            }
        }

        private bool validate_batchNumberTextBox()
        {
            return Regex.IsMatch(batchNumberTextBox.Text, @"^\d+$");
        }

        private void relocateFilesWithNoMetadata()
        {
            string issuesWithNoMetadataFolderPath = Path.Combine(Properties.Settings.Default.OutputFolder, "IssuesWithNoMetadata");
            string sourceFileIssueNumber = String.Empty;

            if (!Directory.Exists(issuesWithNoMetadataFolderPath))
            {
                Directory.CreateDirectory(issuesWithNoMetadataFolderPath);
            }

            List<ListViewItem> itemsToRemove = new List<ListViewItem>();

            foreach (ListViewItem sourceFileItem in sourceFilesListView.Items)
            {
                string sourceFilePath = sourceFileItem.SubItems[0].Text;

                string sourceFileParentFolderName = Directory.GetParent(sourceFilePath).Name;
                string sourceFileParentFolderNameNormalized = sourceFileParentFolderName.Replace("-", "");
                string[] sourceFileParentFolderNameParts = sourceFileParentFolderNameNormalized.Split("_");


                if (sourceFileParentFolderNameParts.Length > 1)
                {
                    sourceFileIssueNumber = sourceFileParentFolderNameParts[1] + Properties.Settings.Default.EditionOrder;
                }
                else
                {
                    logForm.appendTextsToLog($"{sourceFileParentFolderName} contains illegal issue number in the folder name.", logForm.LOG_TYPE_ERROR);
                }

                if (!importMetadataForm.issueMetadata.ContainsKey(sourceFileIssueNumber))
                {
                    string sourceFileName = Path.GetFileName(sourceFilePath);

                    string destinationFilePath = Path.Combine(issuesWithNoMetadataFolderPath, sourceFileParentFolderName, sourceFileName);
                    string destinationFolderPath = Path.GetDirectoryName(destinationFilePath);

                    try
                    {
                        if (!Directory.Exists(destinationFolderPath))
                        {
                            Directory.CreateDirectory(destinationFolderPath);
                        }

                        File.Move(sourceFilePath, destinationFilePath);
                        itemsToRemove.Add(sourceFileItem);

                        logForm.appendTextsToLog($"Issue {sourceFileIssueNumber} has no metadata, relocating {sourceFilePath} to {destinationFilePath}.", logForm.LOG_TYPE_INFO);
                    }
                    catch (IOException ex)
                    {
                        logForm.appendTextsToLog($"Relocating {sourceFilePath} to {destinationFilePath} encountered the following error: \"{ex.Message}\".", logForm.LOG_TYPE_ERROR);
                    }
                }
            }
            logForm.appendTextsToLog($"{itemsToRemove.Count} files will be removed from list.", logForm.LOG_TYPE_INFO);

            foreach (ListViewItem itemToRemove in itemsToRemove)
            {
                sourceFilesListView.Items.Remove(itemToRemove);
            }
            logForm.appendTextsToLog($"{itemsToRemove.Count} files have been removed from source file list.", logForm.LOG_TYPE_INFO);
        }

        private void constructDestinationFileStructure()
        {
            foreach (ListViewItem sourceFileItem in sourceFilesListView.Items)
            {

                DestinationFilesStructure destItem = new DestinationFilesStructure();

                destItem.SOURCE_FILE_PATH = sourceFileItem.SubItems[0].Text;
                destItem.OUTPUT_FOLDER_PATH = Properties.Settings.Default.OutputFolder;
                destItem.BATCH_FOLDER_NAME = batchNamePrefixTextBox.Text + batchNumberTextBox.Text;
                destItem.DATA_FOLDER_NAME = "data";

                destItem.BATCH_XML_FILE_PATH = Path.Combine(destItem.OUTPUT_FOLDER_PATH, destItem.BATCH_FOLDER_NAME, destItem.DATA_FOLDER_NAME, "batch.xml");

                destItem.LCCN_FOLDER_NAME = Properties.Settings.Default.SeletedLccn;
                destItem.LCCN = Properties.Settings.Default.SeletedLccn;
                destItem.PRINT_FOLDER_NAME = "print";

                string sourceFileIssueFolderName = Directory.GetParent(destItem.SOURCE_FILE_PATH).Name;
                string[] sourceFileIsueFolderNameParts = sourceFileIssueFolderName.Split("_");
                if (sourceFileIsueFolderNameParts.Length > 1)
                {
                    destItem.ISSUE_DATE = sourceFileIsueFolderNameParts[1];
                }
                else
                {
                    logForm.appendTextsToLog($"{sourceFileIssueFolderName} contains illegal issue number", logForm.LOG_TYPE_ERROR);
                }

                destItem.ISSUE_FOLDER_NAME = destItem.ISSUE_DATE.Replace("-", "") + Properties.Settings.Default.EditionOrder;
                destItem.ISSUE_NUMBER = destItem.ISSUE_FOLDER_NAME;
                destItem.ISSUE_EDITION_ORDER = Properties.Settings.Default.EditionOrder;

                destItem.DESTINATION_FILE_PATH = Path.Combine(
                    destItem.OUTPUT_FOLDER_PATH, destItem.BATCH_FOLDER_NAME, destItem.DATA_FOLDER_NAME,
                    destItem.LCCN_FOLDER_NAME, destItem.PRINT_FOLDER_NAME,
                    destItem.ISSUE_FOLDER_NAME, Path.GetFileName(destItem.SOURCE_FILE_PATH)
                    );

                destItem.ISSUE_XML_FILE_FULL_PATH = Path.Combine(
                    destItem.OUTPUT_FOLDER_PATH, destItem.BATCH_FOLDER_NAME, destItem.DATA_FOLDER_NAME,
                    destItem.LCCN_FOLDER_NAME, destItem.PRINT_FOLDER_NAME,
                    destItem.ISSUE_FOLDER_NAME, destItem.ISSUE_NUMBER + ".xml"
                    );

                destItem.ISSUE_XML_FILE_RELATIVE_PATH = destItem.LCCN_FOLDER_NAME + "/" + destItem.PRINT_FOLDER_NAME + "/" + destItem.ISSUE_FOLDER_NAME + "/" + destItem.ISSUE_NUMBER + ".xml";

                destinationFileStructure.Add(destItem);
            }

            //foreach ( DestinationFilesStructure item in destinationFileStructure )
            //{
            //    logForm.appendTextsToLog(item.SOURCE_FILE_PATH, logForm.LOG_TYPE_INFO);
            //    logForm.appendTextsToLog(item.DESTINATION_FILE_PATH, logForm.LOG_TYPE_INFO);
            //}

        }

        private void assembleBatch_CopyFiles()
        {
            foreach (DestinationFilesStructure batchItem in destinationFileStructure)
            {
                try
                {
                    string destinationDirectory = Path.GetDirectoryName(batchItem.DESTINATION_FILE_PATH);
                    if (!Directory.Exists(destinationDirectory))
                    {
                        Directory.CreateDirectory(destinationDirectory);
                        logForm.appendTextsToLog($"{destinationDirectory} doesn't existed, but has been created.", logForm.LOG_TYPE_INFO);
                    }

                    File.Copy(batchItem.SOURCE_FILE_PATH, batchItem.DESTINATION_FILE_PATH, overwrite: true);
                    logForm.appendTextsToLog($"File: \"{batchItem.DESTINATION_FILE_PATH}\" has been added to \"{batchItem.BATCH_FOLDER_NAME}\".", logForm.LOG_TYPE_INFO);
                }
                catch (IOException ex)
                {
                    logForm.appendTextsToLog($"An error occurred: {ex.Message}", logForm.LOG_TYPE_ERROR);
                }
            }
        }

        private void assembleBatch_CreateBatchXMLFile()
        {
            //Set batch.xml attributes:
            Batch_XML_Attributes batch_XML_Attributes = new Batch_XML_Attributes();
            batch_XML_Attributes.BATCH_NAME = batchNamePrefixTextBox.Text + batchNumberTextBox.Text;
            batch_XML_Attributes.AWARDEE = Properties.Settings.Default.Awardee;
            batch_XML_Attributes.AWARD_YEAR = Properties.Settings.Default.AwardYear;
            batch_XML_Attributes.BATCH_XML_FILE_PATH = Path.Combine(Properties.Settings.Default.OutputFolder, batch_XML_Attributes.BATCH_NAME, "data", "batch.xml");

            //Construct batch.xml file:
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Indent = true,
                NewLineOnAttributes = true,
                Encoding = System.Text.Encoding.UTF8,
                OmitXmlDeclaration = false
            };

            using (XmlWriter writer = XmlWriter.Create(batch_XML_Attributes.BATCH_XML_FILE_PATH, settings))
            {
                writer.WriteStartDocument();

                writer.WriteStartElement("ndnp", "batch", "http://www.loc.gov/ndnp");

                writer.WriteAttributeString("xmlns", "ndnp", null, "http://www.loc.gov/ndnp");
                writer.WriteAttributeString("xmlns", "xsi", null, "http://www.w3.org/2001/XMLSchema-instance");
                writer.WriteAttributeString("xmlns", null, "http://www.loc.gov/ndnp");
                writer.WriteAttributeString("name", batch_XML_Attributes.BATCH_NAME);
                writer.WriteAttributeString("awardee", batch_XML_Attributes.AWARDEE);
                writer.WriteAttributeString("awardYear", batch_XML_Attributes.AWARD_YEAR);

                writer.WriteWhitespace(Environment.NewLine);
                writer.WriteFullEndElement();

                writer.WriteEndDocument();
            }
        }

        private void assembleBatch_ConstructBatchXMLIssueElements()
        {
            foreach (DestinationFilesStructure item in destinationFileStructure)
            {
                if (!batch_XML_Issue_Elements.ContainsKey(item.ISSUE_NUMBER))
                {
                    Batch_XML_Issue_Element batchXmlIssueItem = new Batch_XML_Issue_Element();
                    batchXmlIssueItem.BATCH_XML_FILE_PATH = item.BATCH_XML_FILE_PATH;
                    batchXmlIssueItem.LCCN = item.LCCN;
                    batchXmlIssueItem.ISSUE_DATE = item.ISSUE_DATE;
                    batchXmlIssueItem.EDITION_ORDER = item.ISSUE_EDITION_ORDER;
                    batchXmlIssueItem.ISSUE_XML_RELATIVE_PATH = item.ISSUE_XML_FILE_RELATIVE_PATH;

                    batch_XML_Issue_Elements.Add(item.ISSUE_NUMBER, batchXmlIssueItem);
                }
            }

            //foreach (KeyValuePair<string, Batch_XML_Issue_Element> item in batch_XML_Issue_Elements)
            //{
            //    string logText = item.Key + ":" + item.Value.LCCN + ":" + item.Value.ISSUE_DATE + ":" + item.Value.EDITION_ORDER + ":" + item.Value.ISSUE_XML_RELATIVE_PATH;
            //    logForm.appendTextsToLog($"\"{logText}\"", logForm.LOG_TYPE_INFO);
            //}

            //logForm.appendTextsToLog($"{batch_XML_Issue_Elements.Count} issues are in this batch.", logForm.LOG_TYPE_INFO);

        }

        private void assembleBatch_AddIssueElementsToBatchXMLFile()
        {
            XmlDocument batchXmlDoc = new XmlDocument();
            batchXmlDoc.Load(batchXmlFileFullPath);

            XmlNamespaceManager nsmgr = new XmlNamespaceManager(batchXmlDoc.NameTable);
            nsmgr.AddNamespace("ndnp", "http://www.loc.gov/ndnp");
            XmlNode batchNode = batchXmlDoc.SelectSingleNode("//ndnp:batch", nsmgr);

            if (batchNode != null)
            {
                foreach (KeyValuePair<string, Batch_XML_Issue_Element> issueDictElement in batch_XML_Issue_Elements)
                {
                    XmlElement issueXmlElement = batchXmlDoc.CreateElement("issue", "http://www.loc.gov/ndnp");
                    issueXmlElement.SetAttribute("lccn", issueDictElement.Value.LCCN);
                    issueXmlElement.SetAttribute("issueDate", issueDictElement.Value.ISSUE_DATE);
                    issueXmlElement.SetAttribute("editionOrder", issueDictElement.Value.EDITION_ORDER);

                    issueXmlElement.InnerText = issueDictElement.Value.ISSUE_XML_RELATIVE_PATH;

                    batchNode.AppendChild(issueXmlElement);
                }

                batchXmlDoc.Save(batchXmlFileFullPath);
                logForm.appendTextsToLog($"\"issue\" elements have been added to {batchXmlFileFullPath}", logForm.LOG_TYPE_INFO);
            }
            else
            {
                logForm.appendTextsToLog($"{batchXmlFileFullPath} doesn't contain the \"ndnp:batch\" node", logForm.LOG_TYPE_ERROR);
            }
        }

        private void assembleBatch_ConstructIssueFilesInformation()
        {
            foreach (DestinationFilesStructure destFileItem in destinationFileStructure)
            {
                if (!issueFilesInformation.ContainsKey(destFileItem.ISSUE_NUMBER))
                {
                    IssueFilesInformation newIssueFilesInfoItem = new IssueFilesInformation();
                    newIssueFilesInfoItem.JP2_FILES = new List<Jp2FileProperties>();
                    newIssueFilesInfoItem.PDF_FILES = new List<string>();
                    newIssueFilesInfoItem.XML_FILES = new List<string>();
                    newIssueFilesInfoItem.NUMBER_OF_PAGES = 0;

                    switch (Path.GetExtension(destFileItem.DESTINATION_FILE_PATH))
                    {
                        case ".jp2":
                            Jp2FileProperties newJp2FilePropertyItem = new Jp2FileProperties();
                            newJp2FilePropertyItem.JP2_FILE_PATH = destFileItem.DESTINATION_FILE_PATH;
                            newIssueFilesInfoItem.JP2_FILES.Add(newJp2FilePropertyItem);
                            break;
                        case ".pdf":
                            newIssueFilesInfoItem.PDF_FILES.Add(destFileItem.DESTINATION_FILE_PATH);
                            break;
                        case ".xml":
                            newIssueFilesInfoItem.XML_FILES.Add(destFileItem.DESTINATION_FILE_PATH);
                            break;
                    }

                    newIssueFilesInfoItem.ISSUE_NUMBER = destFileItem.ISSUE_NUMBER;
                    newIssueFilesInfoItem.ISSUE_XML_FILE_PATH = Path.Combine(
                        Path.GetDirectoryName(destFileItem.DESTINATION_FILE_PATH),
                        destFileItem.ISSUE_NUMBER + ".xml"
                        );
                    newIssueFilesInfoItem.NUMBER_OF_PAGES += 1;

                    LCCN.LCCN_ITEMS.TryGetValue(destFileItem.LCCN, out LCCN_PROPERTIES lccnPropertiesValue);
                    newIssueFilesInfoItem.ISSUE_METS_LABEL = lccnPropertiesValue.TITLE + ", " + destFileItem.ISSUE_DATE;

                    newIssueFilesInfoItem.ISSUE_CREATEDATE = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
                    newIssueFilesInfoItem.LCCN = destFileItem.LCCN;

                    newIssueFilesInfoItem.ISSUE_VOLUME_METADATA_RAW = importMetadataForm.issueMetadata[newIssueFilesInfoItem.ISSUE_NUMBER].VOLUME;
                    MatchCollection volInfoMatches = Regex.Matches(newIssueFilesInfoItem.ISSUE_VOLUME_METADATA_RAW, @"\d+(?:/\d+)?");
                    if (volInfoMatches.Count != 2)
                    {
                        newIssueFilesInfoItem.ISSUE_VOLUME = "N/A";
                        newIssueFilesInfoItem.ISSUE_VOLUME_NUMBER = "N/A";
                    }
                    else
                    {
                        newIssueFilesInfoItem.ISSUE_VOLUME = volInfoMatches[0].Value;
                        newIssueFilesInfoItem.ISSUE_VOLUME_NUMBER = volInfoMatches[1].Value;
                    }

                    newIssueFilesInfoItem.ISSUE_EDITION_ORDER = destFileItem.ISSUE_EDITION_ORDER;
                    newIssueFilesInfoItem.ISSUE_DATE = destFileItem.ISSUE_DATE;

                    issueFilesInformation.Add(destFileItem.ISSUE_NUMBER, newIssueFilesInfoItem);
                }
                else
                {
                    switch (Path.GetExtension(destFileItem.DESTINATION_FILE_PATH))
                    {
                        case ".jp2":
                            Jp2FileProperties newJp2FilePropertyItem = new Jp2FileProperties();
                            newJp2FilePropertyItem.JP2_FILE_PATH = destFileItem.DESTINATION_FILE_PATH;
                            issueFilesInformation[destFileItem.ISSUE_NUMBER].JP2_FILES.Add(newJp2FilePropertyItem);
                            break;
                        case ".pdf":
                            issueFilesInformation[destFileItem.ISSUE_NUMBER].PDF_FILES.Add(destFileItem.DESTINATION_FILE_PATH);
                            break;
                        case ".xml":
                            issueFilesInformation[destFileItem.ISSUE_NUMBER].XML_FILES.Add(destFileItem.DESTINATION_FILE_PATH);
                            break;
                    }

                    issueFilesInformation[destFileItem.ISSUE_NUMBER].NUMBER_OF_PAGES += 1;
                }
            }

            foreach (var issueFileInfoItem in issueFilesInformation)
            {
                issueFileInfoItem.Value.NUMBER_OF_PAGES /= 3;
            }

            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                string issueLogText = $"Issue {issueFileInfoItem.Key} has {issueFileInfoItem.Value.NUMBER_OF_PAGES} pages.";
                logForm.appendTextsToLog(issueLogText, logForm.LOG_TYPE_INFO);
                issueLogText = String.Empty;
                issueLogText = $"Issue {issueFileInfoItem.Key} metadata: " +
                    $"{issueFileInfoItem.Value.ISSUE_NUMBER} - {issueFileInfoItem.Value.ISSUE_XML_FILE_PATH} - " +
                    $"{issueFileInfoItem.Value.ISSUE_METS_LABEL} - {issueFileInfoItem.Value.ISSUE_CREATEDATE} - {issueFileInfoItem.Value.LCCN} - " +
                    $"{issueFileInfoItem.Value.ISSUE_VOLUME_METADATA_RAW} - {issueFileInfoItem.Value.ISSUE_VOLUME} - {issueFileInfoItem.Value.ISSUE_VOLUME_NUMBER} - " +
                    $"{issueFileInfoItem.Value.ISSUE_EDITION_ORDER} - {issueFileInfoItem.Value.ISSUE_DATE}";
                logForm.appendTextsToLog(issueLogText, logForm.LOG_TYPE_INFO);

                foreach (Jp2FileProperties jp2File in issueFileInfoItem.Value.JP2_FILES)
                {
                    string jp2LogText = $"{issueFileInfoItem.Key} - {jp2File.JP2_FILE_PATH} .";
                    logForm.appendTextsToLog(jp2LogText, logForm.LOG_TYPE_INFO);
                }

                foreach (string pdfFile in issueFileInfoItem.Value.PDF_FILES)
                {
                    string pdfLogText = $"{issueFileInfoItem.Key} - {pdfFile} .";
                    logForm.appendTextsToLog(pdfLogText, logForm.LOG_TYPE_INFO);
                }

                foreach (string xmlFile in issueFileInfoItem.Value.XML_FILES)
                {
                    string xmlLogText = $"{issueFileInfoItem.Key} - {xmlFile} .";
                    logForm.appendTextsToLog(xmlLogText, logForm.LOG_TYPE_INFO);
                }
            }
        }

        private void getJp2FileAttributes()
        {
            int count = 0;

            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                foreach (Jp2FileProperties jp2File in issueFileInfoItem.Value.JP2_FILES)
                {
                    using (var image = new MagickImage(jp2File.JP2_FILE_PATH))
                    {
                        jp2File.IMAGE_WIDTH = image.Width.ToString();
                        jp2File.IMAGE_LENGTH = image.Height.ToString();
                    }
                    
                    string jp2LogText = $"{issueFileInfoItem.Key} - {jp2File.JP2_FILE_PATH} - Width: {jp2File.IMAGE_WIDTH} - Length: {jp2File.IMAGE_LENGTH}.";
                    logForm.appendTextsToLog(jp2LogText, logForm.LOG_TYPE_INFO);
                }

                count++;
                logForm.appendTextsToLog($"{count} / {issueFilesInformation.Count} issues' image files have been processed.", logForm.LOG_TYPE_INFO);
            }
        }
        private void assembleBatch_CreateIssueXMLFile_InitializeFile()
        {
            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                StringBuilder initialSection = new StringBuilder();

                initialSection.AppendLine($"<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                initialSection.AppendLine($"<mets xmlns=\"http://www.loc.gov/METS/\"");
                initialSection.AppendLine($"\txmlns:mods=\"http://www.loc.gov/mods/v3\"");
                initialSection.AppendLine($"\txmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"");
                initialSection.AppendLine($"\txmlns:mix=\"http://www.loc.gov/mix/\"");
                initialSection.AppendLine($"\txmlns:ndnp=\"http://www.loc.gov/ndnp\"");
                initialSection.AppendLine($"\txmlns:premis=\"http://www.oclc.org/premis\"");
                initialSection.AppendLine($"\txmlns:xlink=\"http://www.w3.org/1999/xlink\"");
                initialSection.AppendLine($"\tLABEL=\"{issueFileInfoItem.Value.ISSUE_METS_LABEL}\"");
                initialSection.AppendLine($"\tPROFILE=\"urn:library-of-congress:mets:profiles:ndnp:issue:v1.5\"");
                initialSection.AppendLine($"\tTYPE=\"urn:library-of-congress:ndnp:mets:newspaper:issue\"");
                initialSection.AppendLine($"\txsi:schemaLocation=\"http://www.loc.gov/METS/ http://www.loc.gov/standards/mets/version17/mets.v1-7.xsd http://www.loc.gov/mods/v3 http://www.loc.gov/standards/mods/v3/mods-3-3.xsd\">");
                initialSection.AppendLine($""); // Insert a line break.
                initialSection.AppendLine($"METSHDR_SECTION_PLACEHOLDER");
                initialSection.AppendLine($"DMDSEC_PLACEHOLDER");
                initialSection.AppendLine($"AMDSEC_PLACEHOLDER");
                initialSection.AppendLine($"FILESEC_PLACEHOLDER");
                initialSection.AppendLine($"STRUCTMAP_PLACEHOLDER");
                initialSection.AppendLine($"</mets>");

                File.WriteAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH, initialSection.ToString());

                logForm.appendTextsToLog($"{issueFileInfoItem.Value.ISSUE_XML_FILE_PATH} has been initialized.", logForm.LOG_TYPE_INFO);
            }
        }
        private void assembleBatch_UpdateIssueXMLFile_AddMetsHdrSection()
        {
            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                StringBuilder metsHdrSection = new StringBuilder();

                metsHdrSection.AppendLine($"\t<metsHdr CREATEDATE=\"{issueFileInfoItem.Value.ISSUE_CREATEDATE}\">");
                metsHdrSection.AppendLine($"\t\t<agent ROLE=\"CREATOR\" TYPE=\"ORGANIZATION\">");
                metsHdrSection.AppendLine($"\t\t\t<name>Texas A &amp; M University</name>");
                metsHdrSection.AppendLine($"\t\t</agent>");
                metsHdrSection.AppendLine($"\t</metsHdr>");

                string issueXmlFileContent = File.ReadAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH);

                issueXmlFileContent = issueXmlFileContent.Replace("METSHDR_SECTION_PLACEHOLDER", metsHdrSection.ToString());

                File.WriteAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH, issueXmlFileContent);

                logForm.appendTextsToLog($"metsHdr section has been updated in {issueFileInfoItem.Value.ISSUE_XML_FILE_PATH}", logForm.LOG_TYPE_INFO);
            }
        }
        private void assembleBatch_UpdateIssueXMLFile_AddDmdSec()
        {
            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                StringBuilder dmdSec = new StringBuilder();

                dmdSec.AppendLine($"\t<dmdSec ID=\"issueModsBib\">");
                dmdSec.AppendLine($"\t\t<mdWrap LABEL=\"Issue metadata\" MDTYPE=\"MODS\">");
                dmdSec.AppendLine($"\t\t\t<xmlData>");
                dmdSec.AppendLine($"\t\t\t\t<mods:mods>");
                dmdSec.AppendLine($"\t\t\t\t\t<mods:relatedItem type=\"host\">");
                dmdSec.AppendLine($"\t\t\t\t\t<mods:identifier type=\"lccn\">{issueFileInfoItem.Value.LCCN}</mods:identifier>");
                dmdSec.AppendLine($"\t\t\t\t\t<mods:part>");
                dmdSec.AppendLine($"\t\t\t\t\t\t<mods:detail type=\"volume\">");
                dmdSec.AppendLine($"\t\t\t\t\t\t<mods:number>{issueFileInfoItem.Value.ISSUE_VOLUME}</mods:number>");
                dmdSec.AppendLine($"\t\t\t\t\t\t</mods:detail>");
                dmdSec.AppendLine($"\t\t\t\t\t\t<mods:detail type=\"issue\">");
                dmdSec.AppendLine($"\t\t\t\t\t\t<mods:number>{issueFileInfoItem.Value.ISSUE_VOLUME_NUMBER}</mods:number>");
                dmdSec.AppendLine($"\t\t\t\t\t\t</mods:detail>");
                dmdSec.AppendLine($"\t\t\t\t\t\t<mods:detail type=\"edition\">");
                dmdSec.AppendLine($"\t\t\t\t\t\t<mods:number>{issueFileInfoItem.Value.ISSUE_EDITION_ORDER.Replace("0","")}</mods:number>");
                dmdSec.AppendLine($"\t\t\t\t\t\t<mods:caption></mods:caption>");
                dmdSec.AppendLine($"\t\t\t\t\t\t</mods:detail>");
                dmdSec.AppendLine($"\t\t\t\t\t</mods:part>");
                dmdSec.AppendLine($"\t\t\t\t\t</mods:relatedItem>");
                dmdSec.AppendLine($"\t\t\t\t\t<mods:originInfo>");
                dmdSec.AppendLine($"\t\t\t\t\t<mods:dateIssued encoding=\"iso8601\">{issueFileInfoItem.Value.ISSUE_DATE}</mods:dateIssued>");
                dmdSec.AppendLine($"\t\t\t\t\t</mods:originInfo>");
                dmdSec.AppendLine($"\t\t\t\t\t<mods:note type=\"noteAboutReproduction\">Present</mods:note>");
                dmdSec.AppendLine($"\t\t\t\t</mods:mods>");
                dmdSec.AppendLine($"\t\t\t</xmlData>");
                dmdSec.AppendLine($"\t\t</mdWrap>");
                dmdSec.AppendLine($"\t</dmdSec>");

                for (int i = 0; i < issueFileInfoItem.Value.NUMBER_OF_PAGES; i++)
                {
                    dmdSec.AppendLine($"\t<dmdSec ID=\"pageModsBib{i + 1}\">");
                    dmdSec.AppendLine($"\t\t<mdWrap MDTYPE=\"MODS\" LABEL=\"Page metadata\">");
                    dmdSec.AppendLine($"\t\t\t<xmlData>");
                    dmdSec.AppendLine($"\t\t\t\t<mods:mods>");
                    dmdSec.AppendLine($"\t\t\t\t\t<mods:part>");
                    dmdSec.AppendLine($"\t\t\t\t\t\t<mods:extent unit=\"pages\">");
                    dmdSec.AppendLine($"\t\t\t\t\t\t\t<mods:start>{i + 1}</mods:start>");
                    dmdSec.AppendLine($"\t\t\t\t\t\t</mods:extent>");
                    dmdSec.AppendLine($"\t\t\t\t\t</mods:part>");
                    dmdSec.AppendLine($"\t\t\t\t\t<mods:relatedItem type=\"original\">");
                    dmdSec.AppendLine($"\t\t\t\t\t\t<mods:physicalDescription>");
                    dmdSec.AppendLine($"\t\t\t\t\t\t\t<mods:form type=\"print\" />");
                    dmdSec.AppendLine($"\t\t\t\t\t\t</mods:physicalDescription>");
                    dmdSec.AppendLine($"\t\t\t\t\t\t<mods:location> ");
                    dmdSec.AppendLine($"\t\t\t\t\t\t\t<mods:physicalLocation authority=\"marcorg\" displayLabel=\"Texas A &amp; M University; College Station, TX\">TxCM</mods:physicalLocation>");
                    dmdSec.AppendLine($"\t\t\t\t\t\t</mods:location>");
                    dmdSec.AppendLine($"\t\t\t\t\t</mods:relatedItem>");
                    dmdSec.AppendLine($"\t\t\t\t\t<mods:note displayLabel=\"Texas A &amp; M University Libraries; College Station, TX\" type=\"agencyResponsibleForReproduction\">txa</mods:note>");
                    dmdSec.AppendLine($"\t\t\t\t\t<mods:note type=\"noteAboutReproduction\">Present</mods:note>");
                    dmdSec.AppendLine($"\t\t\t\t</mods:mods>");
                    dmdSec.AppendLine($"\t\t\t</xmlData>");
                    dmdSec.AppendLine($"\t\t</mdWrap>");
                    dmdSec.AppendLine($"\t</dmdSec>");
                }

                string issueXmlFileContent = File.ReadAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH);

                issueXmlFileContent = issueXmlFileContent.Replace("DMDSEC_PLACEHOLDER", dmdSec.ToString());

                File.WriteAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH, issueXmlFileContent);

                logForm.appendTextsToLog($"dmdSec section has been updated in {issueFileInfoItem.Value.ISSUE_XML_FILE_PATH}", logForm.LOG_TYPE_INFO);
            }
        }
        private void assembleBatch_UpdateIssueXMLFile_AddAmdSec()
        {
            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                StringBuilder amdSec = new StringBuilder();

                amdSec.AppendLine($"\t<amdSec>");

                for (int i = 0; i < issueFileInfoItem.Value.NUMBER_OF_PAGES; i++)
                {
                    amdSec.AppendLine($"\t\t<techMD ID=\"mixserviceFile{i + 1}\">");
                    amdSec.AppendLine($"\t\t\t<mdWrap MDTYPE=\"NISOIMG\" LABEL=\"NISO still image metadata\">");
                    amdSec.AppendLine($"\t\t\t\t<xmlData>");
                    amdSec.AppendLine($"\t\t\t\t\t<mix:mix>");
                    amdSec.AppendLine($"\t\t\t\t\t\t<mix:BasicImageParameters>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t<mix:Format>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:Compression>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t\t<mix:CompressionScheme>7</mix:CompressionScheme>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t</mix:Compression>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:PhotometricInterpretation> ");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t\t<mix:ColorSpace></mix:ColorSpace>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t</mix:PhotometricInterpretation>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t</mix:Format>");
                    amdSec.AppendLine($"\t\t\t\t\t\t</mix:BasicImageParameters>");
                    amdSec.AppendLine($"\t\t\t\t\t\t<mix:ImagingPerformanceAssessment>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t<mix:SpatialMetrics>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:SamplingFrequencyUnit>2</mix:SamplingFrequencyUnit>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:XSamplingFrequency>400</mix:XSamplingFrequency>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:YSamplingFrequency>400</mix:YSamplingFrequency>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:ImageWidth>{issueFileInfoItem.Value.JP2_FILES[i].IMAGE_WIDTH}</mix:ImageWidth>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:ImageLength>{issueFileInfoItem.Value.JP2_FILES[i].IMAGE_LENGTH}</mix:ImageLength>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t</mix:SpatialMetrics>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t<mix:Energetics>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t\t<mix:BitsPerSample></mix:BitsPerSample>");
                    amdSec.AppendLine($"\t\t\t\t\t\t\t</mix:Energetics>");
                    amdSec.AppendLine($"\t\t\t\t\t\t</mix:ImagingPerformanceAssessment>");
                    amdSec.AppendLine($"\t\t\t\t\t</mix:mix>");
                    amdSec.AppendLine($"\t\t\t\t</xmlData>");
                    amdSec.AppendLine($"\t\t\t</mdWrap>");
                    amdSec.AppendLine($"\t\t</techMD>");
                }

                amdSec.AppendLine($"\t</amdSec>");

                string issueXmlFileContent = File.ReadAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH);

                issueXmlFileContent = issueXmlFileContent.Replace("AMDSEC_PLACEHOLDER", amdSec.ToString());

                File.WriteAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH, issueXmlFileContent);

                logForm.appendTextsToLog($"amdSec section has been updated in {issueFileInfoItem.Value.ISSUE_XML_FILE_PATH}", logForm.LOG_TYPE_INFO);
            }
        }
        private void assembleBatch_UpdateIssueXMLFile_AddFileSec()
        {
            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                StringBuilder fileSec = new StringBuilder();

                fileSec.AppendLine($"\t<fileSec>");

                for (int i = 0; i < issueFileInfoItem.Value.NUMBER_OF_PAGES; i++)
                {
                    fileSec.AppendLine($"\t\t<fileGrp ID=\"pageFileGrp{i + 1}\">");
                    fileSec.AppendLine($"\t\t\t<file ID=\"masterFile{i + 1}\" USE=\"master\">");
                    fileSec.AppendLine($"\t\t\t\t<FLocat LOCTYPE=\"OTHER\" OTHERLOCTYPE=\"file\" xlink:href=\"{Path.GetFileNameWithoutExtension(issueFileInfoItem.Value.JP2_FILES[i].JP2_FILE_PATH)}.tif\" />");
                    fileSec.AppendLine($"\t\t\t</file>");
                    fileSec.AppendLine($"\t\t\t<file ID=\"serviceFile{i + 1}\" USE=\"service\" ADMID=\"mixserviceFile{i + 1}\">");
                    fileSec.AppendLine($"\t\t\t\t<FLocat LOCTYPE=\"OTHER\" OTHERLOCTYPE=\"file\" xlink:href=\"{Path.GetFileName(issueFileInfoItem.Value.JP2_FILES[i].JP2_FILE_PATH)}\" />");
                    fileSec.AppendLine($"\t\t\t</file>");
                    fileSec.AppendLine($"\t\t\t<file ID=\"otherDerivativeFile{i + 1}\" USE=\"derivative\">");
                    fileSec.AppendLine($"\t\t\t\t<FLocat LOCTYPE=\"OTHER\" OTHERLOCTYPE=\"file\" xlink:href=\"{Path.GetFileName(issueFileInfoItem.Value.PDF_FILES[i])}\" />");
                    fileSec.AppendLine($"\t\t\t</file>");
                    fileSec.AppendLine($"\t\t\t<file ID=\"ocrFile{i + 1}\" USE=\"ocr\">");
                    fileSec.AppendLine($"\t\t\t\t<FLocat LOCTYPE=\"OTHER\" OTHERLOCTYPE=\"file\" xlink:href=\"{Path.GetFileName(issueFileInfoItem.Value.XML_FILES[i])}\" />");
                    fileSec.AppendLine($"\t\t\t</file>");
                    fileSec.AppendLine($"\t\t</fileGrp>");
                }

                fileSec.AppendLine($"\t</fileSec>");

                string issueXmlFileContent = File.ReadAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH);

                issueXmlFileContent = issueXmlFileContent.Replace("FILESEC_PLACEHOLDER", fileSec.ToString());

                File.WriteAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH, issueXmlFileContent);

                logForm.appendTextsToLog($"fileSec section has been updated in {issueFileInfoItem.Value.ISSUE_XML_FILE_PATH}", logForm.LOG_TYPE_INFO);
            }
        }
        private void assembleBatch_UpdateIssueXMLFile_AddStructMap()
        {
            foreach (KeyValuePair<string, IssueFilesInformation> issueFileInfoItem in issueFilesInformation)
            {
                StringBuilder structMap = new StringBuilder();

                structMap.AppendLine($"\t<structMap>");
                structMap.AppendLine($"\t\t<div DMDID=\"issueModsBib\" TYPE=\"np:issue\">");

                for (int i = 0; i < issueFileInfoItem.Value.NUMBER_OF_PAGES; i++)
                {
                    structMap.AppendLine($"\t\t\t<div TYPE=\"np:page\" DMDID=\"pageModsBib{i + 1}\">");
                    structMap.AppendLine($"\t\t\t\t<fptr FILEID=\"masterFile{i + 1}\" />");
                    structMap.AppendLine($"\t\t\t\t<fptr FILEID=\"serviceFile{i + 1}\" />");
                    structMap.AppendLine($"\t\t\t\t<fptr FILEID=\"otherDerivativeFile{i + 1}\" />");
                    structMap.AppendLine($"\t\t\t\t<fptr FILEID=\"ocrFile{i + 1}\" />");
                    structMap.AppendLine($"\t\t\t</div>");
                }

                structMap.AppendLine($"\t\t</div>");
                structMap.AppendLine($"\t</structMap>");

                string issueXmlFileContent = File.ReadAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH);

                issueXmlFileContent = issueXmlFileContent.Replace("STRUCTMAP_PLACEHOLDER", structMap.ToString());

                File.WriteAllText(issueFileInfoItem.Value.ISSUE_XML_FILE_PATH, issueXmlFileContent);

                logForm.appendTextsToLog($"structMap section has been updated in {issueFileInfoItem.Value.ISSUE_XML_FILE_PATH}", logForm.LOG_TYPE_INFO);
            }
        }

        #endregion Custom Methods

        private void browseSourceFilesButton_Click(object sender, EventArgs e)
        {
            if (browseSourceFiles_folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                sourceFilesPathTextBox.Text = browseSourceFiles_folderBrowserDialog.SelectedPath;
                Properties.Settings.Default.SourceFolder = browseSourceFiles_folderBrowserDialog.SelectedPath;

                loadSourceFilesButton.Enabled = true;
            }
            else
            {
                sourceFilesPathTextBox.Text = String.Empty;
            }
        }

        private void loadSourceFilesButton_Click(object sender, EventArgs e)
        {
            if (browseSourceFiles_folderBrowserDialog.SelectedPath != null)
            {
                List<string> files = new List<string>();
                files.AddRange(Directory.GetFiles(browseSourceFiles_folderBrowserDialog.SelectedPath, "*.*", SearchOption.AllDirectories));

                foreach (string file in files)
                {
                    ListViewItem item = new ListViewItem(file);
                    item.SubItems.Add(STATUS_LOADED);

                    sourceFilesListView.Items.Add(item);
                }

                relocateFilesWithNoMetadata();

                statusBarNumberOfSourceFilesLabel.Text = $"{sourceFilesListView.Items.Count} files loaded.";

                //Enable Assemble Batch button:
                assembleBatchButton.Enabled = true;
            }
        }

        private void assembleBatchButton_Click(object sender, EventArgs e)
        {
            if (selectLccnComboBox.SelectedIndex == -1)
            {
                logForm.appendTextsToLog($"No LCCN/Newspaper Selected. Please select a newspaper.", logForm.LOG_TYPE_ERROR);
                MessageBox.Show($"No LCCN/Newspaper has been selected. Please select a newspaper.", $"No LCCN/Newspaper Selected");
            }
            else if (!validate_batchNumberTextBox())
            {
                logForm.appendTextsToLog($"Invalid Batch Number. No batch number provided or batch number contains invalid characters!", logForm.LOG_TYPE_ERROR);
                MessageBox.Show($"No batch number provided or batch number contains invalid characters!", $"Invalid Batch Number");
            }
            else
            {
                batchXmlFileFullPath = Path.Combine(
                    Properties.Settings.Default.OutputFolder,
                    batchNamePrefixTextBox.Text + batchNumberTextBox.Text,
                    "data", "batch.xml"
                    );
                logForm.appendTextsToLog($"batch.xml for {batchNamePrefixTextBox.Text + batchNumberTextBox.Text} is located at: {batchXmlFileFullPath}.", logForm.LOG_TYPE_INFO);

                constructDestinationFileStructure();
                logForm.appendTextsToLog($"Destination file structure has been constructed.", logForm.LOG_TYPE_INFO);

                assembleBatch_CopyFiles();
                logForm.appendTextsToLog($"Batch files have been copied.", logForm.LOG_TYPE_INFO);

                //Create batch.xml file:
                assembleBatch_CreateBatchXMLFile();
                logForm.appendTextsToLog($"batch.xml has been created.", logForm.LOG_TYPE_INFO);

                assembleBatch_ConstructBatchXMLIssueElements();
                logForm.appendTextsToLog($"{batch_XML_Issue_Elements.Count} issue elements have been constructed.", logForm.LOG_TYPE_INFO);

                assembleBatch_AddIssueElementsToBatchXMLFile();
                logForm.appendTextsToLog($"{batch_XML_Issue_Elements.Count} issue elements have been added to {batchXmlFileFullPath} .", logForm.LOG_TYPE_INFO);

                //Create issue xml files for each issue:
                assembleBatch_ConstructIssueFilesInformation();
                logForm.appendTextsToLog($"issueFilesInformation has been constructed and contains {issueFilesInformation.Count} issues.", logForm.LOG_TYPE_INFO);

                //Retrieve jp2 file attributes:
                getJp2FileAttributes();
                logForm.appendTextsToLog($"Jp2 file attributes have been added to issueFilesInformation.", logForm.LOG_TYPE_INFO);

                //Create the issue xml file section by section:
                assembleBatch_CreateIssueXMLFile_InitializeFile();
                assembleBatch_UpdateIssueXMLFile_AddMetsHdrSection();
                assembleBatch_UpdateIssueXMLFile_AddDmdSec();
                assembleBatch_UpdateIssueXMLFile_AddAmdSec();
                assembleBatch_UpdateIssueXMLFile_AddFileSec();
                assembleBatch_UpdateIssueXMLFile_AddStructMap();

                //Prompt batch assembly completion message:
                logForm.appendTextsToLog($"Assembly of batch {batchNamePrefixTextBox.Text + batchNumberTextBox.Text} has completed.", logForm.LOG_TYPE_INFO);
                logForm.appendTextsToLog($"Batch {batchNamePrefixTextBox.Text + batchNumberTextBox.Text} contains {issueFilesInformation.Count} issues.", logForm.LOG_TYPE_INFO);
            }
        }

        private void startOverButton_Click(object sender, EventArgs e)
        {

            //Reset settings:
            Properties.Settings.Default.SeletedLccn = String.Empty;
            Properties.Settings.Default.SourceFolder = String.Empty;
            Properties.Settings.Default.OutputFolder = optionsForm.browseOutputFolder_folderBrowserDialog.SelectedPath;
            Properties.Settings.Default.Awardee = "txa";
            Properties.Settings.Default.AwardYear = String.Empty;
            Properties.Settings.Default.MetadataLoaded = false;
            Properties.Settings.Default.SourceFilesLoaded = false;

            optionsForm.editionOrderComboBox.SelectedIndex = 0;
            Properties.Settings.Default.EditionOrder = optionsForm.editionOrderComboBox.SelectedItem?.ToString();

            optionsForm.outputFolderTextBox.Text = Properties.Settings.Default.OutputFolder;
            optionsForm.browseOutputFolder_folderBrowserDialog.SelectedPath = Properties.Settings.Default.OutputFolder;

            Properties.Settings.Default.Save();

            //Print current default settings to logs:
            logForm.appendTextsToLog($"\"SelectedLccn\" is set to: {Properties.Settings.Default.SeletedLccn}", logForm.LOG_TYPE_INFO);
            logForm.appendTextsToLog($"\"OutputFolder\" is set to: {Properties.Settings.Default.OutputFolder}", logForm.LOG_TYPE_INFO);
            logForm.appendTextsToLog($"\"SourceFolder\" is set to: {Properties.Settings.Default.SourceFolder}", logForm.LOG_TYPE_INFO);
            logForm.appendTextsToLog($"\"Awardee\" is set to: {Properties.Settings.Default.Awardee}", logForm.LOG_TYPE_INFO);
            logForm.appendTextsToLog($"\"AwardYear\" is set to: {Properties.Settings.Default.AwardYear}", logForm.LOG_TYPE_INFO);
            logForm.appendTextsToLog($"\"EditionOrder\" is set to: {Properties.Settings.Default.EditionOrder}", logForm.LOG_TYPE_INFO);

            //Reset MainForm UI:
            sourceFilesPathTextBox.Text = String.Empty;
            browseSourceFiles_folderBrowserDialog.SelectedPath = String.Empty;
            selectLccnComboBox.SelectedIndex = -1;
            batchNamePrefixTextBox.Text = String.Empty;
            batchNumberTextBox.Text = String.Empty;
            sourceFilesListView.Items.Clear();
            //browseSourceFilesButton.Enabled = false;
            //loadSourceFilesButton.Enabled = false;
            assembleBatchButton.Enabled = false;
            statusBarNumberOfSourceFilesLabel.Text = $"{sourceFilesListView.Items.Count} files loaded.";
            //statusBarMetadataFileLoadedLabel.Text = $"Metadata not loaded.";

            //Reset ImportMetadataForm:
            //importMetadataForm.issueMetadata.Clear();
            //importMetadataForm.mappedColumnsDict.Clear();
            //importMetadataForm.selectMetadataFileTextBox.Text = String.Empty;
            //importMetadataForm.selectMetadataFile_openFileDialog.FileName = String.Empty;
            //importMetadataForm.columnMappingDataGridView.Rows.Clear();

            //Reset variables and data structures:
            batchXmlFileFullPath = String.Empty;
            destinationFileStructure.Clear();
            batch_XML_Issue_Elements.Clear();
            issueFilesInformation.Clear();
        }

        private void viewLogsButton_Click(object sender, EventArgs e)
        {
            if (logForm.Visible)
            {
                logForm.BringToFront();
                logForm.logsTextBox.ScrollToCaret();
            }
            else
            {
                logForm.Location = new Point(this.Location.X + this.Width + 10, this.Location.Y);
                logForm.Show();
                logForm.logsTextBox.ScrollToCaret();
            }

        }

        private void optionsButton_Click(object sender, EventArgs e)
        {
            if (optionsForm.Visible)
            {
                optionsForm.BringToFront();
            }
            else
            {
                optionsForm.Location = new Point(this.Location.X + this.Width + 10, this.Location.Y);
                optionsForm.Show();
            }
        }

        private void exitButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void importMetadataButton_Click(object sender, EventArgs e)
        {
            importMetadataForm.Location = new Point(this.Location.X + 20, this.Location.Y + 20);
            importMetadataForm.Show();
        }

        private void browseOutputFolderButton_Click(object sender, EventArgs e)
        {

        }
    }
}
