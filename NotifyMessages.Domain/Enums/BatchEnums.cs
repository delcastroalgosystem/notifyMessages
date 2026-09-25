namespace NotifyMessages.Domain.Enums;

// De onde veio um lote de envios.
public enum BatchSource
{
    Connector = 1, // Conector que corre um gatilho agendado (ex. conector Smartarena)
    Api = 2,       // O software do cliente chamou a API de lotes diretamente
    File = 3       // Ficheiro CSV/Excel carregado na interface
}

public enum BatchStatus
{
    PendingApproval = 1, // Itens em Held, à espera de aprovação
    Approved = 2,        // Aprovado: itens passam a Queued
    Dispatching = 3,     // O Worker está a enviar
    Completed = 4,       // Todos os itens terminaram (enviados, falhados ou suprimidos)
    Canceled = 100       // Cancelado antes de enviar: itens em Held passam a Canceled
}

public enum SuppressionSource
{
    Manual = 1,      // Pedido do contacto registado por um operador
    Bounce = 2,      // Endereço devolvido pelo provedor (webhook)
    Unsubscribe = 3, // Link de cancelamento
    Import = 4       // Lista carregada de outro sistema
}
