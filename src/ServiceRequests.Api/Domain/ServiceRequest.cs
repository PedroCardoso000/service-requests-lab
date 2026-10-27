namespace ServiceRequests.Api.Domain;

public enum Category { LevantamentoDeRequisitos, CriacaoDePlataforma, CriacaoDeAplicativo, SuporteDeTI }
public enum Priority { Alta, Media, Baixa }
public enum RequestStatus { Criada, EmAtendimento, Concluida, Cancelada }

public sealed class ServiceRequest
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public Category Category { get; private set; }
    public Priority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string RequesterId { get; private set; } = "";
    public string? OperatorId { get; private set; }
    public RequestStatus Status { get; private set; } = RequestStatus.Criada;
    public Guid Version { get; private set; } = Guid.NewGuid();

    private ServiceRequest() { }
    public ServiceRequest(string requesterId, string title, string description, Category category, Priority priority)
    {
        RequesterId = requesterId;
        Edit(title, description, category, priority);
    }

    public void Edit(string title, string description, Category category, Priority priority)
    {
        RequireCreated();
        Title = title.Trim();
        Description = description.Trim();
        Category = category;
        Priority = priority;
        Version = Guid.NewGuid();
    }
    public void Assign(string operatorId)
    {
        RequireCreated();
        OperatorId = operatorId;
        Status = RequestStatus.EmAtendimento;
        Version = Guid.NewGuid();
    }
    public void Complete()
    {
        if (Status != RequestStatus.EmAtendimento)
            throw new BusinessRuleException("Somente solicitações EmAtendimento podem ser concluídas.");
        Status = RequestStatus.Concluida;
        Version = Guid.NewGuid();
    }
    public void Cancel()
    {
        RequireCreated();
        Status = RequestStatus.Cancelada;
        Version = Guid.NewGuid();
    }
    private void RequireCreated()
    {
        if (Status != RequestStatus.Criada || OperatorId is not null)
            throw new BusinessRuleException("Esta operação exige uma solicitação Criada e sem operador.");
    }
}

public sealed class BusinessRuleException(string message) : Exception(message);
