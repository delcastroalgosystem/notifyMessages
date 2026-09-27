using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using NotifyMessages.Application.DTOs;
using NotifyMessages.Application.Interfaces;
using NotifyMessages.Application.Services;

namespace NotifyMessages.Infrastructure.Import;

// Lê um ficheiro CSV (;, , ou tab) ou Excel (.xlsx, primeira folha) com cabeçalho na primeira linha.
// Colunas reconhecidas (sem distinguir maiúsculas/acentos): contacto (obrigatória: email, e-mail, contacto,
// contact, telefone, telemovel, phone), nome (nome, name, destinatario) e chave (chave, chaveexterna, externalkey).
// Todas as outras colunas vão para as variáveis do template (BusinessData), com o nome do cabeçalho.
public class BatchFileParser : IBatchFileParser
{
    private static readonly HashSet<string> ColunasContacto = ["email", "contacto", "contact", "recipientcontact", "telefone", "telemovel", "phone"];
    private static readonly HashSet<string> ColunasNome = ["nome", "name", "recipientname", "destinatario"];
    private static readonly HashSet<string> ColunasChave = ["chave", "chaveexterna", "externalkey"];

    public ParsedBatchFile Parse(Stream content, string fileName)
    {
        string ext = Path.GetExtension(fileName).ToLowerInvariant();
        List<string[]> linhas;
        try
        {
            linhas = ext switch
            {
                ".csv" or ".txt" => LerCsv(content),
                ".xlsx" => LerExcel(content),
                _ => throw new FormatException($"Formato não suportado ({ext}): use .csv ou .xlsx.")
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidDataException or IOException)
        {
            return new ParsedBatchFile { Errors = [ex.Message] };
        }

        return Mapear(linhas);
    }

    internal static ParsedBatchFile Mapear(List<string[]> linhas)
    {
        var resultado = new ParsedBatchFile();
        if (linhas.Count == 0)
        {
            resultado.Errors.Add("Ficheiro vazio.");
            return resultado;
        }

        string[] cabecalho = linhas[0].Select(c => c.Trim()).ToArray();
        string[] normalizado = cabecalho.Select(Normalizar).ToArray();
        int iContacto = Array.FindIndex(normalizado, ColunasContacto.Contains);
        int iNome = Array.FindIndex(normalizado, ColunasNome.Contains);
        int iChave = Array.FindIndex(normalizado, ColunasChave.Contains);
        if (iContacto < 0)
        {
            resultado.Errors.Add("Falta a coluna de contacto (ex. \"email\" ou \"telefone\") no cabeçalho.");
            return resultado;
        }

        for (int n = 1; n < linhas.Count; n++)
        {
            string[] linha = linhas[n];
            if (linha.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }
            if (resultado.Items.Count >= BatchService.MaxItems)
            {
                resultado.Errors.Add($"Mais de {BatchService.MaxItems} linhas: as seguintes foram ignoradas (a partir da linha {n + 1}).");
                break;
            }

            string Celula(int i) => i >= 0 && i < linha.Length ? linha[i].Trim() : string.Empty;
            string contacto = Celula(iContacto);
            var dados = new Dictionary<string, string>();
            for (int i = 0; i < cabecalho.Length; i++)
            {
                if (i != iContacto && i != iNome && i != iChave && cabecalho[i].Length > 0)
                {
                    dados[cabecalho[i]] = Celula(i);
                }
            }

            resultado.Items.Add(new BatchItemDto
            {
                ExternalKey = Celula(iChave),
                RecipientName = iNome >= 0 && Celula(iNome).Length > 0 ? Celula(iNome) : contacto,
                RecipientContact = contacto,
                BusinessData = dados
            });
        }
        return resultado;
    }

    private static List<string[]> LerCsv(Stream content)
    {
        using var ms = new MemoryStream();
        content.CopyTo(ms);
        string texto = Descodificar(ms.ToArray());

        var linhasTexto = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string primeira = linhasTexto.FirstOrDefault(l => l.Trim().Length > 0) ?? string.Empty;
        char separador = new[] { ';', ',', '\t' }.OrderByDescending(c => primeira.Count(x => x == c)).First();
        return ParseCsv(texto, separador);
    }

    // UTF-8 (com ou sem BOM); se não for UTF-8 válido, Latin-1 (CSV exportado pelo Excel em PT).
    private static string Descodificar(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return Encoding.Latin1.GetString(bytes);
        }
    }

    // CSV com aspas ("a;b" e "" dentro de aspas).
    internal static List<string[]> ParseCsv(string texto, char separador)
    {
        var linhas = new List<string[]>();
        var campos = new List<string>();
        var atual = new StringBuilder();
        bool emAspas = false;

        for (int i = 0; i < texto.Length; i++)
        {
            char c = texto[i];
            if (emAspas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"') { atual.Append('"'); i++; }
                else if (c == '"') emAspas = false;
                else atual.Append(c);
            }
            else if (c == '"') emAspas = true;
            else if (c == separador) { campos.Add(atual.ToString()); atual.Clear(); }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n') i++;
                campos.Add(atual.ToString()); atual.Clear();
                linhas.Add([.. campos]); campos.Clear();
            }
            else atual.Append(c);
        }
        if (atual.Length > 0 || campos.Count > 0)
        {
            campos.Add(atual.ToString());
            linhas.Add([.. campos]);
        }
        return linhas;
    }

    private static List<string[]> LerExcel(Stream content)
    {
        using var workbook = new XLWorkbook(content);
        var folha = workbook.Worksheets.First();
        var usado = folha.RangeUsed();
        if (usado is null)
        {
            return [];
        }
        int colunas = usado.ColumnCount();
        return usado.Rows()
            .Select(r => Enumerable.Range(1, colunas).Select(c => r.Cell(c).GetFormattedString()).ToArray())
            .ToList();
    }

    // "E-mail" → "email", "Telemóvel" → "telemovel", "External_Key" → "externalkey"
    private static string Normalizar(string texto)
    {
        string semAcentos = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return new string(semAcentos.ToLowerInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
