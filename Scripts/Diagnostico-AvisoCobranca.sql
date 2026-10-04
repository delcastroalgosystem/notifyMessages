-- ================================================================
-- Diagnostico-AvisoCobranca.sql — versão 2
-- Executar cada bloco (⑥ e ⑦) separadamente para identificar o culpado.
-- ================================================================

-- ⑥  Testa NULLIF com PERIODO_COB_POSTAL (não coberto antes)
SELECT TOP 5
    s.CODIGO,
    s.PERIODO_COB_POSTAL,
    ISNULL(NULLIF(ISNULL(s.PERIODO_COB_POSTAL, 0), 0), 1) AS P
FROM SOCIO s
WHERE s.SITUACAO < 7;
-- Se falhar aqui: PERIODO_COB_POSTAL é VARCHAR com valores não numéricos.
-- → Solução: substituir NULLIF por CASE WHEN (ver abaixo).


-- ⑦  Testa CAST(SUBSTRING(INFO_ADICIONAL,...) AS INT) com exactamente os mesmos filtros do script
/*
DECLARE @LimiteMB DATETIME = DATEADD(DAY, 3, DATEADD(DAY, 0, DATEDIFF(DAY, 0, GETDATE())));
SELECT TOP 5
    acp.INFO_ADICIONAL,
    CAST(SUBSTRING(acp.INFO_ADICIONAL,  1, 4) AS INT) AS Ano1,
    CAST(SUBSTRING(acp.INFO_ADICIONAL,  5, 2) AS INT) AS Mes1,
    CAST(SUBSTRING(acp.INFO_ADICIONAL,  7, 4) AS INT) AS Ano2,
    CAST(SUBSTRING(acp.INFO_ADICIONAL, 11, 2) AS INT) AS Mes2
FROM AVISO_COBRANCA_POSTAL acp
WHERE acp.TIPO_COBRANCA_POSTAL    = 1
  AND acp.ESTADO_AVISO_MB         = 1
  AND ISNULL(acp.GERAR_RECIBO, 'N') = 'N'
  AND acp.REFERENCIA IS NOT NULL AND acp.REFERENCIA <> ''
  AND LEN(acp.INFO_ADICIONAL)     = 12
  AND (acp.DATA_LIMITE_PAG IS NULL OR acp.DATA_LIMITE_PAG >= @LimiteMB)
  AND ISNUMERIC(SUBSTRING(acp.INFO_ADICIONAL,  1, 4)) = 1
  AND ISNUMERIC(SUBSTRING(acp.INFO_ADICIONAL,  5, 2)) = 1
  AND CAST(SUBSTRING(acp.INFO_ADICIONAL,  5, 2) AS INT) BETWEEN 1 AND 12
  AND ISNUMERIC(SUBSTRING(acp.INFO_ADICIONAL,  7, 4)) = 1
  AND ISNUMERIC(SUBSTRING(acp.INFO_ADICIONAL, 11, 2)) = 1
  AND CAST(SUBSTRING(acp.INFO_ADICIONAL, 11, 2) AS INT) BETWEEN 1 AND 12;
-- Se falhar: o optimizador avalia o CAST antes do ISNUMERIC.
-- → Solução: substituir CAST no WHERE por CASE WHEN (ver script principal actualizado).
*/
