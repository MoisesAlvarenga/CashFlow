namespace CashFlow.Shared.Contracts.Security;

/// <summary>
/// Catálogo de clientes pré-cadastrados para o desafio técnico e suítes de testes automatizados.
/// Todos os clientes possuem a senha padrão unificada: "verity-secret".
/// </summary>
public static class DefaultApiClients
{
    /// <summary>
    /// Senha padrão unificada para todos os clientes cadastrados.
    /// </summary>
    public const string DefaultSecret = "verity-secret";

    /// <summary>
    /// Lista de credenciais padrão que são semeadas com hash criptográfico BCrypt no banco de dados.
    /// </summary>
    public static readonly (string ClientId, string Secret, string Name, string Roles)[] All =
    [
        ("verity-client", DefaultSecret, "Avaliador Técnico Verity", "Operator,Admin"),
        ("operador-financeiro", DefaultSecret, "Operador Financeiro Padrão", "Operator"),
        ("operator-user", DefaultSecret, "Usuário Operador (Testes)", "Operator"),
        ("consolidation-viewer", DefaultSecret, "Visualizador de Consolidação", "Viewer"),
        ("integration-tester", DefaultSecret, "Executor de Testes de Integração", "Admin")
    ];
}
