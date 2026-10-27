using Microsoft.EntityFrameworkCore;
using ServiceRequests.Api.Data;
using ServiceRequests.Api.Development;
using ServiceRequests.Api.Domain;

namespace ServiceRequests.Api.Endpoints;

public sealed record RequestInput(string? Title, string? Description, Category? Category, Priority? Priority)
{
    public Dictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Title) || Title.Trim().Length > 200)
            errors["title"] = ["Informe um título entre 1 e 200 caracteres."];
        if (string.IsNullOrWhiteSpace(Description) || Description.Trim().Length > 5000)
            errors["description"] = ["Informe uma descrição entre 1 e 5000 caracteres."];
        if (Category is null || !Enum.IsDefined(Category.Value)) errors["category"] = ["Categoria inválida ou ausente."];
        if (Priority is null || !Enum.IsDefined(Priority.Value)) errors["priority"] = ["Prioridade inválida ou ausente."];
        return errors;
    }
}
public sealed record AssignmentInput(string? OperatorId);

public static class RequestEndpoints
{
    public static void MapRequests(this WebApplication app)
    {
        var group = app.MapGroup("/requests");
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!app.Environment.IsDevelopment())
                return Results.Problem(statusCode: 503, detail: "Identidade de laboratório desabilitada fora de Development.");
            var id = http.Request.Headers["X-Lab-User"].ToString();
            if (!LabIdentity.Users.TryGetValue(id, out var user))
                return Results.Problem(statusCode: 401, detail: "Informe uma identidade de laboratório válida no header X-Lab-User.");
            http.Items["LabUser"] = user;
            try { return await next(context); }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Problem(statusCode: 409, detail: "A solicitação foi alterada por outra operação. Consulte novamente.");
            }
            catch (BusinessRuleException ex)
            {
                return Results.Problem(statusCode: 409, detail: ex.Message);
            }
        });

        group.MapPost("/", async (RequestInput input, RequestsDbContext db, HttpContext http) =>
        {
            var user = LabIdentity.Current(http);
            if (user.Role != "Solicitante") return Forbidden();
            var errors = input.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            var request = new ServiceRequest(user.Id, input.Title!, input.Description!, input.Category!.Value, input.Priority!.Value);
            db.Requests.Add(request);
            await db.SaveChangesAsync();
            return Results.Created($"/requests/{request.Id}", request);
        });

        group.MapGet("/", async (RequestsDbContext db, HttpContext http) =>
        {
            var user = LabIdentity.Current(http);
            var query = Visible(db, user);
            return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
        });
        group.MapGet("/queue", async (RequestsDbContext db, HttpContext http) =>
        {
            if (LabIdentity.Current(http).Role != "Operador") return Forbidden();
            return Results.Ok(await db.Requests.AsNoTracking().Where(x => x.Status == RequestStatus.Criada && x.OperatorId == null)
                .OrderBy(x => x.CreatedAt).ToListAsync());
        });
        group.MapGet("/{id:guid}", async (Guid id, RequestsDbContext db, HttpContext http) =>
        {
            var request = await Visible(db, LabIdentity.Current(http)).SingleOrDefaultAsync(x => x.Id == id);
            return request is null ? Results.NotFound() : Results.Ok(request);
        });
        group.MapPut("/{id:guid}", async (Guid id, RequestInput input, RequestsDbContext db, HttpContext http) =>
        {
            var user = LabIdentity.Current(http);
            if (user.Role != "Solicitante") return Forbidden();
            var request = await db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.RequesterId == user.Id);
            if (request is null) return Results.NotFound();
            var errors = input.Validate();
            if (errors.Count > 0) return Results.ValidationProblem(errors);
            request.Edit(input.Title!, input.Description!, input.Category!.Value, input.Priority!.Value);
            await db.SaveChangesAsync();
            return Results.Ok(request);
        });
        group.MapPost("/{id:guid}/cancel", async (Guid id, RequestsDbContext db, HttpContext http) =>
        {
            var user = LabIdentity.Current(http);
            if (user.Role != "Solicitante") return Forbidden();
            var request = await db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.RequesterId == user.Id);
            if (request is null) return Results.NotFound();
            request.Cancel();
            await db.SaveChangesAsync();
            return Results.Ok(request);
        });
        group.MapPost("/{id:guid}/claim", async (Guid id, RequestsDbContext db, HttpContext http) =>
        {
            var user = LabIdentity.Current(http);
            if (user.Role != "Operador") return Forbidden();
            var request = await db.Requests.FindAsync(id);
            if (request is null) return Results.NotFound();
            if (request.OperatorId is not null && request.OperatorId != user.Id) return Results.NotFound();
            request.Assign(user.Id);
            await db.SaveChangesAsync();
            return Results.Ok(request);
        });
        group.MapPost("/{id:guid}/complete", async (Guid id, RequestsDbContext db, HttpContext http) =>
        {
            var user = LabIdentity.Current(http);
            if (user.Role != "Operador") return Forbidden();
            var request = await db.Requests.SingleOrDefaultAsync(x => x.Id == id && x.OperatorId == user.Id);
            if (request is null) return Results.NotFound();
            request.Complete();
            await db.SaveChangesAsync();
            return Results.Ok(request);
        });
        group.MapPost("/{id:guid}/assign", async (Guid id, AssignmentInput input, RequestsDbContext db, HttpContext http) =>
        {
            if (LabIdentity.Current(http).Role != "Administrador") return Forbidden();
            if (input.OperatorId is null || !LabIdentity.Users.TryGetValue(input.OperatorId, out var target) || target.Role != "Operador")
                return Results.ValidationProblem(new Dictionary<string, string[]> { ["operatorId"] = ["Informe um operador de laboratório válido."] });
            var request = await db.Requests.FindAsync(id);
            if (request is null) return Results.NotFound();
            request.Assign(target.Id);
            await db.SaveChangesAsync();
            return Results.Ok(request);
        });
    }

    private static IResult Forbidden() => Results.Problem(statusCode: 403, detail: "Este perfil não pode executar a operação.");
    private static IQueryable<ServiceRequest> Visible(RequestsDbContext db, LabUser user) => user.Role switch
    {
        "Solicitante" => db.Requests.AsNoTracking().Where(x => x.RequesterId == user.Id),
        "Operador" => db.Requests.AsNoTracking().Where(x => x.OperatorId == user.Id || (x.Status == RequestStatus.Criada && x.OperatorId == null)),
        _ => db.Requests.AsNoTracking()
    };
}
