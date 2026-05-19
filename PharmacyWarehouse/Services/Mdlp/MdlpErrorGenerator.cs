using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services.Mdlp;

public class MdlpErrorGenerator
{
    private readonly PharmacyWarehouseContext _context;
    private readonly Random _random;

    public MdlpErrorGenerator(PharmacyWarehouseContext context)
    {
        _context = context;
        _random = new Random();
    }

    public async Task<(string? ErrorCode, string? ErrorDesc)> CheckDeterministicErrorsAsync(Document document)
    {
        var settings = await _context.MdlpSettings.FirstOrDefaultAsync();
        if (settings != null && string.IsNullOrEmpty(settings.SubjectId))
            return ("1000", "SubjectId не задан в настройках");

        foreach (var line in document.DocumentLines)
        {
            var product = await _context.Products.FindAsync(line.ProductId);
            if (product != null && product.IsTracked && string.IsNullOrEmpty(product.Gtin))
                return ("1001", "GTIN не заполнен для маркированного товара");
        }

        if (document.Type == DocumentType.Outgoing || document.Type == DocumentType.WriteOff)
        {
            foreach (var line in document.DocumentLines)
            {
                if (!string.IsNullOrEmpty(line.Sgtin))
                {
                    var sgtin = await _context.MdlpSgtins.FirstOrDefaultAsync(s => s.Sgtin == line.Sgtin);
                    if (sgtin == null)
                        return ("1003", "SGTIN не найден в системе");
                    if (sgtin.Status != "InCirculation")
                        return ("1002", "Статус SGTIN не допускает операцию");
                }
            }
        }

        if (document.Date > DateTime.Now)
            return ("1004", "Дата документа в будущем");

        return (null, null);
    }

    public (string? ErrorCode, string? ErrorDesc) CheckRandomErrors(int simulatedErrorRate)
    {
        if (_random.Next(100) < simulatedErrorRate)
        {
            return _random.Next(2) switch
            {
                0 => ("5001", "Сервис временно недоступен"),
                1 => ("2003", "Статус препарата не допускает операцию"),
                _ => (null, null)
            };
        }
        return (null, null);
    }
}
