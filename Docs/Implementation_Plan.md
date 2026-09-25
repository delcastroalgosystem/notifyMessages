Plano de Trabalho (Roadmap de 12 Semanas)
O objetivo não é fazer o software perfeito, mas sim o SaaS vendável.

Fase 1: Fundação & Dados (Semanas 1-3)
Semana 1: Configuração do ambiente .NET 8/9 e criação do Banco de Dados SQL Server com a nova estrutura (Tabela MESSAGE_DISPATCH com suporte a JSON).

Semana 2: Criação da API de Ingestão. É o endpoint que receberá os dados do ERP do cliente.

Meta: Conseguir receber um POST com JSON bruto e salvar no banco com a lógica de não-duplicação (Idempotency).

Semana 3: Integração com Brevo/E-goi.

Meta: Criar o "Worker Service" (serviço de fundo) que lê a fila do banco e dispara o e-mail/WhatsApp real.

Fase 2: O Painel de Controle (Semanas 4-7)
Semana 4: Estrutura do Frontend (Blazor ou MVC). Tela de Login e Gestão de Tenants (Cadastro de Clientes).

Semana 5: Dashboard de Métricas. Implementar a Query SQL que desenhamos para mostrar os gráficos de "Enviados vs. Lidos".

Semana 6: Editor de Templates. Permitir que o usuário escreva o texto da mensagem e insira variáveis {{NOME}} que serão substituídas pelo JSON.

Semana 7: Testes de carga. Simular o envio de 5.000 mensagens em 1 minuto para garantir que o sistema não trava.

Fase 3: A Camada de IA (Semana 8-10)
Semana 8: Integração com o Agente (Google Anti Gravity ou OpenAI via Semantic Kernel).

Semana 9: Criar a funcionalidade "Otimizar Mensagem". Um botão onde o cliente clica e a IA reescreve a cobrança para ser mais efetiva.

Semana 10: Auditoria e Logs. Garantir que tudo o que a IA fez ficou registrado.

Fase 4: Polimento e Lançamento (Semanas 11-12)
Semana 11: Documentação da API (Swagger) para entregar aos parceiros de ERP.

Semana 12: Lançamento da versão Beta para 3 clientes piloto (1 Clube, 1 Contador, 1 Agente).
