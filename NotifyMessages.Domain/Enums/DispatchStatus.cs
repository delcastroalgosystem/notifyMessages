using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotifyMessages.Domain.Enums;

public enum DispatchStatus
{
	Queued = 0,      // Recebido, aguardando Worker
	Processing = 1,  // Worker pegou para enviar
	Sent = 2,        // Enviado para o provedor (Brevo/Zenvia)
	Delivered = 3,   // Entregue no celular do cliente
	Read = 4,        // Lido pelo cliente
	Bounced = 5,     // Rejeitado pelo provedor (endereço/número inválido, spam, etc)
	Failed = 99,     // Erro (Número inválido, etc)
	Canceled = 100   // Cancelado
}
