namespace CashFlow.IntegrationTests.Fixtures;

using Xunit;

/// <summary>
/// Atributo Fact customizado que executa o teste de integração somente se o Docker daemon
/// estiver ativo no ambiente de execução. Caso contrário, ignora o teste com mensagem explicativa.
/// </summary>
public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerEnvironment.IsDockerAvailable())
        {
            Skip = "Docker daemon indisponível no ambiente atual. Teste de integração ignorado.";
        }
    }
}

/// <summary>
/// Atributo Theory customizado para testes parametrizados dependentes de Docker.
/// </summary>
public sealed class DockerTheoryAttribute : TheoryAttribute
{
    public DockerTheoryAttribute()
    {
        if (!DockerEnvironment.IsDockerAvailable())
        {
            Skip = "Docker daemon indisponível no ambiente atual. Teste de integração ignorado.";
        }
    }
}
