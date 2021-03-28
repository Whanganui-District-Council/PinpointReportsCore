using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Graphics;
using Syncfusion.Pdf.Parsing;
using Syncfusion.Pdf.Security;
using Syncfusion.Pdf.Tables;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Odbc;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace PinpointGeospatial.PinpointReports.Controllers
{
    [Route("pinpointreports/[controller]")]
    [ApiController]


    public class ReportController : ControllerBase
    {
        private string FeatureKeys;
        private string DatabaseKey;
        private string ReferenceKey;
        private string PinPointKey;

        [HttpGet]
        public IActionResult GenerateReport()
        {
            byte[] byteArray = CreateReport(HttpContext);
            return File(byteArray, "application/pdf");
        }


        /// <summary>Create a PDF report</summary>
        private byte[] CreateReport(Microsoft.AspNetCore.Http.HttpContext httpcontext)
        {

            string currentConfig = "PinpointReport.config";
            if (httpcontext.Request.Query["report"].ToString() != null)
            {
                string report = "" + httpcontext.Request.Query["report"];
                Regex regexTest1 = new Regex(@"[0-9a-zA-Z\-\._]+");
                Match match1 = regexTest1.Match(report);
                if (match1.Success)
                {
                    currentConfig = "PinpointReport" + report + ".config";
                }
            }


            string xmlCurrentConfig = httpcontext.Request.PathBase + currentConfig;
            XmlDocument xmlConfigDoc1 = new XmlDocument();
            xmlConfigDoc1.Load(xmlCurrentConfig);

            if (Equals(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                if (xmlConfigDoc1.SelectSingleNode("//Settings/GDAL/GDAL_Home") != null)
                {
                    string gdal_home = "" + xmlConfigDoc1.SelectSingleNode("//Settings/GDAL/GDAL_Home").InnerText;
                    SetGDAL(gdal_home);
                }
                else
                {
                    SetGDAL();
                }

            }
            else
            {
                string gdal_home = @"/opt/pinpointreports";
                Gdal.SetConfigOption("GDAL_DATA", @"/opt/pinpointreports/gdal-data");
                Gdal.SetConfigOption("PROJ_LIB", @"/opt/pinpointreports/projlib");
                Gdal.PushFinderLocation(gdal_home);
            }


            // Set defaults
            FeatureKeys = "";
            DatabaseKey = "";
            ReferenceKey = "";
            PinPointKey = "'" + DateTime.Now.Ticks + "'";
            string Connection1 = "";
            string Query1 = "";
            string Value1 = "";
            int statusCode = 200;

            string connectionStringSQLLabels = "Labels";
            if (xmlConfigDoc1.SelectSingleNode("//Settings/QGIS/QPTLayout/SQLConnections/Labels") != null)
            {
                connectionStringSQLLabels = "" + xmlConfigDoc1.SelectSingleNode("//Settings/QGIS/QPTLayout/SQLConnections/Labels").InnerText;
            }

            string connectionStringSQLImages = "Images";
            if (xmlConfigDoc1.SelectSingleNode("//Settings/QGIS/QPTLayout/SQLConnections/Images") != null)
            {
                connectionStringSQLImages = "" + xmlConfigDoc1.SelectSingleNode("//Settings/QGIS/QPTLayout/SQLConnections/Images").InnerText;
            }

            //Set variables from get request
            if (httpcontext.Request.Query["featkey"].ToString() != null)
            {
                string FeatKeysRaw = "" + httpcontext.Request.Query["featkey"];
                if (Int32.TryParse(FeatKeysRaw, out int j))
                {
                    FeatureKeys = "" + FeatKeysRaw;
                }
                else
                {
                    Regex regexTest1 = new Regex(@"[0-9a-fA-F\-\._\*\ \/]+");
                    Match match1 = regexTest1.Match(FeatKeysRaw);
                    if (match1.Success)
                    {
                        FeatureKeys = "'" + FeatKeysRaw + "'";
                    }
                }
            }
            if (httpcontext.Request.Query["datakey"].ToString() != null)
            {
                string DataKeyRaw = "" + httpcontext.Request.Query["datakey"];
                if (Int32.TryParse(DataKeyRaw, out int j))
                {
                    DatabaseKey = "" + DataKeyRaw;
                }
                else
                {
                    Regex regexTest1 = new Regex(@"[0-9a-fA-F\-\._\*\ \/]+");
                    Match match1 = regexTest1.Match(DataKeyRaw);
                    if (match1.Success)
                    {
                        DatabaseKey = "'" + DataKeyRaw + "'";
                    }
                }
            }
            if (httpcontext.Request.Query["refkey"].ToString() != null)
            {
                string RefKeyRaw = "" + httpcontext.Request.Query["refkey"];
                if (Int32.TryParse(RefKeyRaw, out int j))
                {
                    ReferenceKey = "" + RefKeyRaw;
                }
                else
                {
                    Regex regexTest1 = new Regex(@"[0-9a-fA-F\-\._\*\ \/]+");
                    Match match1 = regexTest1.Match(RefKeyRaw);
                    if (match1.Success)
                    {
                        ReferenceKey = "'" + RefKeyRaw + "'";
                    }

                }
            }

            Boolean showFooter = true;
            if (httpcontext.Request.Query["footer"].ToString() != null)
            {
                showFooter = false;
            }

            // check for feature keys before creating PDF document
            if (FeatureKeys != "")
            {
                // Create PDF Document
                PdfDocument PDFDoc1 = new PdfDocument();
                PDFDoc1.PageSettings.SetMargins(0, 0, 0, 0);
                PDFDoc1.ViewerPreferences.FitWindow = true;
                PDFDoc1.ViewerPreferences.PageMode = PdfPageMode.UseThumbs;

                // Set PDF Security
                if (xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Security/OwnerPassword") != null && xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Security/UserPassword") != null)
                {
                    PdfSecurity security = PDFDoc1.Security;
                    security.KeySize = PdfEncryptionKeySize.Key256Bit;
                    security.Algorithm = PdfEncryptionAlgorithm.AES;
                    security.OwnerPassword = xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Security/OwnerPassword").InnerText;
                    security.UserPassword = xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Security/UserPassword").InnerText;
                    security.Permissions = PdfPermissionsFlags.FullQualityPrint | PdfPermissionsFlags.Print;
                }

                // Set PDF Version
                PDFDoc1.FileStructure.Version = PdfVersion.Version1_7;

                // Set PDF Compression
                string Compression = "" + xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/@compression").InnerText;
                SetPDFCompression(PDFDoc1, Compression);

                // Set PDF document properties
                string Title = "" + xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Metadata/Title").InnerText;
                Title = "" + Title.Replace("@featurekey", FeatureKeys);
                Title = "" + Title.Replace("@databasekey", DatabaseKey);
                Title = "" + Title.Replace("@referencekey", ReferenceKey);
                PDFDoc1.DocumentInformation.Title = "" + Title;
                PDFDoc1.DocumentInformation.Author = "" + xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Metadata/Author").InnerText;
                string Subject = "" + xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Metadata/Subject").InnerText;
                Subject = "" + Subject.Replace("@featurekey", FeatureKeys);
                Subject = "" + Subject.Replace("@databasekey", DatabaseKey);
                Subject = "" + Subject.Replace("@referencekey", ReferenceKey);
                PDFDoc1.DocumentInformation.Subject = "" + Subject;
                string Keywords = "" + xmlConfigDoc1.SelectSingleNode("//Settings/Output/PDF/Metadata/Keywords").InnerText;
                Keywords = "" + Keywords.Replace("@featurekey", FeatureKeys);
                Keywords = "" + Keywords.Replace("@databasekey", DatabaseKey);
                Keywords = "" + Keywords.Replace("@referencekey", ReferenceKey);
                PDFDoc1.DocumentInformation.Keywords = "" + Keywords;
                PDFDoc1.DocumentInformation.Creator = "PinPointReports"; //Application
                PDFDoc1.DocumentInformation.CreationDate = DateTime.Now;
                PDFDoc1.DocumentInformation.Producer = "PinPointReports, version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + " (64bit)";


                // Set PDF defaults
                // Default Pens
                PdfPen PenTransparent = new PdfPen(Syncfusion.Drawing.Color.Transparent);
                PdfPen PenBlack = new PdfPen(Syncfusion.Drawing.Color.Black);
                PdfPen PenBlue = new PdfPen(Syncfusion.Drawing.Color.Blue);
                PdfPen PenRed = new PdfPen(Syncfusion.Drawing.Color.Red);
                PdfPen PenWhite = new PdfPen(Syncfusion.Drawing.Color.White);
                PenBlack.Width = 0.5f;
                // Default Brushes
                PdfBrush BrushTransparent = new PdfSolidBrush(Syncfusion.Drawing.Color.Transparent);
                PdfBrush BrushBlack = new PdfSolidBrush(Syncfusion.Drawing.Color.Black);
                PdfBrush BrushWhite = new PdfSolidBrush(Syncfusion.Drawing.Color.White);
                PdfBrush BrushRed = new PdfSolidBrush(Syncfusion.Drawing.Color.Red);
                PdfBrush BrushBlue = new PdfSolidBrush(Syncfusion.Drawing.Color.Blue);
                // Default Fonts
                PdfFont Font4 = new PdfStandardFont(PdfFontFamily.Helvetica, 4);
                PdfFont Font5 = new PdfStandardFont(PdfFontFamily.Helvetica, 5);
                PdfFont Font6 = new PdfStandardFont(PdfFontFamily.Helvetica, 6);
                PdfFont Font8 = new PdfStandardFont(PdfFontFamily.Helvetica, 8);
                PdfFont Font10 = new PdfStandardFont(PdfFontFamily.Helvetica, 10);
                PdfFont Font12 = new PdfStandardFont(PdfFontFamily.Helvetica, 12);
                PdfFont Font8bold = new PdfStandardFont(PdfFontFamily.Helvetica, 8, PdfFontStyle.Bold);
                PdfFont Font10bold = new PdfStandardFont(PdfFontFamily.Helvetica, 10, PdfFontStyle.Bold);
                PdfFont Font12bold = new PdfStandardFont(PdfFontFamily.Helvetica, 12, PdfFontStyle.Bold);
                PdfFont Font14bold = new PdfStandardFont(PdfFontFamily.Helvetica, 14, PdfFontStyle.Bold);

                // User  defaults for Pen and Brush
                int userBrushAlpha = 255;
                int userBrushRed = 255;
                int userBrushGreen = 255;
                int userBrushBlue = 255;

                int userPenAlpha = 255;
                int userPenRed = 255;
                int userPenGreen = 0;
                int userPenBlue = 0;

                float userPenWidth = 2;

                float userBrushTransparency = userBrushAlpha / 255;
                float userPenTransparency = userPenAlpha / 255;

                PdfBrush userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                PdfPen userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                // page size
                PdfUnitConverter convertor1 = new PdfUnitConverter();
                PdfGraphicsUnit mm = PdfGraphicsUnit.Millimeter;
                PdfGraphicsUnit pt = PdfGraphicsUnit.Point;

                // add pages
                XPathDocument xpathdoc1 = new XPathDocument(xmlCurrentConfig);
                XPathNavigator xpathnav1 = xpathdoc1.CreateNavigator();
                string xpath1 = "//Pages/Page";
                XPathNodeIterator xpathnodespages = xpathnav1.Select(xpath1);
                while (xpathnodespages.MoveNext())
                {
                    int PageCurrentPos = xpathnodespages.CurrentPosition;

                    // check for page generation rules
                    Boolean PageGeneration = true;
                    if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/PageGeneration/@include") != null)
                    {
                        string includePage = xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/PageGeneration/@include").InnerText;

                        switch (includePage.ToLower())
                        {
                            case "always":
                                PageGeneration = true;
                                break;
                            case "true":
                                PageGeneration = true;
                                break;
                            case "sql":
                                //producePage value determined by SQL statement returning true or false
                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/PageGeneration/PageGenerationSQL/SQL") != null)
                                {
                                    string productionSQL = "" + xpathnodespages.Current.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/PageGeneration/PageGenerationSQL/SQL").Value;
                                    Value1 = "";  //reset value string
                                    Query1 = "" + productionSQL;
                                    Connection1 = "" + xpathnodespages.Current.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/PageGeneration/PageGenerationSQL/SQL").GetAttribute("connection", "");
                                    DataSet dsProducePage;
                                    dsProducePage = GetDataSetConnName(Query1, Connection1);

                                    if (dsProducePage.Tables.Count > 0)
                                    {
                                        if (dsProducePage.Tables[0].Rows.Count > 0)
                                        {
                                            for (int i1 = 0; i1 < dsProducePage.Tables[0].Rows[0].ItemArray.Length; i1++)
                                            {
                                                Value1 = Value1 + dsProducePage.Tables[0].Rows[0].ItemArray[i1];
                                            }
                                        }
                                    }
                                    if (Value1.ToLower() == "true" || Value1.ToLower() == "false")
                                    {
                                        PageGeneration = System.Convert.ToBoolean(Value1);
                                    }
                                }
                                break;
                            default:
                                PageGeneration = false;
                                break;
                        }


                        // check for specified page generation e.g. 1,2,4,7
                        // Note - this overrides check for page generation rules
                        string GenerateId = "";
                        string GeneratePageList = "";
                        if (httpcontext.Request.Query["generateid"].ToString() != null)
                        {
                            GenerateId = "" + httpcontext.Request.Query["generateid"];
                            Regex regex1 = new Regex(@"[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}");
                            Match match1 = regex1.Match(GenerateId);
                            if (match1.Success)
                            {
                                if (xmlConfigDoc1.SelectSingleNode("//Settings/PageGeneration/@id") != null)
                                {
                                    string QueryGenerateId = "" + xmlConfigDoc1.SelectSingleNode("//Settings/PageGeneration/@id").InnerText;
                                    if (GenerateId == QueryGenerateId)
                                    {
                                        if (httpcontext.Request.Query["generate"].ToString() != null)
                                        {
                                            GeneratePageList = "" + httpcontext.Request.Query["generate"];
                                            Regex regex2 = new Regex(@"^\d+(?:,\d+)*$", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(2));
                                            Match match2 = regex2.Match(GeneratePageList);
                                            if (match2.Success)
                                            {
                                                string[] GeneratePages = GeneratePageList.Split(',');
                                                if (Array.Exists(GeneratePages, element => element == PageCurrentPos.ToString()))
                                                {
                                                    PageGeneration = true;
                                                }
                                                else
                                                {
                                                    PageGeneration = false;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }



                    if (PageGeneration)
                    {
                        // Start generating Pages

                        // check page generation type
                        string PageType = "" + xpathnodespages.Current.GetAttribute("type", "");
                        switch (PageType)
                        {
                            case "Title":
                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/TitlePage") != null)
                                {
                                    string ForeignDoc = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/TitlePage").InnerText;
                                    ForeignDoc = "" + httpcontext.Request.PathBase + ForeignDoc;
                                    Stream fileStream = new FileStream(ForeignDoc, FileMode.Open, FileAccess.Read);
                                    PdfLoadedDocument SourceDoc = new PdfLoadedDocument(fileStream);
                                    string ForeignDocImportPage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/TitlePage/@importpage").InnerText;
                                    Int32 importPageNo = 1 - System.Convert.ToInt32(ForeignDocImportPage);
                                    PDFDoc1.ImportPage(SourceDoc, SourceDoc.Pages[importPageNo]);
                                }
                                break;

                            case "Foreign":
                                //Add footer
                                if (showFooter == true)
                                {
                                    if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignPages") != null || xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignSQLPages") != null)
                                    {
                                        // Apply a footer template
                                        if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate") != null)
                                        {
                                            Boolean showFooterText = true;
                                            Boolean showFooterNumber = true;
                                            if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterText/@include") != null)
                                            {
                                                if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterText/@include").InnerText.ToUpper() != "TRUE")
                                                {
                                                    showFooterText = false;
                                                }
                                            }
                                            if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterPageNumbers/@include") != null)
                                            {
                                                if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterPageNumbers/@include").InnerText.ToUpper() != "TRUE")
                                                {
                                                    showFooterNumber = false;
                                                }
                                            }

                                            string footerTextLeft = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterText/@left").InnerText;
                                            string footerPageNumberRight = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterPageNumbers/@right").InnerText;
                                            string footerBottom = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/@bottom").InnerText;
                                            string footerWidth = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/@width").InnerText;

                                            string footerTextX = "" + footerTextLeft;
                                            string footerPageNumberX = "" + (System.Convert.ToSingle(footerWidth) - System.Convert.ToSingle(footerPageNumberRight));

                                            if (footerTextX != "" && footerPageNumberX != "")
                                            {
                                                Syncfusion.Drawing.RectangleF rect = new Syncfusion.Drawing.RectangleF(0, 0, PDFDoc1.PageSettings.Width, convertor1.ConvertUnits(System.Convert.ToSingle(footerBottom), mm, pt));
                                                PdfPageTemplateElement footer = new PdfPageTemplateElement(rect);
                                                PdfPageNumberField pageNumber = new PdfPageNumberField(Font8);
                                                PdfPageCountField count = new PdfPageCountField(Font10);
                                                PdfCompositeField compositeField = new PdfCompositeField(Font8, BrushBlack, "Page {0} of {1}", pageNumber, count);
                                                compositeField.Bounds = footer.Bounds;
                                                if (showFooterNumber)
                                                {
                                                    compositeField.Draw(footer.Graphics, new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(footerPageNumberX), mm, pt), convertor1.ConvertUnits(0f, mm, pt)));
                                                }
                                                if (showFooterText)
                                                {
                                                    footer.Graphics.DrawString("" + PDFDoc1.DocumentInformation.Title + " - " + PDFDoc1.DocumentInformation.Author, Font8, BrushBlack, new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(footerTextX), mm, pt), convertor1.ConvertUnits(0f, mm, pt)));
                                                }
                                                PDFDoc1.Template.Bottom = footer;
                                            }
                                        }
                                    }
                                }
                                //Handle normal Foreign Page
                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignPages") != null)
                                {
                                    XmlNodeList XmlNodeListForeignPages = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignPages/ForeignPage");
                                    for (int iForeignPage = 0; iForeignPage < XmlNodeListForeignPages.Count; iForeignPage++)
                                    {
                                        int positionValue = iForeignPage + 1;
                                        string ForeignDoc = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignPages/ForeignPage[position()='" + positionValue + "']").InnerText;
                                        PdfLoadedDocument SourceDoc = null;

                                        //Check type of external page (local,web)
                                        if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignPages/ForeignPage[position()='" + positionValue + "']/@type") != null)
                                        {
                                            //check if web
                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignPages/ForeignPage[position()='" + positionValue + "']/@type").InnerText.ToLower() == "web")
                                            {
                                                //check if contains web protocol (http or https)
                                                if (ForeignDoc.ToLower().StartsWith("http"))
                                                {
                                                    //check status code
                                                    statusCode = GetHttpClientStatusCode(ForeignDoc);
                                                    if (statusCode >= 100 && statusCode < 400)
                                                    {
                                                        // Source document
                                                        SourceDoc = new PdfLoadedDocument(GetDownloadBytes(ForeignDoc));
                                                    }

                                                }
                                            }
                                        }
                                        else
                                        {
                                            //Treat as local
                                            ForeignDoc = "" + httpcontext.Request.PathBase + ForeignDoc;
                                            // Source document
                                            Stream fileStream = new FileStream(ForeignDoc, FileMode.Open, FileAccess.Read);
                                            SourceDoc = new PdfLoadedDocument(fileStream);
                                        }
                                        //import pages from the pdf document
                                        if (SourceDoc != null)
                                        {
                                            // Importing pages from source document.
                                            string ForeignDocImportPage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignPages/ForeignPage[position()='" + positionValue + "']/@importpage").InnerText;

                                            //Import multiple pages and page ranges
                                            if (ForeignDocImportPage.Contains("*"))
                                            {
                                                //import all pages
                                                int endIndex = SourceDoc.Pages.Count - 1;
                                                PDFDoc1.ImportPageRange(SourceDoc, 0, endIndex);
                                            }
                                            else
                                            {
                                                if (ForeignDocImportPage.Contains(","))
                                                {
                                                    //e.g. 1,2,6 or 1,2,5-6
                                                    //Split on ","
                                                    string[] splitByComma = ForeignDocImportPage.Split(new char[] { ',' });
                                                    for (int iSplitByComma = 0; iSplitByComma < splitByComma.Length; iSplitByComma++)
                                                    {
                                                        //check for "-"
                                                        if (splitByComma[iSplitByComma].Contains("-"))
                                                        {
                                                            //e.g. 5-6
                                                            //Split on "-"
                                                            string[] splitByDash = splitByComma[iSplitByComma].Split(new char[] { '-' });
                                                            Int32 importPageRangeStartNo = System.Convert.ToInt32(splitByDash[0]) - 1;
                                                            Int32 importPageRangeEndNo = System.Convert.ToInt32(splitByDash[1]) - 1;
                                                            PDFDoc1.ImportPageRange(SourceDoc, importPageRangeStartNo, importPageRangeEndNo);
                                                        }
                                                        else
                                                        {
                                                            //e.g. 1
                                                            Int32 importPageNo = System.Convert.ToInt32(splitByComma[iSplitByComma]) - 1;
                                                            PDFDoc1.ImportPage(SourceDoc, importPageNo);
                                                        }

                                                    }
                                                }
                                                else
                                                {
                                                    //e.g. 1 or 5-6
                                                    //check for "-"
                                                    if (ForeignDocImportPage.Contains("-"))
                                                    {
                                                        //e.g. 5-6
                                                        //Split on "-"
                                                        string[] splitByDash = ForeignDocImportPage.Split(new char[] { '-' });
                                                        Int32 importPageRangeStartNo = System.Convert.ToInt32(splitByDash[0]) - 1;
                                                        Int32 importPageRangeEndNo = System.Convert.ToInt32(splitByDash[1]) - 1;
                                                        PDFDoc1.ImportPageRange(SourceDoc, importPageRangeStartNo, importPageRangeEndNo);
                                                    }
                                                    else
                                                    {
                                                        //e.g. 1
                                                        Int32 importPageNo = System.Convert.ToInt32(ForeignDocImportPage) - 1;
                                                        PDFDoc1.ImportPage(SourceDoc, importPageNo);
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                //Handle SQL Foreign Page
                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignSQLPages") != null)
                                {
                                    XmlNodeList XmlNodeListForeignPages = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignSQLPages/ForeignSQLPage");
                                    for (int iForeignPage = 0; iForeignPage < XmlNodeListForeignPages.Count; iForeignPage++)
                                    {
                                        int positionValue = iForeignPage + 1;
                                        PdfLoadedDocument SourceDoc = null;

                                        //GetDataODBC(Query1,Connection1)
                                        if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignSQLPages/ForeignSQLPage[position()='" + positionValue + "']/SQL") != null)
                                        {
                                            string dataConnection = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignSQLPages/ForeignSQLPage[position()='" + positionValue + "']/SQL/@connection").InnerText;
                                            string dataSQL = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/ForeignSQLPages/ForeignSQLPage[position()='" + positionValue + "']/SQL").InnerText;

                                            Query1 = "" + dataSQL;

                                            DataSet ds2;
                                            Connection1 = dataConnection;
                                            ds2 = GetDataSetConnName(Query1, Connection1);

                                            //Need to check if any tables returned by getdata here
                                            if (ds2.Tables.Count > 0)
                                            {
                                                if (ds2.Tables[0].Rows.Count > 0)
                                                {
                                                    for (int i1 = 0; i1 < ds2.Tables[0].Rows.Count; i1++)
                                                    {
                                                        string ForeignDoc = "" + ds2.Tables[0].Rows[i1].ItemArray[0];
                                                        string ForeignDocImportPage = "" + ds2.Tables[0].Rows[i1].ItemArray[1];
                                                        string ForeignDoctype = "" + ds2.Tables[0].Rows[i1].ItemArray[2];

                                                        //Check type of external page (web)
                                                        if (ForeignDoctype != "")
                                                        {
                                                            if (ForeignDoctype.ToLower() == "web")
                                                            {
                                                                //check if contains web protocol (http or https)
                                                                if (ForeignDoc.ToLower().StartsWith("http"))
                                                                {
                                                                    statusCode = GetHttpClientStatusCode(ForeignDoc);
                                                                    if (statusCode >= 100 && statusCode < 400)
                                                                    {
                                                                        // Source document
                                                                        SourceDoc = new PdfLoadedDocument(GetDownloadBytes(ForeignDoc));
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            ForeignDoc = "" + httpcontext.Request.PathBase + ForeignDoc;
                                                            Stream fileStream = new FileStream(ForeignDoc, FileMode.Open, FileAccess.Read);
                                                            SourceDoc = new PdfLoadedDocument(fileStream);
                                                        }
                                                        //import pages from the pdf document
                                                        if (SourceDoc != null)
                                                        {
                                                            //Import multiple pages and page ranges
                                                            if (ForeignDocImportPage.Contains("*"))
                                                            {
                                                                //import all pages
                                                                int endIndex = SourceDoc.Pages.Count - 1;
                                                                PDFDoc1.ImportPageRange(SourceDoc, 0, endIndex);
                                                            }
                                                            else
                                                            {
                                                                if (ForeignDocImportPage.Contains(","))
                                                                {
                                                                    //e.g. 1,2,6 or 1,2,5-6
                                                                    //Split on ","
                                                                    string[] splitByComma = ForeignDocImportPage.Split(new char[] { ',' });
                                                                    for (int iSplitByComma = 0; iSplitByComma < splitByComma.Length; iSplitByComma++)
                                                                    {
                                                                        //check for "-"
                                                                        if (splitByComma[iSplitByComma].Contains("-"))
                                                                        {
                                                                            //e.g. 5-6
                                                                            //Split on "-"
                                                                            string[] splitByDash = splitByComma[iSplitByComma].Split(new char[] { '-' });
                                                                            Int32 importPageRangeStartNo = System.Convert.ToInt32(splitByDash[0]) - 1;
                                                                            Int32 importPageRangeEndNo = System.Convert.ToInt32(splitByDash[1]) - 1;
                                                                            PDFDoc1.ImportPageRange(SourceDoc, importPageRangeStartNo, importPageRangeEndNo);
                                                                        }
                                                                        else
                                                                        {
                                                                            //e.g. 1
                                                                            Int32 importPageNo = System.Convert.ToInt32(splitByComma[iSplitByComma]) - 1;
                                                                            PDFDoc1.ImportPage(SourceDoc, importPageNo);
                                                                        }

                                                                    }
                                                                }
                                                                else
                                                                {
                                                                    //e.g. 1 or 5-6
                                                                    //check for "-"
                                                                    if (ForeignDocImportPage.Contains("-"))
                                                                    {
                                                                        //e.g. 5-6
                                                                        //Split on "-"
                                                                        string[] splitByDash = ForeignDocImportPage.Split(new char[] { '-' });
                                                                        Int32 importPageRangeStartNo = System.Convert.ToInt32(splitByDash[0]) - 1;
                                                                        Int32 importPageRangeEndNo = System.Convert.ToInt32(splitByDash[1]) - 1;
                                                                        PDFDoc1.ImportPageRange(SourceDoc, importPageRangeStartNo, importPageRangeEndNo);
                                                                    }
                                                                    else
                                                                    {
                                                                        //e.g. 1
                                                                        Int32 importPageNo = System.Convert.ToInt32(ForeignDocImportPage) - 1;
                                                                        PDFDoc1.ImportPage(SourceDoc, importPageNo);
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }



                                                }
                                            }

                                            ds2.Dispose();
                                        }
                                    }
                                }
                                break;
                            default:
                                break;
                        }

                        if (PageType != "Title" && PageType != "Foreign")
                        {
                            string TemplateFile = "" + httpcontext.Request.PathBase + xpathnodespages.Current.GetAttribute("template", "");

                            XmlDocument QGISLayoutTemplate = new XmlDocument();
                            QGISLayoutTemplate.Load(TemplateFile);
                            if (xpathnodespages.Current.HasChildren)
                            {
                                //page size
                                //Set defaults
                                string TemplatePageWidth = "210";
                                string TemplatePageHeight = "297";
                                //get page size values from templates first LayoutItem
                                if (QGISLayoutTemplate.SelectSingleNode("//Layout/PageCollection/LayoutItem[1]/@size") != null)
                                {
                                    string TemplatePageSize = QGISLayoutTemplate.SelectSingleNode("//Layout/PageCollection/LayoutItem[1]/@size").InnerText;
                                    char[] charSeparators = new char[] { ',' };
                                    string[] splitresult;
                                    splitresult = TemplatePageSize.Split(charSeparators);
                                    TemplatePageWidth = splitresult[0];
                                    TemplatePageHeight = splitresult[1];
                                }

                                //set page orientation based on page size values
                                Syncfusion.Drawing.SizeF pageSize1 = new Syncfusion.Drawing.SizeF(System.Convert.ToInt16(convertor1.ConvertUnits(System.Convert.ToSingle(TemplatePageWidth), mm, pt)), System.Convert.ToInt16(convertor1.ConvertUnits(System.Convert.ToSingle(TemplatePageHeight), mm, pt)));
                                PDFDoc1.PageSettings.Size = pageSize1;
                                if (System.Convert.ToSingle(TemplatePageWidth) > System.Convert.ToSingle(TemplatePageHeight))
                                {
                                    PDFDoc1.PageSettings.Orientation = PdfPageOrientation.Landscape;
                                }
                                else
                                {
                                    PDFDoc1.PageSettings.Orientation = PdfPageOrientation.Portrait;
                                }

                                // Add a new page to the document.
                                PdfPage page = PDFDoc1.Pages.Add();
                                PdfGraphics g = page.Graphics;


                                // Apply a footer template for all pages this point onwards)
                                if (showFooter == true)
                                {
                                    if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate") != null)
                                    {
                                        Boolean showFooterText = true;
                                        Boolean showFooterNumber = true;
                                        if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterText/@include") != null)
                                        {
                                            if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterText/@include").InnerText.ToUpper() != "TRUE")
                                            {
                                                showFooterText = false;
                                            }
                                        }
                                        if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterPageNumbers/@include") != null)
                                        {
                                            if (xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterPageNumbers/@include").InnerText.ToUpper() != "TRUE")
                                            {
                                                showFooterNumber = false;
                                            }
                                        }

                                        string footerTextLeft = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterText/@left").InnerText;
                                        string footerPageNumberRight = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/FooterPageNumbers/@right").InnerText;
                                        string footerBottom = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/@bottom").InnerText;
                                        string footerWidth = "" + xmlConfigDoc1.SelectSingleNode("//FooterTemplate/@width").InnerText;

                                        string footerTextX = "" + footerTextLeft;
                                        string footerPageNumberX = "" + (System.Convert.ToSingle(footerWidth) - System.Convert.ToSingle(footerPageNumberRight));

                                        if (footerTextX != "" && footerPageNumberX != "")
                                        {
                                            Syncfusion.Drawing.RectangleF rect = new Syncfusion.Drawing.RectangleF(0, 0, PDFDoc1.PageSettings.Width, convertor1.ConvertUnits(System.Convert.ToSingle(footerBottom), mm, pt));
                                            PdfPageTemplateElement footer = new PdfPageTemplateElement(rect);
                                            PdfPageNumberField pageNumber = new PdfPageNumberField(Font8);
                                            PdfPageCountField count = new PdfPageCountField(Font10);
                                            PdfCompositeField compositeField = new PdfCompositeField(Font8, BrushBlack, "Page {0} of {1}", pageNumber, count);
                                            compositeField.Bounds = footer.Bounds;
                                            if (showFooterNumber)
                                            {
                                                compositeField.Draw(footer.Graphics, new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(footerPageNumberX), mm, pt), convertor1.ConvertUnits(0f, mm, pt)));
                                            }
                                            if (showFooterText)
                                            {
                                                footer.Graphics.DrawString("" + PDFDoc1.DocumentInformation.Title + " - " + PDFDoc1.DocumentInformation.Author, Font8, BrushBlack, new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(footerTextX), mm, pt), convertor1.ConvertUnits(0f, mm, pt)));
                                            }
                                            PDFDoc1.Template.Bottom = footer;
                                        }
                                    }
                                }

                                // Set default Neat Scale variable for scale calcs
                                string MapImageNeatScale = "1";

                                //Future - get global features?

                                //Future - underlays


                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppMap']") != null)
                                {
                                    XmlNodeList XmlNodeListTemplateMaps = QGISLayoutTemplate.SelectNodes("//LayoutItem[@id='ppMap']");
                                    for (int iMap = 1; iMap <= XmlNodeListTemplateMaps.Count; iMap++)
                                    {
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppMap'][" + iMap + "]") != null)
                                        {
                                            if (xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]") != null)
                                            {

                                                string MapImageScaleFactor = "" + xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]").GetAttribute("imageScale", "");

                                                //Map Image position
                                                //Set defaults
                                                string MapImageX = "0";
                                                string MapImageY = "0";
                                                //Set Map Image position from template
                                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppMap'][" + iMap + "]/@position") != null)
                                                {
                                                    string mapPosition = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppMap'][" + iMap + "]/@position").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = mapPosition.Split(charSeparators);
                                                    MapImageX = splitresult[0];
                                                    MapImageY = splitresult[1];
                                                    //MapImagePositionUnits = splitresult[2];
                                                }

                                                //Map Image Size
                                                //Set defaults
                                                string MapImageWidth = "0";
                                                string MapImageHeight = "0";
                                                //Set Map Image Size from template
                                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppMap'][" + iMap + "]/@size") != null)
                                                {
                                                    string mapSize = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppMap'][" + iMap + "]/@size").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = mapSize.Split(charSeparators);
                                                    MapImageWidth = splitresult[0];
                                                    MapImageHeight = splitresult[1];
                                                }

                                                double PageMapImageWidthMM = System.Convert.ToDouble(MapImageWidth);

                                                //Initiate variables for values retrieved by OGR
                                                double OGRFeatureDiagonalLength = 0;
                                                double OGRFeatureCentroidX = 0;
                                                double OGRFeatureCentroidY = 0;
                                                double FeatureScale = 0;

                                                string FeatureURL = "";

                                                Boolean UseScale = true;

                                                string MapWidth = "";
                                                string MapHeight = "";
                                                string MapZoom = "";
                                                string MapX = "";
                                                string MapY = "";
                                                string MapExtentMinX = "";
                                                string MapExtentMaxX = "";
                                                string MapExtentMinY = "";
                                                string MapExtentMaxY = "";



                                                if (xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]").GetAttribute("type", "") != null)
                                                {
                                                    //map feature stuff here
                                                    if (xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("type", "") != null)
                                                    {
                                                        //scale feature stuff here
                                                        string ScaleFeatureMultiplier = "";
                                                        string ScaleFeatureURIPath = "";
                                                        string ScaleFeatureSQL = "";
                                                        string ScaleFeatureSQLConnection = "";
                                                        string ScaleFeatureSQLConnectionName = "";
                                                        string ScaleFeatureSQLOGRDriver = "";
                                                        string ScaleFeatureSQLTable = "";
                                                        string ScaleFeatureSQLDialect = "";


                                                        string MapImageScaleFeatureType = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("type", "").ToLower();
                                                        switch (MapImageScaleFeatureType)
                                                        {
                                                            case "ogcwfs":
                                                                ScaleFeatureMultiplier = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("multiplier", "");
                                                                ScaleFeatureURIPath = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/URI").Value;

                                                                FeatureURL = "" + ScaleFeatureURIPath;
                                                                FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys);
                                                                FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                if (statusCode >= 100 && statusCode < 400)
                                                                {

                                                                    Ogr.RegisterAll();

                                                                    //skip SSL host / certificate verification if https url
                                                                    if (FeatureURL.StartsWith("https"))
                                                                    {
                                                                        if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                        {
                                                                            Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                        }
                                                                    }

                                                                    Layer layer1;
                                                                    DataSource dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                    layer1 = dsOgr.GetLayerByIndex(0);
                                                                    layer1.ResetReading();
                                                                    Feature feature1 = layer1.GetNextFeature();

                                                                    Geometry FeatureGeometry = feature1.GetGeometryRef();



                                                                    //look at first geometry and determine type
                                                                    if (FeatureGeometry.GetGeometryRef(0).GetGeometryName() == "POINT")
                                                                    {
                                                                        //Point Feature
                                                                        OGRFeatureDiagonalLength = System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                    }
                                                                    else
                                                                    {
                                                                        //Non Point Feature - use envelope of feature(s) to determine FeatureScale
                                                                        Envelope ext = new Envelope();
                                                                        FeatureGeometry.GetEnvelope(ext);

                                                                        //Get centroid X and Y of envelope
                                                                        OGRFeatureCentroidX = ext.MinX + ((ext.MaxX - ext.MinX) / 2);
                                                                        OGRFeatureCentroidY = ext.MinY + ((ext.MaxY - ext.MinY) / 2);

                                                                        //Check and set scale for map based on feature scale and scale values from config
                                                                        OGRFeatureDiagonalLength = Math.Sqrt(Math.Abs(Math.Pow((ext.MaxX - ext.MinX), 2) + Math.Pow((ext.MaxY - ext.MinY), 2)));
                                                                        //Add to diagonal length so that selection shows properly
                                                                        OGRFeatureDiagonalLength = OGRFeatureDiagonalLength * System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;

                                                                    }
                                                                }
                                                                break;
                                                            case "esrirest":
                                                                ScaleFeatureMultiplier = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("multiplier", "");
                                                                ScaleFeatureURIPath = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/URI").Value;

                                                                FeatureURL = "" + ScaleFeatureURIPath;
                                                                FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys.Trim('\''));
                                                                FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                if (statusCode >= 100 && statusCode < 400)
                                                                {
                                                                    Ogr.RegisterAll();

                                                                    //skip SSL host / certificate verification if https url
                                                                    if (FeatureURL.StartsWith("https"))
                                                                    {
                                                                        if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                        {
                                                                            Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                        }
                                                                    }

                                                                    Layer layer1;
                                                                    DataSource dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                    layer1 = dsOgr.GetLayerByName("OGRGeoJSON");
                                                                    layer1.ResetReading();
                                                                    Feature feature1 = layer1.GetNextFeature();

                                                                    Geometry FeatureGeometry = feature1.GetGeometryRef();



                                                                    //look at first geometry and determine type
                                                                    if (FeatureGeometry.GetGeometryRef(0).GetGeometryName() == "POINT")
                                                                    {
                                                                        //Point Feature
                                                                        OGRFeatureDiagonalLength = System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                    }
                                                                    else
                                                                    {
                                                                        //Non Point Feature - use envelope of feature(s) to determine FeatureScale
                                                                        Envelope ext = new Envelope();
                                                                        FeatureGeometry.GetEnvelope(ext);

                                                                        //Get centroid X and Y of envelope
                                                                        OGRFeatureCentroidX = ext.MinX + ((ext.MaxX - ext.MinX) / 2);
                                                                        OGRFeatureCentroidY = ext.MinY + ((ext.MaxY - ext.MinY) / 2);

                                                                        //Check and set scale for map based on feature scale and scale values from config
                                                                        OGRFeatureDiagonalLength = Math.Sqrt(Math.Abs(Math.Pow((ext.MaxX - ext.MinX), 2) + Math.Pow((ext.MaxY - ext.MinY), 2)));
                                                                        //Add to diagonal length so that selection shows properly
                                                                        OGRFeatureDiagonalLength = OGRFeatureDiagonalLength * System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;

                                                                    }
                                                                }


                                                                break;
                                                            case "sql":

                                                                ScaleFeatureMultiplier = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("multiplier", "");
                                                                ScaleFeatureSQL = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/SQL").Value;
                                                                ScaleFeatureSQLConnectionName = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/SQL").GetAttribute("connection", "");
                                                                ScaleFeatureSQLOGRDriver = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/SQL").GetAttribute("ogrDriver", "");
                                                                ScaleFeatureSQLTable = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/SQL").GetAttribute("table", "");
                                                                ScaleFeatureSQLDialect = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/SQL").GetAttribute("dialect", "");

                                                                ScaleFeatureSQLConnection = "" + Startup.PinpointConfiguration.Configuration.GetSection("ConnectionStrings")[ScaleFeatureSQLConnectionName].ToString();

                                                                string FeatureSQL = "" + ScaleFeatureSQL;
                                                                FeatureSQL = "" + FeatureSQL.Replace("@featurekey", FeatureKeys);
                                                                FeatureSQL = "" + FeatureSQL.Replace("@datbasekey", DatabaseKey);
                                                                FeatureSQL = "" + FeatureSQL.Replace("@referencekey", ReferenceKey);


                                                                if (FeatureSQL != "" && ScaleFeatureSQLOGRDriver != "" && ScaleFeatureSQLConnection != "" && ScaleFeatureSQLTable != "")
                                                                {
                                                                    string FeatureConnection = "" + ScaleFeatureSQLOGRDriver + ScaleFeatureSQLConnection + ScaleFeatureSQLTable;
                                                                    ;
                                                                    Ogr.RegisterAll();

                                                                    Layer layer1;
                                                                    DataSource dsOgr = Ogr.Open(FeatureConnection, 0);
                                                                    layer1 = dsOgr.ExecuteSQL(FeatureSQL, null, ScaleFeatureSQLDialect);
                                                                    layer1.ResetReading();
                                                                    Feature feature1 = layer1.GetNextFeature();

                                                                    Geometry FeatureGeometry = feature1.GetGeometryRef();

                                                                    //look at first geometry and determine type
                                                                    if (FeatureGeometry.GetGeometryName() == "POINT")
                                                                    {
                                                                        //Point Feature
                                                                        OGRFeatureDiagonalLength = System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                    }
                                                                    else
                                                                    {
                                                                        //Non Point Feature - use envelope of feature(s) to determine FeatureScale
                                                                        Envelope ext = new Envelope();
                                                                        FeatureGeometry.GetEnvelope(ext);

                                                                        //Get centroid X and Y of envelope
                                                                        OGRFeatureCentroidX = ext.MinX + ((ext.MaxX - ext.MinX) / 2);
                                                                        OGRFeatureCentroidY = ext.MinY + ((ext.MaxY - ext.MinY) / 2);

                                                                        //Check and set scale for map based on feature scale and scale values from config
                                                                        OGRFeatureDiagonalLength = Math.Sqrt(Math.Abs(Math.Pow((ext.MaxX - ext.MinX), 2) + Math.Pow((ext.MaxY - ext.MinY), 2)));
                                                                        //Add to diagonal length so that selection shows properly
                                                                        OGRFeatureDiagonalLength = OGRFeatureDiagonalLength * System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;

                                                                    }
                                                                    dsOgr.ReleaseResultSet(layer1);
                                                                }
                                                                break;
                                                            case "refkey":
                                                                // use the url parameter refkey to supply a value for FeatureScale
                                                                Regex regexTest1 = new Regex(@"[0-9]+");
                                                                Match match1 = regexTest1.Match(ReferenceKey);
                                                                if (match1.Success)
                                                                {
                                                                    // refkey is a number so convert to double
                                                                    FeatureScale = System.Convert.ToDouble(ReferenceKey);
                                                                }
                                                                else
                                                                {
                                                                    // default to 1000 if refkey not a number
                                                                    FeatureScale = 1;
                                                                }

                                                                //workout center xy
                                                                ScaleFeatureMultiplier = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("multiplier", "");
                                                                ScaleFeatureURIPath = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/URI").Value;

                                                                FeatureURL = "" + ScaleFeatureURIPath;
                                                                FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys);
                                                                FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                if (statusCode >= 100 && statusCode < 400)
                                                                {
                                                                    Ogr.RegisterAll();

                                                                    //skip SSL host / certificate verification if https url
                                                                    if (FeatureURL.StartsWith("https"))
                                                                    {
                                                                        if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                        {
                                                                            Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                        }
                                                                    }

                                                                    Layer layer1;
                                                                    DataSource dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                    layer1 = dsOgr.GetLayerByIndex(0);
                                                                    layer1.ResetReading();
                                                                    Feature feature1 = layer1.GetNextFeature();

                                                                    Geometry FeatureGeometry = feature1.GetGeometryRef();

                                                                    //look at first geometry and determine type
                                                                    if (FeatureGeometry.GetGeometryRef(0).GetGeometryName() == "POINT")
                                                                    {
                                                                        //Point Feature
                                                                        if (FeatureScale == 1)
                                                                        {
                                                                            OGRFeatureDiagonalLength = System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                            FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                        }
                                                                    }
                                                                    else
                                                                    {
                                                                        //Non Point Feature - use envelope of feature(s) to determine FeatureScale
                                                                        Envelope ext = new Envelope();
                                                                        FeatureGeometry.GetEnvelope(ext);

                                                                        //Get centroid X and Y of envelope
                                                                        OGRFeatureCentroidX = ext.MinX + ((ext.MaxX - ext.MinX) / 2);
                                                                        OGRFeatureCentroidY = ext.MinY + ((ext.MaxY - ext.MinY) / 2);

                                                                        if (FeatureScale == 1)
                                                                        {
                                                                            //Check and set scale for map based on feature scale and scale values from config
                                                                            OGRFeatureDiagonalLength = Math.Sqrt(Math.Abs(Math.Pow((ext.MaxX - ext.MinX), 2) + Math.Pow((ext.MaxY - ext.MinY), 2)));
                                                                            //Add to diagonal length so that selection shows properly
                                                                            OGRFeatureDiagonalLength = OGRFeatureDiagonalLength * System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                            FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                        }
                                                                    }
                                                                }


                                                                break;
                                                            case "urlparams":
                                                                if (httpcontext.Request.Query["scale"].ToString() != null && httpcontext.Request.Query["s_epsg"].ToString() != null && httpcontext.Request.Query["t_epsg"].ToString() != null && httpcontext.Request.Query["x"].ToString() != null && httpcontext.Request.Query["y"].ToString() != null)
                                                                {
                                                                    // all required params (scale,x,y,s_epsg,t_epsg) present
                                                                    string scaleRaw = "" + httpcontext.Request.Query["scale"];
                                                                    string s_epsgRaw = "" + httpcontext.Request.Query["s_epsg"];
                                                                    string t_epsgRaw = "" + httpcontext.Request.Query["t_epsg"];
                                                                    string xRaw = "" + httpcontext.Request.Query["x"];
                                                                    string yRaw = "" + httpcontext.Request.Query["y"];

                                                                    if (int.TryParse(s_epsgRaw, out int s) && int.TryParse(t_epsgRaw, out int t) && double.TryParse(xRaw, out double x) && double.TryParse(yRaw, out double y))
                                                                    {

                                                                        Regex rxScaleTest1 = new Regex(@"[0-9]+");
                                                                        Match rxScaleTestMatch1 = rxScaleTest1.Match(scaleRaw);
                                                                        if (rxScaleTestMatch1.Success)
                                                                        {
                                                                            // scale from url param
                                                                            // refkey is a number so convert to double
                                                                            FeatureScale = System.Convert.ToDouble(scaleRaw);
                                                                        }
                                                                        else
                                                                        {
                                                                            // if url param not number then calulate from feature
                                                                            ScaleFeatureMultiplier = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("multiplier", "");
                                                                            ScaleFeatureURIPath = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/URI").Value;

                                                                            FeatureURL = "" + ScaleFeatureURIPath;
                                                                            FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys);
                                                                            FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                            FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                            statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                            if (statusCode >= 100 && statusCode < 400)
                                                                            {
                                                                                Ogr.RegisterAll();

                                                                                //skip SSL host / certificate verification if https url
                                                                                if (FeatureURL.StartsWith("https"))
                                                                                {
                                                                                    if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                                    {
                                                                                        Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                                    }
                                                                                }

                                                                                Layer layer1;
                                                                                DataSource dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                                layer1 = dsOgr.GetLayerByIndex(0);
                                                                                layer1.ResetReading();
                                                                                Feature feature1 = layer1.GetNextFeature();

                                                                                Geometry FeatureGeometry = feature1.GetGeometryRef();

                                                                                //look at first geometry and determine type
                                                                                if (FeatureGeometry.GetGeometryRef(0).GetGeometryName() == "POINT")
                                                                                {
                                                                                    //Point Feature
                                                                                    OGRFeatureDiagonalLength = System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                                    FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                                }
                                                                                else
                                                                                {
                                                                                    //Non Point Feature - use envelope of feature(s) to determine FeatureScale
                                                                                    Envelope ext = new Envelope();
                                                                                    FeatureGeometry.GetEnvelope(ext);
                                                                                    //Check and set scale for map based on feature scale and scale values from config
                                                                                    OGRFeatureDiagonalLength = Math.Sqrt(Math.Abs(Math.Pow((ext.MaxX - ext.MinX), 2) + Math.Pow((ext.MaxY - ext.MinY), 2)));
                                                                                    //Add to diagonal length so that selection shows properly
                                                                                    OGRFeatureDiagonalLength = OGRFeatureDiagonalLength * System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                                    FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                                }
                                                                            }

                                                                        }




                                                                        int s_epsg = s;
                                                                        int t_epsg = t;
                                                                        SpatialReference s_srs = new SpatialReference("");
                                                                        s_srs.ImportFromEPSG(s);
                                                                        s_srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);  // Fix for TransformPoint method x and y flipped using GDAL 3.0.4 compared to GDAL 2.4
                                                                        SpatialReference t_srs = new SpatialReference("");
                                                                        t_srs.ImportFromEPSG(t);
                                                                        t_srs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER);  // Fix for TransformPoint method x and y flipped using GDAL 3.0.4 compared to GDAL 2.4
                                                                        CoordinateTransformation coordinateTransform = new CoordinateTransformation(s_srs, t_srs);
                                                                        double[] t_pt = { 0, 0 };
                                                                        coordinateTransform.TransformPoint(t_pt, x, y, 0);
                                                                        OGRFeatureCentroidX = t_pt[0];
                                                                        OGRFeatureCentroidY = t_pt[1];
                                                                        coordinateTransform.Dispose();
                                                                    }
                                                                    else
                                                                    {
                                                                        // check if only scale param (scale), assumes position is auto
                                                                        if (httpcontext.Request.Query["scale"].ToString() != null)
                                                                        {
                                                                            scaleRaw = "" + httpcontext.Request.Query["scale"];
                                                                            Regex rxScaleTest1 = new Regex(@"[0-9]+");
                                                                            Match rxScaleTestMatch1 = rxScaleTest1.Match(scaleRaw);
                                                                            if (rxScaleTestMatch1.Success)
                                                                            {
                                                                                // scale from url param
                                                                                // refkey is a number so convert to double
                                                                                FeatureScale = System.Convert.ToDouble(scaleRaw);

                                                                                // get xy from feature
                                                                                ScaleFeatureURIPath = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/URI").Value;

                                                                                FeatureURL = "" + ScaleFeatureURIPath;
                                                                                FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys);
                                                                                FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                                FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                                statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                                if (statusCode >= 100 && statusCode < 400)
                                                                                {
                                                                                    Ogr.RegisterAll();

                                                                                    //skip SSL host / certificate verification if https url
                                                                                    if (FeatureURL.StartsWith("https"))
                                                                                    {
                                                                                        if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                                        {
                                                                                            Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                                        }
                                                                                    }

                                                                                    Layer layer1;
                                                                                    DataSource dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                                    layer1 = dsOgr.GetLayerByIndex(0);
                                                                                    layer1.ResetReading();
                                                                                    Feature feature1 = layer1.GetNextFeature();

                                                                                    Geometry FeatureGeometry = feature1.GetGeometryRef();

                                                                                    //look at first geometry and determine type
                                                                                    if (FeatureGeometry.GetGeometryRef(0).GetGeometryName() == "POINT")
                                                                                    {
                                                                                        //Point Feature
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        //Non Point Feature - use envelope of feature(s) to determine FeatureScale
                                                                                        Envelope ext = new Envelope();
                                                                                        FeatureGeometry.GetEnvelope(ext);
                                                                                        //Get centroid X and Y of envelope
                                                                                        OGRFeatureCentroidX = ext.MinX + ((ext.MaxX - ext.MinX) / 2);
                                                                                        OGRFeatureCentroidY = ext.MinY + ((ext.MaxY - ext.MinY) / 2);
                                                                                    }
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                // if url param not number then calulate from feature
                                                                                ScaleFeatureMultiplier = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature").GetAttribute("multiplier", "");
                                                                                ScaleFeatureURIPath = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleFeature/URI").Value;

                                                                                FeatureURL = "" + ScaleFeatureURIPath;
                                                                                FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys);
                                                                                FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                                FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                                statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                                if (statusCode >= 100 && statusCode < 400)
                                                                                {
                                                                                    Ogr.RegisterAll();

                                                                                    //skip SSL host / certificate verification if https url
                                                                                    if (FeatureURL.StartsWith("https"))
                                                                                    {
                                                                                        if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                                        {
                                                                                            Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                                        }
                                                                                    }

                                                                                    Layer layer1;
                                                                                    DataSource dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                                    layer1 = dsOgr.GetLayerByIndex(0);
                                                                                    layer1.ResetReading();
                                                                                    Feature feature1 = layer1.GetNextFeature();

                                                                                    Geometry FeatureGeometry = feature1.GetGeometryRef();

                                                                                    //look at first geometry and determine type
                                                                                    if (FeatureGeometry.GetGeometryRef(0).GetGeometryName() == "POINT")
                                                                                    {
                                                                                        //Point Feature
                                                                                        OGRFeatureDiagonalLength = System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        //Non Point Feature - use envelope of feature(s) to determine FeatureScale
                                                                                        Envelope ext = new Envelope();
                                                                                        FeatureGeometry.GetEnvelope(ext);
                                                                                        //Get centroid X and Y of envelope
                                                                                        OGRFeatureCentroidX = ext.MinX + ((ext.MaxX - ext.MinX) / 2);
                                                                                        OGRFeatureCentroidY = ext.MinY + ((ext.MaxY - ext.MinY) / 2);
                                                                                        //Check and set scale for map based on feature scale and scale values from config
                                                                                        OGRFeatureDiagonalLength = Math.Sqrt(Math.Abs(Math.Pow((ext.MaxX - ext.MinX), 2) + Math.Pow((ext.MaxY - ext.MinY), 2)));
                                                                                        //Add to diagonal length so that selection shows properly
                                                                                        OGRFeatureDiagonalLength = OGRFeatureDiagonalLength * System.Convert.ToDouble(ScaleFeatureMultiplier);
                                                                                        FeatureScale = (OGRFeatureDiagonalLength * 1000) / PageMapImageWidthMM;
                                                                                    }
                                                                                }

                                                                            }

                                                                        }

                                                                    }
                                                                }
                                                                break;
                                                            default:
                                                                //unknown type so error redirection here???
                                                                break;
                                                        }

                                                        //scale stuff here
                                                        //  iterate through scale values for this map from config file to find map Neat Scale
                                                        if (xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleRanges") != null)
                                                        {
                                                            //  if more than one need to iterate through each one
                                                            XmlNodeList XmlNodeListScales = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/MapImage[position()=" + iMap + "]/ScaleRanges/ScaleRange");
                                                            for (int iScale = 0; iScale < XmlNodeListScales.Count; iScale++)
                                                            {
                                                                string MinScale = "" + XmlNodeListScales.Item(iScale).SelectSingleNode("@min").InnerText;
                                                                string MaxScale = "" + XmlNodeListScales.Item(iScale).SelectSingleNode("@max").InnerText;
                                                                if ((FeatureScale > System.Convert.ToDouble(MinScale)) && (FeatureScale <= System.Convert.ToDouble(MaxScale)))
                                                                {
                                                                    MapImageNeatScale = "" + XmlNodeListScales.Item(iScale).InnerText;
                                                                }
                                                            }
                                                        }


                                                        //map stuff
                                                        MapWidth = "" + System.Convert.ToInt16(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt) * System.Convert.ToSingle(MapImageScaleFactor));
                                                        MapHeight = "" + System.Convert.ToInt16(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt) * System.Convert.ToSingle(MapImageScaleFactor));
                                                        MapZoom = "" + (PageMapImageWidthMM * System.Convert.ToDouble(MapImageNeatScale)) / 1000;
                                                        MapX = "" + OGRFeatureCentroidX;
                                                        MapY = "" + OGRFeatureCentroidY;
                                                        MapExtentMinX = System.Convert.ToString(Math.Round(OGRFeatureCentroidX - ((System.Convert.ToDouble(MapImageNeatScale) * (System.Convert.ToSingle(MapImageWidth) / 1000)) / 2), 5));
                                                        MapExtentMaxX = System.Convert.ToString(Math.Round(OGRFeatureCentroidX + ((System.Convert.ToDouble(MapImageNeatScale) * (System.Convert.ToSingle(MapImageWidth) / 1000)) / 2), 5));
                                                        MapExtentMinY = System.Convert.ToString(Math.Round(OGRFeatureCentroidY - ((System.Convert.ToDouble(MapImageNeatScale) * (System.Convert.ToSingle(MapImageHeight) / 1000)) / 2), 5));
                                                        MapExtentMaxY = System.Convert.ToString(Math.Round(OGRFeatureCentroidY + ((System.Convert.ToDouble(MapImageNeatScale) * (System.Convert.ToSingle(MapImageHeight) / 1000)) / 2), 5));


                                                        string MapImageURL = "";


                                                        //check types OGCWMS/ESRIREST/Intramaps
                                                        string MapImageType = xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]").GetAttribute("type", "").ToLower();
                                                        switch (MapImageType)
                                                        {
                                                            case "wmts":
                                                                // future
                                                                /*
                                                                https://www.gdal.org/frmt_wmts.html
                                                                https://gis.stackexchange.com/questions/268125/transforming-coordinates-to-wmts-tiles
                                                                https://gis.stackexchange.com/questions/133205/wmts-convert-geolocation-lat-long-to-tile-index-at-a-given-zoom-level
                                                                https://msdn.microsoft.com/en-gb/library/bb259689.aspx
                                                                https://wiki.openstreetmap.org/wiki/Slippy_map_tilenames#Lon..2Flat._to_tile_numbers_2
                                                                */
                                                                break;


                                                            case "ogcwms":
                                                                if (UseScale)
                                                                {
                                                                    // use ogc wms service for map image
                                                                    // example: https://data.linz.govt.nz/services;key=c3168b8a33794a18832154de6c053d4e/wms?service=WMS&version=1.1.1&request=GetMap&layers=layer-767&format=image/png&width=364&height=400&bbox=166.315719,-47.534529,178.610868,-34.030252
                                                                    // <Source type="ogcwms" url="https://data.linz.govt.nz/services;key=c3168b8a33794a18832154de6c053d4e/wms?service=WMS&amp;version=1.1.1&amp;request=GetMap&amp;layers=layer-767&amp;format=image/png&amp;srs=EPSG:2193" />
                                                                    string ogcwmsURL = "" + xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/URI").Value;

                                                                    string ogcwmsBBOX = "&bbox=" + MapExtentMinX + "," + MapExtentMinY + "," + MapExtentMaxX + "," + MapExtentMaxY;
                                                                    string ogcwmsWidth = "&width=" + MapWidth;
                                                                    string ogcwmsHeight = "&height=" + MapHeight;

                                                                    MapImageURL = "" + ogcwmsURL + ogcwmsWidth + ogcwmsHeight + ogcwmsBBOX;
                                                                    MapImageURL = "" + MapImageURL.Replace("@featurekey", FeatureKeys);
                                                                    MapImageURL = "" + MapImageURL.Replace("@datbasekey", DatabaseKey);
                                                                    MapImageURL = "" + MapImageURL.Replace("@referencekey", ReferenceKey);
                                                                }
                                                                break;
                                                            case "esrirest":

                                                                if (UseScale)
                                                                {
                                                                    // use ESRI REST ImageServer service for map image (Export Image URL)
                                                                    /*
                                                                        https://hbmaps.hbrc.govt.nz/arcgis/rest/services/Imagery/HawkesBay_Imagery_20042012/ImageServer/exportImage?
                                                                        bbox = 1861600.0 % 2C5517600.0 % 2C2034400.0 % 2C5722800.0 &
                                                                        bboxSR = &
                                                                        size = &
                                                                        imageSR = &
                                                                        time = &
                                                                        format = jpgpng &
                                                                        pixelType = U8 &
                                                                        noData = &
                                                                        noDataInterpretation = esriNoDataMatchAny &
                                                                        interpolation = +RSP_BilinearInterpolation &
                                                                        compression = &
                                                                        compressionQuality = &
                                                                        bandIds = &
                                                                        mosaicRule = &
                                                                        renderingRule = &
                                                                        f = image
                                                                    */

                                                                    string esrirestURL = "" + xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/URI").Value;
                                                                    // BBOX extents... BBOX=minx,miny,maxx,maxy: Bounding box corners (lower left, upper right) in SRS units.

                                                                    // Remove unecessary parameters or parameters that will be calculated
                                                                    //  Regexpr...
                                                                    string pattern = "";
                                                                    string replacement = @"";
                                                                    pattern = @"bbox=[0-9\.]+%2C[0-9\.]+%2C[0-9\.]+%2C[0-9\.]+&";
                                                                    esrirestURL = Regex.Replace(esrirestURL, pattern, replacement, RegexOptions.IgnoreCase);

                                                                    pattern = @"size=[0-9]+%2C[0-9]+&";
                                                                    esrirestURL = Regex.Replace(esrirestURL, pattern, replacement, RegexOptions.IgnoreCase);


                                                                    string esrirestBBOX = "&bbox=" + MapExtentMinX + "," + MapExtentMinY + "," + MapExtentMaxX + "," + MapExtentMaxY;
                                                                    // WIDTH=output_width: Width in pixels of map picture.
                                                                    string esrirestWidth = "" + MapWidth;
                                                                    // HEIGHT=output_height: Height in pixels of map picture.
                                                                    string esrirestHeight = "" + MapHeight;
                                                                    string esrirestSize = "&size=" + esrirestWidth + "," + esrirestHeight;
                                                                    MapImageURL = "" + esrirestURL + esrirestBBOX + esrirestSize;
                                                                    MapImageURL = "" + MapImageURL.Replace("@featurekey", FeatureKeys);
                                                                    MapImageURL = "" + MapImageURL.Replace("@datbasekey", DatabaseKey);
                                                                    MapImageURL = "" + MapImageURL.Replace("@referencekey", ReferenceKey);
                                                                }
                                                                break;
                                                            case "esrirestmapserver":
                                                                if (UseScale)
                                                                {
                                                                    // use ESRI REST MapServer service for map image (Export Map URL)
                                                                    /*
                                                                        https://www.topofthesouthmaps.co.nz/arcgis/rest/services/CacheAerial/MapServer/export?
                                                                        bbox=1241007.2176134433%2C5322021.548482585%2C2108073.3666623267%2C5665271.288126023&
                                                                        bboxSR=2193&
                                                                        layers=&
                                                                        layerDefs=&
                                                                        size=500%2C500&
                                                                        imageSR=&
                                                                        format=png&
                                                                        transparent=false&
                                                                        dpi=&
                                                                        time=&
                                                                        layerTimeOptions=&
                                                                        dynamicLayers=&
                                                                        gdbVersion=&
                                                                        mapScale=100000&
                                                                        rotation=&
                                                                        datumTransformations=&
                                                                        layerParameterValues=&
                                                                        mapRangeValues=&
                                                                        layerRangeValues=&
                                                                        f=image
                                                                    */
                                                                    string esrirestURL = "" + xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/URI").Value;
                                                                    // BBOX extents... BBOX=minx,miny,maxx,maxy: Bounding box corners (lower left, upper right) in SRS units.

                                                                    // Remove unecessary parameters or parameters that will be calculated
                                                                    //  Regexpr...
                                                                    string pattern = "";
                                                                    string replacement = @"";
                                                                    pattern = @"bbox=[0-9\.]+%2C[0-9\.]+%2C[0-9\.]+%2C[0-9\.]+&";
                                                                    esrirestURL = Regex.Replace(esrirestURL, pattern, replacement, RegexOptions.IgnoreCase);

                                                                    pattern = @"size=[0-9]+%2C[0-9]+&";
                                                                    esrirestURL = Regex.Replace(esrirestURL, pattern, replacement, RegexOptions.IgnoreCase);

                                                                    pattern = @"mapScale=[0-9]+&";
                                                                    esrirestURL = Regex.Replace(esrirestURL, pattern, replacement, RegexOptions.IgnoreCase);

                                                                    pattern = @"dpi=([0-9]+)&";
                                                                    esrirestURL = Regex.Replace(esrirestURL, pattern, @"dpi=$1&", RegexOptions.IgnoreCase);

                                                                    Regex regexTest1 = new Regex(@"dpi=&");
                                                                    Match match1 = regexTest1.Match(esrirestURL);
                                                                    if (match1.Success)
                                                                    {
                                                                        string newdpi = "" + System.Convert.ToString(System.Convert.ToSingle(MapImageScaleFactor) * 72);

                                                                        pattern = @"dpi=&";
                                                                        esrirestURL = Regex.Replace(esrirestURL, pattern, "dpi=" + newdpi + "&", RegexOptions.IgnoreCase);
                                                                    }
                                                                    string esrirestScale = "&mapScale=" + System.Convert.ToDouble(MapImageNeatScale);

                                                                    string esrirestBBOX = "&bbox=" + MapExtentMinX + "%2C" + MapExtentMinY + "%2C" + MapExtentMaxX + "%2C" + MapExtentMaxY;
                                                                    // WIDTH=output_width: Width in pixels of map picture.
                                                                    string esrirestWidth = "" + MapWidth;
                                                                    // HEIGHT=output_height: Height in pixels of map picture.
                                                                    string esrirestHeight = "" + MapHeight;
                                                                    string esrirestSize = "&size=" + esrirestWidth + "%2C" + esrirestHeight;

                                                                    MapImageURL = "" + esrirestURL + esrirestBBOX + esrirestSize + esrirestScale;

                                                                    MapImageURL = "" + MapImageURL.Replace("@featurekey", FeatureKeys);
                                                                    MapImageURL = "" + MapImageURL.Replace("@datbasekey", DatabaseKey);
                                                                    MapImageURL = "" + MapImageURL.Replace("@referencekey", ReferenceKey);
                                                                }
                                                                break;
                                                            case "intramaps":
                                                                if (UseScale)
                                                                {
                                                                    // use INtraMaps GetMap service for map image
                                                                    //https://mapping.hdc.govt.nz/IntraMaps80/SpatialEngineWSEmbeddedMaps/getmap.ashx?Project=PropertyMaps&Module=Property&layer=Property%20Data&width=539&height=624&includeData=false&scale=1000&mapkeys=100938

                                                                    string intramapsURL = "" + xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/URI").Value;
                                                                    // BBOX extents... BBOX=minx,miny,maxx,maxy: Bounding box corners (lower left, upper right) in SRS units.

                                                                    // WIDTH=output_width: Width in pixels of map picture.
                                                                    string intramapsWidth = "&width=" + MapWidth;
                                                                    // HEIGHT=output_height: Height in pixels of map picture.
                                                                    string intramapsHeight = "&height=" + MapHeight;
                                                                    // Zoom
                                                                    string intramapsZoom = "&zoom=" + (System.Convert.ToDouble(MapImageWidth) * System.Convert.ToDouble(MapImageNeatScale)) / 1000;
                                                                    //x
                                                                    string intramapsMapX = "&x=" + MapX;
                                                                    //y
                                                                    string intramapsMapY = "&y=" + MapY;

                                                                    MapImageURL = "" + intramapsURL + intramapsWidth + intramapsHeight + intramapsZoom + intramapsMapX + intramapsMapY;
                                                                    MapImageURL = "" + MapImageURL.Replace("@featurekey", FeatureKeys);
                                                                    MapImageURL = "" + MapImageURL.Replace("@datbasekey", DatabaseKey);
                                                                    MapImageURL = "" + MapImageURL.Replace("@referencekey", ReferenceKey);
                                                                }

                                                                break;
                                                            default:
                                                                //insert unknown map type image
                                                                break;
                                                        }
                                                        //get map image here
                                                        //MapImageURL

                                                        if (MapImageURL != "")
                                                        {
                                                            WebRequest request = WebRequest.Create(MapImageURL);
                                                            byte[] rBytes;
                                                            //Get the content
                                                            using (WebResponse result = request.GetResponse())
                                                            {
                                                                Stream rStream = result.GetResponseStream();
                                                                //Bytes from address
                                                                using (BinaryReader br = new BinaryReader(rStream))
                                                                {
                                                                    rBytes = ReadAllBytes(br);
                                                                    br.Close();
                                                                }
                                                                //  > close down the web response object
                                                                result.Close();
                                                            }

                                                            using (MemoryStream imageStream = new MemoryStream(rBytes))
                                                            {
                                                                imageStream.Seek(0, SeekOrigin.Begin); // Sets to beginning of stream
                                                                using (MemoryStream imageStream2 = new MemoryStream())
                                                                {
                                                                    imageStream.CopyTo(imageStream2);
                                                                    imageStream2.Seek(0, SeekOrigin.Begin); // Sets to beginning of stream

                                                                    System.Drawing.Image imageBitmap = System.Drawing.Image.FromStream(imageStream2, false, false);
                                                                    imageBitmap.Save(imageStream2, System.Drawing.Imaging.ImageFormat.Jpeg);
                                                                    PdfImage PdfImage1 = new PdfBitmap(imageStream2);
                                                                    g.DrawImage(PdfImage1, convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt), System.Convert.ToSingle(PdfImage1.Width) / System.Convert.ToSingle(MapImageScaleFactor), System.Convert.ToSingle(PdfImage1.Height) / System.Convert.ToSingle(MapImageScaleFactor));
                                                                }
                                                            }


                                                        }


                                                    }

                                                    //mapfeatures
                                                    if (xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/MapFeatures") != null)
                                                    {
                                                        //  iterate through each map feature
                                                        XmlNodeList XmlNodeListMapFeatures = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/MapImage[position()=" + iMap + "]/MapFeatures/MapFeature");
                                                        for (int iMapFeature = 0; iMapFeature < XmlNodeListMapFeatures.Count; iMapFeature++)
                                                        {
                                                            string MapImageMapFeatureType = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("@type").InnerText.ToLower();

                                                            // Initialise ogr datasource
                                                            DataSource dsOgr = null;
                                                            // Initialise ogr layer
                                                            Layer layer1 = null;
                                                            string MapFeatureURIPath = "";


                                                            switch (MapImageMapFeatureType)
                                                            {
                                                                case "ogcwfs":
                                                                    MapFeatureURIPath = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("URI").InnerText;
                                                                    FeatureURL = "" + MapFeatureURIPath;
                                                                    FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys);
                                                                    FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                    FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                    statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                    if (statusCode >= 100 && statusCode < 400)
                                                                    {
                                                                        Ogr.RegisterAll();

                                                                        //skip SSL host / certificate verification if https url
                                                                        if (FeatureURL.StartsWith("https"))
                                                                        {
                                                                            if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                            {
                                                                                Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                            }
                                                                        }

                                                                        //Layer layer1;
                                                                        dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                        layer1 = dsOgr.GetLayerByIndex(0);
                                                                    }
                                                                    break;
                                                                case "esrirest":
                                                                    MapFeatureURIPath = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("URI").InnerText;
                                                                    FeatureURL = "" + MapFeatureURIPath;
                                                                    FeatureURL = "" + FeatureURL.Replace("@featurekey", FeatureKeys.Trim('\''));
                                                                    FeatureURL = "" + FeatureURL.Replace("@datbasekey", DatabaseKey);
                                                                    FeatureURL = "" + FeatureURL.Replace("@referencekey", ReferenceKey);

                                                                    statusCode = GetHttpClientStatusCode(FeatureURL);

                                                                    if (statusCode >= 100 && statusCode < 400)
                                                                    {
                                                                        Ogr.RegisterAll();

                                                                        //skip SSL host / certificate verification if https url
                                                                        if (FeatureURL.StartsWith("https"))
                                                                        {
                                                                            if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                                            {
                                                                                Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                                            }
                                                                        }

                                                                        dsOgr = Ogr.Open("" + FeatureURL, 0);
                                                                        layer1 = dsOgr.GetLayerByName("OGRGeoJSON");
                                                                    }

                                                                    break;
                                                                case "intramaps":
                                                                    // cant do this as need geometry returned!!!!!!!!!
                                                                    break;
                                                                case "sql":
                                                                    string MapFeatureSQL = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("SQL").InnerText;
                                                                    string MapFeatureSQLConnectionName = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("SQL/@connection").InnerText;
                                                                    string MapFeatureSQLOGRDriver = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("SQL/@ogrDriver").InnerText;
                                                                    string MapFeatureSQLTable = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("SQL/@table").InnerText;
                                                                    string MapFeatureSQLDialect = XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("SQL/@dialect").InnerText;
                                                                    string MapFeatureSQLConnection = "" + Startup.PinpointConfiguration.Configuration.GetSection("ConnectionStrings")[MapFeatureSQLConnectionName].ToString();

                                                                    string FeatureSQL = "" + MapFeatureSQL;
                                                                    FeatureSQL = "" + FeatureSQL.Replace("@featurekey", FeatureKeys);
                                                                    FeatureSQL = "" + FeatureSQL.Replace("@datbasekey", DatabaseKey);
                                                                    FeatureSQL = "" + FeatureSQL.Replace("@referencekey", ReferenceKey);

                                                                    if (FeatureSQL != "" && MapFeatureSQLOGRDriver != "" && MapFeatureSQLConnection != "" && MapFeatureSQLTable != "")
                                                                    {
                                                                        string FeatureConnection = "" + MapFeatureSQLOGRDriver + MapFeatureSQLConnection + MapFeatureSQLTable;
                                                                        Ogr.RegisterAll();

                                                                        //DataSource dsOgr;
                                                                        dsOgr = Ogr.Open(FeatureConnection, 0);

                                                                        if (MapFeatureSQLDialect != "SQLITE")
                                                                        {
                                                                            layer1 = dsOgr.ExecuteSQL(FeatureSQL, null, "");
                                                                        }
                                                                        else
                                                                        {
                                                                            layer1 = dsOgr.ExecuteSQL(FeatureSQL, null, MapFeatureSQLDialect);
                                                                        }


                                                                    }
                                                                    break;
                                                                default:
                                                                    //unknown type so error redirection here???
                                                                    break;
                                                            }

                                                            if (layer1 != null)
                                                            {

                                                                layer1.ResetReading();
                                                                Feature feature1 = null;
                                                                Geometry FeatureGeometry = null;

                                                                float testStringY = 240;
                                                                int featureIndex = 0;

                                                                for (featureIndex = 0; featureIndex < layer1.GetFeatureCount(1); featureIndex++)
                                                                {
                                                                    layer1.SetNextByIndex(featureIndex);
                                                                    feature1 = layer1.GetNextFeature();

                                                                    FeatureGeometry = feature1.GetGeometryRef();

                                                                    testStringY = testStringY + 5;
                                                                    string geocount = "" + FeatureGeometry.GetGeometryCount();

                                                                    double MapFeatureX;
                                                                    double MapFeatureY;
                                                                    string PageFeatureX;
                                                                    string PageFeatureY;

                                                                    float templatePageFeatureX;
                                                                    float templatePageFeatureY;

                                                                    float pointDrawWidth = 0;
                                                                    float pointDrawHeight = 0;
                                                                    float pointDrawOffsetX = 0;
                                                                    float pointDrawOffsetY = 0;
                                                                    float templateFeatureX = 0;
                                                                    float templateFeatureY = 0;


                                                                    PdfTemplate template = new PdfTemplate(1f, 1f);
                                                                    Syncfusion.Drawing.PointF templateStartPoint = new Syncfusion.Drawing.PointF();


                                                                    switch (FeatureGeometry.GetGeometryName())
                                                                    {
                                                                        case "POINT":
                                                                            MapFeatureX = FeatureGeometry.GetX(0);
                                                                            MapFeatureY = FeatureGeometry.GetY(0);
                                                                            PageFeatureX = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageX) + (((MapFeatureX - System.Convert.ToDouble(MapExtentMinX)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));
                                                                            PageFeatureY = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageY) - (((MapFeatureY - System.Convert.ToDouble(MapExtentMaxY)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));

                                                                            //user styles
                                                                            if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush") != null)
                                                                            {
                                                                                userBrushAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                userBrushRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@red").InnerText);
                                                                                userBrushGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@green").InnerText);
                                                                                userBrushBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@blue").InnerText);
                                                                            }
                                                                            if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen") != null)
                                                                            {
                                                                                userPenAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                userPenRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@red").InnerText);
                                                                                userPenGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@green").InnerText);
                                                                                userPenBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@blue").InnerText);
                                                                                userPenWidth = System.Convert.ToSingle(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@width").InnerText);
                                                                            }

                                                                            userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                            userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                            // create template to constrain drawn feature to
                                                                            template = new PdfTemplate(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt));

                                                                            //reposition x and y for template
                                                                            templatePageFeatureX = System.Convert.ToSingle(PageFeatureX) - System.Convert.ToSingle(MapImageX);
                                                                            templatePageFeatureY = System.Convert.ToSingle(PageFeatureY) - System.Convert.ToSingle(MapImageY);

                                                                            pointDrawWidth = 0;
                                                                            pointDrawHeight = 0;
                                                                            pointDrawOffsetX = 0;
                                                                            pointDrawOffsetY = 0;
                                                                            templateFeatureX = 0;
                                                                            templateFeatureY = 0;


                                                                            template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);

                                                                            if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Draw") != null)
                                                                            {
                                                                                XmlNodeList XmlNodeListPointDraw = XmlNodeListMapFeatures.Item(iMapFeature).SelectNodes("Draw");
                                                                                for (int iPointDraw = 0; iPointDraw < XmlNodeListPointDraw.Count; iPointDraw++)
                                                                                {
                                                                                    string pointDrawType = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@type").InnerText.ToLower();
                                                                                    switch (pointDrawType)
                                                                                    {
                                                                                        case "ellipse":
                                                                                            //<Draw type="Ellipse" width="5" height="5"/>

                                                                                            if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush") != null)
                                                                                            {
                                                                                                userBrushAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                                userBrushRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@red").InnerText);
                                                                                                userBrushGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@green").InnerText);
                                                                                                userBrushBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@blue").InnerText);
                                                                                            }
                                                                                            if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen") != null)
                                                                                            {
                                                                                                userPenAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                                userPenRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@red").InnerText);
                                                                                                userPenGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@green").InnerText);
                                                                                                userPenBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@blue").InnerText);
                                                                                                userPenWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@width").InnerText);
                                                                                            }
                                                                                            userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                            userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                                            pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                            pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                            pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                            pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                            templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                            templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                            template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                            template.Graphics.DrawEllipse(userPen, userBrush, convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));

                                                                                            break;
                                                                                        case "image":
                                                                                            //<Draw type="Image" image="images/image1.png" width="10" height="10" alpha="200"/>
                                                                                            int pointDrawImageAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@alpha").InnerText);

                                                                                            template.Graphics.SetTransparency((float)pointDrawImageAlpha / 255, (float)pointDrawImageAlpha / 255);

                                                                                            pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                            pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                            pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                            pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                            templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                            templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                            string pointDrawImage = "" + XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@image").InnerText;
                                                                                            Stream fileStream = new FileStream(pointDrawImage, FileMode.Open, FileAccess.Read);
                                                                                            PdfImage pointDrawImage1 = new PdfBitmap(fileStream);
                                                                                            template.Graphics.DrawImage(pointDrawImage1, convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));

                                                                                            break;
                                                                                        case "rectangle":
                                                                                            //< Draw type = "Rectangle" width = "5" height = "5" />

                                                                                            if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush") != null)
                                                                                            {
                                                                                                userBrushAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                                userBrushRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@red").InnerText);
                                                                                                userBrushGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@green").InnerText);
                                                                                                userBrushBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@blue").InnerText);
                                                                                            }
                                                                                            if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen") != null)
                                                                                            {
                                                                                                userPenAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                                userPenRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@red").InnerText);
                                                                                                userPenGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@green").InnerText);
                                                                                                userPenBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@blue").InnerText);
                                                                                                userPenWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@width").InnerText);
                                                                                            }
                                                                                            userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                            userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                                            pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                            pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                            pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                            pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                            templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                            templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                            template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                            template.Graphics.DrawRectangle(userPen, userBrush, convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));

                                                                                            break;
                                                                                        case "polygon":
                                                                                            //<Draw type="Polygon" points="(10,100),(10,200)"/>

                                                                                            break;
                                                                                        case "string":
                                                                                            //<Draw type="String" text="simon" width="40" height="10" fontFamily="Arial" fontSize="12" fontStyle="Regular" red="0" green="0" blue="255" alignment="Center"/>
                                                                                            pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                            pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                            pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                            pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                            templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                            templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                            string pointDrawText = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@text").InnerText;
                                                                                            pointDrawText = "" + pointDrawText.Replace("@featurekey", FeatureKeys);
                                                                                            pointDrawText = "" + pointDrawText.Replace("@datbasekey", DatabaseKey);
                                                                                            pointDrawText = "" + pointDrawText.Replace("@referencekey", ReferenceKey);
                                                                                            for (int iField = 0; iField < feature1.GetFieldCount(); iField++)
                                                                                            {
                                                                                                pointDrawText = "" + pointDrawText.Replace("@" + iField, feature1.GetFieldAsString(iField));
                                                                                            }



                                                                                            string pointDrawFontFamily = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@fontFamily").InnerText;
                                                                                            float pointDrawFontSize = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@fontSize").InnerText);
                                                                                            string pointDrawFontStyle = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@fontStyle").InnerText;
                                                                                            int pointDrawRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@red").InnerText);
                                                                                            int pointDrawGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@green").InnerText);
                                                                                            int pointDrawBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@blue").InnerText);
                                                                                            string pointDrawAlignment = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@alignment").InnerText;

                                                                                            Syncfusion.Drawing.FontStyle fontStyle = Syncfusion.Drawing.FontStyle.Regular;
                                                                                            switch (pointDrawFontStyle.ToLower())
                                                                                            {
                                                                                                case "bold":
                                                                                                    fontStyle = Syncfusion.Drawing.FontStyle.Bold;
                                                                                                    break;
                                                                                                case "italic":
                                                                                                    fontStyle = Syncfusion.Drawing.FontStyle.Italic;
                                                                                                    break;
                                                                                                case "strikeout":
                                                                                                    fontStyle = Syncfusion.Drawing.FontStyle.Strikeout;
                                                                                                    break;
                                                                                                case "underline":
                                                                                                    fontStyle = Syncfusion.Drawing.FontStyle.Underline;
                                                                                                    break;
                                                                                                default:
                                                                                                    //regular
                                                                                                    break;
                                                                                            }

                                                                                            //default font helvetica
                                                                                            PdfFont pointDrawFont = new PdfStandardFont(PdfFontFamily.Helvetica, pointDrawFontSize);

                                                                                            PdfBrush pointDrawFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(pointDrawRed, pointDrawGreen, pointDrawBlue));

                                                                                            template.Graphics.SetTransparency(1f, 1f);


                                                                                            Syncfusion.Drawing.RectangleF pointDrawRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));
                                                                                            PdfStringFormat pointDrawFormat = new PdfStringFormat();
                                                                                            pointDrawFormat.WordWrap = PdfWordWrapType.Word;
                                                                                            PdfTextAlignment fontAlignment = PdfTextAlignment.Center;
                                                                                            switch (pointDrawAlignment.ToLower())
                                                                                            {
                                                                                                case "justify":
                                                                                                    fontAlignment = PdfTextAlignment.Justify;
                                                                                                    break;
                                                                                                case "left":
                                                                                                    fontAlignment = PdfTextAlignment.Left;
                                                                                                    break;
                                                                                                case "right":
                                                                                                    fontAlignment = PdfTextAlignment.Right;
                                                                                                    break;
                                                                                                default:
                                                                                                    // center
                                                                                                    break;
                                                                                            }
                                                                                            pointDrawFormat.Alignment = fontAlignment;

                                                                                            template.Graphics.DrawString(pointDrawText, pointDrawFont, pointDrawFontColour, pointDrawRectangle, pointDrawFormat);


                                                                                            break;
                                                                                        default:
                                                                                            //default to 2x2 ellipse

                                                                                            break;
                                                                                    }
                                                                                }
                                                                            }


                                                                            template.Graphics.SetTransparency(1f, 1f);

                                                                            templateStartPoint = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt));
                                                                            g.DrawPdfTemplate(template, templateStartPoint);



                                                                            break;
                                                                        case "LINESTRING":
                                                                            // Initialise the point array size
                                                                            Syncfusion.Drawing.PointF[] linepoints = new Syncfusion.Drawing.PointF[FeatureGeometry.GetPointCount()];
                                                                            // Initialise path
                                                                            PdfPath path1 = new PdfPath();
                                                                            // Initialise previous point
                                                                            Syncfusion.Drawing.PointF pp = new Syncfusion.Drawing.PointF();

                                                                            for (var pointIndex = 0; pointIndex < FeatureGeometry.GetPointCount(); pointIndex++)
                                                                            {
                                                                                double[] coords = new double[3];
                                                                                FeatureGeometry.GetPoint(pointIndex, coords);

                                                                                MapFeatureX = coords[0];
                                                                                MapFeatureY = coords[1];

                                                                                PageFeatureX = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageX) + (((MapFeatureX - System.Convert.ToDouble(MapExtentMinX)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));
                                                                                PageFeatureY = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageY) - (((MapFeatureY - System.Convert.ToDouble(MapExtentMaxY)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));

                                                                                Syncfusion.Drawing.PointF p = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureX) - System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureY) - System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                linepoints.SetValue(p, pointIndex);

                                                                                if (pointIndex != 0)
                                                                                {
                                                                                    path1.AddLine(pp, p);
                                                                                    pp = p;
                                                                                }
                                                                                else
                                                                                {
                                                                                    pp = p;
                                                                                }
                                                                            }

                                                                            if (linepoints.Length > 0)
                                                                            {
                                                                                //user styles
                                                                                if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush") != null)
                                                                                {
                                                                                    userBrushAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                    userBrushRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@red").InnerText);
                                                                                    userBrushGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@green").InnerText);
                                                                                    userBrushBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@blue").InnerText);
                                                                                }
                                                                                if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen") != null)
                                                                                {
                                                                                    userPenAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                    userPenRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@red").InnerText);
                                                                                    userPenGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@green").InnerText);
                                                                                    userPenBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@blue").InnerText);
                                                                                    userPenWidth = System.Convert.ToSingle(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@width").InnerText);
                                                                                }

                                                                                userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                                // create template to constrain drawn feature to
                                                                                template = new PdfTemplate(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt));

                                                                                template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                template.Graphics.DrawPath(userPen, userBrush, path1);
                                                                                template.Graphics.SetTransparency(1f, 1f);

                                                                                templateStartPoint = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                g.DrawPdfTemplate(template, templateStartPoint);
                                                                            }

                                                                            break;
                                                                        case "POLYGON":
                                                                            for (var polygonIndex = 0; polygonIndex < FeatureGeometry.GetGeometryCount(); polygonIndex++)
                                                                            {
                                                                                //The first geometry, i.e. the 0th geometry is outer ring. 1st and onwards are inner rings.
                                                                                //Get the ring at this index.
                                                                                var ring2 = FeatureGeometry.GetGeometryRef(polygonIndex);

                                                                                // Initialise the point array size
                                                                                Syncfusion.Drawing.PointF[] points = new Syncfusion.Drawing.PointF[ring2.GetPointCount()];

                                                                                for (var pointIndex = 0; pointIndex < ring2.GetPointCount(); pointIndex++)
                                                                                {
                                                                                    double[] coords = new double[3];
                                                                                    ring2.GetPoint(pointIndex, coords);

                                                                                    MapFeatureX = coords[0];
                                                                                    MapFeatureY = coords[1];

                                                                                    PageFeatureX = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageX) + (((MapFeatureX - System.Convert.ToDouble(MapExtentMinX)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));
                                                                                    PageFeatureY = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageY) - (((MapFeatureY - System.Convert.ToDouble(MapExtentMaxY)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));

                                                                                    Syncfusion.Drawing.PointF p = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureX) - System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureY) - System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                    points.SetValue(p, pointIndex);
                                                                                }

                                                                                if (points.Length > 0)
                                                                                {
                                                                                    //user styles
                                                                                    if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush") != null)
                                                                                    {
                                                                                        userBrushAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                        userBrushRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@red").InnerText);
                                                                                        userBrushGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@green").InnerText);
                                                                                        userBrushBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@blue").InnerText);

                                                                                    }
                                                                                    if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen") != null)
                                                                                    {
                                                                                        userPenAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                        userPenRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@red").InnerText);
                                                                                        userPenGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@green").InnerText);
                                                                                        userPenBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@blue").InnerText);
                                                                                        userPenWidth = System.Convert.ToSingle(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@width").InnerText);
                                                                                    }

                                                                                    userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                    userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);


                                                                                    // create template to constrain drawn feature to
                                                                                    template = new PdfTemplate(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt));

                                                                                    template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                    template.Graphics.DrawPolygon(userPen, userBrush, points);
                                                                                    template.Graphics.SetTransparency(1f, 1f);

                                                                                    templateStartPoint = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                    g.DrawPdfTemplate(template, templateStartPoint);
                                                                                }

                                                                            }
                                                                            break;
                                                                        case "MULTIPOINT":
                                                                            for (var multiPointIndex = 0; multiPointIndex < FeatureGeometry.GetGeometryCount(); multiPointIndex++)
                                                                            {
                                                                                Geometry mutliPointGeom = FeatureGeometry.GetGeometryRef(multiPointIndex);
                                                                                Syncfusion.Drawing.PointF[] multiPointPoints = new Syncfusion.Drawing.PointF[mutliPointGeom.GetPointCount()];

                                                                                float testStringY2 = 240;
                                                                                for (var pointIndex = 0; pointIndex < mutliPointGeom.GetPointCount(); pointIndex++)
                                                                                {
                                                                                    testStringY2 = testStringY2 + 2;

                                                                                    MapFeatureX = mutliPointGeom.GetX(0);
                                                                                    MapFeatureY = mutliPointGeom.GetY(0);
                                                                                    PageFeatureX = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageX) + (((MapFeatureX - System.Convert.ToDouble(MapExtentMinX)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));
                                                                                    PageFeatureY = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageY) - (((MapFeatureY - System.Convert.ToDouble(MapExtentMaxY)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));

                                                                                    //user styles
                                                                                    if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush") != null)
                                                                                    {
                                                                                        userBrushAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                        userBrushRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@red").InnerText);
                                                                                        userBrushGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@green").InnerText);
                                                                                        userBrushBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@blue").InnerText);
                                                                                    }
                                                                                    if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen") != null)
                                                                                    {
                                                                                        userPenAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                        userPenRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@red").InnerText);
                                                                                        userPenGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@green").InnerText);
                                                                                        userPenBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@blue").InnerText);
                                                                                        userPenWidth = System.Convert.ToSingle(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@width").InnerText);
                                                                                    }

                                                                                    userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                    userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                                    // create template to constrain drawn feature to
                                                                                    template = new PdfTemplate(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt));

                                                                                    //reposition x and y for template
                                                                                    templatePageFeatureX = System.Convert.ToSingle(PageFeatureX) - System.Convert.ToSingle(MapImageX);
                                                                                    templatePageFeatureY = System.Convert.ToSingle(PageFeatureY) - System.Convert.ToSingle(MapImageY);

                                                                                    pointDrawWidth = 0;
                                                                                    pointDrawHeight = 0;
                                                                                    pointDrawOffsetX = 0;
                                                                                    pointDrawOffsetY = 0;
                                                                                    templateFeatureX = 0;
                                                                                    templateFeatureY = 0;

                                                                                    template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);

                                                                                    if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Draw") != null)
                                                                                    {
                                                                                        XmlNodeList XmlNodeListPointDraw = XmlNodeListMapFeatures.Item(iMapFeature).SelectNodes("Draw");
                                                                                        for (int iPointDraw = 0; iPointDraw < XmlNodeListPointDraw.Count; iPointDraw++)
                                                                                        {
                                                                                            string pointDrawType = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@type").InnerText.ToLower();
                                                                                            switch (pointDrawType)
                                                                                            {
                                                                                                case "ellipse":
                                                                                                    //<Draw type="Ellipse" width="5" height="5"/>

                                                                                                    if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush") != null)
                                                                                                    {
                                                                                                        userBrushAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                                        userBrushRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@red").InnerText);
                                                                                                        userBrushGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@green").InnerText);
                                                                                                        userBrushBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@blue").InnerText);
                                                                                                    }
                                                                                                    if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen") != null)
                                                                                                    {
                                                                                                        userPenAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                                        userPenRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@red").InnerText);
                                                                                                        userPenGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@green").InnerText);
                                                                                                        userPenBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@blue").InnerText);
                                                                                                        userPenWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@width").InnerText);
                                                                                                    }
                                                                                                    userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                                    userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                                                    pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                                    pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                                    pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                                    pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                                    templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                                    templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                                    template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                                    template.Graphics.DrawEllipse(userPen, userBrush, convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));

                                                                                                    break;
                                                                                                case "image":
                                                                                                    //<Draw type="Image" image="images/image1.png" width="10" height="10" alpha="200"/>
                                                                                                    int pointDrawImageAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@alpha").InnerText);

                                                                                                    template.Graphics.SetTransparency((float)pointDrawImageAlpha / 255, (float)pointDrawImageAlpha / 255);

                                                                                                    pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                                    pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                                    pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                                    pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                                    templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                                    templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                                    string pointDrawImage = "" + XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@image").InnerText;
                                                                                                    FileStream fileStream = new FileStream(pointDrawImage, FileMode.Open, FileAccess.Read);
                                                                                                    PdfImage pointDrawImage1 = new PdfBitmap(fileStream);
                                                                                                    template.Graphics.DrawImage(pointDrawImage1, convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));

                                                                                                    break;
                                                                                                case "rectangle":
                                                                                                    //< Draw type = "Rectangle" width = "5" height = "5" />

                                                                                                    if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush") != null)
                                                                                                    {
                                                                                                        userBrushAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                                        userBrushRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@red").InnerText);
                                                                                                        userBrushGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@green").InnerText);
                                                                                                        userBrushBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Brush/@blue").InnerText);
                                                                                                    }
                                                                                                    if (XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen") != null)
                                                                                                    {
                                                                                                        userPenAlpha = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                                        userPenRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@red").InnerText);
                                                                                                        userPenGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@green").InnerText);
                                                                                                        userPenBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@blue").InnerText);
                                                                                                        userPenWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("Pen/@width").InnerText);
                                                                                                    }
                                                                                                    userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                                    userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                                                    pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                                    pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                                    pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                                    pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                                    templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                                    templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                                    template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                                    template.Graphics.DrawRectangle(userPen, userBrush, convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));

                                                                                                    break;
                                                                                                case "polygon":
                                                                                                    //<Draw type="Polygon" points="(10,100),(10,200)"/>

                                                                                                    break;
                                                                                                case "string":
                                                                                                    //<Draw type="String" text="simon" width="40" height="10" fontFamily="Arial" fontSize="12" fontStyle="Regular" red="0" green="0" blue="255" alignment="Center"/>
                                                                                                    pointDrawWidth = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@width").InnerText);
                                                                                                    pointDrawHeight = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@height").InnerText);

                                                                                                    pointDrawOffsetX = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetX").InnerText);
                                                                                                    pointDrawOffsetY = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@offsetY").InnerText);
                                                                                                    templateFeatureX = templatePageFeatureX + pointDrawOffsetX;
                                                                                                    templateFeatureY = templatePageFeatureY + pointDrawOffsetY;

                                                                                                    string pointDrawText = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@text").InnerText;
                                                                                                    pointDrawText = "" + pointDrawText.Replace("@featurekey", FeatureKeys);
                                                                                                    pointDrawText = "" + pointDrawText.Replace("@datbasekey", DatabaseKey);
                                                                                                    pointDrawText = "" + pointDrawText.Replace("@referencekey", ReferenceKey);
                                                                                                    for (int iField = 0; iField < feature1.GetFieldCount(); iField++)
                                                                                                    {
                                                                                                        pointDrawText = "" + pointDrawText.Replace("@" + iField, feature1.GetFieldAsString(iField));
                                                                                                    }



                                                                                                    string pointDrawFontFamily = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@fontFamily").InnerText;
                                                                                                    float pointDrawFontSize = System.Convert.ToSingle(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@fontSize").InnerText);
                                                                                                    string pointDrawFontStyle = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@fontStyle").InnerText;
                                                                                                    int pointDrawRed = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@red").InnerText);
                                                                                                    int pointDrawGreen = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@green").InnerText);
                                                                                                    int pointDrawBlue = System.Convert.ToInt32(XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@blue").InnerText);
                                                                                                    string pointDrawAlignment = XmlNodeListPointDraw.Item(iPointDraw).SelectSingleNode("@alignment").InnerText;

                                                                                                    Syncfusion.Drawing.FontStyle fontStyle = Syncfusion.Drawing.FontStyle.Regular;
                                                                                                    switch (pointDrawFontStyle.ToLower())
                                                                                                    {
                                                                                                        case "bold":
                                                                                                            fontStyle = Syncfusion.Drawing.FontStyle.Bold;
                                                                                                            break;
                                                                                                        case "italic":
                                                                                                            fontStyle = Syncfusion.Drawing.FontStyle.Italic;
                                                                                                            break;
                                                                                                        case "strikeout":
                                                                                                            fontStyle = Syncfusion.Drawing.FontStyle.Strikeout;
                                                                                                            break;
                                                                                                        case "underline":
                                                                                                            fontStyle = Syncfusion.Drawing.FontStyle.Underline;
                                                                                                            break;
                                                                                                        default:
                                                                                                            //regular
                                                                                                            break;
                                                                                                    }

                                                                                                    //default font helvetica
                                                                                                    PdfFont pointDrawFont = new PdfStandardFont(PdfFontFamily.Helvetica, pointDrawFontSize);

                                                                                                    PdfBrush pointDrawFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(pointDrawRed, pointDrawGreen, pointDrawBlue));

                                                                                                    template.Graphics.SetTransparency(1f, 1f);


                                                                                                    Syncfusion.Drawing.RectangleF pointDrawRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(templateFeatureX - ((float)pointDrawWidth / 2), mm, pt), convertor1.ConvertUnits(templateFeatureY - (float)pointDrawHeight / 2, mm, pt), convertor1.ConvertUnits(pointDrawWidth, mm, pt), convertor1.ConvertUnits(pointDrawHeight, mm, pt));
                                                                                                    PdfStringFormat pointDrawFormat = new PdfStringFormat();
                                                                                                    pointDrawFormat.WordWrap = PdfWordWrapType.Word;
                                                                                                    PdfTextAlignment fontAlignment = PdfTextAlignment.Center;
                                                                                                    switch (pointDrawAlignment.ToLower())
                                                                                                    {
                                                                                                        case "justify":
                                                                                                            fontAlignment = PdfTextAlignment.Justify;
                                                                                                            break;
                                                                                                        case "left":
                                                                                                            fontAlignment = PdfTextAlignment.Left;
                                                                                                            break;
                                                                                                        case "right":
                                                                                                            fontAlignment = PdfTextAlignment.Right;
                                                                                                            break;
                                                                                                        default:
                                                                                                            // center
                                                                                                            break;
                                                                                                    }
                                                                                                    pointDrawFormat.Alignment = fontAlignment;

                                                                                                    template.Graphics.DrawString(pointDrawText, pointDrawFont, pointDrawFontColour, pointDrawRectangle, pointDrawFormat);


                                                                                                    break;
                                                                                                default:
                                                                                                    //default to 2x2 ellipse

                                                                                                    break;
                                                                                            }
                                                                                        }
                                                                                    }


                                                                                    template.Graphics.SetTransparency(1f, 1f);

                                                                                    templateStartPoint = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                    g.DrawPdfTemplate(template, templateStartPoint);




                                                                                }


                                                                            }
                                                                            break;
                                                                        case "MULTILINESTRING":



                                                                            for (var multiLinestringIndex = 0; multiLinestringIndex < FeatureGeometry.GetGeometryCount(); multiLinestringIndex++)
                                                                            {
                                                                                var ring = FeatureGeometry.GetGeometryRef(multiLinestringIndex);

                                                                                // Initialise the point array size
                                                                                Syncfusion.Drawing.PointF[] multilinepoints = new Syncfusion.Drawing.PointF[ring.GetPointCount()];
                                                                                // Initialise path
                                                                                PdfPath multipath1 = new PdfPath();
                                                                                // Initialise previous point
                                                                                Syncfusion.Drawing.PointF multipp = new Syncfusion.Drawing.PointF();

                                                                                for (var pointIndex = 0; pointIndex < ring.GetPointCount(); pointIndex++)
                                                                                {
                                                                                    double[] coords = new double[3];
                                                                                    ring.GetPoint(pointIndex, coords);

                                                                                    MapFeatureX = coords[0];
                                                                                    MapFeatureY = coords[1];

                                                                                    PageFeatureX = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageX) + (((MapFeatureX - System.Convert.ToDouble(MapExtentMinX)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));
                                                                                    PageFeatureY = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageY) - (((MapFeatureY - System.Convert.ToDouble(MapExtentMaxY)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));

                                                                                    Syncfusion.Drawing.PointF p = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureX) - System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureY) - System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                    multilinepoints.SetValue(p, pointIndex);

                                                                                    if (pointIndex != 0)
                                                                                    {
                                                                                        multipath1.AddLine(multipp, p);
                                                                                        multipp = p;
                                                                                    }
                                                                                    else
                                                                                    {
                                                                                        multipp = p;
                                                                                    }
                                                                                }

                                                                                if (multilinepoints.Length > 0)
                                                                                {
                                                                                    //user styles
                                                                                    if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush") != null)
                                                                                    {
                                                                                        userBrushAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                        userBrushRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@red").InnerText);
                                                                                        userBrushGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@green").InnerText);
                                                                                        userBrushBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@blue").InnerText);
                                                                                    }
                                                                                    if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen") != null)
                                                                                    {
                                                                                        userPenAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                        userPenRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@red").InnerText);
                                                                                        userPenGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@green").InnerText);
                                                                                        userPenBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@blue").InnerText);
                                                                                        userPenWidth = System.Convert.ToSingle(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@width").InnerText);
                                                                                    }

                                                                                    userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                    userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);


                                                                                    // create template to constrain drawn feature to
                                                                                    template = new PdfTemplate(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt));

                                                                                    template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                    template.Graphics.DrawPath(userPen, userBrush, multipath1);
                                                                                    template.Graphics.SetTransparency(1f, 1f);

                                                                                    templateStartPoint = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                    g.DrawPdfTemplate(template, templateStartPoint);
                                                                                }

                                                                            }


                                                                            break;
                                                                        case "MULTIPOLYGON":
                                                                            for (var multiPolygonIndex = 0; multiPolygonIndex < FeatureGeometry.GetGeometryCount(); multiPolygonIndex++)
                                                                            {
                                                                                var ring = FeatureGeometry.GetGeometryRef(multiPolygonIndex);

                                                                                for (var polygonIndex = 0; polygonIndex < ring.GetGeometryCount(); polygonIndex++)
                                                                                {
                                                                                    //The first geometry, i.e. the 0th geometry is outer ring. 1st and onwards are inner rings.
                                                                                    //Get the ring at this index.
                                                                                    var ring2 = ring.GetGeometryRef(polygonIndex);

                                                                                    // Initialise the point array size
                                                                                    Syncfusion.Drawing.PointF[] points = new Syncfusion.Drawing.PointF[ring2.GetPointCount()];

                                                                                    for (var pointIndex = 0; pointIndex < ring2.GetPointCount(); pointIndex++)
                                                                                    {
                                                                                        double[] coords = new double[3];
                                                                                        ring2.GetPoint(pointIndex, coords);

                                                                                        MapFeatureX = coords[0];
                                                                                        MapFeatureY = coords[1];

                                                                                        PageFeatureX = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageX) + (((MapFeatureX - System.Convert.ToDouble(MapExtentMinX)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));
                                                                                        PageFeatureY = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageY) - (((MapFeatureY - System.Convert.ToDouble(MapExtentMaxY)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));

                                                                                        Syncfusion.Drawing.PointF p = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureX) - System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureY) - System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                        points.SetValue(p, pointIndex);
                                                                                    }

                                                                                    if (points.Length > 0)
                                                                                    {
                                                                                        //user styles
                                                                                        if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush") != null)
                                                                                        {
                                                                                            userBrushAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                            userBrushRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@red").InnerText);
                                                                                            userBrushGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@green").InnerText);
                                                                                            userBrushBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@blue").InnerText);
                                                                                        }
                                                                                        if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen") != null)
                                                                                        {
                                                                                            userPenAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                            userPenRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@red").InnerText);
                                                                                            userPenGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@green").InnerText);
                                                                                            userPenBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@blue").InnerText);
                                                                                            userPenWidth = System.Convert.ToSingle(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@width").InnerText);
                                                                                        }

                                                                                        userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                        userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);


                                                                                        // create template to constrain drawn feature to
                                                                                        template = new PdfTemplate(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt));

                                                                                        template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                        template.Graphics.DrawPolygon(userPen, userBrush, points);
                                                                                        template.Graphics.SetTransparency(1f, 1f);

                                                                                        templateStartPoint = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                        g.DrawPdfTemplate(template, templateStartPoint);


                                                                                    }

                                                                                }
                                                                            }
                                                                            break;
                                                                        case "MULTISURFACE":
                                                                            for (var multiPolygonIndex = 0; multiPolygonIndex < FeatureGeometry.GetGeometryCount(); multiPolygonIndex++)
                                                                            {
                                                                                var ring = FeatureGeometry.GetGeometryRef(multiPolygonIndex);

                                                                                for (var polygonIndex = 0; polygonIndex < ring.GetGeometryCount(); polygonIndex++)
                                                                                {
                                                                                    //The first geometry, i.e. the 0th geometry is outer ring. 1st and onwards are inner rings.
                                                                                    //Get the ring at this index.
                                                                                    var ring2 = ring.GetGeometryRef(polygonIndex);

                                                                                    // Initialise the point array size
                                                                                    Syncfusion.Drawing.PointF[] points = new Syncfusion.Drawing.PointF[ring2.GetPointCount()];

                                                                                    for (var pointIndex = 0; pointIndex < ring2.GetPointCount(); pointIndex++)
                                                                                    {
                                                                                        double[] coords = new double[3];
                                                                                        ring2.GetPoint(pointIndex, coords);

                                                                                        MapFeatureX = coords[0];
                                                                                        MapFeatureY = coords[1];

                                                                                        PageFeatureX = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageX) + (((MapFeatureX - System.Convert.ToDouble(MapExtentMinX)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));
                                                                                        PageFeatureY = System.Convert.ToString(Math.Round(System.Convert.ToDouble(MapImageY) - (((MapFeatureY - System.Convert.ToDouble(MapExtentMaxY)) * 1000) / System.Convert.ToDouble(MapImageNeatScale)), 5));

                                                                                        Syncfusion.Drawing.PointF p = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureX) - System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(PageFeatureY) - System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                        points.SetValue(p, pointIndex);
                                                                                    }


                                                                                    if (points.Length > 0)
                                                                                    {
                                                                                        //user styles
                                                                                        if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush") != null)
                                                                                        {
                                                                                            userBrushAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@alpha").InnerText);
                                                                                            userBrushRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@red").InnerText);
                                                                                            userBrushGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@green").InnerText);
                                                                                            userBrushBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Brush/@blue").InnerText);
                                                                                        }
                                                                                        if (XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen") != null)
                                                                                        {
                                                                                            userPenAlpha = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@alpha").InnerText);
                                                                                            userPenRed = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@red").InnerText);
                                                                                            userPenGreen = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@green").InnerText);
                                                                                            userPenBlue = System.Convert.ToInt32(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@blue").InnerText);
                                                                                            userPenWidth = System.Convert.ToSingle(XmlNodeListMapFeatures.Item(iMapFeature).SelectSingleNode("Pen/@width").InnerText);
                                                                                        }

                                                                                        userBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(userBrushAlpha, userBrushRed, userBrushGreen, userBrushBlue));
                                                                                        userPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(userPenAlpha, userPenRed, userPenGreen, userPenBlue), userPenWidth);

                                                                                        // create template to constrain drawn feature to
                                                                                        template = new PdfTemplate(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageHeight), mm, pt));

                                                                                        template.Graphics.SetTransparency((float)userPenAlpha / 255, (float)userBrushAlpha / 255);
                                                                                        template.Graphics.DrawPolygon(userPen, userBrush, points);
                                                                                        template.Graphics.SetTransparency(1f, 1f);

                                                                                        templateStartPoint = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY), mm, pt));
                                                                                        g.DrawPdfTemplate(template, templateStartPoint);
                                                                                    }



                                                                                }


                                                                            }
                                                                            break;
                                                                        case "MULTIGEOMETRY":
                                                                            // ??
                                                                            //use goto refernces ?

                                                                            break;
                                                                        default:
                                                                            break;
                                                                    }
                                                                    layer1.DeleteFeature(featureIndex);
                                                                }
                                                            }
                                                            if (MapImageMapFeatureType == "sql")
                                                            {
                                                                dsOgr.ReleaseResultSet(layer1);
                                                            }
                                                        }

                                                    }
                                                    //overlays
                                                }


                                                // if multiple maps, add scale text below map image
                                                if (XmlNodeListTemplateMaps.Count > 1)
                                                {
                                                    if (xpathnodespages.Current.SelectSingleNode("MapImage[position()=" + iMap + "]/ScaleText") != null)
                                                    {
                                                        //Future functionality
                                                        /*
        <ScaleText position="LL">
            <Brush alpha="50" red="255" green="100" blue="0"/>
            <Pen alpha="255" red="0" green="0" blue="255" width="1"/>
            <Draw type="String" text="Scale 1:" width="40" height="40" fontFamily="Arial" fontSize="12" fontStyle="Bold" red="0" green="0" blue="255" alignment="Left"/>
        </ScaleText>
                                                        */
                                                    }
                                                    else
                                                    {
                                                        string scaleText = "Scale 1:" + MapImageNeatScale;
                                                        float scaleTextLength = scaleText.Length;
                                                        //default
                                                        g.DrawRectangle(BrushWhite, convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY) + System.Convert.ToSingle(MapImageHeight), mm, pt), scaleTextLength * 3 + 2.5f, 10);
                                                        g.DrawString(scaleText, Font6, BrushBlack, new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(MapImageX) + 0.5f, mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(MapImageY) + System.Convert.ToSingle(MapImageHeight) + 0.5f, mm, pt)));
                                                    }
                                                }

                                            }
                                        }

                                    }
                                }


                                // floating images from .config (e.g. legends)
                                if (xpathnodespages.Current.SelectSingleNode("FloatingImages") != null && xpathnodespages.Current.SelectSingleNode("FloatingImages/FloatingImage") != null)
                                {
                                    XPathNodeIterator xpathNodeIterator2 = xpathnodespages.Current.Select("FloatingImages/FloatingImage");
                                    if (xpathNodeIterator2.Count > 0)
                                    {

                                        while (xpathNodeIterator2.MoveNext())
                                        {
                                            float ImageMultiplier = 1;
                                            string ImagePositionX = "0";
                                            string ImagePositionY = "0";


                                            switch ((xpathNodeIterator2.Current.GetAttribute("type", "").ToString() ?? "").ToLower())
                                            {
                                                case "file":
                                                    string ImageFile = "" + xpathNodeIterator2.Current.GetAttribute("file", "").ToString() ?? "";
                                                    string ImageFileX = "" + xpathNodeIterator2.Current.GetAttribute("x", "").ToString() ?? "";
                                                    string ImageFileY = "" + xpathNodeIterator2.Current.GetAttribute("y", "").ToString() ?? "";
                                                    string ImageFileMultiplier = "" + xpathNodeIterator2.Current.GetAttribute("multiplier", "").ToString() ?? "";

                                                    if (ImageFile != "" && ImageFileX != "" && ImageFileY != "")
                                                    {
                                                        if (ImageFileMultiplier != "")
                                                        {
                                                            ImageMultiplier = System.Convert.ToSingle(ImageFileMultiplier);
                                                        }

                                                        ImagePositionX = ImageFileX;
                                                        ImagePositionY = ImageFileY;

                                                        FileStream fileStream = new FileStream(ImageFile, FileMode.Open, FileAccess.Read);
                                                        PdfImage Image1 = new PdfBitmap(fileStream);
                                                        g.DrawImage(Image1, convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionY), mm, pt), Image1.Width * ImageMultiplier, Image1.Height * ImageMultiplier);

                                                    }
                                                    continue;
                                                case "uri":
                                                    string ImageURI = "" + xpathNodeIterator2.Current.SelectSingleNode("URI").ToString() ?? "";
                                                    string ImageURIX = "" + xpathNodeIterator2.Current.GetAttribute("x", "").ToString() ?? "";
                                                    string ImageURIY = "" + xpathNodeIterator2.Current.GetAttribute("y", "").ToString() ?? "";
                                                    string ImageURIMultiplier = "" + xpathNodeIterator2.Current.GetAttribute("multiplier", "").ToString() ?? "";
                                                    if (ImageURI != "" && ImageURIX != "" && ImageURIY != "")
                                                    {
                                                        if (ImageURIMultiplier != "")
                                                        {
                                                            ImageMultiplier = System.Convert.ToSingle(ImageURIMultiplier);
                                                        }

                                                        ImagePositionX = ImageURIX;
                                                        ImagePositionY = ImageURIY;

                                                        if (ImageURI != "" && ImageURI.ToLower().StartsWith("http"))
                                                        {
                                                            //check status code
                                                            statusCode = GetHTTPStatusCode(ImageURI, "HEAD");
                                                            if (statusCode == 0)
                                                            {
                                                                statusCode = GetHTTPStatusCode(ImageURI, "GET");
                                                            }
                                                            if (statusCode == 0)
                                                            {
                                                                //assume if still 0 ok!
                                                                statusCode = 200;
                                                            }

                                                            if (statusCode >= 100 && statusCode < 400)
                                                            {

                                                                WebRequest request = WebRequest.Create(ImageURI);
                                                                byte[] rBytes;
                                                                //  > Get the content
                                                                using (WebResponse result = request.GetResponse())
                                                                {
                                                                    Stream rStream = result.GetResponseStream();
                                                                    //  > Bytes from address
                                                                    using (BinaryReader br = new BinaryReader(rStream))
                                                                    {
                                                                        // Ask for bytes bigger than the actual stream
                                                                        rBytes = br.ReadBytes(5000000);
                                                                        br.Close();
                                                                    }
                                                                    //  > close down the web response object
                                                                    result.Close();
                                                                }

                                                                using (MemoryStream imageStream = new MemoryStream(rBytes))
                                                                {
                                                                    imageStream.Position = 0;
                                                                    MemoryStream imageStream2 = new MemoryStream();
                                                                    using (System.Drawing.Image imageBitmap = new System.Drawing.Bitmap(imageStream))
                                                                    {
                                                                        imageBitmap.Save(imageStream2, System.Drawing.Imaging.ImageFormat.Png);
                                                                    }
                                                                    imageStream2.Position = 0;
                                                                    PdfImage PdfImage1 = new PdfBitmap(imageStream2);
                                                                    g.DrawImage(PdfImage1, convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionY), mm, pt), PdfImage1.Width * ImageMultiplier, PdfImage1.Height * ImageMultiplier);
                                                                    imageStream2.Close();
                                                                    imageStream.Close();
                                                                }


                                                            }
                                                        }
                                                    }

                                                    continue;
                                                case "sql":
                                                    string ImageSQL = "";
                                                    string ImageSQLX = "" + xpathNodeIterator2.Current.GetAttribute("x", "").ToString() ?? "";
                                                    string ImageSQLY = "" + xpathNodeIterator2.Current.GetAttribute("y", "").ToString() ?? "";
                                                    string ImageSQLMultiplier = "" + xpathNodeIterator2.Current.GetAttribute("multiplier", "").ToString() ?? "";

                                                    continue;
                                                default:
                                                    continue;
                                            }
                                        }
                                    }
                                }





                                // Data Tables

                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']") != null)
                                {
                                    //Set defaults
                                    int rgbAlpha = 0;
                                    int rgbRed = 0;
                                    int rgbGreen = 0;
                                    int rgbBlue = 0;
                                    PdfBrush dataTableBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.Transparent);
                                    Single dataTablePenWidth = 0.5f;
                                    PdfPen dataTablePen = new PdfPen(Syncfusion.Drawing.Color.Black);

                                    //default font
                                    string dataTableFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/LabelFont/@description").InnerText;
                                    char[] charSeparators = new char[] { ',' };
                                    string[] splitresult;
                                    splitresult = dataTableFontDescription.Split(charSeparators);
                                    Single dataTableFontSize = System.Convert.ToSingle(splitresult[1]);
                                    PdfFontStyle dataTableFontStyle = PdfFontStyle.Regular;
                                    if (splitresult[4] == "75") { dataTableFontStyle = PdfFontStyle.Bold; }
                                    PdfFont dataTableFont = new PdfStandardFont(PdfFontFamily.Helvetica, dataTableFontSize, dataTableFontStyle);
                                    PdfFont dataTableFontBold = new PdfStandardFont(PdfFontFamily.Helvetica, dataTableFontSize, PdfFontStyle.Bold);
                                    PdfFont dataTableFontTitle = new PdfStandardFont(PdfFontFamily.Helvetica, dataTableFontSize + 2, PdfFontStyle.Bold);


                                    //default font color
                                    rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/FontColor/@red").InnerText);
                                    rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/FontColor/@green").InnerText);
                                    rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/FontColor/@blue").InnerText);
                                    PdfBrush dataTableFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));


                                    //default fill color if not transparent
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/BackgroundColor/@alpha").InnerText != "0")
                                    {
                                        rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/BackgroundColor/@alpha").InnerText);
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/BackgroundColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/BackgroundColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/BackgroundColor/@blue").InnerText);
                                        dataTableBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue));
                                    }


                                    //default border color
                                    rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/FrameColor/@alpha").InnerText);
                                    rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/FrameColor/@red").InnerText);
                                    rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/FrameColor/@green").InnerText);
                                    rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/FrameColor/@blue").InnerText);
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position") != null)
                                    {
                                        string dataOutlineWidthM = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@outlineWidthM").InnerText;
                                        charSeparators = new char[] { ',' };
                                        splitresult = dataOutlineWidthM.Split(charSeparators);
                                        dataTablePenWidth = System.Convert.ToSingle(splitresult[0]);
                                    }
                                    dataTablePen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue), dataTablePenWidth);



                                    //Set Data table title style
                                    PdfFont dataTableTitleFont = dataTableFontTitle;
                                    PdfBrush dataTableTitleFontColour = dataTableFontColour;
                                    PdfBrush dataTableTitleBrush = dataTableBrush;
                                    Single dataTableTitlePenWidth = dataTablePenWidth;
                                    PdfPen dataTableTitlePen = dataTablePen;
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']") != null)
                                    {
                                        //font
                                        dataTableFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/LabelFont/@description").InnerText;
                                        splitresult = dataTableFontDescription.Split(charSeparators);
                                        string dataTableFontName = splitresult[0];
                                        dataTableFontSize = System.Convert.ToSingle(splitresult[1]);
                                        Syncfusion.Pdf.Graphics.PdfFontStyle fs1 = PdfFontStyle.Regular;
                                        if (splitresult[4] == "50" && splitresult[5] == "0") { fs1 = PdfFontStyle.Regular; }
                                        if (splitresult[4] == "50" && splitresult[5] == "1") { fs1 = PdfFontStyle.Italic; }
                                        if (splitresult[4] == "75" && splitresult[5] == "0") { fs1 = PdfFontStyle.Bold; }
                                        if (splitresult[4] == "75" && splitresult[5] == "1") { fs1 = PdfFontStyle.Bold | PdfFontStyle.Italic; }

                                        string linuxttfpath = "/usr/share/fonts/truetype/msttcorefonts/" + dataTableFontName.ToLower() + ".ttf";
                                        FileStream fontStream = null;
                                        if (System.IO.File.Exists(linuxttfpath))
                                        {
                                            fontStream = new FileStream(linuxttfpath, FileMode.Open, FileAccess.Read);
                                        }
                                        else
                                        {
                                            fontStream = new FileStream("Arial.ttf", FileMode.Open, FileAccess.Read);
                                        }
                                        dataTableTitleFont = new PdfTrueTypeFont(fontStream, dataTableFontSize, fs1);

                                        //font color
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/FontColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/FontColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/FontColor/@blue").InnerText);
                                        dataTableTitleFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));


                                        //fill color if not transparent
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/BackgroundColor/@alpha").InnerText != "0")
                                        {
                                            rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/BackgroundColor/@alpha").InnerText);
                                            rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/BackgroundColor/@red").InnerText);
                                            rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/BackgroundColor/@green").InnerText);
                                            rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/BackgroundColor/@blue").InnerText);
                                            dataTableTitleBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue));
                                        }

                                        //border color
                                        rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/FrameColor/@alpha").InnerText);
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/FrameColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/FrameColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/FrameColor/@blue").InnerText);
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position") != null)
                                        {
                                            string dataOutlineWidthM = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableTitleStyle']/@outlineWidthM").InnerText;
                                            charSeparators = new char[] { ',' };
                                            splitresult = dataOutlineWidthM.Split(charSeparators);
                                            dataTableTitlePenWidth = System.Convert.ToSingle(splitresult[0]);
                                        }
                                        dataTableTitlePen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue), dataTableTitlePenWidth);
                                    }


                                    //Set Data table description style
                                    PdfFont dataTableDescriptionFont = dataTableFont;
                                    PdfBrush dataTableDescriptionFontColour = dataTableFontColour;
                                    PdfBrush dataTableDescriptionBrush = dataTableBrush;
                                    Single dataTableDescriptionPenWidth = dataTablePenWidth;
                                    PdfPen dataTableDescriptionPen = dataTablePen;
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']") != null)
                                    {
                                        //font
                                        dataTableFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/LabelFont/@description").InnerText;
                                        splitresult = dataTableFontDescription.Split(charSeparators);
                                        string dataTableFontName = splitresult[0];
                                        dataTableFontSize = System.Convert.ToSingle(splitresult[1]);
                                        Syncfusion.Pdf.Graphics.PdfFontStyle fs1 = PdfFontStyle.Regular;
                                        if (splitresult[4] == "50" && splitresult[5] == "0") { fs1 = PdfFontStyle.Regular; }
                                        if (splitresult[4] == "50" && splitresult[5] == "1") { fs1 = PdfFontStyle.Italic; }
                                        if (splitresult[4] == "75" && splitresult[5] == "0") { fs1 = PdfFontStyle.Bold; }
                                        if (splitresult[4] == "75" && splitresult[5] == "1") { fs1 = PdfFontStyle.Bold | PdfFontStyle.Italic; }

                                        string linuxttfpath = "/usr/share/fonts/truetype/msttcorefonts/" + dataTableFontName.ToLower() + ".ttf";
                                        FileStream fontStream = null;
                                        if (System.IO.File.Exists(linuxttfpath))
                                        {
                                            fontStream = new FileStream(linuxttfpath, FileMode.Open, FileAccess.Read);
                                        }
                                        else
                                        {
                                            fontStream = new FileStream("Arial.ttf", FileMode.Open, FileAccess.Read);
                                        }
                                        dataTableDescriptionFont = new PdfTrueTypeFont(fontStream, dataTableFontSize, fs1);

                                        //font color
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/FontColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/FontColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/FontColor/@blue").InnerText);
                                        dataTableDescriptionFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                        //fill color if not transparent
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/@background").InnerText != "false")
                                        {
                                            rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/BackgroundColor/@alpha").InnerText);
                                            rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/BackgroundColor/@red").InnerText);
                                            rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/BackgroundColor/@green").InnerText);
                                            rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/BackgroundColor/@blue").InnerText);
                                            dataTableDescriptionBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue));
                                        }

                                        //border color
                                        rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/FrameColor/@alpha").InnerText);
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/FrameColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/FrameColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/FrameColor/@blue").InnerText);
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position") != null)
                                        {
                                            string dataOutlineWidthM = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableDescriptionStyle']/@outlineWidthM").InnerText;
                                            charSeparators = new char[] { ',' };
                                            splitresult = dataOutlineWidthM.Split(charSeparators);
                                            dataTableDescriptionPenWidth = System.Convert.ToSingle(splitresult[0]);
                                        }
                                        dataTableDescriptionPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue), dataTableDescriptionPenWidth);
                                    }

                                    //Set Data table heading row style
                                    PdfFont dataTableHeadingFont = dataTableFontBold;
                                    PdfBrush dataTableHeadingFontColour = dataTableFontColour;
                                    PdfBrush dataTableHeadingBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.LightGray);
                                    Single dataTableHeadingPenWidth = dataTablePenWidth;
                                    PdfPen dataTableHeadingPen = dataTablePen;
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']") != null)
                                    {
                                        //font
                                        dataTableFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/LabelFont/@description").InnerText;
                                        splitresult = dataTableFontDescription.Split(charSeparators);
                                        string dataTableFontName = splitresult[0];
                                        dataTableFontSize = System.Convert.ToSingle(splitresult[1]);
                                        Syncfusion.Pdf.Graphics.PdfFontStyle fs1 = PdfFontStyle.Regular;
                                        if (splitresult[4] == "50" && splitresult[5] == "0") { fs1 = PdfFontStyle.Regular; }
                                        if (splitresult[4] == "50" && splitresult[5] == "1") { fs1 = PdfFontStyle.Italic; }
                                        if (splitresult[4] == "75" && splitresult[5] == "0") { fs1 = PdfFontStyle.Bold; }
                                        if (splitresult[4] == "75" && splitresult[5] == "1") { fs1 = PdfFontStyle.Bold | PdfFontStyle.Italic; }

                                        string linuxttfpath = "/usr/share/fonts/truetype/msttcorefonts/" + dataTableFontName.ToLower() + ".ttf";
                                        FileStream fontStream = null;
                                        if (System.IO.File.Exists(linuxttfpath))
                                        {
                                            fontStream = new FileStream(linuxttfpath, FileMode.Open, FileAccess.Read);
                                        }
                                        else
                                        {
                                            fontStream = new FileStream("Arial.ttf", FileMode.Open, FileAccess.Read);
                                        }
                                        dataTableHeadingFont = new PdfTrueTypeFont(fontStream, dataTableFontSize, fs1);

                                        //font color
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/FontColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/FontColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/FontColor/@blue").InnerText);
                                        dataTableHeadingFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                        //fill color if not transparent
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/@background").InnerText != "false")
                                        {
                                            rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/BackgroundColor/@alpha").InnerText);
                                            rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/BackgroundColor/@red").InnerText);
                                            rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/BackgroundColor/@green").InnerText);
                                            rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/BackgroundColor/@blue").InnerText);
                                            dataTableHeadingBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue));
                                        }

                                        //border color
                                        rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/FrameColor/@alpha").InnerText);
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/FrameColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/FrameColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/FrameColor/@blue").InnerText);
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position") != null)
                                        {
                                            string dataOutlineWidthM = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableHeadingStyle']/@outlineWidthM").InnerText;
                                            charSeparators = new char[] { ',' };
                                            splitresult = dataOutlineWidthM.Split(charSeparators);
                                            dataTableHeadingPenWidth = System.Convert.ToSingle(splitresult[0]);
                                        }
                                        dataTableHeadingPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue), dataTableHeadingPenWidth);
                                    }

                                    //Set Data table default row style
                                    PdfFont dataTableRowDefaultFont = dataTableFont;
                                    PdfBrush dataTableRowDefaultFontColour = dataTableFontColour;
                                    PdfBrush dataTableRowDefaultBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.White);
                                    Single dataTableRowDefaultPenWidth = dataTablePenWidth;
                                    PdfPen dataTableRowDefaultPen = dataTablePen;
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']") != null)
                                    {
                                        //font
                                        dataTableFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/LabelFont/@description").InnerText;
                                        splitresult = dataTableFontDescription.Split(charSeparators);
                                        string dataTableFontName = splitresult[0];
                                        dataTableFontSize = System.Convert.ToSingle(splitresult[1]);
                                        Syncfusion.Pdf.Graphics.PdfFontStyle fs1 = PdfFontStyle.Regular;
                                        if (splitresult[4] == "50" && splitresult[5] == "0") { fs1 = PdfFontStyle.Regular; }
                                        if (splitresult[4] == "50" && splitresult[5] == "1") { fs1 = PdfFontStyle.Italic; }
                                        if (splitresult[4] == "75" && splitresult[5] == "0") { fs1 = PdfFontStyle.Bold; }
                                        if (splitresult[4] == "75" && splitresult[5] == "1") { fs1 = PdfFontStyle.Bold | PdfFontStyle.Italic; }

                                        string linuxttfpath = "/usr/share/fonts/truetype/msttcorefonts/" + dataTableFontName.ToLower() + ".ttf";
                                        FileStream fontStream = null;
                                        if (System.IO.File.Exists(linuxttfpath))
                                        {
                                            fontStream = new FileStream(linuxttfpath, FileMode.Open, FileAccess.Read);
                                        }
                                        else
                                        {
                                            fontStream = new FileStream("Arial.ttf", FileMode.Open, FileAccess.Read);
                                        }
                                        dataTableRowDefaultFont = new PdfTrueTypeFont(fontStream, dataTableFontSize, fs1);

                                        //font color
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/FontColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/FontColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/FontColor/@blue").InnerText);
                                        dataTableRowDefaultFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                        //fill color if not transparent
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/@background").InnerText != "false")
                                        {
                                            rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/BackgroundColor/@alpha").InnerText);
                                            rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/BackgroundColor/@red").InnerText);
                                            rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/BackgroundColor/@green").InnerText);
                                            rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/BackgroundColor/@blue").InnerText);
                                            dataTableRowDefaultBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue));
                                        }

                                        //border color
                                        rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/FrameColor/@alpha").InnerText);
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/FrameColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/FrameColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/FrameColor/@blue").InnerText);
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position") != null)
                                        {
                                            string dataOutlineWidthM = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowDefaultStyle']/@outlineWidthM").InnerText;
                                            charSeparators = new char[] { ',' };
                                            splitresult = dataOutlineWidthM.Split(charSeparators);
                                            dataTableRowDefaultPenWidth = System.Convert.ToSingle(splitresult[0]);
                                        }
                                        dataTableRowDefaultPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue), dataTableRowDefaultPenWidth);
                                    }

                                    //Set Data table alternate row style
                                    PdfFont dataTableRowAlternateFont = dataTableFont;
                                    PdfBrush dataTableRowAlternateFontColour = dataTableFontColour;
                                    PdfBrush dataTableRowAlternateBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(245, 245, 245));
                                    Single dataTableRowAlternatePenWidth = dataTablePenWidth;
                                    PdfPen dataTableRowAlternatePen = dataTablePen;
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']") != null)
                                    {
                                        //font
                                        dataTableFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/LabelFont/@description").InnerText;
                                        splitresult = dataTableFontDescription.Split(charSeparators);
                                        string dataTableFontName = splitresult[0];
                                        dataTableFontSize = System.Convert.ToSingle(splitresult[1]);
                                        Syncfusion.Pdf.Graphics.PdfFontStyle fs1 = PdfFontStyle.Regular;
                                        if (splitresult[4] == "50" && splitresult[5] == "0") { fs1 = PdfFontStyle.Regular; }
                                        if (splitresult[4] == "50" && splitresult[5] == "1") { fs1 = PdfFontStyle.Italic; }
                                        if (splitresult[4] == "75" && splitresult[5] == "0") { fs1 = PdfFontStyle.Bold; }
                                        if (splitresult[4] == "75" && splitresult[5] == "1") { fs1 = PdfFontStyle.Bold | PdfFontStyle.Italic; }

                                        string linuxttfpath = "/usr/share/fonts/truetype/msttcorefonts/" + dataTableFontName.ToLower() + ".ttf";
                                        FileStream fontStream = null;
                                        if (System.IO.File.Exists(linuxttfpath))
                                        {
                                            fontStream = new FileStream(linuxttfpath, FileMode.Open, FileAccess.Read);
                                        }
                                        else
                                        {
                                            fontStream = new FileStream("Arial.ttf", FileMode.Open, FileAccess.Read);
                                        }
                                        dataTableRowAlternateFont = new PdfTrueTypeFont(fontStream, dataTableFontSize, fs1);

                                        //font color
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/FontColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/FontColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/FontColor/@blue").InnerText);
                                        dataTableRowAlternateFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                        //fill color if not transparent
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/@background").InnerText != "false")
                                        {
                                            rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/BackgroundColor/@alpha").InnerText);
                                            rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/BackgroundColor/@red").InnerText);
                                            rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/BackgroundColor/@green").InnerText);
                                            rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/BackgroundColor/@blue").InnerText);
                                            dataTableRowAlternateBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue));
                                        }

                                        //border color
                                        rgbAlpha = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/FrameColor/@alpha").InnerText);
                                        rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/FrameColor/@red").InnerText);
                                        rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/FrameColor/@green").InnerText);
                                        rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/FrameColor/@blue").InnerText);
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position") != null)
                                        {
                                            string dataOutlineWidthM = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppDataTableRowAlternateStyle']/@outlineWidthM").InnerText;
                                            charSeparators = new char[] { ',' };
                                            splitresult = dataOutlineWidthM.Split(charSeparators);
                                            dataTableRowAlternatePenWidth = System.Convert.ToSingle(splitresult[0]);
                                        }
                                        dataTableRowAlternatePen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue), dataTableRowAlternatePenWidth);
                                    }

                                    //Data Table position
                                    //Set defaults
                                    string DataTableX = "0";
                                    string DataTableY = "0";
                                    //Set Map Image position from template
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position") != null)
                                    {
                                        string dataPosition = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@position").InnerText;
                                        charSeparators = new char[] { ',' };
                                        splitresult = dataPosition.Split(charSeparators);
                                        DataTableX = splitresult[0];
                                        DataTableY = splitresult[1];
                                    }

                                    //Data Table Size
                                    //Set defaults
                                    string DataTableWidth = "0";
                                    string DataTableHeight = "0";
                                    //Set Map Image Size from template
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@size") != null)
                                    {
                                        string dataSize = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppData']/@size").InnerText;
                                        charSeparators = new char[] { ',' };
                                        splitresult = dataSize.Split(charSeparators);
                                        DataTableWidth = splitresult[0];
                                        DataTableHeight = splitresult[1];
                                    }

                                    Syncfusion.Drawing.SizeF dataSize1 = new Syncfusion.Drawing.SizeF(convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(DataTableHeight), mm, pt) - 15);

                                    //Create header / footer for extra data pages resulting from pagination - this stops the table from sitting hard against top and bottom of the page
                                    Syncfusion.Drawing.RectangleF rectHeader = new Syncfusion.Drawing.RectangleF(0, 0, convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt), convertor1.ConvertUnits(10f, mm, pt));
                                    Syncfusion.Drawing.RectangleF rectFooter = new Syncfusion.Drawing.RectangleF(0, 0, convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt), convertor1.ConvertUnits(10f, mm, pt));
                                    PdfPageTemplateElement header = new PdfPageTemplateElement(rectHeader);
                                    PdfPageTemplateElement footer = new PdfPageTemplateElement(rectFooter);
                                    //Add the header at the top
                                    PDFDoc1.Template.Top = header;
                                    if (PDFDoc1.Template.Bottom == null)
                                    {
                                        PDFDoc1.Template.Bottom = footer;
                                    }



                                    //Initiate and set variables used to determine end of tables
                                    PdfLightTableLayoutResult TableLayoutResult1;
                                    Syncfusion.Drawing.PointF TableLayoutLocation = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(DataTableX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(DataTableY), mm, pt));
                                    Syncfusion.Drawing.PointF TableLayoutLocationInitial = new Syncfusion.Drawing.PointF(convertor1.ConvertUnits(System.Convert.ToSingle(DataTableX), mm, pt), rectHeader.Y);
                                    PdfPage TableLayoutPage = page;


                                    //......................................................................................................................................
                                    //....  Get Data
                                    //......................................................................................................................................
                                    if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables") != null)
                                    {
                                        //SQL Data Table
                                        if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable") != null)
                                        {
                                            //iterate through each SQL data table
                                            XmlNodeList XmlNodeListSQLData = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable");
                                            for (int iSQLData = 1; iSQLData <= XmlNodeListSQLData.Count; iSQLData++)
                                            {

                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/SQL") != null)
                                                {
                                                    string dataConnection = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/SQL/@connection").InnerText;
                                                    string dataSQL = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/SQL").InnerText;

                                                    Query1 = "" + dataSQL;

                                                    DataSet ds2;
                                                    Connection1 = dataConnection;
                                                    ds2 = GetDataSetConnName(Query1, Connection1);

                                                    //Need to check if any tables returned by getdata here
                                                    if (ds2.Tables.Count > 0)
                                                    {

                                                        if (ds2.Tables[0].Rows.Count > 0)
                                                        {
                                                            //......................................................................................................................................
                                                            //....  Title table
                                                            //......................................................................................................................................
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@caption") != null)
                                                            {
                                                                string tableTitleCaption = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@caption").InnerText;
                                                                PdfLightTable tableTitle = new PdfLightTable();

                                                                tableTitle.Style.CellPadding = 2;

                                                                // Set the DataSourceType as Direct
                                                                tableTitle.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                // Create Columns
                                                                tableTitle.Columns.Add(new PdfColumn(tableTitleCaption));

                                                                // Add Rows
                                                                tableTitle.Rows.Add(new object[] { tableTitleCaption });

                                                                // Create default cell style
                                                                PdfCellStyle tableTitleStyle = new PdfCellStyle(dataTableTitleFont, dataTableTitleFontColour, dataTableTitlePen);
                                                                tableTitleStyle.BorderPen = PenTransparent;
                                                                tableTitle.Style.DefaultStyle = tableTitleStyle;
                                                                tableTitle.Style.DefaultStyle.BackgroundBrush = dataTableTitleBrush;
                                                                tableTitle.Style.ShowHeader = false;

                                                                // Set Format
                                                                PdfLightTableLayoutFormat tableTitleFormat = new PdfLightTableLayoutFormat();
                                                                tableTitleFormat.Layout = PdfLayoutType.Paginate;

                                                                // Draw the PdfLightTable
                                                                try
                                                                {
                                                                    TableLayoutResult1 = tableTitle.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableTitleFormat);
                                                                }
                                                                catch
                                                                {
                                                                    // Catch error "Can't draw table, because there is not enough space for it" 
                                                                    // add table to a new page
                                                                    PdfPage page1 = PDFDoc1.Pages.Add();
                                                                    TableLayoutResult1 = tableTitle.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableTitleFormat);
                                                                }
                                                                TableLayoutPage = TableLayoutResult1.Page;
                                                                // Get the location where the table ends
                                                                TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 1);
                                                            }
                                                            //......................................................................................................................................
                                                            //....  Data Description Table
                                                            //......................................................................................................................................
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@description") != null)
                                                            {
                                                                string descriptionmessage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@description").InnerText;
                                                                PdfLightTable tableDataDescription = new PdfLightTable();

                                                                tableDataDescription.Style.CellPadding = 2;

                                                                // Set the DataSourceType as Direct
                                                                tableDataDescription.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                tableDataDescription.Columns.Add(new PdfColumn(""));
                                                                tableDataDescription.Columns[0].Width = convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt);
                                                                // Add Rows
                                                                tableDataDescription.Rows.Add(new object[] { "" + descriptionmessage });

                                                                // Create default cell style
                                                                PdfCellStyle tableDataStyle = new PdfCellStyle(dataTableDescriptionFont, dataTableDescriptionFontColour, dataTableDescriptionPen);
                                                                tableDataStyle.BorderPen = PenTransparent;
                                                                tableDataStyle.BackgroundBrush = dataTableDescriptionBrush;
                                                                tableDataDescription.Style.DefaultStyle = tableDataStyle;
                                                                tableDataDescription.Style.ShowHeader = false;

                                                                // Set Format
                                                                PdfLightTableLayoutFormat tableDataFormat = new PdfLightTableLayoutFormat();
                                                                tableDataFormat.Layout = PdfLayoutType.Paginate;

                                                                // Draw the PdfLightTable
                                                                try
                                                                {
                                                                    TableLayoutResult1 = tableDataDescription.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                catch
                                                                {
                                                                    // Catch error "Can't draw table, because there is not enough space for it"
                                                                    // add table on a new page
                                                                    PdfPage page1 = PDFDoc1.Pages.Add();
                                                                    TableLayoutResult1 = tableDataDescription.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                TableLayoutPage = TableLayoutResult1.Page;
                                                                // Get location where the table ends
                                                                TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom);
                                                            }

                                                            //......................................................................................................................................
                                                            //....  Data Table
                                                            //......................................................................................................................................
                                                            PdfLightTable tableData = new PdfLightTable();

                                                            tableData.Style.CellPadding = 2;
                                                            tableData.Style.BorderPen = dataTableRowDefaultPen;

                                                            tableData.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                            PdfColumnCollection tableColumns = tableData.Columns;

                                                            foreach (DataColumn col in ds2.Tables[0].Columns)
                                                            {
                                                                tableColumns.Add(new PdfColumn(col.ColumnName));
                                                            }
                                                            List<object> rowlist = new List<object>();
                                                            foreach (DataRow row in ds2.Tables[0].Rows)
                                                            {
                                                                //convert allitems in array to string
                                                                tableData.Rows.Add(row.ItemArray.Select(i => i.ToString()).ToArray());
                                                            }
                                                            tableData.DataSource = rowlist;

                                                            tableData.Style.HeaderSource = PdfHeaderSource.ColumnCaptions;
                                                            tableData.Style.ShowHeader = true;
                                                            tableData.Style.RepeatHeader = true;


                                                            // Create header cell style
                                                            PdfCellStyle headerStyle = new PdfCellStyle(dataTableHeadingFont, dataTableHeadingFontColour, dataTableHeadingPen);
                                                            PdfBrush brushTableHeader = dataTableHeadingBrush;
                                                            headerStyle.BackgroundBrush = brushTableHeader;
                                                            headerStyle.BorderPen = PenTransparent;
                                                            tableData.Style.HeaderStyle = headerStyle;
                                                            tableData.Style.HeaderStyle.BackgroundBrush = dataTableHeadingBrush;

                                                            // Create default cell style
                                                            PdfCellStyle defaultStyle = new PdfCellStyle(dataTableRowDefaultFont, dataTableRowDefaultFontColour, dataTableRowDefaultPen);
                                                            defaultStyle.BorderPen = PenTransparent;
                                                            defaultStyle.BackgroundBrush = dataTableRowDefaultBrush;
                                                            tableData.Style.DefaultStyle = defaultStyle;

                                                            // Create alternate cell style
                                                            PdfCellStyle alternateStyle = new PdfCellStyle(dataTableRowAlternateFont, dataTableRowAlternateFontColour, dataTableRowAlternatePen);
                                                            PdfBrush brushTableAlternate = dataTableRowAlternateBrush;
                                                            alternateStyle.BackgroundBrush = brushTableAlternate;
                                                            alternateStyle.BorderPen = PenTransparent;
                                                            tableData.Style.AlternateStyle = alternateStyle;

                                                            // Set column widths
                                                            // Check if colwidths available in config
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/SQL/@colwidths") != null)
                                                            {
                                                                string colwidthsList = xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/SQL/@colwidths").InnerText;
                                                                char[] colwidthsListDelim = new char[] { ',' };
                                                                string[] colwidths;
                                                                colwidths = colwidthsList.Split(colwidthsListDelim);
                                                                // check column counts match
                                                                if (colwidths.Length == tableColumns.Count)
                                                                {
                                                                    // check if add up to 100
                                                                    float colwidthValueSum = 0;
                                                                    Array.ForEach(colwidths, delegate (string colwidthValue) { colwidthValueSum += System.Convert.ToSingle(colwidthValue); });
                                                                    if (colwidthValueSum == 100)
                                                                    {
                                                                        for (int iColumn = 0; iColumn < colwidths.Length; iColumn++)
                                                                        {
                                                                            float colwidthPercent = System.Convert.ToSingle(colwidths[iColumn]); // percentage of width
                                                                            float colwidthmm = System.Convert.ToSingle(DataTableWidth) * (colwidthPercent / 100);
                                                                            tableColumns[iColumn].Width = convertor1.ConvertUnits(System.Convert.ToSingle(colwidthmm), mm, pt);
                                                                        }
                                                                    }
                                                                }
                                                            }

                                                            // Set Format
                                                            PdfLightTableLayoutFormat format = new PdfLightTableLayoutFormat();
                                                            format.Layout = PdfLayoutType.Paginate;

                                                            // Draw the table
                                                            try
                                                            {
                                                                TableLayoutResult1 = tableData.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, format);
                                                            }
                                                            catch
                                                            {
                                                                //Catch error "Can't draw table, because there is not enough space for it" 
                                                                // add table on a new page
                                                                PdfPage page1 = PDFDoc1.Pages.Add();
                                                                TableLayoutResult1 = tableData.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, format);
                                                            }
                                                            TableLayoutPage = TableLayoutResult1.Page;
                                                            // Get location where the table ends
                                                            TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 15);
                                                        }
                                                        else
                                                        {
                                                            // No Data
                                                            // Check if nodata message available
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@nodata") != null)
                                                            {
                                                                //......................................................................................................................................
                                                                //....  Title table
                                                                //......................................................................................................................................
                                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@caption") != null)
                                                                {
                                                                    string tableTitleCaption = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@caption").InnerText;

                                                                    PdfLightTable tableTitle = new PdfLightTable();

                                                                    tableTitle.Style.CellPadding = 2;

                                                                    // Set the DataSourceType as Direct
                                                                    tableTitle.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                    // Create Columns
                                                                    tableTitle.Columns.Add(new PdfColumn(tableTitleCaption));

                                                                    // Add Rows
                                                                    tableTitle.Rows.Add(new object[] { tableTitleCaption });

                                                                    // Create default cell style
                                                                    PdfCellStyle tableTitleStyle = new PdfCellStyle(dataTableTitleFont, dataTableTitleFontColour, dataTableTitlePen);
                                                                    tableTitleStyle.BorderPen = PenTransparent;
                                                                    tableTitleStyle.BackgroundBrush = dataTableTitleBrush;
                                                                    tableTitle.Style.DefaultStyle = tableTitleStyle;
                                                                    tableTitle.Style.ShowHeader = false;

                                                                    // Set Format
                                                                    PdfLightTableLayoutFormat tableTitleFormat = new PdfLightTableLayoutFormat();
                                                                    tableTitleFormat.Layout = PdfLayoutType.Paginate;

                                                                    // Drawing the PdfLightTable
                                                                    try
                                                                    {
                                                                        TableLayoutResult1 = tableTitle.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableTitleFormat);
                                                                    }
                                                                    catch
                                                                    {
                                                                        //Catch error "Can't draw table, because there is not enough space for it" 
                                                                        // add table on a new page
                                                                        PdfPage page1 = PDFDoc1.Pages.Add();
                                                                        TableLayoutResult1 = tableTitle.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableTitleFormat);
                                                                    }
                                                                    TableLayoutPage = TableLayoutResult1.Page;
                                                                    // Get location where the table ends
                                                                    TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 1);
                                                                }

                                                                //......................................................................................................................................
                                                                //....  Message Table
                                                                //......................................................................................................................................
                                                                string nodatamessage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/SQLDataTable[position()=" + iSQLData + "]/@nodata").InnerText;
                                                                PdfLightTable tableData = new PdfLightTable();

                                                                tableData.Style.CellPadding = 2;

                                                                // Set the DataSourceType as Direct
                                                                tableData.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                tableData.Columns.Add(new PdfColumn(""));
                                                                tableData.Columns[0].Width = convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt);
                                                                // Add Rows
                                                                tableData.Rows.Add(new object[] { "" + nodatamessage });

                                                                // Create default cell style
                                                                PdfCellStyle tableDataStyle = new PdfCellStyle(dataTableDescriptionFont, dataTableDescriptionFontColour, dataTableDescriptionPen);
                                                                tableDataStyle.BorderPen = PenTransparent;
                                                                tableDataStyle.BackgroundBrush = dataTableBrush;
                                                                tableData.Style.DefaultStyle = tableDataStyle;
                                                                tableData.Style.ShowHeader = false;

                                                                // Set Format
                                                                PdfLightTableLayoutFormat tableDataFormat = new PdfLightTableLayoutFormat();
                                                                tableDataFormat.Layout = PdfLayoutType.Paginate;

                                                                // Drawing the PdfLightTable
                                                                try
                                                                {
                                                                    TableLayoutResult1 = tableData.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                catch
                                                                {
                                                                    // Catch error "Can't draw table, because there is not enough space for it" 
                                                                    // add table on a new page
                                                                    PdfPage page1 = PDFDoc1.Pages.Add();
                                                                    TableLayoutResult1 = tableData.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                TableLayoutPage = TableLayoutResult1.Page;
                                                                // Get location where the table ends
                                                                TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 15);

                                                            }
                                                        }
                                                    }
                                                    ds2.Dispose();
                                                }
                                            }
                                        }

                                        //JSON Data Tables
                                        if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable") != null)
                                        {
                                            // iterate through each JSON data table
                                            XmlNodeList XmlNodeListJSON = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable");
                                            for (int iJSON = 1; iJSON <= XmlNodeListJSON.Count; iJSON++)
                                            {
                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@url") != null)
                                                {
                                                    string dataURL = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@url").InnerText;

                                                    dataURL = "" + dataURL.Replace("@featurekey", FeatureKeys);
                                                    dataURL = "" + dataURL.Replace("@databasekey", DatabaseKey);
                                                    dataURL = "" + dataURL.Replace("@referencekey", ReferenceKey);

                                                    DataSet dsJSON = null;
                                                    if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@username") != null && xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@password") != null)
                                                    {
                                                        string username = xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@username").InnerText;
                                                        string password = xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@password").InnerText;
                                                        if (username != "" && password != "")
                                                        {
                                                            dsJSON = GetDataSetJSON(dataURL, username, password);
                                                        }
                                                        else
                                                        {
                                                            dsJSON = GetDataSetJSON(dataURL);
                                                        }

                                                    }
                                                    else
                                                    {
                                                        dsJSON = GetDataSetJSON(dataURL);
                                                    }

                                                    if (dsJSON.Tables.Count > 0)
                                                    {
                                                        if (dsJSON.Tables[0].Rows.Count > 0)
                                                        {

                                                            //......................................................................................................................................
                                                            //....  Title table
                                                            //......................................................................................................................................
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@caption") != null)
                                                            {
                                                                string tableTitleCaption = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@caption").InnerText;
                                                                PdfLightTable tableTitle = new PdfLightTable();

                                                                tableTitle.Style.CellPadding = 2;

                                                                // Set the DataSourceType as Direct
                                                                tableTitle.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                // Create Columns
                                                                tableTitle.Columns.Add(new PdfColumn(tableTitleCaption));

                                                                // Add Rows
                                                                tableTitle.Rows.Add(new object[] { tableTitleCaption });

                                                                //Create default cell style
                                                                PdfCellStyle tableTitleStyle = new PdfCellStyle(dataTableTitleFont, dataTableTitleFontColour, dataTableTitlePen);
                                                                tableTitleStyle.BorderPen = PenTransparent;
                                                                tableTitle.Style.DefaultStyle = tableTitleStyle;
                                                                tableTitle.Style.ShowHeader = false;

                                                                // Set Format
                                                                PdfLightTableLayoutFormat tableTitleFormat = new PdfLightTableLayoutFormat();
                                                                tableTitleFormat.Layout = PdfLayoutType.Paginate;

                                                                // Draw the PdfLightTable
                                                                try
                                                                {
                                                                    TableLayoutResult1 = tableTitle.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableTitleFormat);
                                                                }
                                                                catch
                                                                {
                                                                    // Catch error "Can't draw table, because there is not enough space for it" 
                                                                    // add table on a new page
                                                                    PdfPage page1 = PDFDoc1.Pages.Add();
                                                                    TableLayoutResult1 = tableTitle.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableTitleFormat);
                                                                }
                                                                TableLayoutPage = TableLayoutResult1.Page;
                                                                // Get location where the table ends
                                                                TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 1);
                                                            }
                                                            //......................................................................................................................................
                                                            //....  Data Description Table
                                                            //......................................................................................................................................
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@description") != null)
                                                            {
                                                                string descriptionmessage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@description").InnerText;
                                                                PdfLightTable tableDataDescription = new PdfLightTable();

                                                                tableDataDescription.Style.CellPadding = 2;

                                                                // Set the DataSourceType as Direct
                                                                tableDataDescription.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                tableDataDescription.Columns.Add(new PdfColumn(""));
                                                                tableDataDescription.Columns[0].Width = convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt);
                                                                // Add Rows
                                                                tableDataDescription.Rows.Add(new object[] { "" + descriptionmessage });

                                                                // Create default cell style
                                                                PdfCellStyle tableDataStyle = new PdfCellStyle(dataTableDescriptionFont, dataTableDescriptionFontColour, dataTableDescriptionPen);
                                                                tableDataStyle.BorderPen = PenTransparent;
                                                                tableDataDescription.Style.DefaultStyle = tableDataStyle;
                                                                tableDataDescription.Style.ShowHeader = false;

                                                                // Set Format
                                                                PdfLightTableLayoutFormat tableDataFormat = new PdfLightTableLayoutFormat();
                                                                tableDataFormat.Layout = PdfLayoutType.Paginate;

                                                                // Draw the PdfLightTable
                                                                try
                                                                {
                                                                    TableLayoutResult1 = tableDataDescription.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                catch
                                                                {
                                                                    // Catch error "Can't draw table, because there is not enough space for it" 
                                                                    // add table on a new page
                                                                    PdfPage page1 = PDFDoc1.Pages.Add();
                                                                    TableLayoutResult1 = tableDataDescription.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                TableLayoutPage = TableLayoutResult1.Page;
                                                                // Get location where the table ends
                                                                TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom);
                                                            }

                                                            //......................................................................................................................................
                                                            //....  Data Table
                                                            //......................................................................................................................................
                                                            PdfLightTable tableData = new PdfLightTable();

                                                            tableData.Style.CellPadding = 2;
                                                            tableData.Style.BorderPen = dataTableRowDefaultPen;

                                                            tableData.DataSource = dsJSON.Tables[0];

                                                            tableData.Style.HeaderSource = PdfHeaderSource.ColumnCaptions;
                                                            tableData.Style.ShowHeader = true;
                                                            tableData.Style.RepeatHeader = true;

                                                            // Create header cell style
                                                            PdfCellStyle headerStyle = new PdfCellStyle(dataTableHeadingFont, dataTableHeadingFontColour, dataTableHeadingPen);
                                                            PdfBrush brushTableHeader = dataTableHeadingBrush;
                                                            headerStyle.BackgroundBrush = brushTableHeader;
                                                            headerStyle.BorderPen = PenTransparent;
                                                            tableData.Style.HeaderStyle = headerStyle;

                                                            // Create default cell style
                                                            PdfCellStyle defaultStyle = new PdfCellStyle(dataTableRowDefaultFont, dataTableRowDefaultFontColour, dataTableRowDefaultPen);
                                                            defaultStyle.BorderPen = PenTransparent;
                                                            tableData.Style.DefaultStyle = defaultStyle;

                                                            // Create alternate cell style
                                                            PdfCellStyle alternateStyle = new PdfCellStyle(dataTableRowAlternateFont, dataTableRowAlternateFontColour, dataTableRowAlternatePen);
                                                            PdfBrush brushTableAlternate = dataTableRowAlternateBrush;
                                                            alternateStyle.BackgroundBrush = brushTableAlternate;
                                                            alternateStyle.BorderPen = PenTransparent;
                                                            tableData.Style.AlternateStyle = alternateStyle;

                                                            // Set column widths
                                                            PdfColumnCollection tableColumns = tableData.Columns;
                                                            // Check if colwidths available
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@colwidths") != null)
                                                            {
                                                                string colwidthsList = xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/JSON/@colwidths").InnerText;
                                                                char[] colwidthsListDelim = new char[] { ',' };
                                                                string[] colwidths;
                                                                colwidths = colwidthsList.Split(colwidthsListDelim);
                                                                // check column counts match
                                                                if (colwidths.Length == tableColumns.Count)
                                                                {
                                                                    // check if add up to 100
                                                                    float colwidthValueSum = 0;
                                                                    Array.ForEach(colwidths, delegate (string colwidthValue) { colwidthValueSum += System.Convert.ToSingle(colwidthValue); });
                                                                    if (colwidthValueSum == 100)
                                                                    {
                                                                        for (int iColumn = 0; iColumn < colwidths.Length; iColumn++)
                                                                        {
                                                                            float colwidthPercent = System.Convert.ToSingle(colwidths[iColumn]); // percentage of width
                                                                            float colwidthmm = System.Convert.ToSingle(DataTableWidth) * (colwidthPercent / 100);
                                                                            tableColumns[iColumn].Width = convertor1.ConvertUnits(System.Convert.ToSingle(colwidthmm), mm, pt);
                                                                        }
                                                                    }
                                                                }
                                                            }

                                                            // Set Format
                                                            PdfLightTableLayoutFormat format = new PdfLightTableLayoutFormat();
                                                            format.Layout = PdfLayoutType.Paginate;

                                                            // Draw table
                                                            try
                                                            {
                                                                TableLayoutResult1 = tableData.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, format);
                                                            }
                                                            catch
                                                            {
                                                                // Catch error "Can't draw table, because there is not enough space for it" 
                                                                // add table on a new page
                                                                PdfPage page1 = PDFDoc1.Pages.Add();
                                                                TableLayoutResult1 = tableData.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, format);
                                                            }
                                                            TableLayoutPage = TableLayoutResult1.Page;
                                                            // Get location where the table ends
                                                            TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 15);
                                                        }
                                                        else
                                                        {
                                                            // No Data
                                                            // Check if nodata message available
                                                            if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@nodata") != null)
                                                            {
                                                                //......................................................................................................................................
                                                                //....  Title table
                                                                //......................................................................................................................................
                                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@caption") != null)
                                                                {
                                                                    string tableTitleCaption = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@caption").InnerText;

                                                                    PdfLightTable tableTitle = new PdfLightTable();

                                                                    tableTitle.Style.CellPadding = 2;

                                                                    // Set the DataSourceType as Direct
                                                                    tableTitle.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                    // Create Columns
                                                                    tableTitle.Columns.Add(new PdfColumn(tableTitleCaption));

                                                                    // Add Rows
                                                                    tableTitle.Rows.Add(new object[] { tableTitleCaption });

                                                                    // Create default cell style
                                                                    PdfCellStyle tableTitleStyle = new PdfCellStyle(dataTableTitleFont, dataTableTitleFontColour, dataTableTitlePen);
                                                                    tableTitleStyle.BorderPen = PenTransparent;
                                                                    tableTitle.Style.DefaultStyle = tableTitleStyle;
                                                                    tableTitle.Style.ShowHeader = false;

                                                                    // Set Format
                                                                    PdfLightTableLayoutFormat tableTitleFormat = new PdfLightTableLayoutFormat();
                                                                    tableTitleFormat.Layout = PdfLayoutType.Paginate;

                                                                    // Draw PdfLightTable
                                                                    try
                                                                    {
                                                                        TableLayoutResult1 = tableTitle.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableTitleFormat);
                                                                    }
                                                                    catch
                                                                    {
                                                                        // Catch error "Can't draw table, because there is not enough space for it" 
                                                                        // add table on a new page
                                                                        PdfPage page1 = PDFDoc1.Pages.Add();
                                                                        TableLayoutResult1 = tableTitle.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableTitleFormat);
                                                                    }
                                                                    TableLayoutPage = TableLayoutResult1.Page;
                                                                    // Get location where the table ends
                                                                    TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 1);
                                                                }

                                                                //......................................................................................................................................
                                                                //....  Message Table
                                                                //......................................................................................................................................
                                                                string nodatamessage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/JSONDataTable[position()=" + iJSON + "]/@nodata").InnerText;
                                                                PdfLightTable tableData = new PdfLightTable();

                                                                tableData.Style.CellPadding = 2;

                                                                // Set the DataSourceType as Direct
                                                                tableData.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                tableData.Columns.Add(new PdfColumn(""));
                                                                tableData.Columns[0].Width = convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt);
                                                                // Add Rows
                                                                tableData.Rows.Add(new object[] { "" + nodatamessage });

                                                                // Create default cell style
                                                                PdfCellStyle tableDataStyle = new PdfCellStyle(dataTableDescriptionFont, dataTableDescriptionFontColour, dataTableDescriptionPen);
                                                                tableDataStyle.BorderPen = PenTransparent;
                                                                tableData.Style.DefaultStyle = tableDataStyle;
                                                                tableData.Style.ShowHeader = false;

                                                                // Set Format
                                                                PdfLightTableLayoutFormat tableDataFormat = new PdfLightTableLayoutFormat();
                                                                tableDataFormat.Layout = PdfLayoutType.Paginate;

                                                                // Drawing the PdfLightTable
                                                                try
                                                                {
                                                                    TableLayoutResult1 = tableData.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                catch
                                                                {
                                                                    // Catch error "Can't draw table, because there is not enough space for it" 
                                                                    // add table on a new page
                                                                    PdfPage page1 = PDFDoc1.Pages.Add();
                                                                    TableLayoutResult1 = tableData.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableDataFormat);
                                                                }
                                                                TableLayoutPage = TableLayoutResult1.Page;
                                                                // Get location where the table ends
                                                                TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 15);

                                                            }


                                                        }
                                                    }
                                                    dsJSON.Dispose();
                                                }
                                            }
                                        }

                                        //OGC WFS Data (Many)
                                        if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable") != null)
                                        {
                                            // iterate through each OGC WFS data table
                                            XmlNodeList XmlNodeListOGCWFS = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable");
                                            for (int iOGCWFS = 1; iOGCWFS <= XmlNodeListOGCWFS.Count; iOGCWFS++)
                                            {
                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/URI") != null)
                                                {
                                                    string dataURL = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/URI").InnerText;

                                                    dataURL = "" + dataURL.Replace("@featurekey", FeatureKeys);
                                                    dataURL = "" + dataURL.Replace("@databasekey", DatabaseKey);
                                                    dataURL = "" + dataURL.Replace("@referencekey", ReferenceKey);


                                                    string propertyName = "";
                                                    string querystring = dataURL.Substring(dataURL.IndexOf('?'));
                                                    if (System.Web.HttpUtility.ParseQueryString(querystring).GetValues("propertyName") != null)
                                                    {
                                                        propertyName = System.Web.HttpUtility.ParseQueryString(querystring).GetValues("propertyName")[0].ToString();
                                                    }

                                                    /*
                                                    example
                                                    return encumbrances
                                                    https://data.linz.govt.nz/services;key=dd4308b96a1743a195b4e9044fb70313/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=table-51695&cql_filter=title_no=325429
                                                    https://data.linz.govt.nz/services;key=dd4308b96a1743a195b4e9044fb70313/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=table-51695&PropertyName=title_no,memorial_text&cql_filter=title_no=@featurekey
                                                URI=https://data.linz.govt.nz/services;key=dd4308b96a1743a195b4e9044fb70313/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=table-51695&PropertyName=title_no,memorial_text&cql_filter=title_no=325429
*/

                                                    // Initialise ogr datasource
                                                    DataSource dsOgr = null;
                                                    // Initialise ogr layer
                                                    Layer layer1 = null;

                                                    statusCode = GetHttpClientStatusCode(dataURL);

                                                    if (statusCode >= 100 && statusCode < 400)
                                                    {
                                                        Ogr.RegisterAll();

                                                        //skip SSL host / certificate verification if https url
                                                        if (dataURL.StartsWith("https"))
                                                        {
                                                            if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                            {
                                                                Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                            }
                                                        }

                                                        dsOgr = Ogr.Open("" + dataURL, 0);
                                                        layer1 = dsOgr.GetLayerByIndex(0);

                                                        if (propertyName != "")
                                                        {
                                                            string FeatureSQL = "SELECT \"" + propertyName.Replace(",", "\", \"") + "\" FROM \"" + layer1.GetName() + "\"";
                                                            layer1 = dsOgr.ExecuteSQL(FeatureSQL, null, "SQLITE");
                                                        }

                                                    }

                                                    if (layer1 != null)
                                                    {
                                                        DataSet dsOGCWFS = new DataSet();

                                                        layer1.ResetReading();
                                                        Feature feature1 = null;

                                                        // create table for dataset
                                                        var dataTable1 = new DataTable();


                                                        int featureIndex = 0;
                                                        for (featureIndex = 0; featureIndex < layer1.GetFeatureCount(1); featureIndex++)
                                                        {
                                                            layer1.SetNextByIndex(featureIndex);
                                                            feature1 = layer1.GetNextFeature();

                                                            // create data columns
                                                            if (featureIndex == 0)
                                                            {
                                                                int fieldIndex = 0;
                                                                for (fieldIndex = 0; fieldIndex < feature1.GetFieldCount(); fieldIndex++)
                                                                {

                                                                    dataTable1.Columns.Add(new DataColumn(feature1.GetFieldDefnRef(fieldIndex).GetName()));

                                                                }
                                                            }

                                                            var row = dataTable1.NewRow();
                                                            //populate data column values for each feature
                                                            for (int iField = 0; iField < feature1.GetFieldCount(); iField++)
                                                            {
                                                                row[iField] = "" + feature1.GetFieldAsString(iField).ToString();
                                                            }
                                                            dataTable1.Rows.Add(row);

                                                            layer1.DeleteFeature(featureIndex);
                                                        }

                                                        dsOGCWFS.Tables.Add(dataTable1);

                                                        if (dsOGCWFS.Tables.Count > 0)
                                                        {
                                                            if (dsOGCWFS.Tables[0].Rows.Count > 0)
                                                            {
                                                                //......................................................................................................................................
                                                                //....  Title table
                                                                //......................................................................................................................................
                                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@caption") != null)
                                                                {
                                                                    string tableTitleCaption = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@caption").InnerText;
                                                                    PdfLightTable tableTitle = new PdfLightTable();

                                                                    tableTitle.Style.CellPadding = 2;

                                                                    // Set the DataSourceType as Direct
                                                                    tableTitle.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                    // Create Columns
                                                                    tableTitle.Columns.Add(new PdfColumn(tableTitleCaption));

                                                                    // Add Rows
                                                                    tableTitle.Rows.Add(new object[] { tableTitleCaption });

                                                                    //Create default cell style
                                                                    PdfCellStyle tableTitleStyle = new PdfCellStyle(dataTableTitleFont, dataTableTitleFontColour, dataTableTitlePen);
                                                                    tableTitleStyle.BorderPen = PenTransparent;
                                                                    tableTitle.Style.DefaultStyle = tableTitleStyle;
                                                                    tableTitle.Style.ShowHeader = false;

                                                                    // Set Format
                                                                    PdfLightTableLayoutFormat tableTitleFormat = new PdfLightTableLayoutFormat();
                                                                    tableTitleFormat.Layout = PdfLayoutType.Paginate;

                                                                    // Draw the PdfLightTable
                                                                    try
                                                                    {
                                                                        TableLayoutResult1 = tableTitle.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableTitleFormat);
                                                                    }
                                                                    catch
                                                                    {
                                                                        // Catch error "Can't draw table, because there is not enough space for it" 
                                                                        // add table on a new page
                                                                        PdfPage page1 = PDFDoc1.Pages.Add();
                                                                        TableLayoutResult1 = tableTitle.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableTitleFormat);
                                                                    }
                                                                    TableLayoutPage = TableLayoutResult1.Page;
                                                                    // Get location where the table ends
                                                                    TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 1);
                                                                }
                                                                //......................................................................................................................................
                                                                //....  Data Description Table
                                                                //......................................................................................................................................
                                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@description") != null)
                                                                {
                                                                    string descriptionmessage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@description").InnerText;
                                                                    PdfLightTable tableDataDescription = new PdfLightTable();

                                                                    tableDataDescription.Style.CellPadding = 2;

                                                                    // Set the DataSourceType as Direct
                                                                    tableDataDescription.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                    tableDataDescription.Columns.Add(new PdfColumn(""));
                                                                    tableDataDescription.Columns[0].Width = convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt);
                                                                    // Add Rows
                                                                    tableDataDescription.Rows.Add(new object[] { "" + descriptionmessage });

                                                                    // Create default cell style
                                                                    PdfCellStyle tableDataStyle = new PdfCellStyle(dataTableDescriptionFont, dataTableDescriptionFontColour, dataTableDescriptionPen);
                                                                    tableDataStyle.BorderPen = PenTransparent;
                                                                    tableDataDescription.Style.DefaultStyle = tableDataStyle;
                                                                    tableDataDescription.Style.ShowHeader = false;

                                                                    // Set Format
                                                                    PdfLightTableLayoutFormat tableDataFormat = new PdfLightTableLayoutFormat();
                                                                    tableDataFormat.Layout = PdfLayoutType.Paginate;

                                                                    // Draw the PdfLightTable
                                                                    try
                                                                    {
                                                                        TableLayoutResult1 = tableDataDescription.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableDataFormat);
                                                                    }
                                                                    catch
                                                                    {
                                                                        // Catch error "Can't draw table, because there is not enough space for it" 
                                                                        // add table on a new page
                                                                        PdfPage page1 = PDFDoc1.Pages.Add();
                                                                        TableLayoutResult1 = tableDataDescription.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableDataFormat);
                                                                    }
                                                                    TableLayoutPage = TableLayoutResult1.Page;
                                                                    // Get location where the table ends
                                                                    TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom);
                                                                }

                                                                //......................................................................................................................................
                                                                //....  Data Table
                                                                //......................................................................................................................................
                                                                PdfLightTable tableData = new PdfLightTable();

                                                                tableData.Style.CellPadding = 2;
                                                                tableData.Style.BorderPen = dataTableRowDefaultPen;

                                                                tableData.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                PdfColumnCollection tableColumns = tableData.Columns;

                                                                foreach (DataColumn col in dsOGCWFS.Tables[0].Columns)
                                                                {
                                                                    tableColumns.Add(new PdfColumn(col.ColumnName));
                                                                }
                                                                List<object> rowlist = new List<object>();
                                                                foreach (DataRow row in dsOGCWFS.Tables[0].Rows)
                                                                {
                                                                    tableData.Rows.Add(row.ItemArray);
                                                                }
                                                                tableData.DataSource = rowlist;

                                                                tableData.Style.HeaderSource = PdfHeaderSource.ColumnCaptions;
                                                                tableData.Style.ShowHeader = true;
                                                                tableData.Style.RepeatHeader = true;

                                                                // Create header cell style
                                                                PdfCellStyle headerStyle = new PdfCellStyle(dataTableHeadingFont, dataTableHeadingFontColour, dataTableHeadingPen);
                                                                PdfBrush brushTableHeader = dataTableHeadingBrush;
                                                                headerStyle.BackgroundBrush = brushTableHeader;
                                                                headerStyle.BorderPen = PenTransparent;
                                                                tableData.Style.HeaderStyle = headerStyle;

                                                                // Create default cell style
                                                                PdfCellStyle defaultStyle = new PdfCellStyle(dataTableRowDefaultFont, dataTableRowDefaultFontColour, dataTableRowDefaultPen);
                                                                defaultStyle.BorderPen = PenTransparent;
                                                                tableData.Style.DefaultStyle = defaultStyle;

                                                                // Create alternate cell style
                                                                PdfCellStyle alternateStyle = new PdfCellStyle(dataTableRowAlternateFont, dataTableRowAlternateFontColour, dataTableRowAlternatePen);
                                                                PdfBrush brushTableAlternate = dataTableRowAlternateBrush;
                                                                alternateStyle.BackgroundBrush = brushTableAlternate;
                                                                alternateStyle.BorderPen = PenTransparent;
                                                                tableData.Style.AlternateStyle = alternateStyle;

                                                                // Set column widths
                                                                // Check if colwidths available
                                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/URI/@colwidths") != null)
                                                                {
                                                                    string colwidthsList = xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/URI/@colwidths").InnerText;
                                                                    char[] colwidthsListDelim = new char[] { ',' };
                                                                    string[] colwidths;
                                                                    colwidths = colwidthsList.Split(colwidthsListDelim);
                                                                    // check column counts match
                                                                    if (colwidths.Length == tableColumns.Count)
                                                                    {
                                                                        // check if add up to 100
                                                                        float colwidthValueSum = 0;
                                                                        Array.ForEach(colwidths, delegate (string colwidthValue) { colwidthValueSum += System.Convert.ToSingle(colwidthValue); });
                                                                        if (colwidthValueSum == 100)
                                                                        {
                                                                            for (int iColumn = 0; iColumn < colwidths.Length; iColumn++)
                                                                            {
                                                                                float colwidthPercent = System.Convert.ToSingle(colwidths[iColumn]); // percentage of width
                                                                                float colwidthmm = System.Convert.ToSingle(DataTableWidth) * (colwidthPercent / 100);
                                                                                tableColumns[iColumn].Width = convertor1.ConvertUnits(System.Convert.ToSingle(colwidthmm), mm, pt);
                                                                            }
                                                                        }
                                                                    }
                                                                }

                                                                // Set Format
                                                                PdfLightTableLayoutFormat format = new PdfLightTableLayoutFormat();
                                                                format.Layout = PdfLayoutType.Paginate;

                                                                // Draw table
                                                                try
                                                                {
                                                                    TableLayoutResult1 = tableData.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, format);
                                                                }
                                                                catch
                                                                {
                                                                    // Catch error "Can't draw table, because there is not enough space for it" 
                                                                    // add table on a new page
                                                                    PdfPage page1 = PDFDoc1.Pages.Add();
                                                                    TableLayoutResult1 = tableData.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, format);
                                                                }
                                                                TableLayoutPage = TableLayoutResult1.Page;
                                                                // Get location where the table ends
                                                                TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 15);
                                                            }
                                                            else
                                                            {
                                                                // No Data
                                                                // Check if nodata message available
                                                                if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@nodata") != null)
                                                                {
                                                                    //......................................................................................................................................
                                                                    //....  Title table
                                                                    //......................................................................................................................................
                                                                    if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@caption") != null)
                                                                    {
                                                                        string tableTitleCaption = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@caption").InnerText;

                                                                        PdfLightTable tableTitle = new PdfLightTable();

                                                                        tableTitle.Style.CellPadding = 2;

                                                                        // Set the DataSourceType as Direct
                                                                        tableTitle.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                        // Create Columns
                                                                        tableTitle.Columns.Add(new PdfColumn(tableTitleCaption));

                                                                        // Add Rows
                                                                        tableTitle.Rows.Add(new object[] { tableTitleCaption });

                                                                        // Create default cell style
                                                                        PdfCellStyle tableTitleStyle = new PdfCellStyle(dataTableTitleFont, dataTableTitleFontColour, dataTableTitlePen);
                                                                        tableTitleStyle.BorderPen = PenTransparent;
                                                                        tableTitle.Style.DefaultStyle = tableTitleStyle;
                                                                        tableTitle.Style.ShowHeader = false;

                                                                        // Set Format
                                                                        PdfLightTableLayoutFormat tableTitleFormat = new PdfLightTableLayoutFormat();
                                                                        tableTitleFormat.Layout = PdfLayoutType.Paginate;

                                                                        // Draw PdfLightTable
                                                                        try
                                                                        {
                                                                            TableLayoutResult1 = tableTitle.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableTitleFormat);
                                                                        }
                                                                        catch
                                                                        {
                                                                            // Catch error "Can't draw table, because there is not enough space for it" 
                                                                            // add table on a new page
                                                                            PdfPage page1 = PDFDoc1.Pages.Add();
                                                                            TableLayoutResult1 = tableTitle.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableTitleFormat);
                                                                        }
                                                                        TableLayoutPage = TableLayoutResult1.Page;
                                                                        // Get location where the table ends
                                                                        TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 1);
                                                                    }

                                                                    //......................................................................................................................................
                                                                    //....  Message Table
                                                                    //......................................................................................................................................
                                                                    string nodatamessage = "" + xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/OGCWFSDataTable[position()=" + iOGCWFS + "]/@nodata").InnerText;
                                                                    PdfLightTable tableData = new PdfLightTable();

                                                                    tableData.Style.CellPadding = 2;

                                                                    // Set the DataSourceType as Direct
                                                                    tableData.DataSourceType = PdfLightTableDataSourceType.TableDirect;

                                                                    tableData.Columns.Add(new PdfColumn(""));
                                                                    tableData.Columns[0].Width = convertor1.ConvertUnits(System.Convert.ToSingle(DataTableWidth), mm, pt);
                                                                    // Add Rows
                                                                    tableData.Rows.Add(new object[] { "" + nodatamessage });

                                                                    // Create default cell style
                                                                    PdfCellStyle tableDataStyle = new PdfCellStyle(dataTableDescriptionFont, dataTableDescriptionFontColour, dataTableDescriptionPen);
                                                                    tableDataStyle.BorderPen = PenTransparent;
                                                                    tableData.Style.DefaultStyle = tableDataStyle;
                                                                    tableData.Style.ShowHeader = false;

                                                                    // Set Format
                                                                    PdfLightTableLayoutFormat tableDataFormat = new PdfLightTableLayoutFormat();
                                                                    tableDataFormat.Layout = PdfLayoutType.Paginate;

                                                                    // Drawing the PdfLightTable
                                                                    try
                                                                    {
                                                                        TableLayoutResult1 = tableData.Draw(TableLayoutPage, TableLayoutLocation.X, TableLayoutLocation.Y, dataSize1.Width, tableDataFormat);
                                                                    }
                                                                    catch
                                                                    {
                                                                        // Catch error "Can't draw table, because there is not enough space for it" 
                                                                        // add table on a new page
                                                                        PdfPage page1 = PDFDoc1.Pages.Add();
                                                                        TableLayoutResult1 = tableData.Draw(page1, TableLayoutLocationInitial.X, TableLayoutLocationInitial.Y, dataSize1.Width, tableDataFormat);
                                                                    }
                                                                    TableLayoutPage = TableLayoutResult1.Page;
                                                                    // Get location where the table ends
                                                                    TableLayoutLocation = new Syncfusion.Drawing.PointF(TableLayoutResult1.Bounds.Left, TableLayoutResult1.Bounds.Bottom + 15);

                                                                }


                                                            }
                                                        }
                                                        dsOGCWFS.Dispose();

                                                    }



                                                }
                                            }
                                        }

                                        //ESRI REST Data (Many)
                                        if (xmlConfigDoc1.SelectSingleNode("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/ESRIRESTDataTable") != null)
                                        {
                                            // iterate through each ESRIR EST data table
                                            XmlNodeList XmlNodeListESRIREST = xmlConfigDoc1.SelectNodes("//Pages/Page[position()=" + PageCurrentPos + "]/DataTables/ESRIRESTDataTable");
                                            for (int iESRIREST = 1; iESRIREST <= XmlNodeListESRIREST.Count; iESRIREST++)
                                            {

                                            }
                                        }
                                    }

                                    PDFDoc1.Template.Top = null;
                                }
                                //endDataTables:







                                // Borders
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@shapeType='1']") != null)
                                {
                                    //if more than one border, iterate through creating each border
                                    XmlNodeList XmlNodeListTemplateBorders = QGISLayoutTemplate.SelectNodes("//LayoutItem[@shapeType='1']");
                                    for (int iBorder = 0; iBorder < XmlNodeListTemplateBorders.Count; iBorder++)
                                    {
                                        //Set colour defaults
                                        int rgbAlpha = 0;
                                        int rgbRed = 0;
                                        int rgbGreen = 0;
                                        int rgbBlue = 0;
                                        //Set brush defaults
                                        PdfBrush borderBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.Transparent);
                                        //Set pen defaults
                                        PdfPen borderPen = new PdfPen(Syncfusion.Drawing.Color.Black);
                                        Single borderPenWidth = 0.5f;

                                        //Create fill color if not transparent
                                        if (XmlNodeListTemplateBorders.Item(iBorder).SelectSingleNode("symbol/layer[last()]") != null)
                                        {
                                            // use new QGIS Template symbol layer styles e.g. <prop k="color" v="0,0,255,128"/>
                                            string rgba = XmlNodeListTemplateBorders.Item(iBorder).SelectSingleNode("symbol/layer[last()]/prop[@k='color']/@v").InnerText;
                                            char[] charSeparators = new char[] { ',' };
                                            string[] splitresult;
                                            splitresult = rgba.Split(charSeparators);
                                            if (splitresult[3] != "0")
                                            {
                                                rgbAlpha = System.Convert.ToInt16(splitresult[3]);
                                                rgbRed = System.Convert.ToInt16(splitresult[0]);
                                                rgbGreen = System.Convert.ToInt16(splitresult[1]);
                                                rgbBlue = System.Convert.ToInt16(splitresult[2]);
                                                borderBrush = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue));
                                            }
                                        }

                                        //Create border color
                                        if (XmlNodeListTemplateBorders.Item(iBorder).SelectSingleNode("symbol/layer[last()]") != null)
                                        {
                                            // use new QGIS Template symbol layer styles e.g. <prop k="outline_color" v="255,0,0,255"/>
                                            string rgba = XmlNodeListTemplateBorders.Item(iBorder).SelectSingleNode("symbol/layer[last()]/prop[@k='outline_color']/@v").InnerText;
                                            char[] charSeparators = new char[] { ',' };
                                            string[] splitresult;
                                            splitresult = rgba.Split(charSeparators);
                                            rgbAlpha = System.Convert.ToInt16(splitresult[3]);
                                            rgbRed = System.Convert.ToInt16(splitresult[0]);
                                            rgbGreen = System.Convert.ToInt16(splitresult[1]);
                                            rgbBlue = System.Convert.ToInt16(splitresult[2]);
                                            borderPenWidth = System.Convert.ToSingle(XmlNodeListTemplateBorders.Item(iBorder).SelectSingleNode("symbol/layer[last()]/prop[@k='outline_width']/@v").InnerText);
                                            borderPen = new PdfPen(Syncfusion.Drawing.Color.FromArgb(rgbAlpha, rgbRed, rgbGreen, rgbBlue), borderPenWidth);
                                        }

                                        //Set defaults
                                        string borderX = "0";
                                        string borderY = "0";

                                        if (XmlNodeListTemplateBorders.Item(iBorder).Attributes.GetNamedItem("position") != null)
                                        {
                                            string borderPosition = XmlNodeListTemplateBorders.Item(iBorder).Attributes.GetNamedItem("position").InnerText;
                                            char[] charSeparators = new char[] { ',' };
                                            string[] splitresult;
                                            splitresult = borderPosition.Split(charSeparators);
                                            borderX = splitresult[0];
                                            borderY = splitresult[1];
                                        }

                                        //Set defaults
                                        string borderWidth = "0";
                                        string borderHeight = "0";
                                        if (XmlNodeListTemplateBorders.Item(iBorder).Attributes.GetNamedItem("size") != null)
                                        {
                                            string borderSize = XmlNodeListTemplateBorders.Item(iBorder).Attributes.GetNamedItem("size").InnerText;
                                            char[] charSeparators = new char[] { ',' };
                                            string[] splitresult;
                                            splitresult = borderSize.Split(charSeparators);
                                            borderWidth = splitresult[0];
                                            borderHeight = splitresult[1];
                                        }

                                        //Draw the border
                                        if (XmlNodeListTemplateBorders.Item(iBorder).SelectSingleNode("symbol/layer[last()]/prop[@k='style']/@v").InnerText == "no")
                                        {
                                            //Border with no brush
                                            g.DrawRectangle(borderPen, convertor1.ConvertUnits(System.Convert.ToSingle(borderX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(borderY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(borderWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(borderHeight), mm, pt));
                                        }
                                        else
                                        {
                                            //Border with solid brush
                                            g.DrawRectangle(borderPen, borderBrush, convertor1.ConvertUnits(System.Convert.ToSingle(borderX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(borderY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(borderWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(borderHeight), mm, pt));
                                        }
                                    }
                                }

                                // Images
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppImage']") != null)
                                {
                                    //if more than one image, iterate through creating each image
                                    XmlNodeList XmlNodeListImages = QGISLayoutTemplate.SelectNodes("//LayoutItem[@id='ppImage']");
                                    for (int iImage = 0; iImage < XmlNodeListImages.Count; iImage++)
                                    {
                                        string ImageFile = "" + XmlNodeListImages.Item(iImage).Attributes.GetNamedItem("file").InnerText;
                                        if (ImageFile != "")
                                        {
                                            //Image scaling
                                            //Set defaults
                                            float ScaleImageByValue = 1;

                                            if (XmlNodeListImages.Item(iImage).Attributes.GetNamedItem("outlineWidthM") != null)
                                            {
                                                // use "outlineWidthM" for scale value
                                                string RawScaleImageBy = XmlNodeListImages.Item(iImage).Attributes.GetNamedItem("outlineWidthM").InnerText;
                                                char[] charSeparators = new char[] { ',' };
                                                string[] splitresult;
                                                splitresult = RawScaleImageBy.Split(charSeparators);
                                                ScaleImageByValue = System.Convert.ToSingle(splitresult[0]);
                                            }

                                            //Image position
                                            //Set defaults
                                            string ImagePositionX = "0";
                                            string ImagePositionY = "0";

                                            if (XmlNodeListImages.Item(iImage).Attributes.GetNamedItem("position") != null)
                                            {
                                                string borderPosition = XmlNodeListImages.Item(iImage).Attributes.GetNamedItem("position").InnerText;
                                                char[] charSeparators = new char[] { ',' };
                                                string[] splitresult;
                                                splitresult = borderPosition.Split(charSeparators);
                                                ImagePositionX = splitresult[0];
                                                ImagePositionY = splitresult[1];
                                            }
                                            FileStream fileStream = new FileStream(ImageFile, FileMode.Open, FileAccess.Read);
                                            PdfImage Image1 = new PdfBitmap(fileStream);
                                            g.DrawImage(Image1, convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionY), mm, pt), Image1.Width * ScaleImageByValue, Image1.Height * ScaleImageByValue);
                                        }
                                    }
                                }

                                // Images via SQL
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppImageSQL']") != null)
                                {
                                    //if more than one image, iterate through creating each image
                                    XmlNodeList XmlNodeListImagesSQL = QGISLayoutTemplate.SelectNodes("//LayoutItem[@id='ppImageSQL']");
                                    for (int iImageSQL = 0; iImageSQL < XmlNodeListImagesSQL.Count; iImageSQL++)
                                    {
                                        string ImageSQLURI = "";
                                        string ImageSQLURISQLString = "" + XmlNodeListImagesSQL.Item(iImageSQL).Attributes.GetNamedItem("labelText").InnerText;
                                        if (ImageSQLURISQLString != "")
                                        {
                                            //Get path for image from SQL datasource
                                            DataSet dsSQLImage;
                                            dsSQLImage = GetDataSetConnName(ImageSQLURISQLString, connectionStringSQLImages);
                                            //Need to check if any tables returned by getdata here
                                            if (dsSQLImage.Tables.Count > 0)
                                            {
                                                if (dsSQLImage.Tables[0].Rows.Count > 0)
                                                {
                                                    ImageSQLURI = "" + dsSQLImage.Tables[0].Rows[0].ItemArray[0];
                                                }
                                            }

                                            //Get image and write to PDF page
                                            if (ImageSQLURI != "" && ImageSQLURI.ToLower().StartsWith("http"))
                                            {
                                                //check status code
                                                statusCode = GetHTTPStatusCode(ImageSQLURI, "HEAD");
                                                if (statusCode == 0)
                                                {
                                                    statusCode = GetHTTPStatusCode(ImageSQLURI, "GET");
                                                }
                                                if (statusCode >= 100 && statusCode < 400)
                                                {

                                                    //Image scaling
                                                    //Set defaults
                                                    float ScaleImageByValue = 1;

                                                    if (XmlNodeListImagesSQL.Item(iImageSQL).Attributes.GetNamedItem("outlineWidthM") != null)
                                                    {
                                                        // use "outlineWidthM" for scale value
                                                        string RawScaleImageBy = XmlNodeListImagesSQL.Item(iImageSQL).Attributes.GetNamedItem("outlineWidthM").InnerText;
                                                        char[] charSeparators = new char[] { ',' };
                                                        string[] splitresult;
                                                        splitresult = RawScaleImageBy.Split(charSeparators);
                                                        ScaleImageByValue = System.Convert.ToSingle(splitresult[0]);
                                                    }

                                                    //Image position
                                                    //Set defaults
                                                    string ImagePositionX = "0";
                                                    string ImagePositionY = "0";

                                                    if (XmlNodeListImagesSQL.Item(iImageSQL).Attributes.GetNamedItem("position") != null)
                                                    {
                                                        string borderPosition = XmlNodeListImagesSQL.Item(iImageSQL).Attributes.GetNamedItem("position").InnerText;
                                                        char[] charSeparators = new char[] { ',' };
                                                        string[] splitresult;
                                                        splitresult = borderPosition.Split(charSeparators);
                                                        ImagePositionX = splitresult[0];
                                                        ImagePositionY = splitresult[1];
                                                    }

                                                    WebRequest request = WebRequest.Create(ImageSQLURI);
                                                    byte[] rBytes;
                                                    //  > Get the content
                                                    using (WebResponse result = request.GetResponse())
                                                    {
                                                        Stream rStream = result.GetResponseStream();
                                                        //  > Bytes from address
                                                        using (BinaryReader br = new BinaryReader(rStream))
                                                        {
                                                            // Ask for bytes bigger than the actual stream
                                                            rBytes = br.ReadBytes(5000000);
                                                            br.Close();
                                                        }
                                                        //  > close down the web response object
                                                        result.Close();
                                                    }

                                                    using (MemoryStream imageStream = new MemoryStream(rBytes))
                                                    {
                                                        imageStream.Position = 0;
                                                        MemoryStream imageStream2 = new MemoryStream();
                                                        using (Bitmap imageBitmap = new System.Drawing.Bitmap(imageStream))
                                                        {
                                                            //Use Bmp - solves issue with generic error in GDI+
                                                            imageBitmap.Save(imageStream2, System.Drawing.Imaging.ImageFormat.Bmp);
                                                        }
                                                        imageStream2.Position = 0;
                                                        PdfImage PdfImage1 = new PdfBitmap(imageStream2);
                                                        g.DrawImage(PdfImage1, convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(ImagePositionY), mm, pt), PdfImage1.Width * ScaleImageByValue, PdfImage1.Height * ScaleImageByValue);
                                                        imageStream2.Close();
                                                        imageStream.Close();
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                // Labels
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppLabel']") != null)
                                {
                                    XmlNodeList XmlNodeListLabels = QGISLayoutTemplate.SelectNodes("//LayoutItem[@id='ppLabel']");
                                    for (int iLabel = 0; iLabel < XmlNodeListLabels.Count; iLabel++)
                                    {
                                        if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("labelText") != null)
                                        {
                                            string LabelText = "" + XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("labelText").InnerText;
                                            LabelText = "" + LabelText.Replace("@featurekey", FeatureKeys);
                                            LabelText = "" + LabelText.Replace("@databasekey", DatabaseKey);
                                            LabelText = "" + LabelText.Replace("@referencekey", ReferenceKey);
                                            if (LabelText != "")
                                            {
                                                PdfFont LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

                                                //Set font size, style
                                                if (XmlNodeListLabels.Item(iLabel).SelectSingleNode("LabelFont/@description") != null)
                                                {
                                                    string LabelFontDescription = "" + XmlNodeListLabels.Item(iLabel).SelectSingleNode("LabelFont/@description").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = LabelFontDescription.Split(charSeparators);
                                                    Single LabelFontSize = System.Convert.ToSingle(splitresult[1]);
                                                    PdfFontStyle LabelFontStyle = PdfFontStyle.Regular;
                                                    if (splitresult[4] == "75") { LabelFontStyle = PdfFontStyle.Bold; }
                                                    LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, LabelFontSize, LabelFontStyle);
                                                }

                                                //Set font color
                                                int rgbRed = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@red").InnerText);
                                                int rgbGreen = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@green").InnerText);
                                                int rgbBlue = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@blue").InnerText);
                                                PdfBrush LabelFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                                //Label position
                                                //Set defaults
                                                string LabelPositionX = "0";
                                                string LabelPositionY = "0";

                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("position") != null)
                                                {
                                                    string LabelPosition = XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("position").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = LabelPosition.Split(charSeparators);
                                                    LabelPositionX = splitresult[0];
                                                    LabelPositionY = splitresult[1];
                                                }

                                                //Label size
                                                //Set defaults
                                                string LabelWidth = "0";
                                                string LabelHeight = "0";
                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("size") != null)
                                                {
                                                    string LabelSize = XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("size").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = LabelSize.Split(charSeparators);
                                                    LabelWidth = splitresult[0];
                                                    LabelHeight = splitresult[1];
                                                }

                                                Syncfusion.Drawing.RectangleF LabelRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelHeight), mm, pt));
                                                PdfStringFormat LabelFormat = new PdfStringFormat();
                                                LabelFormat.WordWrap = PdfWordWrapType.Word;
                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("halign") != null)
                                                {
                                                    switch (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("halign").InnerText)
                                                    {
                                                        case "4":
                                                            LabelFormat.Alignment = PdfTextAlignment.Center;
                                                            break;
                                                        case "2":
                                                            LabelFormat.Alignment = PdfTextAlignment.Right;
                                                            break;
                                                        case "8":
                                                            LabelFormat.Alignment = PdfTextAlignment.Justify;
                                                            break;
                                                        default:
                                                            LabelFormat.Alignment = PdfTextAlignment.Left;
                                                            break;
                                                    }
                                                }
                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("valign") != null)
                                                {
                                                    switch (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("valign").InnerText)
                                                    {
                                                        case "128":
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Middle;
                                                            break;
                                                        case "64":
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Bottom;
                                                            break;
                                                        default:
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Top;
                                                            break;
                                                    }
                                                }
                                                g.DrawString(LabelText, LabelFont, LabelFontColour, LabelRectangle, LabelFormat);

                                            }
                                        }
                                    }
                                }

                                // Labels via SQL
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppLabelSQL']") != null)
                                {
                                    XmlNodeList XmlNodeListLabels = QGISLayoutTemplate.SelectNodes("//LayoutItem[@id='ppLabelSQL']");
                                    for (int iLabel = 0; iLabel < XmlNodeListLabels.Count; iLabel++)
                                    {
                                        if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("labelText") != null)
                                        {
                                            string LabelText = "" + XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("labelText").InnerText;

                                            if (LabelText != "")
                                            {
                                                //Get text for label from SQL datasource
                                                DataSet LabelSQLDS;
                                                LabelSQLDS = GetDataSetConnName(LabelText, connectionStringSQLLabels);
                                                //Need to check if any tables returned by getdata here
                                                if (LabelSQLDS.Tables.Count > 0)
                                                {
                                                    if (LabelSQLDS.Tables[0].Rows.Count > 0)
                                                    {
                                                        LabelText = "" + LabelSQLDS.Tables[0].Rows[0].ItemArray[0];
                                                    }
                                                }

                                                PdfFont LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

                                                //Set font size, style
                                                if (XmlNodeListLabels.Item(iLabel).SelectSingleNode("LabelFont/@description") != null)
                                                {
                                                    string LabelFontDescription = "" + XmlNodeListLabels.Item(iLabel).SelectSingleNode("LabelFont/@description").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = LabelFontDescription.Split(charSeparators);
                                                    Single LabelFontSize = System.Convert.ToSingle(splitresult[1]);
                                                    PdfFontStyle LabelFontStyle = PdfFontStyle.Regular;
                                                    if (splitresult[4] == "75") { LabelFontStyle = PdfFontStyle.Bold; }
                                                    LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, LabelFontSize, LabelFontStyle);
                                                }

                                                //Set font color
                                                int rgbRed = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@red").InnerText);
                                                int rgbGreen = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@green").InnerText);
                                                int rgbBlue = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@blue").InnerText);
                                                PdfBrush LabelFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                                //Label position
                                                //Set defaults
                                                string LabelPositionX = "0";
                                                string LabelPositionY = "0";

                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("position") != null)
                                                {
                                                    string LabelPosition = XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("position").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = LabelPosition.Split(charSeparators);
                                                    LabelPositionX = splitresult[0];
                                                    LabelPositionY = splitresult[1];
                                                }

                                                //Label size
                                                //Set defaults
                                                string LabelWidth = "0";
                                                string LabelHeight = "0";
                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("size") != null)
                                                {
                                                    string LabelSize = XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("size").InnerText;
                                                    char[] charSeparators = new char[] { ',' };
                                                    string[] splitresult;
                                                    splitresult = LabelSize.Split(charSeparators);
                                                    LabelWidth = splitresult[0];
                                                    LabelHeight = splitresult[1];
                                                }

                                                Syncfusion.Drawing.RectangleF LabelRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelHeight), mm, pt));
                                                PdfStringFormat LabelFormat = new PdfStringFormat();
                                                LabelFormat.WordWrap = PdfWordWrapType.Word;
                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("halign") != null)
                                                {
                                                    switch (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("halign").InnerText)
                                                    {
                                                        case "4":
                                                            LabelFormat.Alignment = PdfTextAlignment.Center;
                                                            break;
                                                        case "2":
                                                            LabelFormat.Alignment = PdfTextAlignment.Right;
                                                            break;
                                                        case "8":
                                                            LabelFormat.Alignment = PdfTextAlignment.Justify;
                                                            break;
                                                        default:
                                                            LabelFormat.Alignment = PdfTextAlignment.Left;
                                                            break;
                                                    }
                                                }
                                                if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("valign") != null)
                                                {
                                                    switch (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("valign").InnerText)
                                                    {
                                                        case "128":
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Middle;
                                                            break;
                                                        case "64":
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Bottom;
                                                            break;
                                                        default:
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Top;
                                                            break;
                                                    }
                                                }
                                                g.DrawString(LabelText, LabelFont, LabelFontColour, LabelRectangle, LabelFormat);

                                            }
                                        }
                                    }
                                }

                                // Labels via WFS
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppLabelWFS']") != null)
                                {
                                    XmlNodeList XmlNodeListLabels = QGISLayoutTemplate.SelectNodes("//LayoutItem[@id='ppLabelWFS']");
                                    for (int iLabel = 0; iLabel < XmlNodeListLabels.Count; iLabel++)
                                    {
                                        if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("labelText") != null)
                                        {
                                            string LabelText = "" + XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("labelText").InnerText;

                                            if (LabelText != "")
                                            {

                                                string dataURL = "" + LabelText;

                                                dataURL = "" + dataURL.Replace("@featurekey", FeatureKeys);
                                                dataURL = "" + dataURL.Replace("@databasekey", DatabaseKey);
                                                dataURL = "" + dataURL.Replace("@referencekey", ReferenceKey);


                                                string propertyName = "";
                                                string querystring = dataURL.Substring(dataURL.IndexOf('?'));
                                                if (System.Web.HttpUtility.ParseQueryString(querystring).GetValues("propertyName") != null)
                                                {
                                                    propertyName = System.Web.HttpUtility.ParseQueryString(querystring).GetValues("propertyName")[0].ToString();
                                                }

                                                /*
                                                example
                                                return encumbrances
                                                https://data.linz.govt.nz/services;key=dd4308b96a1743a195b4e9044fb70313/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=table-51695&cql_filter=title_no=325429
                                                https://data.linz.govt.nz/services;key=dd4308b96a1743a195b4e9044fb70313/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=table-51695&PropertyName=title_no,memorial_text&cql_filter=title_no=@featurekey
                                            URI=https://data.linz.govt.nz/services;key=dd4308b96a1743a195b4e9044fb70313/wfs?service=WFS&version=2.0.0&request=GetFeature&typeNames=table-51695&PropertyName=title_no,memorial_text&cql_filter=title_no=325429
*/

                                                // Initialise ogr datasource
                                                DataSource dsOgr = null;
                                                // Initialise ogr layer
                                                Layer layer1 = null;

                                                statusCode = GetHttpClientStatusCode(dataURL);

                                                if (statusCode >= 100 && statusCode < 400)
                                                {

                                                    //Geometry FeatureGeometry = Ogr.CreateGeometryFromGML(FeatureDoc);
                                                    Ogr.RegisterAll();

                                                    //skip SSL host / certificate verification if https url
                                                    if (dataURL.StartsWith("https"))
                                                    {
                                                        if (Gdal.GetConfigOption("GDAL_HTTP_UNSAFESSL", "NO") == "NO")
                                                        {
                                                            Gdal.SetConfigOption("GDAL_HTTP_UNSAFESSL", "YES");
                                                        }
                                                    }

                                                    //Layer layer1;
                                                    dsOgr = Ogr.Open("" + dataURL, 0);
                                                    layer1 = dsOgr.GetLayerByIndex(0);

                                                    if (propertyName != "")
                                                    {
                                                        string FeatureSQL = "SELECT \"" + propertyName.Replace(",", "\", \"") + "\" FROM \"" + layer1.GetName() + "\"";
                                                        layer1 = dsOgr.ExecuteSQL(FeatureSQL, null, "SQLITE");
                                                    }
                                                    dsOgr.Dispose();
                                                }

                                                if (layer1 != null)
                                                {
                                                    DataSet LabelSQLDS = new DataSet();

                                                    layer1.ResetReading();
                                                    Feature feature1 = null;

                                                    // create table for dataset
                                                    var dataTable1 = new DataTable();


                                                    int featureIndex = 0;
                                                    for (featureIndex = 0; featureIndex < layer1.GetFeatureCount(1); featureIndex++)
                                                    {
                                                        layer1.SetNextByIndex(featureIndex);
                                                        feature1 = layer1.GetNextFeature();

                                                        // create data columns
                                                        if (featureIndex == 0)
                                                        {
                                                            int fieldIndex = 0;
                                                            for (fieldIndex = 0; fieldIndex < feature1.GetFieldCount(); fieldIndex++)
                                                            {

                                                                dataTable1.Columns.Add(new DataColumn(feature1.GetFieldDefnRef(fieldIndex).GetName()));

                                                            }
                                                        }

                                                        var row = dataTable1.NewRow();
                                                        //populate data column values for each feature
                                                        for (int iField = 0; iField < feature1.GetFieldCount(); iField++)
                                                        {
                                                            row[iField] = "" + feature1.GetFieldAsString(iField).ToString();
                                                        }
                                                        dataTable1.Rows.Add(row);

                                                        layer1.DeleteFeature(featureIndex);
                                                    }

                                                    LabelSQLDS.Tables.Add(dataTable1);

                                                    LabelText = "";

                                                    if (LabelSQLDS.Tables.Count > 0)
                                                    {
                                                        if (LabelSQLDS.Tables[0].Rows.Count > 0)
                                                        {
                                                            LabelText = "" + LabelSQLDS.Tables[0].Rows[0].ItemArray[0];
                                                        }
                                                    }

                                                    PdfFont LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

                                                    //Set font size, style
                                                    if (XmlNodeListLabels.Item(iLabel).SelectSingleNode("LabelFont/@description") != null)
                                                    {
                                                        string LabelFontDescription = "" + XmlNodeListLabels.Item(iLabel).SelectSingleNode("LabelFont/@description").InnerText;
                                                        char[] charSeparators = new char[] { ',' };
                                                        string[] splitresult;
                                                        splitresult = LabelFontDescription.Split(charSeparators);
                                                        Single LabelFontSize = System.Convert.ToSingle(splitresult[1]);
                                                        PdfFontStyle LabelFontStyle = PdfFontStyle.Regular;
                                                        if (splitresult[4] == "75") { LabelFontStyle = PdfFontStyle.Bold; }
                                                        LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, LabelFontSize, LabelFontStyle);
                                                    }

                                                    //Set font color
                                                    int rgbRed = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@red").InnerText);
                                                    int rgbGreen = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@green").InnerText);
                                                    int rgbBlue = System.Convert.ToInt16(XmlNodeListLabels.Item(iLabel).SelectSingleNode("FontColor/@blue").InnerText);
                                                    PdfBrush LabelFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                                    //Label position
                                                    //Set defaults
                                                    string LabelPositionX = "0";
                                                    string LabelPositionY = "0";

                                                    if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("position") != null)
                                                    {
                                                        string LabelPosition = XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("position").InnerText;
                                                        char[] charSeparators = new char[] { ',' };
                                                        string[] splitresult;
                                                        splitresult = LabelPosition.Split(charSeparators);
                                                        LabelPositionX = splitresult[0];
                                                        LabelPositionY = splitresult[1];
                                                    }

                                                    //Label size
                                                    //Set defaults
                                                    string LabelWidth = "0";
                                                    string LabelHeight = "0";
                                                    if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("size") != null)
                                                    {
                                                        string LabelSize = XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("size").InnerText;
                                                        char[] charSeparators = new char[] { ',' };
                                                        string[] splitresult;
                                                        splitresult = LabelSize.Split(charSeparators);
                                                        LabelWidth = splitresult[0];
                                                        LabelHeight = splitresult[1];
                                                    }

                                                    Syncfusion.Drawing.RectangleF LabelRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelHeight), mm, pt));
                                                    PdfStringFormat LabelFormat = new PdfStringFormat();
                                                    LabelFormat.WordWrap = PdfWordWrapType.Word;
                                                    if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("halign") != null)
                                                    {
                                                        switch (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("halign").InnerText)
                                                        {
                                                            case "4":
                                                                LabelFormat.Alignment = PdfTextAlignment.Center;
                                                                break;
                                                            case "2":
                                                                LabelFormat.Alignment = PdfTextAlignment.Right;
                                                                break;
                                                            case "8":
                                                                LabelFormat.Alignment = PdfTextAlignment.Justify;
                                                                break;
                                                            default:
                                                                LabelFormat.Alignment = PdfTextAlignment.Left;
                                                                break;
                                                        }
                                                    }
                                                    if (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("valign") != null)
                                                    {
                                                        switch (XmlNodeListLabels.Item(iLabel).Attributes.GetNamedItem("valign").InnerText)
                                                        {
                                                            case "128":
                                                                LabelFormat.LineAlignment = PdfVerticalAlignment.Middle;
                                                                break;
                                                            case "64":
                                                                LabelFormat.LineAlignment = PdfVerticalAlignment.Bottom;
                                                                break;
                                                            default:
                                                                LabelFormat.LineAlignment = PdfVerticalAlignment.Top;
                                                                break;
                                                        }
                                                    }
                                                    g.DrawString(LabelText, LabelFont, LabelFontColour, LabelRectangle, LabelFormat);
                                                }

                                            }
                                        }
                                    }

                                }

                                // Label - Scale Text
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppMap']") != null)
                                {
                                    //Only show scale text if there is one map.  
                                    //Scales for more than one map defined in spatial page generation code
                                    XmlNodeList XmlNodeListTemplateMaps = QGISLayoutTemplate.SelectNodes("//LayoutItem[@id='ppMap']");
                                    if (XmlNodeListTemplateMaps.Count == 1)
                                    {

                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']") != null)
                                        {
                                            PdfFont LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

                                            //Set font size, style
                                            if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/LabelFont/@description") != null)
                                            {
                                                string LabelFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/LabelFont/@description").InnerText;
                                                char[] charSeparators = new char[] { ',' };
                                                string[] splitresult;
                                                splitresult = LabelFontDescription.Split(charSeparators);
                                                Single LabelFontSize = System.Convert.ToSingle(splitresult[1]);
                                                PdfFontStyle LabelFontStyle = PdfFontStyle.Regular;
                                                if (splitresult[4] == "75") { LabelFontStyle = PdfFontStyle.Bold; }
                                                LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, LabelFontSize, LabelFontStyle);
                                            }

                                            //Set font color
                                            int rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/FontColor/@red").InnerText);
                                            int rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/FontColor/@green").InnerText);
                                            int rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/FontColor/@blue").InnerText);
                                            PdfBrush LabelFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                            //Label position
                                            //Set defaults
                                            string LabelPositionX = "0";
                                            string LabelPositionY = "0";

                                            if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@position") != null)
                                            {
                                                string LabelPosition = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@position").InnerText;
                                                char[] charSeparators = new char[] { ',' };
                                                string[] splitresult;
                                                splitresult = LabelPosition.Split(charSeparators);
                                                LabelPositionX = splitresult[0];
                                                LabelPositionY = splitresult[1];
                                            }

                                            //Label size
                                            //Set defaults
                                            string LabelWidth = "0";
                                            string LabelHeight = "0";
                                            if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@size") != null)
                                            {
                                                string LabelSize = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@size").InnerText;
                                                char[] charSeparators = new char[] { ',' };
                                                string[] splitresult;
                                                splitresult = LabelSize.Split(charSeparators);
                                                LabelWidth = splitresult[0];
                                                LabelHeight = splitresult[1];
                                            }

                                            string LabelText = "" + MapImageNeatScale;
                                            if (LabelPositionX != "" && LabelPositionY != "")
                                            {
                                                Syncfusion.Drawing.RectangleF LabelRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelHeight), mm, pt));
                                                PdfStringFormat LabelFormat = new PdfStringFormat();
                                                LabelFormat.WordWrap = PdfWordWrapType.Word;
                                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@halign") != null)
                                                {
                                                    switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@halign").InnerText)
                                                    {
                                                        case "4":
                                                            LabelFormat.Alignment = PdfTextAlignment.Center;
                                                            break;
                                                        case "2":
                                                            LabelFormat.Alignment = PdfTextAlignment.Right;
                                                            break;
                                                        case "8":
                                                            LabelFormat.Alignment = PdfTextAlignment.Justify;
                                                            break;
                                                        default:
                                                            LabelFormat.Alignment = PdfTextAlignment.Left;
                                                            break;
                                                    }
                                                }
                                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@valign") != null)
                                                {
                                                    switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppScaleText']/@valign").InnerText)
                                                    {
                                                        case "128":
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Middle;
                                                            break;
                                                        case "64":
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Bottom;
                                                            break;
                                                        default:
                                                            LabelFormat.LineAlignment = PdfVerticalAlignment.Top;
                                                            break;
                                                    }
                                                }
                                                g.DrawString(LabelText, LabelFont, LabelFontColour, LabelRectangle, LabelFormat);
                                            }
                                        }
                                    }
                                }

                                // Label - Page Size
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']") != null)
                                {
                                    PdfFont LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

                                    //Set font size, style
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/LabelFont/@description") != null)
                                    {
                                        string LabelFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/LabelFont/@description").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelFontDescription.Split(charSeparators);
                                        Single LabelFontSize = System.Convert.ToSingle(splitresult[1]);
                                        PdfFontStyle LabelFontStyle = PdfFontStyle.Regular;
                                        if (splitresult[4] == "75") { LabelFontStyle = PdfFontStyle.Bold; }
                                        LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, LabelFontSize, LabelFontStyle);
                                    }

                                    //Set font color
                                    int rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/FontColor/@red").InnerText);
                                    int rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/FontColor/@green").InnerText);
                                    int rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/FontColor/@blue").InnerText);
                                    PdfBrush LabelFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                    //Label position
                                    //Set defaults
                                    string LabelPositionX = "0";
                                    string LabelPositionY = "0";

                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@position") != null)
                                    {
                                        string LabelPosition = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@position").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelPosition.Split(charSeparators);
                                        LabelPositionX = splitresult[0];
                                        LabelPositionY = splitresult[1];
                                    }

                                    //Label size
                                    //Set defaults
                                    string LabelWidth = "0";
                                    string LabelHeight = "0";
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@size") != null)
                                    {
                                        string LabelSize = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@size").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelSize.Split(charSeparators);
                                        LabelWidth = splitresult[0];
                                        LabelHeight = splitresult[1];
                                    }

                                    string LabelText = "" + System.Convert.ToInt16(convertor1.ConvertUnits(page.Size.Width, pt, mm)) + "x" + System.Convert.ToInt16(convertor1.ConvertUnits(page.Size.Height, pt, mm)) + "mm";
                                    if (LabelPositionX != "" && LabelPositionY != "")
                                    {
                                        Syncfusion.Drawing.RectangleF LabelRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelHeight), mm, pt));
                                        PdfStringFormat LabelFormat = new PdfStringFormat();
                                        LabelFormat.WordWrap = PdfWordWrapType.Word;
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@halign") != null)
                                        {
                                            switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@halign").InnerText)
                                            {
                                                case "4":
                                                    LabelFormat.Alignment = PdfTextAlignment.Center;
                                                    break;
                                                case "2":
                                                    LabelFormat.Alignment = PdfTextAlignment.Right;
                                                    break;
                                                case "8":
                                                    LabelFormat.Alignment = PdfTextAlignment.Justify;
                                                    break;
                                                default:
                                                    LabelFormat.Alignment = PdfTextAlignment.Left;
                                                    break;
                                            }
                                        }
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@valign") != null)
                                        {
                                            switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppPageSize']/@valign").InnerText)
                                            {
                                                case "128":
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Middle;
                                                    break;
                                                case "64":
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Bottom;
                                                    break;
                                                default:
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Top;
                                                    break;
                                            }
                                        }
                                        g.DrawString(LabelText, LabelFont, LabelFontColour, LabelRectangle, LabelFormat);
                                    }
                                }

                                // Label - Current Date
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']") != null)
                                {
                                    PdfFont LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

                                    //Set font size, style
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/LabelFont/@description") != null)
                                    {
                                        string LabelFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/LabelFont/@description").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelFontDescription.Split(charSeparators);
                                        Single LabelFontSize = System.Convert.ToSingle(splitresult[1]);
                                        PdfFontStyle LabelFontStyle = PdfFontStyle.Regular;
                                        if (splitresult[4] == "75") { LabelFontStyle = PdfFontStyle.Bold; }
                                        LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, LabelFontSize, LabelFontStyle);
                                    }

                                    //Set font color
                                    int rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/FontColor/@red").InnerText);
                                    int rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/FontColor/@green").InnerText);
                                    int rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/FontColor/@blue").InnerText);
                                    PdfBrush LabelFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                    //Label position
                                    //Set defaults
                                    string LabelPositionX = "0";
                                    string LabelPositionY = "0";

                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@position") != null)
                                    {
                                        string LabelPosition = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@position").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelPosition.Split(charSeparators);
                                        LabelPositionX = splitresult[0];
                                        LabelPositionY = splitresult[1];
                                    }

                                    //Label size
                                    //Set defaults
                                    string LabelWidth = "0";
                                    string LabelHeight = "0";
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@size") != null)
                                    {
                                        string LabelSize = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@size").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelSize.Split(charSeparators);
                                        LabelWidth = splitresult[0];
                                        LabelHeight = splitresult[1];
                                    }

                                    string LabelText = "" + DateTime.Now.ToLongDateString();
                                    if (LabelPositionX != "" && LabelPositionY != "")
                                    {
                                        Syncfusion.Drawing.RectangleF LabelRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelHeight), mm, pt));
                                        PdfStringFormat LabelFormat = new PdfStringFormat();
                                        LabelFormat.WordWrap = PdfWordWrapType.Word;
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@halign") != null)
                                        {
                                            switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@halign").InnerText)
                                            {
                                                case "4":
                                                    LabelFormat.Alignment = PdfTextAlignment.Center;
                                                    break;
                                                case "2":
                                                    LabelFormat.Alignment = PdfTextAlignment.Right;
                                                    break;
                                                case "8":
                                                    LabelFormat.Alignment = PdfTextAlignment.Justify;
                                                    break;
                                                default:
                                                    LabelFormat.Alignment = PdfTextAlignment.Left;
                                                    break;
                                            }
                                        }
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@valign") != null)
                                        {
                                            switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppCurrentDate']/@valign").InnerText)
                                            {
                                                case "128":
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Middle;
                                                    break;
                                                case "64":
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Bottom;
                                                    break;
                                                default:
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Top;
                                                    break;
                                            }
                                        }
                                        g.DrawString(LabelText, LabelFont, LabelFontColour, LabelRectangle, LabelFormat);
                                    }
                                }

                                // Label - Title
                                if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']") != null)
                                {

                                    string TitleText = "test";
                                    if (xpathnodespages.Current.GetAttribute("title", "") != null)
                                    {
                                        TitleText = "" + xpathnodespages.Current.GetAttribute("title", "");

                                        TitleText = "" + TitleText.Replace("@featurekey", FeatureKeys);
                                        TitleText = "" + TitleText.Replace("@datbasekey", DatabaseKey);
                                        TitleText = "" + TitleText.Replace("@referencekey", ReferenceKey);

                                    }

                                    PdfFont LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, 10);

                                    //Set font size, style
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/LabelFont/@description") != null)
                                    {
                                        string LabelFontDescription = "" + QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/LabelFont/@description").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelFontDescription.Split(charSeparators);
                                        Single LabelFontSize = System.Convert.ToSingle(splitresult[1]);
                                        PdfFontStyle LabelFontStyle = PdfFontStyle.Regular;
                                        if (splitresult[4] == "75") { LabelFontStyle = PdfFontStyle.Bold; }
                                        LabelFont = new PdfStandardFont(PdfFontFamily.Helvetica, LabelFontSize, LabelFontStyle);
                                    }

                                    //Set font color
                                    int rgbRed = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/FontColor/@red").InnerText);
                                    int rgbGreen = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/FontColor/@green").InnerText);
                                    int rgbBlue = System.Convert.ToInt16(QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/FontColor/@blue").InnerText);
                                    PdfBrush LabelFontColour = new PdfSolidBrush(Syncfusion.Drawing.Color.FromArgb(rgbRed, rgbGreen, rgbBlue));

                                    //Label position
                                    //Set defaults
                                    string LabelPositionX = "0";
                                    string LabelPositionY = "0";

                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@position") != null)
                                    {
                                        string LabelPosition = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@position").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelPosition.Split(charSeparators);
                                        LabelPositionX = splitresult[0];
                                        LabelPositionY = splitresult[1];
                                    }

                                    //Label size
                                    //Set defaults
                                    string LabelWidth = "0";
                                    string LabelHeight = "0";
                                    if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@size") != null)
                                    {
                                        string LabelSize = QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@size").InnerText;
                                        char[] charSeparators = new char[] { ',' };
                                        string[] splitresult;
                                        splitresult = LabelSize.Split(charSeparators);
                                        LabelWidth = splitresult[0];
                                        LabelHeight = splitresult[1];
                                    }

                                    string LabelText = "" + TitleText;
                                    if (LabelPositionX != "" && LabelPositionY != "")
                                    {
                                        Syncfusion.Drawing.RectangleF LabelRectangle = new Syncfusion.Drawing.RectangleF(convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionX), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelPositionY), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelWidth), mm, pt), convertor1.ConvertUnits(System.Convert.ToSingle(LabelHeight), mm, pt));
                                        PdfStringFormat LabelFormat = new PdfStringFormat();
                                        LabelFormat.WordWrap = PdfWordWrapType.Word;
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@halign") != null)
                                        {
                                            switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@halign").InnerText)
                                            {
                                                case "4":
                                                    LabelFormat.Alignment = PdfTextAlignment.Center;
                                                    break;
                                                case "2":
                                                    LabelFormat.Alignment = PdfTextAlignment.Right;
                                                    break;
                                                case "8":
                                                    LabelFormat.Alignment = PdfTextAlignment.Justify;
                                                    break;
                                                default:
                                                    LabelFormat.Alignment = PdfTextAlignment.Left;
                                                    break;
                                            }
                                        }
                                        if (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@valign") != null)
                                        {
                                            switch (QGISLayoutTemplate.SelectSingleNode("//LayoutItem[@id='ppTitle']/@valign").InnerText)
                                            {
                                                case "128":
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Middle;
                                                    break;
                                                case "64":
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Bottom;
                                                    break;
                                                default:
                                                    LabelFormat.LineAlignment = PdfVerticalAlignment.Top;
                                                    break;
                                            }
                                        }
                                        g.DrawString(LabelText, LabelFont, LabelFontColour, LabelRectangle, LabelFormat);
                                    }
                                }

                            }
                        }
                    }
                }

                // Return PDF Document to client
                System.IO.MemoryStream memStream = new MemoryStream();
                PDFDoc1.Save(memStream);
                memStream.Seek(0, SeekOrigin.Begin);
                byte[] byteArray = memStream.ToArray();
                memStream.Close();

                return byteArray;
            }
            else
            {
                //no featurekeys supplied
                //unable to produce PDF document
                //redirect to an error page
                return null;
            }
        }

        private void SetPDFCompression(PdfDocument PDFDoc1, string Compression)
        {
            switch (Compression)
            {
                case "None":
                    PDFDoc1.Compression = PdfCompressionLevel.None;
                    break;
                case "BestSpeed":
                    PDFDoc1.Compression = PdfCompressionLevel.BestSpeed;
                    break;
                case "BelowNormal":
                    PDFDoc1.Compression = PdfCompressionLevel.BelowNormal;
                    break;
                case "Normal":
                    PDFDoc1.Compression = PdfCompressionLevel.Normal;
                    break;
                case "AboveNormal":
                    PDFDoc1.Compression = PdfCompressionLevel.AboveNormal;
                    break;
                default:
                    PDFDoc1.Compression = PdfCompressionLevel.Best;
                    break;
            }
        }



        private void SetGDAL()
        {
            string GDAL_HOME = @";C:\Program Files\GDAL";
            string path = Environment.GetEnvironmentVariable("PATH");
            path += ";" + GDAL_HOME;
            Environment.SetEnvironmentVariable("PATH", path);

            Gdal.SetConfigOption("GDAL_DATA", @"C:\Program Files\GDAL\gdal-data\");
            Gdal.SetConfigOption("GDAL_DRIVER_PATH", @"C:\Program Files\GDAL\gdalplugins\");
            Gdal.SetConfigOption("PROJ_LIB", @"C:\Program Files\GDAL\projlib\");

            Gdal.PushFinderLocation(GDAL_HOME);

        }
        private void SetGDAL(string GDAL_HOME)
        {
            string path = Environment.GetEnvironmentVariable("PATH");
            path += ";" + GDAL_HOME;
            Environment.SetEnvironmentVariable("PATH", path);

            Gdal.SetConfigOption("GDAL_DATA", GDAL_HOME + @"\gdal-data\");
            Gdal.SetConfigOption("GDAL_DRIVER_PATH", @"C:\Program Files\GDAL\gdalplugins\");
            Gdal.SetConfigOption("PROJ_LIB", @"C:\Program Files\GDAL\projlib\");

            Gdal.PushFinderLocation(GDAL_HOME);

        }

        private byte[] GetDownloadBytes(string Url)
        {
            string empty = string.Empty;
            return GetDownloadBytes(Url, out empty);
        }

        private byte[] GetDownloadBytes(string Url, out string responseUrl)
        {
            byte[] downloadedData = new byte[0];
            try
            {
                //Get a data stream from the url  
                WebRequest req = WebRequest.Create(Url);
                WebResponse response = req.GetResponse();
                Stream stream = response.GetResponseStream();

                responseUrl = response.ResponseUri.ToString();

                //Download in chunks  
                byte[] buffer = new byte[1024];

                //Get Total Size  
                int dataLength = (int)response.ContentLength;

                //read to memory stream
                MemoryStream memStream = new MemoryStream();
                while (true)
                {
                    //Try to read the data  
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        break;
                    }
                    else
                    {
                        //Write the downloaded data  
                        memStream.Write(buffer, 0, bytesRead);
                    }
                }

                //Convert the downloaded stream to a byte array  
                downloadedData = memStream.ToArray();

                //Clean up  
                stream.Close();
                memStream.Close();
            }
            catch (Exception)
            {
                responseUrl = string.Empty;
                return new byte[0];
            }

            return downloadedData;
        }

        private DataSet GetDataSetConnName(String queryString, String databaseConnName)
        {
            DataSet ds = new DataSet();
            try
            {
                OdbcConnection connection;
                connection = new OdbcConnection(Startup.PinpointConfiguration.Configuration.GetSection("ConnectionStrings")[databaseConnName].ToString());
                connection.Open();
                using (OdbcCommand cmd = new OdbcCommand(queryString, connection))
                {
                    OdbcDataAdapter adapter = new OdbcDataAdapter(queryString, connection);

                    var pFeatureKeys = new OdbcParameter("FeatureKeys", FeatureKeys);
                    adapter.SelectCommand.Parameters.Add(pFeatureKeys);

                    var pReferenceKey = new OdbcParameter("ReferenceKey", ReferenceKey);
                    adapter.SelectCommand.Parameters.Add(pReferenceKey);

                    var pDatabaseKey = new OdbcParameter("DatabaseKey", DatabaseKey);
                    adapter.SelectCommand.Parameters.Add(pDatabaseKey);

                    var pPinPointKey = new OdbcParameter("PinPointKey", PinPointKey);
                    adapter.SelectCommand.Parameters.Add(pPinPointKey);

                    if (Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["SQLCommandTimeOut"] != null)
                    {
                        adapter.SelectCommand.CommandTimeout = Convert.ToInt16(Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["SQLCommandTimeOut"]);
                    }

                    adapter.Fill(ds);
                }
                connection.Close();
            }
            catch (Exception ex)
            {
            }
            return ds;
        }

        private DataSet GetDataSetConnString(String queryString, String databaseConnString)
        {
            DataSet ds = new DataSet();
            try
            {
                OdbcConnection connection;
                connection = new OdbcConnection(databaseConnString);
                connection.Open();
                using (OdbcCommand cmd = new OdbcCommand(queryString, connection))
                {
                    OdbcDataAdapter adapter = new OdbcDataAdapter(queryString, connection);

                    var pFeatureKeys = new OdbcParameter("FeatureKeys", FeatureKeys);
                    adapter.SelectCommand.Parameters.Add(pFeatureKeys);

                    var pReferenceKey = new OdbcParameter("ReferenceKey", ReferenceKey);
                    adapter.SelectCommand.Parameters.Add(pReferenceKey);

                    var pDatabaseKey = new OdbcParameter("DatabaseKey", DatabaseKey);
                    adapter.SelectCommand.Parameters.Add(pDatabaseKey);

                    var pPinPointKey = new OdbcParameter("PinPointKey", PinPointKey);
                    adapter.SelectCommand.Parameters.Add(pPinPointKey);

                    if (Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["SQLCommandTimeOut"] != null)
                    {
                        adapter.SelectCommand.CommandTimeout = Convert.ToInt16(Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["SQLCommandTimeOut"]);
                    }

                    adapter.Fill(ds);
                }
                connection.Close();
            }
            catch (Exception ex)
            {
            }
            return ds;
        }

        private DataSet GetDataSetJSON(String requestUrl)
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    var json = wc.DownloadString(requestUrl);
                    DataSet dataSet = JsonConvert.DeserializeObject<DataSet>(json);
                    return dataSet;
                }
            }
            catch (Exception exception1)
            {
                return null;
            }
        }
        private DataSet GetDataSetJSON(String requestUrl, String username, String password)
        {
            try
            {
                using (WebClient wc = new WebClient())
                {
                    // Add credentials, base64 encode of username:password
                    string credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(username + ":" + password));
                    // Authorization header
                    wc.Headers[HttpRequestHeader.Authorization] = string.Format("Basic {0}", credentials);

                    var json = wc.DownloadString(requestUrl);
                    DataSet dataSet = JsonConvert.DeserializeObject<DataSet>(json);
                    return dataSet;
                }
            }
            catch (Exception exception1)
            {
                return null;
            }
        }

        private int GetHTTPStatusCode(String url, String method)
        {
            try
            {
                int statusCode = 0;
                if (url != "" && method != "")
                {


                    HttpWebRequest request = HttpWebRequest.Create(url) as HttpWebRequest;
                    request.UseDefaultCredentials = true;
                    request.Credentials = (ICredentials)CredentialCache.DefaultNetworkCredentials;
                    //set the timeout to 5 seconds default to keep the user from waiting too long for the page to load
                    request.Timeout = 5000;

                    //use timeout setting from web.config if set
                    if (Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["SQLCommandTimeOut"] != null)
                    {
                        request.Timeout = Convert.ToInt32(Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["SQLCommandTimeOut"]);
                    }
                    request.Method = method;
                    using (HttpWebResponse response = request.GetResponse() as HttpWebResponse)
                    {
                        string type = response.GetType().ToString();
                    }
                }
                return statusCode;
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError) //400 errors
                {
                    return Convert.ToInt32(((HttpWebResponse)ex.Response).StatusCode);
                }
                else
                {
                    return 0;
                }
            }
            catch (SocketException ex)
            {
                if (ex.Message != "")
                {
                }
                return 1;
            }
            catch (Exception ex)
            {
                if (ex.Message != "")
                {
                }
                return 2;
            }
        }

        private int GetHttpClientStatusCode(string url)
        {
            int statusCode = 0;

            HttpClientHandler handler = new HttpClientHandler
            {
                UseDefaultCredentials = true,
                Credentials = (ICredentials)CredentialCache.DefaultNetworkCredentials
            };

            using (HttpClient client = new HttpClient(handler))
            {

                //set the timeout to 5 seconds default to keep the user from waiting too long for the page to load
                //ticks = A time period expressed in 100 nanosecond units.
                // 5000000000 nanoseconds = 50000000 ticks
                client.Timeout = new TimeSpan(50000000);
                //use timeout setting (in milliseconds) from web.config if set
                if (Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["WebRequestTimeOut"] != null)
                {
                    client.Timeout = new TimeSpan(Convert.ToInt64(Startup.PinpointConfiguration.Configuration.GetSection("AppSettings")["WebRequestTimeOut"]) * 10000);
                }


                HttpResponseMessage response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).Result;
                if (response.IsSuccessStatusCode)
                {
                    statusCode = (int)response.StatusCode;
                }
                else
                {
                    //possibly do something else here, but for now just return status code
                    statusCode = (int)response.StatusCode;
                }
            }
            return statusCode;
        }

        public System.Drawing.Imaging.ImageFormat GetImageFormat(System.Drawing.Image img)
        {
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg))
                return System.Drawing.Imaging.ImageFormat.Jpeg;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Bmp))
                return System.Drawing.Imaging.ImageFormat.Bmp;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Png))
                return System.Drawing.Imaging.ImageFormat.Png;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Emf))
                return System.Drawing.Imaging.ImageFormat.Emf;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Exif))
                return System.Drawing.Imaging.ImageFormat.Exif;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Gif))
                return System.Drawing.Imaging.ImageFormat.Gif;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Icon))
                return System.Drawing.Imaging.ImageFormat.Icon;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.MemoryBmp))
                return System.Drawing.Imaging.ImageFormat.MemoryBmp;
            if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Tiff))
                return System.Drawing.Imaging.ImageFormat.Tiff;
            else
                return System.Drawing.Imaging.ImageFormat.Wmf;
        }

        public static byte[] ReadAllBytes(BinaryReader reader)
        {
            const int bufferSize = 4096;
            using (var ms = new MemoryStream())
            {
                byte[] buffer = new byte[bufferSize];
                int count;
                while ((count = reader.Read(buffer, 0, buffer.Length)) != 0)
                    ms.Write(buffer, 0, count);
                return ms.ToArray();
            }

        }
    }
}