namespace CashFlow.IntegrationTests.Fixtures;

using System.Diagnostics;
using System.Net.Sockets;

/// <summary>
/// Helper para detecção dinâmica da disponibilidade do Docker daemon no ambiente de execução.
/// Permite que a suíte execute testes de integração reais via Testcontainers sempre que o Docker
/// estiver ativo (localmente ou no pipeline CI/CD) e degrade graciosamente com Skip caso indisponível.
/// </summary>
public static class DockerEnvironment
{
    private static readonly Lazy<bool> DockerAvailableLazy = new(CheckDockerAvailability);

    public static bool IsDockerAvailable() => DockerAvailableLazy.Value;

    private static bool CheckDockerAvailability()
    {
        try
        {
            // 1. Verifica se a variável DOCKER_HOST está configurada
            var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
            if (!string.IsNullOrEmpty(dockerHost))
            {
                if (Uri.TryCreate(dockerHost, UriKind.Absolute, out var uri) && uri.Scheme == "tcp")
                {
                    using var tcpClient = new TcpClient();
                    var connectTask = tcpClient.ConnectAsync(uri.Host, uri.Port);
                    if (connectTask.Wait(TimeSpan.FromSeconds(1)) && tcpClient.Connected)
                    {
                        return true;
                    }
                }
            }

            // 2. Tenta executar 'docker info' com timeout curto de 2 segundos
            var startInfo = new ProcessStartInfo
            {
                FileName = "docker",
                Arguments = "info",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return false;

            if (process.WaitForExit(2000))
            {
                return process.ExitCode == 0;
            }

            try { process.Kill(); } catch { }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
