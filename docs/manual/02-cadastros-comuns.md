# Cadastros comuns

Estes cadastros existem em todos os módulos e têm o mesmo código em todos eles. Todos usam a barra **Incluir · Alterar · Excluir · Salvar · Cancelar**, mostram o **Nº** de cada registro e a lista dos cadastrados à direita. As datas, CNPJ, CPF e CEP são formatados enquanto você digita.

## Empresas

**Para que serve:** cadastrar as empresas atendidas. Só o administrador cria e altera.
**Onde fica:** *Cadastros › Empresas*.

### Passo a passo
1. Clique em **Incluir**.
2. Digite o **CNPJ** (aceita o novo CNPJ alfanumérico) e clique em **Buscar dados na Receita**: razão social, natureza jurídica, CNAE, endereço, início de atividade e opção pelo Simples/MEI são preenchidos. Revise os dados. Os sócios (QSA) também são incluídos.
3. Confira a **inscrição estadual** (digite só números ou ISENTO). **Buscar IE (SINTEGRA)** procura o número; o sistema sempre pergunta antes de usá-lo. Se não encontrar, grava ISENTO. **Abrir SINTEGRA** abre o portal para conferência manual.
4. Preencha o **NIRE** (11 dígitos), endereço e contatos.
5. Em **Módulos**, marque onde a empresa será usada (Contábil, Fiscal, Folha de Pagamento). A lista de empresas no topo de cada programa mostra só as empresas ativas naquele módulo.
6. Em **Escrituração**, escolha o **regime tributário** (tem histórico por ano; é definido na inclusão) e o primeiro exercício.
7. **Salvar**.

### Abas da empresa
- **Dados cadastrais**: tudo acima.
- **Sócios** e **Contadores**: iguais às telas [Sócios](#socios) e [Contadores](#contadores).
- **Contabilidade**: modelo e máscara do plano de contas, centros de custo, início da escrituração, regras dos lançamentos e contas do encerramento (veja abaixo).

### Aba Contabilidade (campos principais)
| Campo | Para que serve |
|---|---|
| Modelo do plano | 13 modelos prontos (comércio, indústria e serviços por regime, ITG 1000, entidade sem fins lucrativos, completo). "Plano próprio" não cria contas. |
| Máscara dos códigos | Ex.: `9.9.99.999`; usada na importação e na conversão do plano. |
| Usar centros de custo / máscara | Liga os centros de custo e define o formato dos códigos. |
| Início da escrituração | Data a partir da qual a empresa é escriturada no sistema. |
| Exigir nº do documento | Cada partida do lançamento precisa do número do documento (saldos iniciais, encerramento e estornos ficam isentos). |
| Bloquear lançamentos até | Recusa lançamentos com data até a data informada. |
| Usar estorno de lançamentos | Ligado (padrão): lançamentos gravados são corrigidos por estorno. Desligado: não há estorno; **Editar** altera o próprio lançamento e **Excluir** o remove (sempre com registro na Auditoria). Veja o tópico Lançamentos. |
| Plano com participantes | Clientes e fornecedores ficam dentro das contas contábeis (veja [Participantes](#clientes-e-fornecedores-participantes)). |
| Grande porte, apuração do IRPJ, regime de caixa | Informações usadas na ECD e nas apurações. |
| Contas do encerramento | Apuração do resultado, lucros e prejuízos acumulados, usadas como padrão em *Encerramento do exercício*. |

### Regras e cuidados
- **Excluir uma empresa apaga toda a escrituração dela** (lançamentos, plano, períodos, sócios, acessos). O sistema avisa e **gera um backup automático antes**, informando onde ficou.
- Não é possível trocar o plano de contas depois que existem lançamentos.
- A consulta de CNPJ usa a base pública da Receita (via BrasilAPI e Minha Receita); empresas muito recentes podem não constar. A consulta de IE permite 3 consultas por minuto.

## Clientes e fornecedores (participantes)

**Para que serve:** cadastrar quem compra e quem vende. A mesma pessoa pode ser cliente e fornecedor (são menus separados).
**Onde fica:** *Cadastros › Clientes* e *Cadastros › Fornecedores*.

1. **Incluir** e digite CPF ou CNPJ (validado); **Buscar dados na Receita** preenche o restante.
2. Complete nome, inscrição estadual (ou ISENTO), endereço (o CEP preenche o endereço) e contatos.
3. Em **Contabilidade**, informe a **conta contábil** (código ou reduzido; opcional).
4. **Salvar**.

**Participante na conta:** com a opção "plano com participantes" ligada na empresa, informe a conta (ex.: Clientes) e marque **Participante**. A conta passa a exigir o participante em cada lançamento, a tela de lançamento ganha a coluna *Part.* e o balancete pode **detalhar participantes**.

## Sócios

**Onde fica:** *Cadastros › Empresas*, aba **Sócios** (empresa selecionada no topo).

1. **Importar quadro societário da Receita** traz os sócios da base pública. O CPF vem mascarado: complete-o.
2. Para incluir à mão: nome, CPF ou CNPJ, qualificação, **capital (%)**, data de entrada e de saída, e-mail e telefone.
3. Marque **Representante legal** em quem assina a ECD e os termos.

**Regra:** a soma da participação dos sócios ativos não pode passar de 100%.

## Contadores

**Para que serve:** vincular à empresa o contador ou escritório responsável.
**Onde fica:** *Cadastros › Contadores* (ou a aba *Contadores* da empresa).

1. Escolha **Contador** (CPF, CRC/UF) ou **Escritório contábil** (CNPJ, CRC). O cadastro é único e reaproveitado entre empresas: se já existe, escolha na lista "Já cadastrado".
2. Informe o início do vínculo e contatos (e-mail e telefone são exigidos pela ECD).
3. Marque **Responsável técnico** (apenas um vigente por empresa) e **Assina ECD/ECF** se for o signatário. Sem marcação, assina o responsável técnico.

**Dica:** na troca de contabilidade, **encerre o vínculo** (campo *Fim do vínculo*); não exclua.

## Procuradores

**Para que serve:** pessoa que assina a ECD/ECF e transmite o eSocial pela empresa, com procuração eletrônica.
**Onde fica:** *Cadastros › Procurador (assinatura ECD/ECF)*.

1. **Incluir**: nome, CPF, e-mail, telefone e a vigência (desde / até).
2. Use a observação para anotar a identificação da procuração no e-CAC.

**Importante:** a procuração em si deve ser outorgada no e-CAC da Receita; o cadastro no Vorticis apenas registra quem é o procurador e por quanto tempo.
