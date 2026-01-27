using System;
using System.IO;
using System.Linq;
using System.Windows;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Syncfusion.DocIO.DLS;
using ModelsDocument = PharmacyWarehouse.Models.Document;
using WordDocument = DocumentFormat.OpenXml.Wordprocessing.Document;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services.DocumentGeneration
{
    public interface IWordDocumentGenerator
    {
        void GenerateArrivalInvoice(ModelsDocument document, string outputPath);
        void GenerateOutgoingInvoice(ModelsDocument document, string outputPath);
        void GenerateWriteOffAct(ModelsDocument document, string outputPath);
        void GenerateCorrectionAct(ModelsDocument document, string outputPath);
        void GenerateGenericDocument(ModelsDocument document, string templatePath, string outputPath);
    }

    public class WordDocumentGenerator : IWordDocumentGenerator
    {
        private readonly DocumentService _documentService;
        private readonly ProductService _productService;

        public WordDocumentGenerator(DocumentService documentService, ProductService productService)
        {
            _documentService = documentService;
            _productService = productService;
        }

        public void GenerateArrivalInvoice(ModelsDocument document, string outputPath)
        {
            try
            {
                // Загружаем документ с деталями
                var fullDocument = _documentService.GetDocumentWithDetails(document.Id);
                if (fullDocument == null) throw new Exception("Документ не найден");

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
                {
                    // Добавляем основную часть документа
                    MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                    mainPart.Document = new WordDocument();
                    Body body = new Body();

                    // Заголовок документа
                    body.Append(CreateTitle("ПРИХОДНАЯ НАКЛАДНАЯ"));
                    body.Append(CreateDocumentInfo(fullDocument));

                    // Таблица с товарами
                    body.Append(CreateProductsTable(fullDocument));

                    // Подписи
                    body.Append(CreateSignaturesSection(fullDocument));

                    // Итоговая сумма
                    body.Append(CreateTotalAmount(fullDocument.TotalAmount));

                    mainPart.Document.Append(body);
                    mainPart.Document.Save();
                }

                MessageBox.Show($"Документ сохранен: {outputPath}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации документа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void GenerateOutgoingInvoice(ModelsDocument document, string outputPath)
        {
            try
            {
                var fullDocument = _documentService.GetDocumentWithDetails(document.Id);
                if (fullDocument == null) throw new Exception("Документ не найден");

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                    mainPart.Document = new WordDocument();
                    Body body = new Body();

                    body.Append(CreateTitle("РАСХОДНАЯ НАКЛАДНАЯ"));
                    body.Append(CreateDocumentInfo(fullDocument));
                    body.Append(CreateProductsTable(fullDocument));
                    body.Append(CreateSignaturesSection(fullDocument));
                    body.Append(CreateTotalAmount(fullDocument.TotalAmount));

                    mainPart.Document.Append(body);
                    mainPart.Document.Save();
                }

                MessageBox.Show($"Расходная накладная сохранена: {outputPath}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации документа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void GenerateWriteOffAct(ModelsDocument document, string outputPath)
        {
            try
            {
                var fullDocument = _documentService.GetDocumentWithDetails(document.Id);
                if (fullDocument == null) throw new Exception("Документ не найден");

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                    mainPart.Document = new WordDocument();
                    Body body = new Body();

                    body.Append(CreateTitle("АКТ СПИСАНИЯ ТОВАРОВ"));
                    body.Append(CreateDocumentInfo(fullDocument));

                    // Добавляем причину списания
                    if (fullDocument.WriteOffReason.HasValue)
                    {
                        string reason = GetWriteOffReasonDescription(fullDocument.WriteOffReason.Value);
                        body.Append(CreateParagraph($"Причина списания: {reason}", 14, false));
                    }

                    if (!string.IsNullOrEmpty(fullDocument.Notes))
                    {
                        body.Append(CreateParagraph($"Примечание: {fullDocument.Notes}", 12, false));
                    }

                    body.Append(CreateProductsTable(fullDocument));
                    body.Append(CreateSignaturesSection(fullDocument));

                    mainPart.Document.Append(body);
                    mainPart.Document.Save();
                }

                MessageBox.Show($"Акт списания сохранен: {outputPath}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации документа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void GenerateCorrectionAct(ModelsDocument document, string outputPath)
        {
            try
            {
                var fullDocument = _documentService.GetDocumentWithCorrections(document.Id);
                if (fullDocument == null) throw new Exception("Документ не найден");

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Create(outputPath, WordprocessingDocumentType.Document))
                {
                    MainDocumentPart mainPart = wordDoc.AddMainDocumentPart();
                    mainPart.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                    Body body = new Body();

                    // Заголовок документа
                    body.Append(CreateTitle("КОРРЕКТИРОВОЧНЫЙ АКТ"));

                    // Информация о документе
                    string docInfo = $"от {DateTime.Now:dd.MM.yyyy} № {document.Number}";
                    body.Append(CreateParagraph(docInfo, 14, false, JustificationValues.Left));

                    // Информация о базовом документе
                    if (fullDocument.OriginalDocumentId.HasValue)
                    {
                        var originalDoc = _documentService.GetById(fullDocument.OriginalDocumentId.Value);
                        if (originalDoc != null)
                        {
                            string originalDocInfo = $"Базовый документ: {originalDoc.Number} от {originalDoc.Date:dd.MM.yyyy}";
                            body.Append(CreateParagraph(originalDocInfo, 12, false, JustificationValues.Left));
                        }
                    }

                    // Причина корректировки
                    if (!string.IsNullOrEmpty(fullDocument.CorrectionReason))
                    {
                        string reasonText = $"Причина корректировки: {fullDocument.CorrectionReason}";
                        body.Append(CreateParagraph(reasonText, 12, false, JustificationValues.Left));
                    }

                    // Добавляем отступ
                    body.Append(CreateParagraph("", 12, false));

                    // Таблица с товарами - стиль как в примере
                    body.Append(CreateCorrectionTable(fullDocument));

                    // Добавляем отступ
                    body.Append(CreateParagraph("", 12, false));

                    // Подписи
                    body.Append(CreateSignaturesSection(fullDocument));

                    mainPart.Document.Append(body);
                    mainPart.Document.Save();
                }

                MessageBox.Show($"Корректировочный акт сохранен: {outputPath}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации документа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void GenerateGenericDocument(ModelsDocument document, string templatePath, string outputPath)
        {
            // Метод для использования шаблонов Word
            try
            {
                if (!File.Exists(templatePath))
                    throw new FileNotFoundException("Шаблон не найден", templatePath);

                File.Copy(templatePath, outputPath, true);

                using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputPath, true))
                {
                    var body = wordDoc.MainDocumentPart.Document.Body;

                    // Заменяем метки в шаблоне
                    ReplacePlaceholder(body, "{DOC_NUMBER}", document.Number);
                    ReplacePlaceholder(body, "{DOC_DATE}", document.Date.ToString("dd.MM.yyyy"));
                    ReplacePlaceholder(body, "{CREATED_BY}", document.CreatedBy);
                    ReplacePlaceholder(body, "{NOTES}", document.Notes ?? "");

                    wordDoc.MainDocumentPart.Document.Save();
                }

                MessageBox.Show($"Документ создан: {outputPath}", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации документа: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Вспомогательные методы для создания элементов документа

        private Table CreateCorrectionTable(ModelsDocument document)
        {
            Table table = new Table();

            // Настройки таблицы
            TableProperties tableProperties = new TableProperties();
            TableBorders tableBorders = new TableBorders();
            tableBorders.Append(new TopBorder() { Val = BorderValues.Single, Size = 2 });
            tableBorders.Append(new BottomBorder() { Val = BorderValues.Single, Size = 2 });
            tableBorders.Append(new LeftBorder() { Val = BorderValues.Single, Size = 2 });
            tableBorders.Append(new RightBorder() { Val = BorderValues.Single, Size = 2 });
            tableBorders.Append(new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 1 });
            tableBorders.Append(new InsideVerticalBorder() { Val = BorderValues.Single, Size = 1 });
            tableProperties.Append(tableBorders);
            tableProperties.Append(new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Pct });

            // Центрирование таблицы
            TableJustification tableJustification = new TableJustification() { Val = TableRowAlignmentValues.Center };
            tableProperties.Append(tableJustification);

            table.Append(tableProperties);

            // Заголовок таблицы - как в примере
            TableRow headerRow = new TableRow();
            string[] headers = {
        "№",
        "Наименование товара",
        "Количество до изменения, шт",
        "Количество после изменения, шт",
        "Цена за единицу до изменения, руб.",
        "Цена за единицы после изменения, руб.",
        "Стоимость до изменения, руб.",
        "Стоимость после изменения, руб."
    };

            foreach (string header in headers)
            {
                TableCell cell = CreateTableCell(header, true, JustificationValues.Center);
                headerRow.Append(cell);
            }
            table.Append(headerRow);

            // Строки с товарами
            int rowNumber = 1;
            decimal totalAmountBefore = 0;
            decimal totalAmountAfter = 0;

            if (document.DocumentLines != null && document.DocumentLines.Any())
            {
                foreach (var line in document.DocumentLines.OrderBy(l => l.Id))
                {
                    var product = _productService.GetById(line.ProductId);
                    if (product == null) continue;

                    // Для корректировок используем OldValue и NewValue
                    int quantityBefore = 0;
                    int quantityAfter = line.Quantity;
                    decimal priceBefore = 0;
                    decimal priceAfter = line.UnitPrice;

                    if (!string.IsNullOrEmpty(line.OldValue) && document.CorrectionType == CorrectionType.Quantity)
                    {
                        int.TryParse(line.OldValue, out quantityBefore);
                    }
                    else if (!string.IsNullOrEmpty(line.OldValue) && document.CorrectionType == CorrectionType.Price)
                    {
                        decimal.TryParse(line.OldValue, out priceBefore);
                    }

                    decimal amountBefore = quantityBefore * priceBefore;
                    decimal amountAfter = quantityAfter * priceAfter;

                    totalAmountBefore += amountBefore;
                    totalAmountAfter += amountAfter;

                    TableRow row = new TableRow();
                    row.Append(CreateTableCell(rowNumber.ToString(), false, JustificationValues.Center));
                    row.Append(CreateTableCell(product.Name ?? "-", false, JustificationValues.Left));
                    row.Append(CreateTableCell(quantityBefore.ToString("N0"), false, JustificationValues.Center));
                    row.Append(CreateTableCell(quantityAfter.ToString("N0"), false, JustificationValues.Center));
                    row.Append(CreateTableCell(priceBefore.ToString("N2"), false, JustificationValues.Right));
                    row.Append(CreateTableCell(priceAfter.ToString("N2"), false, JustificationValues.Right));
                    row.Append(CreateTableCell(amountBefore.ToString("N2"), false, JustificationValues.Right));
                    row.Append(CreateTableCell(amountAfter.ToString("N2"), false, JustificationValues.Right));

                    table.Append(row);
                    rowNumber++;
                }
            }

            // Итоговая строка - как в примере
            TableRow totalRow1 = new TableRow();
            totalRow1.Append(CreateTableCell("Итого:", true, JustificationValues.Left, 6));
            totalRow1.Append(CreateTableCell(totalAmountBefore.ToString("N2"), true, JustificationValues.Right));
            totalRow1.Append(CreateTableCell(totalAmountAfter.ToString("N2"), true, JustificationValues.Right));
            table.Append(totalRow1);

            // Строка "НДС не облагается" - как в примере
            TableRow ndsRow = new TableRow();
            ndsRow.Append(CreateTableCell("НДС не облагается", false, JustificationValues.Left, 7));
            ndsRow.Append(CreateTableCell("", false, JustificationValues.Right));
            table.Append(ndsRow);

            // Строка "Всего" - как в примере
            TableRow totalRow2 = new TableRow();
            totalRow2.Append(CreateTableCell("Всего:", true, JustificationValues.Left, 7));
            totalRow2.Append(CreateTableCell(totalAmountAfter.ToString("N2"), true, JustificationValues.Right));
            table.Append(totalRow2);

            return table;
        }

        private TableCell CreateTableCell(string text, bool isBold, JustificationValues alignment, int colSpan = 1)
        {
            TableCell cell = new TableCell();
            TableCellProperties cellProperties = new TableCellProperties();
            cellProperties.Append(new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center });

            if (colSpan > 1)
            {
                cellProperties.Append(new GridSpan() { Val = colSpan });
            }

            Paragraph paragraph = new Paragraph();
            ParagraphProperties paragraphProperties = new ParagraphProperties();
            paragraphProperties.Append(new Justification() { Val = alignment });

            Run run = new Run();
            RunProperties runProperties = new RunProperties();
            if (isBold) runProperties.Append(new Bold());
            runProperties.Append(new FontSize() { Val = "11" });
            runProperties.Append(new RunFonts() { Ascii = "Arial" });

            run.Append(runProperties);
            run.Append(new Text(text));

            paragraph.Append(paragraphProperties);
            paragraph.Append(run);
            cell.Append(cellProperties);
            cell.Append(paragraph);

            return cell;
        }

        private Paragraph CreateParagraph(string text, int fontSize, bool isBold, JustificationValues alignment)
        {
            Paragraph paragraph = new Paragraph();
            ParagraphProperties paragraphProperties = new ParagraphProperties();
            paragraphProperties.Append(new SpacingBetweenLines() { After = "100" });
            paragraphProperties.Append(new Justification() { Val = alignment = JustificationValues.Left});

            Run run = new Run();
            RunProperties runProperties = new RunProperties();
            if (isBold) runProperties.Append(new Bold());
            runProperties.Append(new FontSize() { Val = fontSize.ToString() });
            runProperties.Append(new RunFonts() { Ascii = "Arial" });

            run.Append(runProperties);

            // Разбиваем текст на строки
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                run.Append(new Text(lines[i]));
                if (i < lines.Length - 1)
                {
                    run.Append(new DocumentFormat.OpenXml.Wordprocessing.Break());
                }
            }

            paragraph.Append(paragraphProperties);
            paragraph.Append(run);

            return paragraph;
        }

        private Paragraph CreateTitle(string titleText)
        {
            Paragraph paragraph = new Paragraph();
            ParagraphProperties paragraphProperties = new ParagraphProperties();
            paragraphProperties.Append(new Justification() { Val = JustificationValues.Center });

            Run run = new Run();
            RunProperties runProperties = new RunProperties();
            runProperties.Append(new Bold());
            runProperties.Append(new FontSize() { Val = "28" });
            runProperties.Append(new RunFonts() { Ascii = "Times New Roman" });

            run.Append(runProperties);
            run.Append(new Text(titleText));

            paragraph.Append(paragraphProperties);
            paragraph.Append(run);

            return paragraph;
        }

        private Paragraph CreateDocumentInfo(ModelsDocument document)
        {
            string infoText = $"№ {document.Number} от {document.Date:dd.MM.yyyy} г.\n" +
                             $"Поставщик/Получатель: {(document.Supplier?.Name ?? document.CustomerName ?? "-")}\n" +
                             $"Основание: {(string.IsNullOrEmpty(document.Notes) ? "-" : document.Notes)}\n" +
                             $"Создал: {document.CreatedBy} {document.CreatedAt:dd.MM.yyyy HH:mm}";

            if (document.SignedBy != null)
            {
                infoText += $"\nПровел: {document.SignedBy} {document.SignedAt:dd.MM.yyyy HH:mm}";
            }

            return CreateParagraph(infoText, 12, false);
        }

        private Table CreateProductsTable(ModelsDocument document)
        {
            Table table = new Table();

            // Настройки таблицы
            TableProperties tableProperties = new TableProperties();
            TableBorders tableBorders = new TableBorders();
            tableBorders.Append(new TopBorder() { Val = BorderValues.Single, Size = 4 });
            tableBorders.Append(new BottomBorder() { Val = BorderValues.Single, Size = 4 });
            tableBorders.Append(new LeftBorder() { Val = BorderValues.Single, Size = 4 });
            tableBorders.Append(new RightBorder() { Val = BorderValues.Single, Size = 4 });
            tableBorders.Append(new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 2 });
            tableBorders.Append(new InsideVerticalBorder() { Val = BorderValues.Single, Size = 2 });
            tableProperties.Append(tableBorders);
            tableProperties.Append(new TableWidth() { Width = "100%", Type = TableWidthUnitValues.Pct });
            table.Append(tableProperties);

            // Заголовок таблицы
            TableRow headerRow = new TableRow();
            string[] headers = { "№", "Наименование товара", "Серия", "Срок годности", "Ед. изм.", "Кол-во", "Цена", "Сумма" };

            foreach (string header in headers)
            {
                TableCell cell = CreateTableCell(header, true);
                headerRow.Append(cell);
            }
            table.Append(headerRow);

            // Строки с товарами
            int rowNumber = 1;
            decimal totalAmount = 0;

            if (document.DocumentLines != null && document.DocumentLines.Any())
            {
                foreach (var line in document.DocumentLines.OrderBy(l => l.Id))
                {
                    var product = _productService.GetById(line.ProductId);
                    if (product == null) continue;

                    decimal lineAmount = line.TotalPrice;
                    totalAmount += lineAmount;

                    TableRow row = new TableRow();
                    row.Append(CreateTableCell(rowNumber.ToString(), false));
                    row.Append(CreateTableCell(product.Name ?? "-", false));
                    row.Append(CreateTableCell(line.Series ?? "-", false));
                    row.Append(CreateTableCell(line.ExpirationDate?.ToString("dd.MM.yyyy") ?? "-", false));
                    row.Append(CreateTableCell(product.UnitOfMeasure ?? "шт.", false));
                    row.Append(CreateTableCell(line.Quantity.ToString("N2"), false));
                    row.Append(CreateTableCell(line.UnitPrice.ToString("N2"), false));
                    row.Append(CreateTableCell(lineAmount.ToString("N2"), false));

                    table.Append(row);
                    rowNumber++;
                }
            }

            // Итоговая строка
            TableRow totalRow = new TableRow();
            totalRow.Append(CreateTableCell("ИТОГО:", true, 7));
            totalRow.Append(CreateTableCell(totalAmount.ToString("N2") + " руб.", true));
            table.Append(totalRow);

            return table;
        }

        private TableCell CreateTableCell(string text, bool isBold, int colSpan = 1)
        {
            TableCell cell = new TableCell();
            TableCellProperties cellProperties = new TableCellProperties();
            cellProperties.Append(new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center });

            if (colSpan > 1)
            {
                cellProperties.Append(new GridSpan() { Val = colSpan });
            }

            Paragraph paragraph = new Paragraph();
            ParagraphProperties paragraphProperties = new ParagraphProperties();
            paragraphProperties.Append(new Justification() { Val = JustificationValues.Center });

            Run run = new Run();
            RunProperties runProperties = new RunProperties();
            if (isBold) runProperties.Append(new Bold());
            runProperties.Append(new FontSize() { Val = "12" });

            run.Append(runProperties);
            run.Append(new Text(text));

            paragraph.Append(paragraphProperties);
            paragraph.Append(run);
            cell.Append(cellProperties);
            cell.Append(paragraph);

            return cell;
        }

        private Paragraph CreateSignaturesSection(ModelsDocument document)
        {
            string signaturesText = $"\n\nОтпустил: _________________ / {document.CreatedBy} /\n" +
                                   $"Получил: _________________ / _________________ /\n" +
                                   $"Дата: {DateTime.Now:dd.MM.yyyy}";

            return CreateParagraph(signaturesText, 12, false);
        }

        private Paragraph CreateTotalAmount(decimal amount)
        {
            string totalText = $"Всего к оплате: {amount:N2} руб.\n" +
                              $"Сумма прописью: {NumberToWordsRu.Convert(amount)}";

            return CreateParagraph(totalText, 14, true);
        }

        private Paragraph CreateParagraph(string text, int fontSize, bool isBold)
        {
            Paragraph paragraph = new Paragraph();
            ParagraphProperties paragraphProperties = new ParagraphProperties();
            paragraphProperties.Append(new SpacingBetweenLines() { After = "100" });

            Run run = new Run();
            RunProperties runProperties = new RunProperties();
            if (isBold) runProperties.Append(new Bold());
            runProperties.Append(new FontSize() { Val = fontSize.ToString() });

            run.Append(runProperties);

            // Разбиваем текст на строки
            string[] lines = text.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                run.Append(new Text(lines[i]));
                if (i < lines.Length - 1)
                {
                    run.Append(new DocumentFormat.OpenXml.Wordprocessing.Break());
                }
            }

            paragraph.Append(paragraphProperties);
            paragraph.Append(run);

            return paragraph;
        }

        private void ReplacePlaceholder(Body body, string placeholder, string replacement)
        {
            foreach (var text in body.Descendants<Text>())
            {
                if (text.Text.Contains(placeholder))
                {
                    text.Text = text.Text.Replace(placeholder, replacement);
                }
            }
        }

        private string GetWriteOffReasonDescription(WriteOffReason reason)
        {
            return reason switch
            {
                WriteOffReason.Expired => "Просрочка",
                WriteOffReason.Damaged => "Бой/повреждение",
                WriteOffReason.Defect => "Брак",
                WriteOffReason.Lost => "Потеря",
                WriteOffReason.Recall => "Отзыв Росздравнадзора",
                WriteOffReason.SaleBan => "Запрет на реализацию",
                WriteOffReason.StorageViolation => "Нарушение условий хранения",
                WriteOffReason.Utilization => "Утилизация",
                WriteOffReason.InventoryDifference => "Инвентаризационная разница",
                WriteOffReason.Other => "Иное",
                _ => "Не указано"
            };
        }

        #endregion
    }

    // Класс для преобразования чисел в слова (русский язык)
    public static class NumberToWordsRu
    {
        private static readonly string[] units = { "", "один", "два", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
        private static readonly string[] unitsFemale = { "", "одна", "две", "три", "четыре", "пять", "шесть", "семь", "восемь", "девять" };
        private static readonly string[] teens = { "десять", "одиннадцать", "двенадцать", "тринадцать", "четырнадцать", "пятнадцать", "шестнадцать", "семнадцать", "восемнадцать", "девятнадцать" };
        private static readonly string[] tens = { "", "", "двадцать", "тридцать", "сорок", "пятьдесят", "шестьдесят", "семьдесят", "восемьдесят", "девяносто" };
        private static readonly string[] hundreds = { "", "сто", "двести", "триста", "четыреста", "пятьсот", "шестьсот", "семьсот", "восемьсот", "девятьсот" };

        private static readonly string[] thousands = { "", "тысяча", "тысячи", "тысяч" };
        private static readonly string[] millions = { "", "миллион", "миллиона", "миллионов" };
        private static readonly string[] billions = { "", "миллиард", "миллиарда", "миллиардов" };

        public static string Convert(decimal number)
        {
            if (number == 0) return "ноль рублей 00 копеек";

            long rubles = (long)Math.Floor(number);
            long kopecks = (long)((number - rubles) * 100);

            string rublesText = ConvertNumber(rubles, true);
            string kopecksText = ConvertNumber(kopecks, false);

            string result = rublesText + " " + GetRublesWord(rubles);

            if (kopecks > 0)
            {
                result += " " + kopecksText + " " + GetKopecksWord(kopecks);
            }
            else
            {
                result += " 00 копеек";
            }

            // Первая буква заглавная
            if (!string.IsNullOrEmpty(result))
            {
                result = char.ToUpper(result[0]) + result.Substring(1);
            }

            return result.Trim();
        }

        private static string ConvertNumber(long number, bool isRubles)
        {
            if (number == 0) return "ноль";

            string result = "";

            // Миллиарды
            long billionsCount = number / 1000000000;
            if (billionsCount > 0)
            {
                result += ConvertHundreds(billionsCount, false) + " " + GetForm(billionsCount, billions) + " ";
                number %= 1000000000;
            }

            // Миллионы
            long millionsCount = number / 1000000;
            if (millionsCount > 0)
            {
                result += ConvertHundreds(millionsCount, false) + " " + GetForm(millionsCount, millions) + " ";
                number %= 1000000;
            }

            // Тысячи
            long thousandsCount = number / 1000;
            if (thousandsCount > 0)
            {
                result += ConvertHundreds(thousandsCount, true) + " " + GetForm(thousandsCount, thousands) + " ";
                number %= 1000;
            }

            // Сотни, десятки, единицы
            if (number > 0 || string.IsNullOrEmpty(result))
            {
                result += ConvertHundreds(number, isRubles);
            }

            return result.Trim();
        }

        private static string ConvertHundreds(long number, bool useFemaleForm)
        {
            if (number == 0) return "";

            string result = "";

            // Сотни
            long hundredsCount = number / 100;
            if (hundredsCount > 0)
            {
                result += hundreds[hundredsCount] + " ";
                number %= 100;
            }

            // Десятки и единицы
            if (number >= 20)
            {
                long tensCount = number / 10;
                result += tens[tensCount] + " ";
                number %= 10;

                if (number > 0)
                {
                    result += (useFemaleForm ? unitsFemale[number] : units[number]) + " ";
                }
            }
            else if (number >= 10)
            {
                result += teens[number - 10] + " ";
            }
            else if (number > 0)
            {
                result += (useFemaleForm ? unitsFemale[number] : units[number]) + " ";
            }

            return result.Trim();
        }

        private static string GetForm(long number, string[] forms)
        {
            long lastDigit = number % 10;
            long lastTwoDigits = number % 100;

            if (lastTwoDigits >= 11 && lastTwoDigits <= 19) return forms[3];
            if (lastDigit == 1) return forms[1];
            if (lastDigit >= 2 && lastDigit <= 4) return forms[2];
            return forms[3];
        }

        private static string GetRublesWord(long number)
        {
            long lastDigit = number % 10;
            long lastTwoDigits = number % 100;

            if (lastTwoDigits >= 11 && lastTwoDigits <= 19) return "рублей";
            if (lastDigit == 1) return "рубль";
            if (lastDigit >= 2 && lastDigit <= 4) return "рубля";
            return "рублей";
        }

        private static string GetKopecksWord(long number)
        {
            long lastDigit = number % 10;
            long lastTwoDigits = number % 100;

            if (lastTwoDigits >= 11 && lastTwoDigits <= 19) return "копеек";
            if (lastDigit == 1) return "копейка";
            if (lastDigit >= 2 && lastDigit <= 4) return "копейки";
            return "копеек";
        }
    }
}