using Microsoft.Extensions.DependencyInjection;
using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;
using System;
using System.Threading.Tasks;

namespace PharmacyWarehouse.Services.Mdlp;

public class MdlpServiceProxy : IMdlpService
{
    private readonly IServiceProvider _serviceProvider;

    public MdlpServiceProxy(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task<MdlpDocument> SendReceiptAsync(Document document)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyWarehouseContext>();
        var settings = context.MdlpSettings.FirstOrDefault();
        bool useMock = settings == null || settings.UseMock;

        var service = useMock 
            ? (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpMockService>() 
            : (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpRealService>();

        return await service.SendReceiptAsync(document);
    }

    public async Task<MdlpDocument> SendOutgoingAsync(Document document)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyWarehouseContext>();
        var settings = context.MdlpSettings.FirstOrDefault();
        bool useMock = settings == null || settings.UseMock;

        var service = useMock 
            ? (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpMockService>() 
            : (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpRealService>();

        return await service.SendOutgoingAsync(document);
    }

    public async Task<MdlpDocument> SendWriteOffAsync(Document document)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyWarehouseContext>();
        var settings = context.MdlpSettings.FirstOrDefault();
        bool useMock = settings == null || settings.UseMock;

        var service = useMock 
            ? (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpMockService>() 
            : (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpRealService>();

        return await service.SendWriteOffAsync(document);
    }

    public async Task<MdlpDocument> RetryDocumentAsync(MdlpDocument document)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyWarehouseContext>();
        var settings = context.MdlpSettings.FirstOrDefault();
        bool useMock = settings == null || settings.UseMock;

        var service = useMock 
            ? (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpMockService>() 
            : (IMdlpService)scope.ServiceProvider.GetRequiredService<MdlpRealService>();

        return await service.RetryDocumentAsync(document);
    }
}
