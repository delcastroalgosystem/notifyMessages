namespace NotifyMessages.Application.Exceptions;

/// <summary>
/// Pedido com a mesma chave de idempotência de um envio já enfileirado.
/// Herda de InvalidOperationException e mantém o prefixo "DUPLICATE_REQUEST" por compatibilidade.
/// </summary>
public class DuplicateDispatchException : InvalidOperationException
{
    public DuplicateDispatchException(long existingDispatchId)
        : base("DUPLICATE_REQUEST: Esta mensagem já foi enfileirada.")
    {
        ExistingDispatchId = existingDispatchId;
    }

    public long ExistingDispatchId { get; }
}
