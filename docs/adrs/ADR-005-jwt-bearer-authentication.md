# ADR-005: Autenticação e Autorização via JWT Bearer Token

## Contexto

Em arquiteturas de microsserviços modernos, a exposição pública de rotas financeiras sem autenticação representa um risco severo de segurança da informação, violando normas regulatórias (como LGPD, PCI-DSS e ISO 27001). É mandatório garantir que todas as requisições que afetam lançamentos e saldos sejam autenticadas e autorizadas via padrões abertos e interoperáveis.

## Decisão Arquitetural

1. **Adoção do Padrão RFC 7519 (JSON Web Token - JWT)**:
   * Tokens assinados digitalmente com algoritmo HMAC-SHA256 (`SecurityAlgorithms.HmacSha256`), contendo claims estruturadas:
     * `sub`: identificador do cliente solicitante;
     * `jti`: identificador único do token para prevenção de ataques de repetição;
     * `role`: papéis operacionais do cliente (ex: `Operator`, `Admin`);
     * `exp`: tempo de vida estrito (configurável, padrão de 60 minutos).

2. **Middleware JwtBearer do ASP.NET Core**:
   * Ambos os microsserviços (`Transactions.Api` e `Consolidation.Api`) validam os tokens através do pacote oficial `Microsoft.AspNetCore.Authentication.JwtBearer`:
     * Validação estrita da chave simétrica (`ValidateIssuerSigningKey = true`);
     * Validação do emissor (`ValidateIssuer = true`);
     * Validação da audiência (`ValidateAudience = true`);
     * Validação de tempo de vida com tolerância zero de relógio (`ClockSkew = TimeSpan.Zero`).

3. **Endpoint de Emissão e Integração com Swagger**:
   * Ambas as APIs expõem `POST /api/v1/auth/token` (público) para emissão de credenciais temporárias;
   * O Swagger UI foi configurado com esquema de segurança `Bearer` e botão "Authorize" interativo.

4. **Proteção Rigorosa dos Endpoints de Negócio**:
   * As rotas `POST /api/v1/transactions`, `GET /api/v1/transactions` e `GET /api/v1/daily-balances` aplicam `.RequireAuthorization()`, retornando `401 Unauthorized` para qualquer requisição anônima ou com token forjado/expirado.

## Consequências

### Positivas
* Autenticação desacoplada e sem estado (*stateless*), viabilizando escalabilidade horizontal das APIs;
* Controle granular de acesso e rastreabilidade da identidade do solicitante nas auditorias;
* Experiência fluida de teste via Swagger UI.
