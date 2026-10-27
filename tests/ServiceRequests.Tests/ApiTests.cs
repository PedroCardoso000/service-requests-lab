using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceRequests.Api.Data;

namespace ServiceRequests.Tests;

public class ApiTests
{
    private static object Body(string title = "Título") => new { title, description = "Descrição", category = "SuporteDeTI", priority = "Alta" };
    private static async Task<JsonElement> Create(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/requests", Body());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var request = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.EndsWith(request.GetProperty("id").GetString(), response.Headers.Location!.ToString());
        return request;
    }
    private static string Id(JsonElement request) => request.GetProperty("id").GetString()!;
    private static async Task<string[]> Ids(HttpClient client, string path = "/requests") =>
        (await client.GetFromJsonAsync<JsonElement[]>(path))!.Select(Id).ToArray();

    [Fact]
    public async Task OwnerCanEditAndReadPersistedDataButOtherRequesterCannot()
    {
        using var app = new ApiFactory();
        using var owner = app.User("solicitante-1");
        using var other = app.User("solicitante-2");
        var request = await Create(owner);
        var id = Id(request);
        Assert.Equal(HttpStatusCode.OK, (await owner.PutAsJsonAsync($"/requests/{id}", Body("Editado"))).StatusCode);
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/requests/{id}");
        Assert.Equal("Editado", saved.GetProperty("title").GetString());
        Assert.Equal("solicitante-1", saved.GetProperty("requesterId").GetString());
        Assert.Equal(request.GetProperty("createdAt").GetString(), saved.GetProperty("createdAt").GetString());
        Assert.EndsWith("Z", saved.GetProperty("createdAt").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/requests/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/requests/{id}", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/requests/{id}/cancel", null)).StatusCode);
        Assert.DoesNotContain(id, await Ids(other));
        using var scope = app.Services.CreateScope();
        var entity = await scope.ServiceProvider.GetRequiredService<RequestsDbContext>().Requests.SingleAsync();
        Assert.Equal("Editado", entity.Title);
    }

    [Fact]
    public async Task ClaimRemovesFromQueueAndOnlyResponsibleOperatorCanComplete()
    {
        using var app = new ApiFactory();
        using var owner = app.User("solicitante-1");
        using var op = app.User("operador-1");
        using var other = app.User("operador-2");
        var id = Id(await Create(owner));
        Assert.Contains(id, await Ids(op, "/requests/queue"));
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsync($"/requests/{id}/claim", null)).StatusCode);
        Assert.DoesNotContain(id, await Ids(op, "/requests/queue"));
        Assert.Contains(id, await Ids(op));
        Assert.DoesNotContain(id, await Ids(other));
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/requests/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/requests/{id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.PostAsync($"/requests/{id}/claim", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/requests/{id}", Body())).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/requests/{id}/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await op.PostAsync($"/requests/{id}/complete", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsync($"/requests/{id}/complete", null)).StatusCode);
        var saved = await owner.GetFromJsonAsync<JsonElement>($"/requests/{id}");
        Assert.Equal("Concluida", saved.GetProperty("status").GetString());
        Assert.Contains(id, await Ids(op));
    }

    [Fact]
    public async Task CancelledRequestStaysVisibleToOwnerAndAdminButLeavesQueue()
    {
        using var app = new ApiFactory();
        using var owner = app.User("solicitante-1");
        using var op = app.User("operador-1");
        using var admin = app.User("admin-1");
        var id = Id(await Create(owner));
        Assert.Equal(HttpStatusCode.OK, (await owner.PostAsync($"/requests/{id}/cancel", null)).StatusCode);
        Assert.Contains(id, await Ids(owner));
        Assert.Contains(id, await Ids(admin));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/requests/{id}")).StatusCode);
        Assert.DoesNotContain(id, await Ids(op, "/requests/queue"));
        Assert.Equal(HttpStatusCode.NotFound, (await op.GetAsync($"/requests/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await op.PostAsync($"/requests/{id}/claim", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PutAsJsonAsync($"/requests/{id}", Body())).StatusCode);
    }

    [Fact]
    public async Task AdminAssignsExistingOperatorAndCannotReassign()
    {
        using var app = new ApiFactory();
        using var owner = app.User("solicitante-1");
        using var admin = app.User("admin-1");
        using var op = app.User("operador-2");
        var id = Id(await Create(owner));
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync($"/requests/{id}/assign", new { operatorId = "solicitante-1" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.PostAsJsonAsync($"/requests/{id}/assign", new { operatorId = "operador-2" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsJsonAsync($"/requests/{id}/assign", new { operatorId = "operador-1" })).StatusCode);
        var saved = await op.GetFromJsonAsync<JsonElement>($"/requests/{id}");
        Assert.Equal("EmAtendimento", saved.GetProperty("status").GetString());
        Assert.Equal("operador-2", saved.GetProperty("operatorId").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await owner.PostAsync($"/requests/{id}/cancel", null)).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("inexistente")]
    public async Task MissingOrUnknownIdentityReturns401(string? user)
    {
        using var app = new ApiFactory();
        using var client = app.User(user);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/requests")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
    }

    [Theory]
    [InlineData("operador-1", "POST", "")]
    [InlineData("admin-1", "POST", "")]
    [InlineData("solicitante-1", "POST", "/claim")]
    [InlineData("solicitante-1", "POST", "/complete")]
    [InlineData("operador-1", "POST", "/cancel")]
    [InlineData("operador-1", "POST", "/assign")]
    [InlineData("admin-1", "PUT", "")]
    public async Task WrongRoleReturns403(string user, string method, string action)
    {
        using var app = new ApiFactory();
        using var owner = app.User("solicitante-1");
        var id = Id(await Create(owner));
        using var client = app.User(user);
        var path = method == "POST" && action == "" ? "/requests" : $"/requests/{id}{action}";
        var response = await client.SendAsync(new HttpRequestMessage(new HttpMethod(method), path) { Content = JsonContent.Create(Body()) });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        if (user == "solicitante-1") Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/requests/queue")).StatusCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"title\":\" \",\"description\":\"x\",\"category\":\"SuporteDeTI\",\"priority\":\"Alta\"}")]
    [InlineData("{\"title\":\"x\",\"description\":\"x\",\"category\":\"Outra\",\"priority\":\"Alta\"}")]
    [InlineData("{\"title\":\"x\",\"description\":\"x\",\"category\":0,\"priority\":0}")]
    [InlineData("not-json")]
    public async Task InvalidInputDoesNotCreateRecord(string json)
    {
        using var app = new ApiFactory();
        using var client = app.User("solicitante-1");
        var response = await client.PostAsync("/requests", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await Ids(client));
    }

    [Fact]
    public async Task LabIdentityCannotBeUsedInProduction()
    {
        using var app = new ApiFactory("Production");
        using var client = app.User("admin-1");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/requests")).StatusCode);
    }

    [Fact]
    public async Task StaleCancellationCannotOverwriteClaim()
    {
        using var app = new ApiFactory();
        using var client = app.User("solicitante-1");
        var id = Guid.Parse(Id(await Create(client)));
        using var scope1 = app.Services.CreateScope();
        using var scope2 = app.Services.CreateScope();
        var first = scope1.ServiceProvider.GetRequiredService<RequestsDbContext>();
        var second = scope2.ServiceProvider.GetRequiredService<RequestsDbContext>();
        var claimed = await first.Requests.SingleAsync(x => x.Id == id);
        var stale = await second.Requests.SingleAsync(x => x.Id == id);
        claimed.Assign("operador-1");
        await first.SaveChangesAsync();
        stale.Cancel();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
        second.ChangeTracker.Clear();
        var saved = await second.Requests.SingleAsync(x => x.Id == id);
        Assert.Equal("operador-1", saved.OperatorId);
        Assert.Equal(ServiceRequests.Api.Domain.RequestStatus.EmAtendimento, saved.Status);
    }
}
