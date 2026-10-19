namespace CashFlow.Shared.Contracts.Context;

/// <summary>
/// Contrato para provedor desacoplado de CancellationToken do contexto de execução.
/// Permite que repositórios e serviços acessem o sinal de cancelamento sem poluir assinaturas de métodos.
/// </summary>
public interface ICancellationTokenProvider
{
    /// <summary>
    /// Obtém o CancellationToken do contexto atual.
    /// </summary>
    CancellationToken Token { get; }
}

/// <summary>
/// Provedor padrão com CancellationToken.None para cenários isolados, workers e testes unitários.
/// </summary>
public class DefaultCancellationTokenProvider : ICancellationTokenProvider
{
    public static readonly DefaultCancellationTokenProvider Instance = new();

    public CancellationToken Token => CancellationToken.None;
}
