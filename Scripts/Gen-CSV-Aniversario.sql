-- ================================================================
-- Gen-CSV-Aniversario.sql
-- Gatilho SMARTARENA.ANIVERSARIO — dados para carga CSV no NotifyMessages
-- Executa na BD do clube (ligação de leitura, sem alterações).
-- Compatível com SQL Server 2008 R2 (10.50+).
--
-- Colunas para POST /admin/batches/upload:
--   contacto, nome, chave           — campos de controlo do lote
--   Nome, PrimeiroNome, NumeroSocio, Idade, Ano  — variáveis do template
--
-- Exportar com separador ; e encoding UTF-8:
--   PowerShell: Invoke-Sqlcmd -Query (Get-Content .\Gen-CSV-Aniversario.sql -Raw) |
--               Export-Csv aniversario.csv -Delimiter ";" -NoTypeInformation -Encoding UTF8
-- ================================================================

-- Hoje sem hora (DATETIME; DATE não existia antes do 2008 mas DATETIME é universal)
DECLARE @DataExecucao DATETIME = DATEADD(DAY, 0, DATEDIFF(DAY, 0, GETDATE()));
-- Para simular outra data (nunca em Produção), descomente e ajuste:
-- DECLARE @DataExecucao DATETIME = '2026-02-28';

DECLARE @Mes  INT = MONTH(@DataExecucao);
DECLARE @Dia1 INT = DAY(@DataExecucao);
DECLARE @Dia2 INT = @Dia1;

-- Quem nasceu a 29 de fevereiro festeja a 28 nos anos não bissextos.
-- Ano bissexto: divisível por 4 E (não divisível por 100 OU divisível por 400).
-- Ano NÃO bissexto: a condição oposta — activar @Dia2 = 29 nesses anos.
IF @Mes = 2 AND @Dia1 = 28
   AND (YEAR(@DataExecucao) % 4 <> 0
        OR (YEAR(@DataExecucao) % 100 = 0 AND YEAR(@DataExecucao) % 400 <> 0))
    SET @Dia2 = 29;

SELECT
    LTRIM(RTRIM(s.EMAIL))                                           AS contacto,
    s.NOME                                                          AS nome,
    -- Chave SOCIO:{CODIGO}:{ano} — idempotência anual
    'SOCIO:' + CAST(CAST(s.CODIGO AS BIGINT) AS VARCHAR(20))
        + ':' + CAST(YEAR(@DataExecucao) AS VARCHAR(4))            AS chave,

    -- Variáveis do template
    -- (Nome não é declarado: a coluna "nome" de controlo já entra no BusinessData
    --  e o renderer resolve {{Nome}} case-insensitive — duplicar causaria ArgumentException)
    -- Primeiro nome: tudo antes do primeiro espaço
    LEFT(LTRIM(s.NOME),
         CHARINDEX(' ', LTRIM(s.NOME) + ' ') - 1)                 AS PrimeiroNome,
    CAST(s.NUMERO AS VARCHAR(20))                                    AS NumeroSocio,
    -- Idade em anos completos no dia de execução
    DATEDIFF(YEAR, s.DATANASCIMENTO, @DataExecucao)
        - CASE
            WHEN MONTH(s.DATANASCIMENTO) > @Mes
              OR (MONTH(s.DATANASCIMENTO) = @Mes
                  AND DAY(s.DATANASCIMENTO) > @Dia1)
            THEN 1 ELSE 0
          END                                                       AS Idade,
    CAST(YEAR(@DataExecucao) AS VARCHAR(4))                         AS Ano

FROM SOCIO s
WHERE s.SITUACAO < 7
  AND s.EMAIL IS NOT NULL
  AND LTRIM(RTRIM(s.EMAIL)) <> ''
  AND CHARINDEX('@', s.EMAIL) > 1
  AND s.DATANASCIMENTO IS NOT NULL
  AND MONTH(s.DATANASCIMENTO) = @Mes
  AND DAY(s.DATANASCIMENTO) IN (@Dia1, @Dia2)
ORDER BY s.NOME;
