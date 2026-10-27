@echo off
cd /d "%~dp0.."
echo ======================================================
echo   CashFlow - Inicializador de APIs em Terminais Separados
echo ======================================================
echo [1/3] Garantindo containers de infraestrutura (PostgreSQL, RabbitMQ, Redis)...
docker compose up -d postgres rabbitmq redis
docker stop cashflow-transactions-api cashflow-consolidation-api 2>nul

echo.
echo [2/3] Abrindo Transactions API (Porta 5101) em novo terminal...
start "CashFlow - Transactions API (5101)" "%~dp0run-transactions-api.cmd"

echo.
echo [3/3] Abrindo Consolidation API (Porta 5102) em novo terminal...
start "CashFlow - Consolidation API (5102)" "%~dp0run-consolidation-api.cmd"

echo.
echo ======================================================
echo   APIs em execucao nos terminais dedicados!
echo   * Swagger Lancamentos:   http://localhost:5101/swagger
echo   * Swagger Consolidacao:  http://localhost:5102/swagger
echo   * RabbitMQ Management:   http://localhost:15672 (verity / verity_secret)
echo   * PostgreSQL Database:   localhost:5433 (cashflow_db)
echo   * Redis Distributed Cache: localhost:6379
echo ======================================================
