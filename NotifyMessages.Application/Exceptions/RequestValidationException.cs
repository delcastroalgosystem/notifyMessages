namespace NotifyMessages.Application.Exceptions;

/// <summary>Pedido inválido detetado no serviço (ex. gatilho de outro tenant, lote sem template) → 400.</summary>
public class RequestValidationException : Exception
{
    public RequestValidationException(string title, string detail) : base(detail)
    {
        Title = title;
    }

    public string Title { get; }
}

/// <summary>Já existe um lote de conector para este gatilho e dia → 409 com o id existente.</summary>
public class DuplicateBatchException : Exception
{
    public DuplicateBatchException(long existingBatchId)
        : base("DUPLICATE_BATCH: já existe um lote deste gatilho para este dia.")
    {
        ExistingBatchId = existingBatchId;
    }

    public long ExistingBatchId { get; }
}
