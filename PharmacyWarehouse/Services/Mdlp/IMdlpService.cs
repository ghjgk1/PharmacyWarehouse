using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services.Mdlp;

public interface IMdlpService
{
    Task<MdlpDocument> SendReceiptAsync(Document document);
    Task<MdlpDocument> SendOutgoingAsync(Document document);
    Task<MdlpDocument> SendWriteOffAsync(Document document);
    Task<MdlpDocument> RetryDocumentAsync(MdlpDocument previousDocument);
}
