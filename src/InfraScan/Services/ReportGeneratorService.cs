using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using InfraScan.Models;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace InfraScan.Services
{
    [SupportedOSPlatform("windows")]
    public class ReportGeneratorService
    {
        private static readonly string AssetsDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Resources", "report_assets");

        // Colors matching the original document
        private const string HeaderBgColor = "AD3333"; // Dark red
        private const string HeaderTextColor = "FFFFFF"; // White
        private const string NormalTextColor = "000000"; // Black

        public string GenerateReport(ReportData data, List<CommandConfig> outputCommands)
        {
            string reportsDir = StorageService.GetReportsDirectory();
            string fileName = $"Informe_{data.Hostname}_{data.ReportDate:yyyy-MM-dd_HHmm}.docx";
            string filePath = Path.Combine(reportsDir, fileName);

            using var doc = WordprocessingDocument.Create(filePath, WordprocessingDocumentType.Document);

            // Main document part
            var mainPart = doc.AddMainDocumentPart();
            mainPart.Document = new Document(new Body());
            var body = mainPart.Document.Body;

            // Set page size and margins
            var sectionProps = new SectionProperties(
                new PageSize { Width = 12240, Height = 15840 }, // Letter size  
                new PageMargin
                {
                    Top = 1440, Bottom = 1440, Left = 1080, Right = 1080,
                    Header = (UInt32Value)720U, Footer = (UInt32Value)720U
                }
            );

            // Add styles
            AddStyles(mainPart);

            // === TITLE ===
            var titleText = $"INFORME DE MONITOREO DEL SERVIDORES DE LA NUBE ({data.Hostname})";
            body.Append(CreateStyledParagraph(titleText, "44", true, "C00000", JustificationValues.Center));
            body.Append(CreateStyledParagraph(data.ReportDate.ToString("dd 'de' MMMM 'de' yyyy",
                new CultureInfo("es-ES")), "24", false, NormalTextColor, JustificationValues.Center));
            body.Append(new Paragraph()); // spacer

            // === H1: MONITOREO DEL SERVIDOR ===
            body.Append(CreateHeading($"MONITOREO DEL SERVIDOR {data.Hostname}", 1));

            // === TABLE 0: Information + Monitoring Summary ===
            var table0 = CreateMainTable(data);
            body.Append(table0);
            body.Append(new Paragraph()); // spacer

            // === OUTPUT IMAGES (terminal screenshots) ===
            int imageCounter = 1;
            foreach (var img in data.OutputImages.OrderBy(i => i.Order))
            {
                body.Append(CreateHeading(img.Title, 2));
                string relId = AddImageToDocument(mainPart, img.ImageData, $"output_{imageCounter}.png");
                body.Append(CreateImageParagraph(relId, img.ImageData));
                body.Append(new Paragraph());
                imageCounter++;
            }

            // === COCKPIT SCREENSHOTS ===
            if (data.CockpitOverviewScreenshot != null)
            {
                body.Append(CreateHeading("Monitoreo Métricas desde la herramienta Cockpit", 2));
                string relId = AddImageToDocument(mainPart, data.CockpitOverviewScreenshot, "cockpit_overview.png");
                body.Append(CreateImageParagraph(relId, data.CockpitOverviewScreenshot));
                body.Append(new Paragraph());
            }

            if (data.CockpitMetricsScreenshot != null)
            {
                body.Append(CreateHeading("Monitoreo de recursos desde la herramienta Cockpit", 2));
                string relId = AddImageToDocument(mainPart, data.CockpitMetricsScreenshot, "cockpit_metrics.png");
                body.Append(CreateImageParagraph(relId, data.CockpitMetricsScreenshot));
                body.Append(new Paragraph());
            }

            // === FINAL TABLE ===
            var finalTable = CreateFinalTable(data);
            body.Append(finalTable);

            // Add section properties
            body.Append(sectionProps);

            mainPart.Document.Save();
            return filePath;
        }

        private Table CreateMainTable(ReportData data)
        {
            var table = new Table();

            // Table properties
            var tblProp = new TableProperties(
                new TableWidth { Width = "9388", Type = TableWidthUnitValues.Dxa },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new BottomBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new LeftBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new RightBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor }
                )
            );
            table.Append(tblProp);

            // Grid columns (4 columns)
            var grid = new TableGrid(
                new GridColumn { Width = "1763" },
                new GridColumn { Width = "3365" },
                new GridColumn { Width = "2205" },
                new GridColumn { Width = "2055" }
            );
            table.Append(grid);

            // Header rows
            table.Append(CreateInfoRow("Entidad", data.Entity, "Contrato", data.Contract));
            table.Append(CreateInfoRow("Servidor", data.Hostname, "Fecha",
                data.ReportDate.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"))));
            table.Append(CreateInfoRow("Responsable", data.OperatorName, "Frecuencia", data.Frequency));

            // Tool row (span 3)
            table.Append(CreateToolRow(data.ToolDescription));

            // "Parámetros monitoreados" header (span 4)
            table.Append(CreateSpannedHeaderRow("Parámetros monitoreados", 4));

            // Column headers for monitoring
            table.Append(CreateMonitoringHeaderRow());

            // Data rows
            table.Append(CreateMonitoringRow("Resumen del sistema",
                BuildSystemResource(data), data.SystemStatus, data.SystemActionRequired ? "Sí" : "No"));

            table.Append(CreateMonitoringRow("Rendimiento CPU",
                BuildCpuResource(data), data.CpuStatus, data.CpuActionRequired ? "Sí" : "No"));

            table.Append(CreateMonitoringRow("Rendimiento memoria",
                BuildMemoryResource(data), data.MemoryStatus, data.MemoryActionRequired ? "Sí" : "No"));

            table.Append(CreateMonitoringRow("Rendimiento disco",
                $"Uso:           {data.DiskUsed} / {data.DiskTotal}", data.DiskStatus, data.DiskActionRequired ? "Sí" : "No"));

            table.Append(CreateMonitoringRow("Red",
                data.NetworkConnections.ToString(), data.NetworkStatus, data.NetworkActionRequired ? "Sí" : "No"));

            table.Append(CreateMonitoringRow("Registros (Logs)",
                $"Intentos fallidos de login (últimas 24h): {data.SshFailedAttempts}\n{data.LogStatus}",
                data.LogObservation, data.LogActionRequired ? "Sí" : ""));

            table.Append(CreateMonitoringRow("Actualizaciones del sistema",
                data.UpdateStatus, "", data.UpdateActionRequired ? "Sí" : ""));

            table.Append(CreateMonitoringRow("Servicios",
                data.ListeningPorts, data.ServiceStatus, data.ServiceAction));

            return table;
        }

        private string BuildSystemResource(ReportData data)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(data.Uptime)) parts.Add($"Uptime: {data.Uptime}");
            if (!string.IsNullOrEmpty(data.Model)) parts.Add($"Modelo: {data.Model}");
            if (!string.IsNullOrEmpty(data.Hostname)) parts.Add($"Hostname: {data.Hostname}");
            if (!string.IsNullOrEmpty(data.OSVersion)) parts.Add(data.OSVersion);
            return string.Join("\n", parts);
        }

        private string BuildCpuResource(ReportData data)
        {
            return $"Carga actual:  {data.CpuLoad} de {data.CpuCores} núcleos de CPU\n" +
                   $"CPU en uso de usuario:                  {data.CpuUserPercent}%\n" +
                   $"CPU en uso de sistema (kernel):         {data.CpuSystemPercent}%\n" +
                   $"CPU en procesos nice:                   {data.CpuNicePercent}%\n" +
                   $"CPU inactiva:                           {data.CpuIdlePercent}%\n" +
                   $"CPU esperando por I/O:                  {data.CpuIoWaitPercent}%\n" +
                   $"CPU en interrupciones de hardware:      {data.CpuHwIrqPercent}%\n" +
                   $"CPU en interrupciones de software:      {data.CpuSwIrqPercent}%\n" +
                   $"CPU robada por otros entornos (VMs):    {data.CpuStealPercent}%";
        }

        private string BuildMemoryResource(ReportData data)
        {
            return $"Uso:           {data.MemoryUsed} / {data.MemoryTotal}\nSwap:          {data.SwapUsed}";
        }

        // === Row builders ===

        private TableRow CreateInfoRow(string label1, string value1, string label2, string value2)
        {
            var row = new TableRow();
            row.Append(CreateHeaderCell(label1, "1763"));
            row.Append(CreateValueCell(value1, "3365"));
            row.Append(CreateHeaderCell(label2, "2205"));
            row.Append(CreateValueCell(value2, "2055"));
            return row;
        }

        private TableRow CreateToolRow(string toolDescription)
        {
            var row = new TableRow();
            row.Append(CreateHeaderCell("Herramienta", "1763"));

            var cell = new TableCell();
            var tcp = new TableCellProperties(
                new TableCellWidth { Width = "7625", Type = TableWidthUnitValues.Dxa },
                new GridSpan { Val = 3 },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            );
            cell.Append(tcp);
            cell.Append(CreateCellParagraph(toolDescription, NormalTextColor, false, "20"));
            row.Append(cell);
            return row;
        }

        private TableRow CreateSpannedHeaderRow(string text, int span)
        {
            var row = new TableRow();
            var cell = new TableCell();
            var tcp = new TableCellProperties(
                new TableCellWidth { Width = "9388", Type = TableWidthUnitValues.Dxa },
                new GridSpan { Val = span },
                new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderBgColor },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            );
            cell.Append(tcp);

            var para = new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = JustificationValues.Center },
                    new SpacingBetweenLines { Before = "0", After = "0" }
                ),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new DocumentFormat.OpenXml.Wordprocessing.Color { Val = NormalTextColor },
                        new FontSize { Val = "20" }
                    ),
                    new Text(text)
                )
            );
            cell.Append(para);
            row.Append(cell);
            return row;
        }

        private TableRow CreateMonitoringHeaderRow()
        {
            var row = new TableRow();
            row.Append(CreateHeaderCell("Revisión", "1763", JustificationValues.Center));
            row.Append(CreateHeaderCell("Recurso", "3365", JustificationValues.Center));
            row.Append(CreateHeaderCell("Estado/Observación", "2205", JustificationValues.Center));
            row.Append(CreateHeaderCell("Acción Requerida", "2055", JustificationValues.Center));
            return row;
        }

        private TableRow CreateMonitoringRow(string revision, string resource, string status, string action)
        {
            var row = new TableRow();
            row.Append(CreateRevisionCell(revision, "1763"));
            row.Append(CreateMultilineValueCell(resource, "3365"));
            row.Append(CreateValueCell(status, "2205"));
            row.Append(CreateValueCell(action, "2055"));
            return row;
        }

        // === Cell builders ===

        private TableCell CreateHeaderCell(string text, string width)
        {
            return CreateHeaderCell(text, width, JustificationValues.Left);
        }

        private TableCell CreateHeaderCell(string text, string width,
            JustificationValues justify)
        {
            var cell = new TableCell();
            var tcp = new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },
                new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderBgColor },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            );
            cell.Append(tcp);
            cell.Append(CreateCellParagraph(text, HeaderTextColor, true, "18", justify));
            return cell;
        }

        private TableCell CreateRevisionCell(string text, string width)
        {
            var cell = new TableCell();
            var tcp = new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },
                new Shading { Val = ShadingPatternValues.Clear, Fill = HeaderBgColor },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            );
            cell.Append(tcp);
            cell.Append(CreateCellParagraph(text, HeaderTextColor, false, "18"));
            return cell;
        }

        private TableCell CreateValueCell(string text, string width)
        {
            var cell = new TableCell();
            var tcp = new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            );
            cell.Append(tcp);

            // Handle multi-line
            if (text.Contains('\n'))
            {
                foreach (var line in text.Split('\n'))
                    cell.Append(CreateCellParagraph(line, NormalTextColor, false, "18"));
            }
            else
            {
                cell.Append(CreateCellParagraph(text, NormalTextColor, false, "18"));
            }
            return cell;
        }

        private TableCell CreateMultilineValueCell(string text, string width)
        {
            var cell = new TableCell();
            var tcp = new TableCellProperties(
                new TableCellWidth { Width = width, Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }
            );
            cell.Append(tcp);

            foreach (var line in text.Split('\n'))
                cell.Append(CreateCellParagraph(line, NormalTextColor, false, "20"));

            return cell;
        }

        private Paragraph CreateCellParagraph(string text, string color, bool bold, string fontSize)
        {
            return CreateCellParagraph(text, color, bold, fontSize, JustificationValues.Left);
        }

        private Paragraph CreateCellParagraph(string text, string color, bool bold, string fontSize,
            JustificationValues justify)
        {
            var runProps = new RunProperties(
                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color },
                new FontSize { Val = fontSize },
                new RunFonts { Ascii = "Arial", ComplexScript = "Arial" }
            );
            if (bold) runProps.Append(new Bold());

            return new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = justify },
                    new SpacingBetweenLines { Before = "0", After = "0", Line = "240", LineRule = LineSpacingRuleValues.Auto }
                ),
                new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve })
            );
        }

        // === Final Table ===

        private Table CreateFinalTable(ReportData data)
        {
            var table = new Table();

            var tblProp = new TableProperties(
                new TableWidth { Width = "9525", Type = TableWidthUnitValues.Dxa },
                new TableLayout { Type = TableLayoutValues.Fixed },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new BottomBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new LeftBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new RightBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 6, Color = NormalTextColor }
                )
            );
            table.Append(tblProp);

            var grid = new TableGrid(
                new GridColumn { Width = "1406" },
                new GridColumn { Width = "2840" },
                new GridColumn { Width = "3575" },
                new GridColumn { Width = "1704" }
            );
            table.Append(grid);

            // Header row
            var headerRow = new TableRow();
            headerRow.Append(CreateHeaderCell("Fecha", "1406", JustificationValues.Center));
            headerRow.Append(CreateHeaderCell("Servidor", "2840", JustificationValues.Center));
            headerRow.Append(CreateHeaderCell("Novedad", "3575", JustificationValues.Center));
            headerRow.Append(CreateHeaderCell("Realizado Por", "1704", JustificationValues.Center));
            table.Append(headerRow);

            // Month row
            string monthName = data.ReportDate.ToString("MMMM", new CultureInfo("es-ES")).ToUpperInvariant();
            table.Append(CreateSpannedHeaderRow(monthName, 4));

            // Data row
            var dataRow = new TableRow();

            var dateCell = new TableCell();
            dateCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "1406", Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            dateCell.Append(CreateCellParagraph(
                data.ReportDate.ToString("dd 'de' MMMM 'de' yyyy", new CultureInfo("es-ES")),
                NormalTextColor, false, "20", JustificationValues.Center));
            dataRow.Append(dateCell);

            var serverCell = new TableCell();
            serverCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "2840", Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            serverCell.Append(CreateCellParagraph(data.Hostname, NormalTextColor, false, "20", JustificationValues.Center));
            dataRow.Append(serverCell);

            var noveltyCell = new TableCell();
            noveltyCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "3575", Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            noveltyCell.Append(CreateCellParagraph(data.NovedadSummary, NormalTextColor, false, "20", JustificationValues.Center));
            dataRow.Append(noveltyCell);

            // Realizado Por cell (operator + title)
            var byCell = new TableCell();
            byCell.Append(new TableCellProperties(
                new TableCellWidth { Width = "1704", Type = TableWidthUnitValues.Dxa },
                new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center }));
            byCell.Append(CreateCellParagraph(data.OperatorName, NormalTextColor, false, "20", JustificationValues.Center));
            byCell.Append(CreateCellParagraph("Analista de infraestructura tecnológica", NormalTextColor, false, "18", JustificationValues.Center));
            dataRow.Append(byCell);

            table.Append(dataRow);
            return table;
        }

        // === Image handling ===

        private string AddImageToDocument(MainDocumentPart mainPart, byte[] imageBytes, string imageName)
        {
            var imgPart = mainPart.AddImagePart(ImagePartType.Png);
            using var stream = new MemoryStream(imageBytes);
            imgPart.FeedData(stream);
            return mainPart.GetIdOfPart(imgPart);
        }

        private Paragraph CreateImageParagraph(string relId, byte[] imageData)
        {
            // Calculate dimensions to fit page width (max ~6 inches = 5486400 EMU)
            long maxWidth = 5486400;
            long maxHeight = 7000000;

            // Try to get actual image dimensions
            long imgWidth = maxWidth;
            long imgHeight = (long)(maxWidth * 0.6); // default aspect ratio

            try
            {
                using var ms = new MemoryStream(imageData);
                using var img = System.Drawing.Image.FromStream(ms);
                double aspect = (double)img.Height / img.Width;
                imgWidth = maxWidth;
                imgHeight = (long)(maxWidth * aspect);

                if (imgHeight > maxHeight)
                {
                    imgHeight = maxHeight;
                    imgWidth = (long)(maxHeight / aspect);
                }
            }
            catch { /* use defaults */ }

            var element = new Drawing(
                new DW.Inline(
                    new DW.Extent { Cx = imgWidth, Cy = imgHeight },
                    new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
                    new DW.DocProperties { Id = (UInt32Value)1U, Name = "Image" },
                    new DW.NonVisualGraphicFrameDrawingProperties(
                        new A.GraphicFrameLocks { NoChangeAspect = true }),
                    new A.Graphic(
                        new A.GraphicData(
                            new PIC.Picture(
                                new PIC.NonVisualPictureProperties(
                                    new PIC.NonVisualDrawingProperties { Id = 0U, Name = "Image" },
                                    new PIC.NonVisualPictureDrawingProperties()),
                                new PIC.BlipFill(
                                    new A.Blip { Embed = relId },
                                    new A.Stretch(new A.FillRectangle())),
                                new PIC.ShapeProperties(
                                    new A.Transform2D(
                                        new A.Offset { X = 0, Y = 0 },
                                        new A.Extents { Cx = imgWidth, Cy = imgHeight }),
                                    new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })
                            )
                        )
                        { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }
                    )
                )
                { DistanceFromTop = 0U, DistanceFromBottom = 0U, DistanceFromLeft = 0U, DistanceFromRight = 0U }
            );

            return new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(element)
            );
        }

        // === Helpers ===

        private Paragraph CreateStyledParagraph(string text, string fontSize, bool bold, string color,
            JustificationValues justify)
        {
            var runProps = new RunProperties(
                new RunFonts { Ascii = "Arial", ComplexScript = "Arial" },
                new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color },
                new FontSize { Val = fontSize },
                new FontSizeComplexScript { Val = fontSize }
            );
            if (bold) runProps.Append(new Bold(), new BoldComplexScript());

            return new Paragraph(
                new ParagraphProperties(
                    new Justification { Val = justify },
                    new SpacingBetweenLines { Before = "0", After = "120" }
                ),
                new Run(runProps, new Text(text) { Space = SpaceProcessingModeValues.Preserve })
            );
        }

        private Paragraph CreateHeading(string text, int level)
        {
            string size = level == 1 ? "28" : "24";
            var para = new Paragraph(
                new ParagraphProperties(
                    new ParagraphStyleId { Val = $"Heading{level}" },
                    new SpacingBetweenLines { Before = "240", After = "120" }
                ),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new FontSize { Val = size },
                        new FontSizeComplexScript { Val = size },
                        new RunFonts { Ascii = "Arial", ComplexScript = "Arial" }
                    ),
                    new Text(text) { Space = SpaceProcessingModeValues.Preserve }
                )
            );
            return para;
        }

        private void AddStyles(MainDocumentPart mainPart)
        {
            var stylesPart = mainPart.AddNewPart<StyleDefinitionsPart>();
            var styles = new Styles();

            // Default paragraph style
            var defaultStyle = new Style { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
            defaultStyle.Append(new StyleName { Val = "Normal" });
            defaultStyle.Append(new StyleRunProperties(
                new RunFonts { Ascii = "Arial", HighAnsi = "Arial", ComplexScript = "Arial" },
                new FontSize { Val = "22" }
            ));
            styles.Append(defaultStyle);

            // Heading 1
            var h1 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading1" };
            h1.Append(new StyleName { Val = "heading 1" });
            h1.Append(new BasedOn { Val = "Normal" });
            h1.Append(new StyleParagraphProperties(
                new SpacingBetweenLines { Before = "240", After = "120" }
            ));
            h1.Append(new StyleRunProperties(
                new Bold(),
                new FontSize { Val = "28" },
                new RunFonts { Ascii = "Arial", ComplexScript = "Arial" }
            ));
            styles.Append(h1);

            // Heading 2
            var h2 = new Style { Type = StyleValues.Paragraph, StyleId = "Heading2" };
            h2.Append(new StyleName { Val = "heading 2" });
            h2.Append(new BasedOn { Val = "Normal" });
            h2.Append(new StyleParagraphProperties(
                new SpacingBetweenLines { Before = "200", After = "100" }
            ));
            h2.Append(new StyleRunProperties(
                new Bold(),
                new FontSize { Val = "24" },
                new RunFonts { Ascii = "Arial", ComplexScript = "Arial" }
            ));
            styles.Append(h2);

            stylesPart.Styles = styles;
            stylesPart.Styles.Save();
        }
    }
}
