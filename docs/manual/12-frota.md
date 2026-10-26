# Frota

Módulo **Vorticis Frota** (ícone grafite): coordenação da frota e manutenção para transportadoras. A empresa escolhida no topo é a **transportadora** (cadastre-a em *Cadastros › Transportadoras* com o CNPJ e marque o módulo **Frota**). Emissão de CT-e/MDF-e e rastreamento por GPS ficam para uma próxima etapa.

## Onde o Frota está

O Frota é a base do **[Vorticis Logística](13-logistica.md)**, o programa único da transportadora: ali, os menus **Operação**, **Manutenção** e **Relatórios** são os deste capítulo, ao lado do financeiro, do fiscal, do contábil e da folha. O Vorticis Frota também pode ser aberto sozinho.

## Ordem dos cadastros

1. **A transportadora:** *Cadastros › Transportadoras*, com o módulo **Frota** marcado (e Gerencial, Contábil, Fiscal e Folha, se for usar). Escolha-a no topo da tela.
2. **Parâmetros:** *Cadastros › Parâmetros*, com a antecedência dos avisos e a tolerância do consumo (já vêm com 30 dias e 15%).
3. **Veículos** e **Motoristas**.
4. **Documentos e vencimentos** de cada veículo e motorista.
5. **Manutenção:** os **Planos de manutenção** (preventivas) e o catálogo de **Peças e serviços**.
6. No dia a dia: **Abastecimentos** e **Viagens** (menu Operação), **Ordens de serviço**, **Autorização de serviços** e **Notas de compra** (menu Manutenção); acompanhe tudo no **Painel da frota**.

## Veículos

**Onde fica:** *Cadastros › Veículos*.

1. **Novo veículo**: o código já vem sugerido. Informe a **Placa** (padrão antigo ABC-1234 ou Mercosul ABC1D23), o **RENAVAM** (o sistema confere o dígito), o **Tipo** (cavalo mecânico, truck, toco, carreta, bitrem, van, utilitário, reboque, semirreboque...), marca, modelo e anos.
2. Escolha o **Combustível**. Reboque e semirreboque ficam como **Não abastece**.
3. Informe o **Km atual** e a **Média esperada (km/l)**: é com ela que o sistema avisa quando o consumo cai.
4. **Gravar**. Para tirar um veículo de uso, desmarque **Veículo ativo** (um veículo com abastecimentos, manutenções ou viagens não pode ser excluído).

O km atual é atualizado sozinho pelos abastecimentos, ordens de serviço e viagens.

## Motoristas

**Onde fica:** *Cadastros › Motoristas*.

1. **Novo motorista**: nome, **CPF** (conferido), número da **CNH**, **categoria** e **validade**, telefone.
2. **Gravar**. A coluna **Situação da CNH** mostra quantos dias faltam para vencer; a validade também aparece no **Painel da frota**.

O exame toxicológico e o curso MOPP são lançados em *Documentos e vencimentos*, escolhendo o motorista.

## Planos de manutenção

**Onde fica:** *Manutenção › Planos de manutenção*.

Cada plano é uma preventiva de um veículo: troca de óleo, filtros, revisão de freios, lubrificação...

1. **Novo plano**: escolha o **Veículo** e descreva o **Serviço**.
2. Informe **A cada (km)**, **A cada (dias)** ou os dois: vale o que vencer primeiro.
3. Informe a **Última vez** (km e/ou data) em que o serviço foi feito. Com intervalo em dias, a data é obrigatória.
4. **Gravar**. A coluna **Próxima** mostra o km e a data da próxima troca e a **Situação** fica **A VENCER** (a 10% do intervalo de km ou dentro da antecedência dos avisos) ou **VENCIDA**.

Ao concluir uma ordem de serviço **preventiva** deste plano (*Manutenção › Ordens de serviço*), a última vez é atualizada sozinha.

## Documentos e vencimentos

**Onde fica:** *Cadastros › Documentos e vencimentos*.

Licenciamento (CRLV), IPVA, seguro, ANTT/RNTRC, aferição do tacógrafo, AET, exame toxicológico, curso MOPP e outros.

1. **Novo documento**: escolha o **Documento**, depois o **Veículo** ou o **motorista** (um ou outro; sem nenhum dos dois, o documento é da própria transportadora, como o RNTRC da empresa).
2. Informe o **Número**, o **Vencimento** e o valor, se quiser.
3. **Gravar**. Quando renovar, selecione o documento e altere o vencimento.

A coluna **Situação** mostra "vence em X dia(s)" dentro da antecedência dos avisos, ou "vencido há X dia(s)".

## Abastecimentos

**Onde fica:** *Operação › Abastecimentos* (atalho **Ctrl+B**).

1. Escolha o **Veículo** (o combustível do veículo já vem escolhido) e o **Motorista**.
2. Informe a **Data**, o **Km do hodômetro**, os **Litros**, o **Valor total** e o posto.
3. Deixe marcado **Completou o tanque** quando encheu o tanque. Abastecimento parcial: desmarque.
4. **Gravar**. A mensagem mostra a média desde o último tanque cheio e avisa se ficou abaixo do esperado.

Como a média é calculada: entre dois abastecimentos de **tanque cheio**, o km rodado dividido pelos litros colocados (os parciais do meio entram na soma). **Arla 32** é lançado como combustível próprio e não entra na média. O sistema recusa km menor que o do abastecimento anterior e combustível que não combina com o veículo.

Na lista, a coluna **Média** traz ⚠ quando a média ficou abaixo da esperada do veículo menos a tolerância dos *Parâmetros* (pode indicar vazamento, desvio de combustível ou necessidade de manutenção).

## Peças e serviços

**Onde fica:** *Manutenção › Peças e serviços*.

Catálogo das peças e serviços que a frota costuma usar (filtros, óleo, pastilhas, pneus, alinhamento, mão de obra...).

1. **Novo item**: o código já vem sugerido. Escolha **Peça** ou **Serviço**, escreva a **Descrição**, a **Unidade** (UN, L, H...) e o **Valor de referência**.
2. **Gravar**. Desmarque **Item ativo** para tirar o item da lista das ordens sem perder o histórico.

Na ordem de serviço, ao escolher um item do catálogo, a descrição e o valor de referência já vêm preenchidos (e podem ser alterados).

## Ordens de serviço

**Onde fica:** *Manutenção › Ordens de serviço* (atalho **Ctrl+O**).

Cada ordem passa por estas situações: **Aberta** → **Aguardando autorização** → **Autorizada** (ou **Reprovada**) → **Concluída**. Pode ser **Cancelada** a qualquer momento antes de concluir.

1. **Nova ordem**: escolha o **Veículo**. Se for uma preventiva, escolha o **Plano atendido** (o tipo vira **Preventiva** e o serviço vem do plano). Sem plano, é **Corretiva**.
2. Informe a **abertura** e a **previsão** de entrega, o **Km do veículo**, o **Serviço**, a **Oficina** e o **Solicitante**.
3. Em **Peças e serviços**, escolha um item do catálogo ou deixe "(digitar)" e escreva a descrição; informe a quantidade e o valor unitário e clique em **Adicionar**. Repita para cada peça e serviço. Para tirar um item, selecione-o e clique em **Remover item**. O total de peças e de serviços é a soma dos itens (sem itens, digite os dois valores).
4. **Gravar**: o número da ordem é automático.
5. Clique em **Pedir autorização**. Se o valor estiver dentro do limite dos *Parâmetros*, a ordem já fica **Autorizada**; acima dele, fica **Aguardando autorização** até alguém decidir em *Manutenção › Autorização de serviços*.
6. Com a ordem autorizada e o serviço feito, escolha a data em **Conclusão** e clique em **Concluir**. A preventiva concluída renova o plano a partir deste km e desta data.

Depois de enviada para autorização, a ordem não pode mais ser alterada. Para mudar o orçamento, clique em **Reabrir**: ela volta a ficar aberta e precisa de nova autorização. A ordem **reprovada** pode ser alterada e enviada de novo. Só se exclui uma ordem aberta ou cancelada; a que já passou pela autorização é **Cancelada**, para o histórico ficar completo.

Embaixo da ordem aparecem as **notas de compra** ligadas a ela, com o total.

## Autorização de serviços

**Onde fica:** *Manutenção › Autorização de serviços*. Precisa da função **Autorizar ou reprovar ordens de serviço de manutenção** (grupo Frota) no perfil do usuário (*Configurações › Usuários*, aba *Perfis de acesso*).

1. A lista mostra as ordens **aguardando autorização**, das mais antigas para as mais novas, com o total.
2. Clique na ordem: aparecem as peças e os serviços, a oficina e quem pediu.
3. Clique em **Autorizar**, ou escreva o motivo em **Observação** e clique em **Reprovar** (sem motivo, o sistema não deixa reprovar).

Embaixo ficam as decisões dos últimos 30 dias (quem decidiu, quando e o valor). As ordens autorizadas sozinhas pelo limite aparecem como **Autorizada (automática)**, pelo usuário **Sistema**. O **Painel da frota** também avisa quando há ordens aguardando autorização.

## Notas de compra

**Onde fica:** *Manutenção › Notas de compra*.

1. **Nova nota**: escolha a **Ordem de serviço** atendida. A oficina da ordem vem como fornecedor, e embaixo aparece o valor da ordem para conferir com a nota.
2. Informe o **Fornecedor**, o **CNPJ/CPF**, o **Número** e a **série**, a **Emissão** e, se tiver, a **Chave de acesso** de 44 dígitos (o sistema confere o dígito e se a chave é do CNPJ informado).
3. Separe o valor de **Peças** e de **Serviços** e clique em **Gravar**. Se a nota passar o valor da ordem, o sistema avisa.

A mesma nota (fornecedor, número e série) não pode ser lançada duas vezes. Nota sem ordem de serviço é aceita, mas aparece destacada no relatório de notas.

## Viagens

**Onde fica:** *Operação › Viagens*.

1. **Nova viagem**: veículo, motorista, cliente, **origem** e **destino**, data e **km de saída** (vem o km atual do veículo).
2. Informe o **Valor do frete** e as despesas: **pedágio**, **diárias** e outras.
3. Ao chegar, informe a **chegada** e o **km de chegada** e grave como **Concluída**. Viagem **Cancelada** não entra nos relatórios.

O combustível da viagem não é digitado aqui: o relatório *Resultado das viagens* soma os abastecimentos do veículo entre a saída e a chegada.

## Painel da frota

**Onde fica:** *Relatórios › Painel da frota* (atalho **Ctrl+P**).

Mostra, primeiro os vencidos e depois os a vencer:

- documentos e CNHs vencidos ou dentro da antecedência dos avisos;
- manutenções preventivas vencidas ou a vencer;
- veículos cuja última média de consumo ficou abaixo do esperado.

Embaixo do título, os números da frota: veículos ativos, motoristas e viagens em andamento. Clique em **Atualizar** depois de lançar algo.

## Relatórios da frota

**Onde fica:** *Relatórios* (Custos por veículo, Consumo de combustível, Resultado das viagens, Vencimentos e manutenções, Acompanhamento das ordens de serviço, Aprovações de serviços e Notas fiscais de compra).

Escolha o **Período** e clique em **Gerar**; o relatório aparece na tela e pode ser exportado em PDF, Excel ou CSV.

- **Custos por veículo:** combustível, manutenção (ordens de serviço, menos as canceladas e reprovadas) e despesas de viagem de cada veículo, o km rodado, a média e o **custo por km**.
- **Consumo de combustível:** cada média de tanque cheio a tanque cheio, com a média esperada e a marcação **ABAIXO DO ESPERADO**.
- **Resultado das viagens:** frete menos despesas e combustível de cada viagem; viagens com resultado negativo saem em destaque.
- **Vencimentos e manutenções:** a mesma lista do painel, para imprimir. Não pede período: é a posição de hoje.

## Relatórios da manutenção

**Onde fica:** *Relatórios › Acompanhamento das ordens de serviço*, *Aprovações de serviços* e *Notas fiscais de compra*.

Em cada um, informe as datas desejadas em **Período** (de/até) e clique em **Gerar**. Todos saem na tela e podem ser exportados em PDF, Excel ou CSV.

- **Acompanhamento das ordens de serviço:** as ordens abertas no período, com a situação, os **dias em aberto** (até a conclusão, ou até hoje), o valor da ordem, o valor **autorizado**, o total das **notas de compra** e a diferença. Ficam em destaque as ordens aguardando autorização e as que têm notas acima do autorizado.
- **Aprovações de serviços:** as autorizações e reprovações feitas no período, com data e hora, ordem, placa, decisão, valor, usuário e o motivo. As reprovadas ficam em destaque.
- **Notas fiscais de compra:** as notas emitidas no período, com fornecedor, CNPJ, ordem de serviço, placa e os valores de peças e serviços, com o total.

## Parâmetros da Frota

**Onde fica:** *Cadastros › Parâmetros*.

- **Avisar vencimentos com antecedência de (dias):** a partir de quantos dias antes do vencimento documentos, CNHs e preventivas por data passam a **a vencer** (padrão 30).
- **Tolerância do consumo (%):** quanto a média pode ficar abaixo da esperada antes de gerar alerta (padrão 15%). Ex.: média esperada 2,5 km/l e 15% → alerta abaixo de 2,13 km/l.
- **Autorizar sozinhas as ordens de até (R$):** ordens de serviço com valor até este limite são autorizadas automaticamente ao clicar em **Pedir autorização**. Com 0 (padrão), toda ordem passa por *Manutenção › Autorização de serviços*.
