-- ================================================================
-- Gen-CSV-AvisoVencimento.sql
-- Gatilho SMARTARENA.AVISO_VENCIMENTO — dados para carga CSV no NotifyMessages
-- Executa na BD do clube (ligação de leitura, sem alterações).
-- Equivalente T-SQL de GeradorAvisoVencimento.GerarAsync.
-- Compatível com SQL Server 2008 R2 (10.50+).
--
-- Executado no dia configurado do mês M, avisa do período que começa em R = M + 1.
-- Valor = só as quotas em aberto (ESTADO = 3) do próximo período (C13).
-- Inclui referência Multibanco quando válida (MB-2, MB-3).
--
-- Colunas produzidas:
--   contacto, nome, chave
--   Nome, PrimeiroNome, NumeroSocio, MesReferencia, Periodicidade,
--   FimPeriodo, Valor, Referencia,
--   EntidadeMB, ReferenciaMB, ValorMB, DataLimiteMB, DescricaoMB, CodigoAvisoMB
--
-- Exportar com separador ; e encoding UTF-8:
--   PowerShell: Invoke-Sqlcmd -Query (Get-Content .\Gen-CSV-AvisoVencimento.sql -Raw) |
--               Export-Csv aviso_vencimento.csv -Delimiter ";" -NoTypeInformation -Encoding UTF8
-- ================================================================

-- Necessário para comportamento consistente com AvisoCobranca (que usa FOR XML PATH + .value())
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- DATEADD(DAY, 0, DATEDIFF(DAY, 0, GETDATE())) = hoje à meia-noite como DATETIME
-- (DATEFROMPARTS não disponível no SQL Server 2008 R2)
DECLARE @DataExecucao DATETIME = DATEADD(DAY, 0, DATEDIFF(DAY, 0, GETDATE()));
-- Para simular outra data (nunca em Produção), descomente e ajuste:
-- DECLARE @DataExecucao DATETIME = '2026-10-01';

-- Mês de referência R = primeiro dia do mês seguinte
-- Substituição de DATEFROMPARTS: DATEADD(MONTH, (ano-1900)*12 + mes-1, '19000101')
DECLARE @MesR INT      = MONTH(DATEADD(MONTH, 1, @DataExecucao));
DECLARE @AnoR INT      = YEAR(DATEADD(MONTH, 1, @DataExecucao));
DECLARE @R    DATETIME = DATEADD(MONTH, (@AnoR - 1900) * 12 + @MesR - 1,
                                 CAST('19000101' AS DATETIME));

-- Mês de início da época (CONFIG.MESINICIOEPOCA)
DECLARE @M INT = ISNULL(
    (SELECT TOP 1 MESINICIOEPOCA FROM CONFIG WHERE MESINICIOEPOCA BETWEEN 1 AND 12), 1);

-- Âmbito de épocas a ler: cobre anuais (P+11) e referências MB com quotas em atraso (MB-2)
DECLARE @AnoMin INT = @AnoR - 1;
DECLARE @AnoMax INT = @AnoR + 2;

-- MB-3: referências só entram se DATA_LIMITE_PAG >= hoje + 3 dias
DECLARE @LimiteMB DATETIME = DATEADD(DAY, 3, @DataExecucao);

-- Nomes dos meses em português (FormatoPt.Mes)
DECLARE @Meses TABLE (N TINYINT PRIMARY KEY, Nome VARCHAR(20));
INSERT INTO @Meses VALUES
    (1,'janeiro'),(2,'fevereiro'),(3,'março'),(4,'abril'),
    (5,'maio'),(6,'junho'),(7,'julho'),(8,'agosto'),
    (9,'setembro'),(10,'outubro'),(11,'novembro'),(12,'dezembro');

;WITH

-- Sócios ativos (SITUACAO < 7) com e-mail válido e categoria não isenta (C1, Q4)
Socios AS (
    SELECT
        s.CODIGO,
        CAST(s.NUMERO AS VARCHAR(20))                                        AS Numero,
        s.NOME,
        LTRIM(RTRIM(s.EMAIL))                                                AS Email,
        -- P: periodicidade; NULL/0 = mensal (Q1)
        CASE WHEN CAST(ISNULL(s.PERIODO_COB_POSTAL, 0) AS VARCHAR(10)) = '0'
             THEN 1
             ELSE CAST(s.PERIODO_COB_POSTAL AS INT) END                      AS P,
        -- MesBase fora de [1,12] = janeiro (C12); IIF não disponível no SQL 2008
        CASE WHEN ISNULL(s.MES_BASE_COBRANCA, 0) BETWEEN 1 AND 12
             THEN s.MES_BASE_COBRANCA ELSE 1 END                             AS MesBase
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
        (((q.NUMORDEM + @M - 2) % 12 + 12) % 12 + 1)                       AS MesQ,
        CASE
            WHEN @M = 1 THEN e.ANOFIM
            WHEN (((q.NUMORDEM + @M - 2) % 12 + 12) % 12 + 1) >= @M THEN e.ANOINICIO
            ELSE e.ANOFIM
        END                                                                  AS AnoQ,
        q.VALORQUOTA
    FROM QUOTA q
    JOIN EPOCA e ON e.CODIGO = q.EPOCA
    WHERE e.ANOFIM BETWEEN @AnoMin AND @AnoMax
      AND q.ESTADO = 3
),

-- Pré-computa QuotaDate para evitar repetir a expressão DATEADD em vários CTEs
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

-- Sócios cujo período começa em R (secção 6.2, C10, C12)
--   P=1  → sempre (mensal)
--   P=12 → só quando R.Mes = @M (anual)
--   P∈{2,3,6} → EXISTS em MES_COBRAR_PERIODO com MESES_SOMAR = P − 1
InicioPeriodo AS (
    SELECT s.*
    FROM Socios s
    WHERE s.P IN (1, 2, 3, 6, 12)
      AND (
           s.P = 1
        OR (s.P = 12 AND @MesR = @M)
        OR (s.P IN (2, 3, 6) AND EXISTS (
               SELECT 1 FROM MES_COBRAR_PERIODO mcp
               WHERE mcp.MES_BASE        = s.MesBase
                 AND mcp.PERIODO_COBRANCA = s.P
                 AND mcp.MES_COBRANCA    = @MesR
                 AND mcp.MESES_SOMAR     = mcp.PERIODO_COBRANCA - 1))
      )
),

-- Valor em aberto no período [R, R + P − 1] (C11, C13)
-- Sem quotas nesse período → sem linha (C7). Tudo pago → sem linha (Q3).
ValoresPeriodo AS (
    SELECT
        ip.CODIGO,
        ip.P,
        SUM(qa.VALORQUOTA)                                                   AS Valor
    FROM InicioPeriodo ip
    JOIN QuotasAbertasComData qa ON qa.SOCIO = ip.CODIGO
    WHERE qa.QuotaDate BETWEEN @R AND DATEADD(MONTH, ip.P - 1, @R)
    GROUP BY ip.CODIGO, ip.P
    HAVING SUM(qa.VALORQUOTA) > 0
),

-- Referências MB elegíveis para os sócios que vão receber o aviso
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
            CAST('19000101' AS DATETIME))                                    AS Ate,
        CASE WHEN acp.FICH_SIBS IS NULL THEN 1 ELSE 0 END                   AS Avulsa
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
      AND CASE WHEN acp.INFO_ADICIONAL LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
               THEN CAST(SUBSTRING(acp.INFO_ADICIONAL,  5, 2) AS INT) ELSE NULL END BETWEEN 1 AND 12
      AND CASE WHEN acp.INFO_ADICIONAL LIKE '[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]'
               THEN CAST(SUBSTRING(acp.INFO_ADICIONAL, 11, 2) AS INT) ELSE NULL END BETWEEN 1 AND 12
      AND acp.SOCIO IN (SELECT CODIGO FROM ValoresPeriodo)
),

-- Referências com valor correspondente: abertas em [Desde, Ate] + portes ≈ VALOR (tolerância 0,005 €)
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
      -- cobre pelo menos um mês do período do e-mail [R, R+P-1] (MB-2)
      AND EXISTS (
          SELECT 1
          FROM QuotasAbertasComData qa3
          JOIN ValoresPeriodo vp ON vp.CODIGO = r.SOCIO
          WHERE qa3.SOCIO = r.SOCIO
            AND qa3.QuotaDate BETWEEN r.Desde AND r.Ate
            AND qa3.QuotaDate BETWEEN @R AND DATEADD(MONTH, vp.P - 1, @R))
),

-- Melhor referência por sócio: maior cobertura dos meses do e-mail, depois data limite mais tardia
MelhorRef AS (
    SELECT *,
        ROW_NUMBER() OVER (
            PARTITION BY r.SOCIO
            ORDER BY
                (SELECT COUNT(*)
                 FROM QuotasAbertasComData qa4
                 JOIN ValoresPeriodo vp2 ON vp2.CODIGO = r.SOCIO
                 WHERE qa4.SOCIO = r.SOCIO
                   AND qa4.QuotaDate BETWEEN r.Desde AND r.Ate
                   AND qa4.QuotaDate BETWEEN @R AND DATEADD(MONTH, vp2.P - 1, @R)
                ) DESC,
                ISNULL(r.DATA_LIMITE_PAG, '9999-12-31') DESC
        ) AS rn
    FROM RefMBValidas r
)

-- Resultado: uma linha por sócio a avisar
SELECT
    ip.Email                                                                 AS contacto,
    ip.NOME                                                                  AS nome,
    'AVISO:' + CAST(CAST(ip.CODIGO AS BIGINT) AS VARCHAR(20))
        + ':' + LEFT(CONVERT(VARCHAR(10), @R, 120), 7)                      AS chave,

    -- Variáveis do template
    -- (Nome não é declarado: a coluna "nome" de controlo já entra no BusinessData
    --  e o renderer resolve {{Nome}} case-insensitive — duplicar causaria ArgumentException)
    LEFT(LTRIM(ip.NOME), CHARINDEX(' ', LTRIM(ip.NOME) + ' ') - 1)         AS PrimeiroNome,
    ip.Numero                                                                AS NumeroSocio,
    nm_r.Nome + ' de ' + CAST(@AnoR AS VARCHAR(4))                         AS MesReferencia,
    CASE vp.P
        WHEN 1  THEN 'Mensal'
        WHEN 2  THEN 'Bimestral'
        WHEN 3  THEN 'Trimestral'
        WHEN 6  THEN 'Semestral'
        WHEN 12 THEN 'Anual'
        ELSE CAST(vp.P AS VARCHAR(3))
    END                                                                      AS Periodicidade,
    nm_f.Nome + ' de '
        + CAST(YEAR(DATEADD(MONTH, vp.P - 1, @R)) AS VARCHAR(4))           AS FimPeriodo,
    REPLACE(CAST(CAST(vp.Valor AS DECIMAL(15, 2)) AS VARCHAR(20)), '.', ',')
        + ' €'                                                               AS Valor,
    LEFT(CONVERT(VARCHAR(10), @R, 120), 7)                                  AS Referencia,

    -- Referência MB (vazia quando não existe — o template usa {{#EntidadeMB}}…{{/EntidadeMB}})
    ISNULL(mb.Entidade, '')                                                  AS EntidadeMB,
    ISNULL(CASE WHEN LEN(mb.REFERENCIA) = 9
                THEN SUBSTRING(mb.REFERENCIA, 1, 3) + ' '
                   + SUBSTRING(mb.REFERENCIA, 4, 3) + ' '
                   + SUBSTRING(mb.REFERENCIA, 7, 3)
                ELSE mb.REFERENCIA END, '')                                  AS ReferenciaMB,
    ISNULL(REPLACE(CAST(CAST(mb.VALOR AS DECIMAL(15, 2)) AS VARCHAR(20)),
                   '.', ',') + ' €', '')                                     AS ValorMB,
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

FROM InicioPeriodo ip
JOIN ValoresPeriodo vp    ON vp.CODIGO  = ip.CODIGO
JOIN @Meses nm_r          ON nm_r.N     = @MesR
JOIN @Meses nm_f          ON nm_f.N     = MONTH(DATEADD(MONTH, vp.P - 1, @R))
LEFT JOIN MelhorRef mb    ON mb.SOCIO   = ip.CODIGO AND mb.rn = 1
LEFT JOIN @Meses nm_d     ON nm_d.N     = MONTH(mb.Desde)
LEFT JOIN @Meses nm_a     ON nm_a.N     = MONTH(mb.Ate)
ORDER BY ip.NOME;
