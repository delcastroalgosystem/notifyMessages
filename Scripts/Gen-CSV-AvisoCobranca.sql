-- ================================================================
-- Gen-CSV-AvisoCobranca.sql
-- Gatilho SMARTARENA.AVISO_COBRANCA — dados para carga CSV no NotifyMessages
-- Executa na BD do clube (ligação de leitura, sem alterações).
-- Equivalente T-SQL de GeradorAvisoCobranca.GerarAsync.
-- Compatível com SQL Server 2008 R2 (10.50+).
--
-- Executado no dia configurado do mês M, avisa das quotas em aberto
-- cujo vencimento é anterior a M (não inclui as quotas do próprio mês M — D2).
-- Inclui referência Multibanco quando válida (MB-2, MB-3).
--
-- Parâmetros (D4, D5):
--   @MinimoQuotas          — mínimo de quotas vencidas para avisar (por omissão 1)
--   @AntiguidadeMaximaAnos — anos para trás; NULL = sem limite (por omissão NULL)
--
-- Colunas produzidas:
--   contacto, nome, chave
--   Nome, PrimeiroNome, NumeroSocio, ValorEmDivida, NumeroQuotas,
--   QuotaMaisAntiga, ListaQuotas, Referencia,
--   EntidadeMB, ReferenciaMB, ValorMB, DataLimiteMB, DescricaoMB, CodigoAvisoMB
--
-- Exportar com separador ; e encoding UTF-8:
--   PowerShell: Invoke-Sqlcmd -Query (Get-Content .\Gen-CSV-AvisoCobranca.sql -Raw) |
--               Export-Csv aviso_cobranca.csv -Delimiter ";" -NoTypeInformation -Encoding UTF8
-- ================================================================

-- Necessário para o método .value() do FOR XML PATH (ListaQuotas)
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- DATEADD(DAY, 0, DATEDIFF(DAY, 0, GETDATE())) = hoje à meia-noite como DATETIME
-- (DATEFROMPARTS não disponível no SQL Server 2008 R2)
DECLARE @DataExecucao DATETIME = DATEADD(DAY, 0, DATEDIFF(DAY, 0, GETDATE()));
-- Para simular outra data (nunca em Produção), descomente e ajuste:
-- DECLARE @DataExecucao DATETIME = '2026-10-01';

-- Mês corrente M = primeiro dia do mês de execução
DECLARE @MesM INT      = MONTH(@DataExecucao);
DECLARE @AnoM INT      = YEAR(@DataExecucao);
DECLARE @M    DATETIME = DATEADD(MONTH, (@AnoM - 1900) * 12 + @MesM - 1,
                                 CAST('19000101' AS DATETIME));

-- Parâmetros do gatilho (MESSAGE_TRIGGER.PARAMETERS — D4, D5)
DECLARE @MinimoQuotas          INT = 1;     -- mínimo de quotas vencidas para avisar
DECLARE @AntiguidadeMaximaAnos INT = NULL;  -- NULL = todas as épocas; ex. 5 = últimas 5

-- Configuração do clube: mês de início da época
DECLARE @MesIni INT = ISNULL(
    (SELECT TOP 1 MESINICIOEPOCA FROM CONFIG WHERE MESINICIOEPOCA BETWEEN 1 AND 12), 1);

-- Âmbito de épocas a ler (por ANOFIM)
DECLARE @AnoMax INT = @AnoM + 1;
DECLARE @AnoMin INT = CASE
    WHEN @AntiguidadeMaximaAnos IS NULL THEN @AnoM - 30
    ELSE @AnoM - @AntiguidadeMaximaAnos
END;

-- MB-3: referências só entram se DATA_LIMITE_PAG >= hoje + 3 dias
DECLARE @LimiteMB DATETIME = DATEADD(DAY, 3, @DataExecucao);

-- Nomes dos meses em português (FormatoPt.Mes)
DECLARE @Meses TABLE (N TINYINT PRIMARY KEY, Nome VARCHAR(20));
INSERT INTO @Meses VALUES
    (1,'janeiro'),(2,'fevereiro'),(3,'março'),(4,'abril'),
    (5,'maio'),(6,'junho'),(7,'julho'),(8,'agosto'),
    (9,'setembro'),(10,'outubro'),(11,'novembro'),(12,'dezembro');

;WITH

-- Sócios ativos com e-mail válido e categoria não isenta
Socios AS (
    SELECT
        s.CODIGO,
        CAST(s.NUMERO AS VARCHAR(20))                                        AS Numero,
        s.NOME,
        LTRIM(RTRIM(s.EMAIL))                                                AS Email,
        -- NULLIF(col, 0) com col VARCHAR força conversão implícita; CASE WHEN evita-a
        CASE WHEN CAST(ISNULL(s.PERIODO_COB_POSTAL, 0) AS VARCHAR(10)) = '0'
             THEN 1
             ELSE CAST(s.PERIODO_COB_POSTAL AS INT) END                      AS P
    FROM SOCIO s
    LEFT JOIN CATEGORIA c ON c.CODIGO = s.CATEGORIA
    WHERE s.SITUACAO < 7
      AND s.EMAIL IS NOT NULL AND LTRIM(RTRIM(s.EMAIL)) <> ''
      AND CHARINDEX('@', s.EMAIL) > 1
      AND ISNULL(c.ISENTOQUOTAS, 0) <> 1
),

-- Quotas em aberto (ESTADO = 3) com mês/ano calculados (C3, C9, CalendarioQuotas.MesDaQuota)
QuotasAbertas AS (
    SELECT
        q.SOCIO,
        (((q.NUMORDEM + @MesIni - 2) % 12 + 12) % 12 + 1)                  AS MesQ,
        CASE
            WHEN @MesIni = 1 THEN e.ANOFIM
            WHEN (((q.NUMORDEM + @MesIni - 2) % 12 + 12) % 12 + 1) >= @MesIni
                             THEN e.ANOINICIO
            ELSE e.ANOFIM
        END                                                                  AS AnoQ,
        q.VALORQUOTA
    FROM QUOTA q
    JOIN EPOCA e ON e.CODIGO = q.EPOCA
    WHERE e.ANOFIM BETWEEN @AnoMin AND @AnoMax
      AND q.ESTADO = 3
),

-- Pré-computa QuotaDate para não repetir a expressão DATEADD em cada CTE
QuotasAbertasComData AS (
    SELECT
        qa.SOCIO,
        qa.MesQ,
        qa.AnoQ,
        qa.VALORQUOTA,
        DATEADD(MONTH, (qa.AnoQ - 1900) * 12 + qa.MesQ - 1,
                CAST('19000101' AS DATETIME))                                AS QuotaDate
    FROM QuotasAbertas qa
),

-- Vencimento de cada quota (CalendarioQuotas.VencimentoDaQuota — C11, D1)
--   P=1 → vencimento = mês da quota
--   P>1 → max(quotaDate − (P−1) meses, início da época da quota)
-- IIF não disponível no SQL 2008; substituído por CASE WHEN.
QuotasComVencimento AS (
    SELECT
        qa.SOCIO,
        qa.MesQ,
        qa.AnoQ,
        qa.VALORQUOTA,
        qa.QuotaDate,
        -- Início da época da quota
        DATEADD(MONTH,
            (CASE WHEN qa.MesQ >= @MesIni THEN qa.AnoQ ELSE qa.AnoQ - 1 END - 1900) * 12
            + @MesIni - 1,
            CAST('19000101' AS DATETIME))                                    AS EpochStart,
        CASE
            WHEN s.P <= 1 THEN qa.QuotaDate
            -- max(quotaDate − (P−1) meses, início da época)
            WHEN DATEADD(MONTH, -(s.P - 1), qa.QuotaDate)
                 < DATEADD(MONTH,
                       (CASE WHEN qa.MesQ >= @MesIni THEN qa.AnoQ ELSE qa.AnoQ - 1 END - 1900) * 12
                       + @MesIni - 1,
                       CAST('19000101' AS DATETIME))
            THEN DATEADD(MONTH,
                     (CASE WHEN qa.MesQ >= @MesIni THEN qa.AnoQ ELSE qa.AnoQ - 1 END - 1900) * 12
                     + @MesIni - 1,
                     CAST('19000101' AS DATETIME))
            ELSE DATEADD(MONTH, -(s.P - 1), qa.QuotaDate)
        END                                                                  AS Vencimento
    FROM QuotasAbertasComData qa
    JOIN Socios s ON s.CODIGO = qa.SOCIO
),

-- Quotas em atraso: vencimento < @M (D2 — o mês M em si não conta)
QuotasAtraso AS (
    SELECT * FROM QuotasComVencimento WHERE Vencimento < @M
),

-- Totais por sócio (D4: mínimo de quotas vencidas)
TotaisSocio AS (
    SELECT
        qa.SOCIO,
        COUNT(*)           AS NumQuotas,
        SUM(qa.VALORQUOTA) AS ValorTotal,
        MIN(qa.Vencimento) AS VencimentoMaisAntigo
    FROM QuotasAtraso qa
    GROUP BY qa.SOCIO
    HAVING COUNT(*) >= @MinimoQuotas
),

-- Referências MB elegíveis para os sócios que vão receber o aviso de cobrança
-- INFO_ADICIONAL = "AAAAMMAAAAMM" (12 chars): ano1(4)+mes1(2)+ano2(4)+mes2(2)
-- TRY_CAST não disponível no SQL 2008; ISNUMERIC + CAST equivalente
RefMBEleg AS (
    SELECT
        acp.SOCIO,
        acp.CODIGO                                                           AS CodAviso,
        CAST(ISNULL(acp.ENTIDADE, tc.ENTIDADE_MB) AS VARCHAR(20))           AS Entidade,
        acp.REFERENCIA,
        acp.VALOR,
        ISNULL(acp.VALORPORTES, 0)                                          AS Portes,
        acp.DATA_LIMITE_PAG,
        DATEADD(MONTH,
            (CAST(SUBSTRING(acp.INFO_ADICIONAL, 1, 4) AS INT) - 1900) * 12
            + CAST(SUBSTRING(acp.INFO_ADICIONAL, 5, 2) AS INT) - 1,
            CAST('19000101' AS DATETIME))                                    AS Desde,
        DATEADD(MONTH,
            (CAST(SUBSTRING(acp.INFO_ADICIONAL, 7, 4) AS INT) - 1900) * 12
            + CAST(SUBSTRING(acp.INFO_ADICIONAL, 11, 2) AS INT) - 1,
            CAST('19000101' AS DATETIME))                                    AS Ate
    FROM AVISO_COBRANCA_POSTAL acp
    LEFT JOIN TIPO_COBRANCA_POSTAL tc ON tc.CODIGO = acp.TIPO_COBRANCA_POSTAL
    WHERE acp.TIPO_COBRANCA_POSTAL    = 1
      AND acp.ESTADO_AVISO_MB         = 1
      AND ISNULL(acp.GERAR_RECIBO, 'N') = 'N'
      AND acp.SOCIO      IS NOT NULL
      AND acp.REFERENCIA IS NOT NULL AND acp.REFERENCIA <> ''
      AND LEN(acp.INFO_ADICIONAL)     = 12
      AND (acp.DATA_LIMITE_PAG IS NULL OR acp.DATA_LIMITE_PAG >= @LimiteMB)
      -- CASE WHEN garante short-circuit: CAST só executa quando o LIKE valida todos os 12 dígitos
      -- (ISNUMERIC + CAST no WHERE é inseguro: o optimizador pode inverter a ordem de avaliação)
      AND CASE WHEN acp.INFO_ADICIONAL LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
               THEN CAST(SUBSTRING(acp.INFO_ADICIONAL,  5, 2) AS INT) ELSE NULL END BETWEEN 1 AND 12
      AND CASE WHEN acp.INFO_ADICIONAL LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
               THEN CAST(SUBSTRING(acp.INFO_ADICIONAL, 11, 2) AS INT) ELSE NULL END BETWEEN 1 AND 12
      AND acp.SOCIO IN (SELECT SOCIO FROM TotaisSocio)
),

-- Referências com valor correspondente (tolerância 0,005 €) que cobrem
-- pelo menos uma das quotas em atraso (MB-2)
RefMBValidas AS (
    SELECT r.*
    FROM RefMBEleg r
    WHERE ABS(
              ISNULL((SELECT SUM(qa2.VALORQUOTA)
                      FROM QuotasAbertasComData qa2
                      WHERE qa2.SOCIO = r.SOCIO
                        AND qa2.QuotaDate BETWEEN r.Desde AND r.Ate
                     ), 0)
              + r.Portes - r.VALOR
          ) < 0.005
      AND EXISTS (
          SELECT 1 FROM QuotasAtraso qa3
          WHERE qa3.SOCIO = r.SOCIO
            AND qa3.QuotaDate BETWEEN r.Desde AND r.Ate)
),

-- Melhor referência por sócio: maior cobertura das quotas em atraso, depois data limite mais tardia
MelhorRef AS (
    SELECT *,
        ROW_NUMBER() OVER (
            PARTITION BY r.SOCIO
            ORDER BY
                (SELECT COUNT(*)
                 FROM QuotasAtraso qa4
                 WHERE qa4.SOCIO = r.SOCIO
                   AND qa4.QuotaDate BETWEEN r.Desde AND r.Ate) DESC,
                ISNULL(r.DATA_LIMITE_PAG, '9999-12-31') DESC
        ) AS rn
    FROM RefMBValidas r
),

-- ListaQuotas por sócio: "mês de ano — valor; ..." ordenado por data (D7)
-- STRING_AGG não disponível no SQL 2008; substituído por FOR XML PATH.
-- NCHAR(8212) = em dash (—); NCHAR(8364) = euro (€)
ListaQuotasStr AS (
    SELECT DISTINCT
        qa.SOCIO,
        STUFF((
            SELECT N'; ' + nm2.Nome + N' de ' + CAST(qa2.AnoQ AS VARCHAR(4))
                   + N' ' + NCHAR(8212) + N' '
                   + REPLACE(CAST(CAST(qa2.VALORQUOTA AS DECIMAL(15, 2)) AS VARCHAR(20)),
                              '.', ',')
                   + N' ' + NCHAR(8364)
            FROM QuotasAtraso qa2
            JOIN @Meses nm2 ON nm2.N = qa2.MesQ
            WHERE qa2.SOCIO = qa.SOCIO
            ORDER BY qa2.QuotaDate
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 2, '')                            AS ListaQuotas
    FROM QuotasAtraso qa
)

-- Resultado: uma linha por sócio a cobrar
SELECT
    s.Email                                                                  AS contacto,
    s.NOME                                                                   AS nome,
    -- Chave COBRANCA:{CODIGO}:{AAAA-MM} — um aviso por sócio por mês (D3)
    'COBRANCA:' + CAST(CAST(s.CODIGO AS BIGINT) AS VARCHAR(20))
        + ':' + LEFT(CONVERT(VARCHAR(10), @M, 120), 7)                      AS chave,

    -- Variáveis do template
    -- (Nome não é declarado: a coluna "nome" de controlo já entra no BusinessData
    --  e o renderer resolve {{Nome}} case-insensitive — duplicar causaria ArgumentException)
    LEFT(LTRIM(s.NOME), CHARINDEX(' ', LTRIM(s.NOME) + ' ') - 1)           AS PrimeiroNome,
    s.Numero                                                                 AS NumeroSocio,
    REPLACE(CAST(CAST(ts.ValorTotal AS DECIMAL(15, 2)) AS VARCHAR(20)),
            '.', ',') + N' ' + NCHAR(8364)                                  AS ValorEmDivida,
    CAST(ts.NumQuotas AS VARCHAR(10))                                        AS NumeroQuotas,
    -- QuotaMaisAntiga: "mês de ano" do vencimento mais antigo (ex. "outubro de 2025")
    nm_ant.Nome + ' de '
        + CAST(YEAR(ts.VencimentoMaisAntigo) AS VARCHAR(4))                 AS QuotaMaisAntiga,
    lq.ListaQuotas                                                           AS ListaQuotas,
    -- Referencia: discrimina idempotência no NotifyMessages (Ano-Mês de M)
    LEFT(CONVERT(VARCHAR(10), @M, 120), 7)                                   AS Referencia,

    -- Referência MB (vazia quando não existe — o template usa {{#EntidadeMB}}…{{/EntidadeMB}})
    ISNULL(mb.Entidade, '')                                                  AS EntidadeMB,
    ISNULL(CASE WHEN LEN(mb.REFERENCIA) = 9
                THEN SUBSTRING(mb.REFERENCIA, 1, 3) + ' '
                   + SUBSTRING(mb.REFERENCIA, 4, 3) + ' '
                   + SUBSTRING(mb.REFERENCIA, 7, 3)
                ELSE mb.REFERENCIA END, '')                                  AS ReferenciaMB,
    ISNULL(REPLACE(CAST(CAST(mb.VALOR AS DECIMAL(15, 2)) AS VARCHAR(20)),
                   '.', ',') + N' ' + NCHAR(8364), '')                      AS ValorMB,
    ISNULL(CONVERT(VARCHAR(10), mb.DATA_LIMITE_PAG, 103), '')               AS DataLimiteMB,
    ISNULL(CASE
               WHEN mb.Desde = mb.Ate
               THEN nm_d.Nome + ' de ' + CAST(YEAR(mb.Desde) AS VARCHAR(4))
               WHEN YEAR(mb.Desde) = YEAR(mb.Ate)
               THEN nm_d.Nome + ' a ' + nm_a.Nome
                    + ' de ' + CAST(YEAR(mb.Ate) AS VARCHAR(4))
               ELSE nm_d.Nome + ' de ' + CAST(YEAR(mb.Desde) AS VARCHAR(4))
                    + ' a ' + nm_a.Nome + ' de ' + CAST(YEAR(mb.Ate) AS VARCHAR(4))
           END, '')                                                          AS DescricaoMB,
    ISNULL(CAST(mb.CodAviso AS VARCHAR(20)), '')                            AS CodigoAvisoMB

FROM Socios s
JOIN TotaisSocio ts         ON ts.SOCIO   = s.CODIGO
JOIN ListaQuotasStr lq      ON lq.SOCIO   = s.CODIGO
JOIN @Meses nm_ant          ON nm_ant.N   = MONTH(ts.VencimentoMaisAntigo)
LEFT JOIN MelhorRef mb      ON mb.SOCIO   = s.CODIGO AND mb.rn = 1
LEFT JOIN @Meses nm_d       ON nm_d.N     = MONTH(mb.Desde)
LEFT JOIN @Meses nm_a       ON nm_a.N     = MONTH(mb.Ate)
ORDER BY s.NOME;
