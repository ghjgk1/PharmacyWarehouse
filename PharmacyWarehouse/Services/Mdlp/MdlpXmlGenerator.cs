using System;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services.Mdlp;

public class MdlpXmlGenerator
{
    private readonly XNamespace _ns = "urn:sklz-ru:documents:mdlp:v2";
    private readonly XNamespace _xsi = "http://www.w3.org/2001/XMLSchema-instance";

    public string GenerateSchema415(Document document, string subjectId, string receiverInn)
    {
        var root = new XElement(_ns + "document",
            new XAttribute(XNamespace.Xmlns + "xsi", _xsi),
            new XAttribute(_xsi + "schemaLocation", "urn:sklz-ru:documents:mdlp:v2 mdlp.xsd"),
            new XAttribute("action_id", "415"),
            new XElement(_ns + "doc_num", document.SupplierInvoiceNumber ?? document.Number),
            new XElement(_ns + "doc_date", document.Date.ToString("yyyy-MM-dd")),
            new XElement(_ns + "subject_id", subjectId),
            new XElement(_ns + "receiver_id", receiverInn),
            new XElement(_ns + "operation_date", document.Date.ToString("yyyy-MM-dd")),
            new XElement(_ns + "signs",
                document.DocumentLines.SelectMany(line =>
                {
                    string gtin = line.Product.Gtin?.PadLeft(14, '0') ?? new string('0', 14);
                    return Enumerable.Range(0, line.Quantity).Select(i =>
                    {
                        string serialNumber = (i + 1).ToString("D13");
                        return new XElement(_ns + "sign",
                            new XElement(_ns + "gtin", gtin),
                            new XElement(_ns + "serial_number", serialNumber)
                        );
                    });
                })
            )
        );

        return root.ToString();
    }

    public string GenerateSchema416(Document document, string subjectId)
    {
        var root = new XElement(_ns + "document",
            new XAttribute(XNamespace.Xmlns + "xsi", _xsi),
            new XAttribute(_xsi + "schemaLocation", "urn:sklz-ru:documents:mdlp:v2 mdlp.xsd"),
            new XAttribute("action_id", "416"),
            new XElement(_ns + "doc_num", document.Number),
            new XElement(_ns + "doc_date", document.Date.ToString("yyyy-MM-dd")),
            new XElement(_ns + "subject_id", subjectId),
            new XElement(_ns + "operation_date", document.Date.ToString("yyyy-MM-dd")),
            new XElement(_ns + "signs",
                document.DocumentLines.SelectMany(line =>
                {
                    string gtin = line.Product.Gtin?.PadLeft(14, '0') ?? new string('0', 14);
                    return Enumerable.Range(0, line.Quantity).Select(i =>
                    {
                        string serialNumber = (i + 1).ToString("D13");
                        return new XElement(_ns + "sign",
                            new XElement(_ns + "gtin", gtin),
                            new XElement(_ns + "serial_number", serialNumber)
                        );
                    });
                })
            )
        );

        return root.ToString();
    }

    public string GenerateSchema552(Document document, string subjectId)
    {
        string reasonText = document.WriteOffReason switch
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
            _ => "Иное"
        };

        var root = new XElement(_ns + "document",
            new XAttribute(XNamespace.Xmlns + "xsi", _xsi),
            new XAttribute(_xsi + "schemaLocation", "urn:sklz-ru:documents:mdlp:v2 mdlp.xsd"),
            new XAttribute("action_id", "552"),
            new XElement(_ns + "doc_num", document.Number),
            new XElement(_ns + "doc_date", document.Date.ToString("yyyy-MM-dd")),
            new XElement(_ns + "subject_id", subjectId),
            new XElement(_ns + "operation_date", document.Date.ToString("yyyy-MM-dd")),
            new XElement(_ns + "act_write_off_reason", reasonText),
            new XElement(_ns + "signs",
                document.DocumentLines.SelectMany(line =>
                {
                    string gtin = line.Product.Gtin?.PadLeft(14, '0') ?? new string('0', 14);
                    return Enumerable.Range(0, line.Quantity).Select(i =>
                    {
                        string serialNumber = (i + 1).ToString("D13");
                        return new XElement(_ns + "sign",
                            new XElement(_ns + "gtin", gtin),
                            new XElement(_ns + "serial_number", serialNumber)
                        );
                    });
                })
            )
        );

        return root.ToString();
    }

    public string GenerateResponseXml(string ticket, string operationId, string status, DateTime createTime, string? errorCode = null, string? errorDesc = null, int? errorPosition = null)
    {
        var root = new XElement(_ns + "response",
            new XAttribute(XNamespace.Xmlns + "xsi", _xsi),
            new XElement(_ns + "ticket", ticket),
            new XElement(_ns + "operation_id", operationId),
            new XElement(_ns + "status", status),
            new XElement(_ns + "create_time", createTime.ToString("yyyy-MM-ddTHH:mm:ss"))
        );

        if (!string.IsNullOrEmpty(errorCode) || !string.IsNullOrEmpty(errorDesc))
        {
            var errorsElement = new XElement(_ns + "errors");
            if (!string.IsNullOrEmpty(errorCode))
                errorsElement.Add(new XElement(_ns + "error_code", errorCode));
            if (!string.IsNullOrEmpty(errorDesc))
                errorsElement.Add(new XElement(_ns + "error_desc", errorDesc));
            if (errorPosition.HasValue)
                errorsElement.Add(new XElement(_ns + "error_position", errorPosition.Value));
            root.Add(errorsElement);
        }

        return root.ToString();
    }
}
