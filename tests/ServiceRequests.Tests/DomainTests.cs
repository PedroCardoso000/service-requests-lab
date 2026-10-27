using ServiceRequests.Api.Domain;

namespace ServiceRequests.Tests;

public class DomainTests
{
    private static ServiceRequest NewRequest() => new("solicitante-1", " Título ", " Descrição ", Category.SuporteDeTI, Priority.Media);

    [Fact]
    public void CreationDefinesIdentityUtcAndInitialState()
    {
        var before = DateTime.UtcNow;
        var request = NewRequest();
        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal("solicitante-1", request.RequesterId);
        Assert.Equal(RequestStatus.Criada, request.Status);
        Assert.Null(request.OperatorId);
        Assert.Equal(DateTimeKind.Utc, request.CreatedAt.Kind);
        Assert.InRange(request.CreatedAt, before, DateTime.UtcNow);
        Assert.Equal("Título", request.Title);
        Assert.Equal("Descrição", request.Description);
    }

    [Fact]
    public void EditChangesFieldsButPreservesIdentityAndCreationDate()
    {
        var request = NewRequest();
        var id = request.Id;
        var created = request.CreatedAt;
        var version = request.Version;
        request.Edit("Novo", "Detalhes", Category.CriacaoDeAplicativo, Priority.Alta);
        Assert.Equal("Novo", request.Title);
        Assert.Equal("Detalhes", request.Description);
        Assert.Equal(Category.CriacaoDeAplicativo, request.Category);
        Assert.Equal(Priority.Alta, request.Priority);
        Assert.Equal(id, request.Id);
        Assert.Equal(created, request.CreatedAt);
        Assert.Equal("solicitante-1", request.RequesterId);
        Assert.NotEqual(version, request.Version);
    }

    [Fact]
    public void MainFlowRetainsResponsibleOperator()
    {
        var request = NewRequest();
        var version = request.Version;
        request.Assign("operador-1");
        Assert.Equal(RequestStatus.EmAtendimento, request.Status);
        Assert.Equal("operador-1", request.OperatorId);
        Assert.NotEqual(version, request.Version);
        version = request.Version;
        request.Complete();
        Assert.Equal(RequestStatus.Concluida, request.Status);
        Assert.Equal("operador-1", request.OperatorId);
        Assert.NotEqual(version, request.Version);
    }

    [Fact]
    public void CreatedRequestCanBeCancelledWithoutOperator()
    {
        var request = NewRequest();
        var version = request.Version;
        request.Cancel();
        Assert.Equal(RequestStatus.Cancelada, request.Status);
        Assert.Null(request.OperatorId);
        Assert.NotEqual(version, request.Version);
    }

    [Theory]
    [InlineData(RequestStatus.EmAtendimento)]
    [InlineData(RequestStatus.Concluida)]
    [InlineData(RequestStatus.Cancelada)]
    public void OnlyCreatedRequestsCanBeEditedCancelledOrAssigned(RequestStatus state)
    {
        var request = NewRequest();
        if (state == RequestStatus.Cancelada) request.Cancel();
        else { request.Assign("operador-1"); if (state == RequestStatus.Concluida) request.Complete(); }
        var version = request.Version;
        Assert.Throws<BusinessRuleException>(() => request.Edit("Novo", "Detalhes", Category.SuporteDeTI, Priority.Baixa));
        Assert.Throws<BusinessRuleException>(() => request.Cancel());
        Assert.Throws<BusinessRuleException>(() => request.Assign("operador-2"));
        Assert.Equal(state, request.Status);
        Assert.Equal(version, request.Version);
        Assert.Equal("Título", request.Title);
    }

    [Theory]
    [InlineData(RequestStatus.Criada)]
    [InlineData(RequestStatus.Cancelada)]
    [InlineData(RequestStatus.Concluida)]
    public void CompletionRequiresActiveAttendance(RequestStatus state)
    {
        var request = NewRequest();
        if (state == RequestStatus.Cancelada) request.Cancel();
        if (state == RequestStatus.Concluida) { request.Assign("operador-1"); request.Complete(); }
        Assert.Throws<BusinessRuleException>(() => request.Complete());
        Assert.Equal(state, request.Status);
    }
}
