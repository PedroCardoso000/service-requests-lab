using ServiceRequests.Api.Domain;
using ServiceRequests.Api.Endpoints;

namespace ServiceRequests.Tests;

public class InputTests
{
    [Theory]
    [InlineData(1, 1, true)]
    [InlineData(200, 5000, true)]
    [InlineData(201, 1, false)]
    [InlineData(1, 5001, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    public void TitleAndDescriptionRespectLimits(int titleLength, int descriptionLength, bool valid)
    {
        var input = new RequestInput(new string('t', titleLength), new string('d', descriptionLength), Category.SuporteDeTI, Priority.Alta);
        Assert.Equal(valid, input.Validate().Count == 0);
    }

    [Fact]
    public void UndefinedEnumsAreRejected()
    {
        var errors = new RequestInput("Título", "Descrição", (Category)999, (Priority)999).Validate();
        Assert.Contains("category", errors.Keys);
        Assert.Contains("priority", errors.Keys);
    }

    [Theory]
    [InlineData(Category.LevantamentoDeRequisitos, Priority.Alta)]
    [InlineData(Category.CriacaoDePlataforma, Priority.Media)]
    [InlineData(Category.CriacaoDeAplicativo, Priority.Baixa)]
    [InlineData(Category.SuporteDeTI, Priority.Alta)]
    public void DefinedCategoriesAndPrioritiesAreAccepted(Category category, Priority priority)
    {
        Assert.Empty(new RequestInput("Título", "Descrição", category, priority).Validate());
    }
}
