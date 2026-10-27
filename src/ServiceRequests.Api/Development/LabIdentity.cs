namespace ServiceRequests.Api.Development;

// Identidades fixas para exercitar autorização; não são autenticação real.
public sealed record LabUser(string Id, string Role);
public static class LabIdentity
{
    public static readonly IReadOnlyDictionary<string, LabUser> Users = new Dictionary<string, LabUser>
    {
        ["solicitante-1"] = new("solicitante-1", "Solicitante"),
        ["solicitante-2"] = new("solicitante-2", "Solicitante"),
        ["operador-1"] = new("operador-1", "Operador"),
        ["operador-2"] = new("operador-2", "Operador"),
        ["admin-1"] = new("admin-1", "Administrador")
    };
    public static LabUser Current(HttpContext context) => (LabUser)context.Items["LabUser"]!;
}
