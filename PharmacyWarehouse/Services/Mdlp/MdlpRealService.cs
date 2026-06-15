using System;
using System.Net.Http;
using System.Threading.Tasks;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services.Mdlp;

public class MdlpRealService : IMdlpService
{
    private readonly HttpClient _httpClient;
    private readonly MdlpSetting _settings;

    public MdlpRealService(HttpClient httpClient, MdlpSetting settings)
    {
        _httpClient = httpClient;
        _settings = settings;
    }

    public Task<MdlpDocument> SendReceiptAsync(Document document)
    {
        throw new NotImplementedException("Реальный МДЛП API не подключён. Используйте Mock-режим в настройках.");
    }

    public Task<MdlpDocument> SendOutgoingAsync(Document document)
    {
        throw new NotImplementedException("Реальный МДЛП API не подключён. Используйте Mock-режим в настройках.");
    }

    public Task<MdlpDocument> SendWriteOffAsync(Document document)
    {
        throw new NotImplementedException("Реальный МДЛП API не подключён. Используйте Mock-режим в настройках.");
    }

    public Task<MdlpDocument> RetryDocumentAsync(MdlpDocument document)
    {
        throw new NotImplementedException("Реальный МДЛП API не подключён. Используйте Mock-режим в настройках.");
    }
}
