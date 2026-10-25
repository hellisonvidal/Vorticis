# Frota

Módulo **Vorticis Frota** (ícone grafite, **piloto**): controle de frota para transportadoras. A empresa escolhida no topo é a **transportadora** (cadastre-a em *Cadastros › Transportadoras* com o CNPJ e marque o módulo **Frota (piloto)**). Por ser piloto, o módulo vem **desmarcado** no instalador: escolha a instalação personalizada e marque **Vorticis Frota (piloto)**. Emissão de CT-e/MDF-e e rastreamento por GPS ficam para uma próxima etapa.

## Ordem dos cadastros

1. **A transportadora:** *Cadastros › Transportadoras*, com o módulo **Frota (piloto)** marcado. Escolha-a no topo da tela.
2. **Parâmetros:** *Cadastros › Parâmetros*, com a antecedência dos avisos e a tolerância do consumo (já vêm com 30 dias e 15%).
3. **Veículos** e **Motoristas**.
4. **Planos de manutenção** (preventivas) e **Documentos e vencimentos** de cada veículo e motorista.
5. No dia a dia: **Abastecimentos**, **Ordens de serviço** e **Viagens**; acompanhe tudo no **Painel da frota**.

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

**Onde fica:** *Cadastros › Planos de manutenção*.

Cada plano é uma preventiva de um veículo: troca de óleo, filtros, revisão de freios, lubrificação...

1. **Novo plano**: escolha o **Veículo** e descreva o **Serviço**.
2. Informe **A cada (km)**, **A cada (dias)** ou os dois: vale o que vencer primeiro.
3. Informe a **Última vez** (km e/ou data) em que o serviço foi feito. Com intervalo em dias, a data é obrigatória.
4. **Gravar**. A coluna **Próxima** mostra o km e a data da próxima troca e a **Situação** fica **A VENCER** (a 10% do intervalo de km ou dentro da antecedência dos avisos) ou **VENCIDA**.

Ao concluir uma ordem de serviço **preventiva** deste plano, a última vez é atualizada sozinha.

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

## Ordens de serviço

**Onde fica:** *Operação › Ordens de serviço*.

1. **Nova ordem**: escolha o **Veículo**. Se for uma preventiva, escolha o **Plano atendido** (o tipo vira **Preventiva** e o serviço vem do plano). Sem plano, é **Corretiva**.
2. Informe a data, o **Km do veículo**, a oficina e os valores de **peças** e **mão de obra**.
3. Deixe **Aberta** enquanto o veículo estiver na oficina e grave como **Concluída** ao terminar. A preventiva concluída renova o plano a partir deste km e desta data.

O número da ordem é automático.

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

**Onde fica:** *Relatórios* (Custos por veículo, Consumo de combustível, Resultado das viagens, Vencimentos e manutenções).

Escolha o **Período** e clique em **Gerar**; o relatório aparece na tela e pode ser exportado em PDF, Excel ou CSV.

- **Custos por veículo:** combustível, manutenção (ordens de serviço) e despesas de viagem de cada veículo, o km rodado, a média e o **custo por km**.
- **Consumo de combustível:** cada média de tanque cheio a tanque cheio, com a média esperada e a marcação **ABAIXO DO ESPERADO**.
- **Resultado das viagens:** frete menos despesas e combustível de cada viagem; viagens com resultado negativo saem em destaque.
- **Vencimentos e manutenções:** a mesma lista do painel, para imprimir. Não pede período: é a posição de hoje.

## Parâmetros da Frota

**Onde fica:** *Cadastros › Parâmetros*.

- **Avisar vencimentos com antecedência de (dias):** a partir de quantos dias antes do vencimento documentos, CNHs e preventivas por data passam a **a vencer** (padrão 30).
- **Tolerância do consumo (%):** quanto a média pode ficar abaixo da esperada antes de gerar alerta (padrão 15%). Ex.: média esperada 2,5 km/l e 15% → alerta abaixo de 2,13 km/l.
