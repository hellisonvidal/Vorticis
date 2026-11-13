# Fiscal

Menus do módulo Vorticis Fiscal. As escriturações usam os cadastros de clientes e fornecedores e o plano de contas do módulo Contábil, no mesmo banco. No cadastro de *Clientes* e *Fornecedores* a opção **Criar conta no plano de contas** abre automaticamente a conta da pessoa abaixo da conta título escolhida na empresa (veja [Clientes e fornecedores](02-cadastros-comuns.md#clientes-e-fornecedores-participantes)).

Veja também [Filtros nas listas](01-primeiros-passos.md#filtros-nas-listas): toda lista deste módulo tem busca rápida e filtros por coluna.

## Produtos

**Para que serve:** cadastro das mercadorias e serviços, usado na digitação das notas e na EFD.
**Onde fica:** *Cadastros › Produtos*.

A tela mostra a **lista dos produtos** (com filtro). A **ficha do produto** abre numa janela por cima da lista: **Novo**, **Abrir ficha**, duplo clique ou **Enter** na lista a abrem; **Salvar** grava e fecha a janela, e **Cancelar**, **Esc** ou o **X** fecham sem gravar. **Excluir** fica na tela.

1. **Novo** e informe código, descrição, unidade, **NCM**, CEST, código de barras (GTIN/EAN), origem e tipo do item.
2. Informe os padrões fiscais: CFOP e CST de entrada e de saída, e as alíquotas de ICMS, IPI, PIS e COFINS.
3. **Salvar** (a janela fecha). O filtro busca por descrição, código, GTIN ou NCM; *Somente ativos* esconde os inativos.

## Regras fiscais

**Para que serve:** ensinar o sistema a escriturar as notas de **entrada** importadas do XML do fornecedor. O fornecedor emite a nota com o CFOP de saída dele (5.102, 6.102, 5.405...); na sua escrituração o CFOP é de entrada (1.102, 2.102, 1.403...). Aqui você define essa troca, o CST e se a empresa tem direito a crédito de ICMS, IPI, PIS e COFINS.
**Onde fica:** *Cadastros › Regras fiscais*.

1. Clique em **Gerar regras padrão** para criar a troca usual (5.xxx vira 1.xxx, 6.xxx vira 2.xxx, com as correspondências conhecidas, como 5.405 para 1.403 e 5.933 para 1.933). Só entram correspondências seguras; as demais ficam sem regra. As regras que você já cadastrou não são alteradas.
2. Para uma regra própria, clique em **Nova** (a ficha da regra abre numa janela por cima da lista; também por **Abrir ficha** ou duplo clique numa regra), escolha o tipo **Entrada** e informe o **CFOP do emitente** e o **CFOP a usar**. Se quiser, restrinja a regra a um fornecedor, a um NCM (ou ao começo dele, para valer para um grupo), a um produto ou a um CST/CSOSN do XML.
3. Em **Então**, informe, se precisar, o CST/CSOSN a usar e desmarque os impostos que **não geram crédito** (os valores de crédito do item ficam zerados na escrituração, e o XML original não muda). Também pode indicar as contas de débito e de crédito (código da conta analítica) para a contabilização.
4. **Salvar** (a janela fecha). A regra vale nas próximas importações; para notas já importadas use **Reaplicar regras** (veja abaixo).

**Qual regra vale:** a mais específica. Produto vence NCM, que vence fornecedor, que vence a regra só com o CFOP. Se duas forem igualmente específicas, vale a de menor **Ordem**.

**Sem regra:** se nenhuma regra servir, o item recebe a troca padrão (5 vira 1, 6 vira 2, 7 vira 3) e fica marcado como **Sem regra**, para você conferir. Na tela de entrada marque *Só notas com itens sem regra fiscal* para listar essas notas.

**Notas de saída:** crie uma regra do tipo **Saída** para o sistema sugerir o CFOP e o CST ao digitar um item, conforme o produto, o NCM, o cliente e o destino (*Dentro do estado* ou *Outro estado*). A sugestão aparece quando você busca o produto no item e pode ser alterada.

**Reforma tributária (IBS/CBS):** em **Então**, a regra também pode trazer o **CST IBS/CBS** e a **Classificação (cClassTrib)** a sugerir nas notas de saída, e a caixa **Credita IBS/CBS**, que diz se as compras que caem na regra geram crédito de IBS e CBS (desmarque para uso e consumo pessoal, por exemplo). Na importação das notas de entrada, a caixa é aplicada ao item.

## Classificação tributária (IBS/CBS)

**Para que serve:** cadastrar as classificações tributárias (**cClassTrib**) do IBS e da CBS que as suas empresas usam, com o CST, a redução da alíquota e se a compra gera crédito. Na nota, a classificação faz o cálculo certo do IBS e da CBS (por exemplo, os itens com alíquota reduzida em 60% pagam só 40% da alíquota).
**Onde fica:** *Cadastros › Classificação tributária (IBS/CBS)*. Vale para todas as empresas.

1. Clique em **Nova** e informe o **código** de 6 dígitos da tabela oficial (publicada pela Receita Federal e pelo Comitê Gestor do IBS; na maior parte dos códigos, os 3 primeiros dígitos são o CST).
2. Escolha o **CST do IBS/CBS** (000 tributação integral, 200 alíquota reduzida, 400 isenção, 410 imunidade e não incidência, 510 diferimento, 550 suspensão, 620 tributação monofásica...) e escreva a **descrição**.
3. Informe a **Redução da alíquota (%)**: 60 para quem paga 40% da alíquota, 30 para quem paga 70%, 100 para alíquota zero. Deixe em branco quando não há redução.
4. Desmarque **A compra gera crédito de IBS/CBS** quando a aquisição não dá direito a crédito.
5. **Gravar**. Para tirar de uso sem apagar, desmarque **Ativa**; **Excluir** apaga (as notas já lançadas não mudam).

A classificação **000001** (tributação integral) já vem cadastrada.

## Documentos de entrada e de saída (digitação)

**Para que serve:** consultar as notas já lançadas e lançar à mão as que não têm XML (ou conferir um XML antes de gravar).
**Onde fica:** *Lançamentos › Documentos fiscais › Entrada* e *Saída*.

### A tela de consulta
Ao abrir, a tela mostra a **grade das notas lançadas** (digitadas e importadas), em tela cheia:

- **Filtros:** período (de/até), cliente ou fornecedor (nome ou CNPJ/CPF), número, **Origem** (todas, digitadas ou importadas por XML) e **Situação** (todas, autorizadas, canceladas, contabilizadas ou não contabilizadas). Clique em **Filtrar**; **Limpar filtros** volta ao padrão.
- **Rodapé:** quantidade de notas e totais de valor, ICMS, IPI, PIS, COFINS e ISS do que está na grade (notas canceladas não entram nos valores).
- **Novo lançamento:** abre a digitação de uma nota em branco numa janela à parte (pop-up).
- **Editar** (ou dois cliques na nota, ou Enter): abre a nota para alterar.
- **Consultar:** abre a nota só para leitura.
- **Copiar:** abre uma nota **nova** com o cliente/fornecedor, os itens e os tributos da selecionada (sem número, chave de acesso e parcelas), para notas que se repetem. Informe o número, confira as datas e grave; a nota original não muda. A cópia de uma nota importada vira nota digitada.
- **Excluir:** apaga a nota e as duplicatas dela (notas contabilizadas devem ter a contabilização desfeita antes; nota com duplicata já baixada só pode ser excluída depois de estornar as baixas).
- **Importar XML:** importa arquivos, pastas ou .zip em lote (veja [Importação de XML](#importacao-de-xml-nf-e-nfc-e-nfs-e)); as notas aparecem na grade.

### A janela da nota
A nota tem abas na mesma janela: **Nota** (cabeçalho e itens), **Tributos**, **Parcelas** e **Escrituração**. **Enter** passa para o próximo campo; **Esc** (ou **Fechar**) fecha a janela. Ao **Gravar nota**, a janela fecha e a grade é atualizada.

1. Aba **Nota**: modelo, série, número, **Emissão**, data de entrada/saída, chave de acesso (opcional) e natureza da operação. Digite o **CNPJ/CPF** do cliente/fornecedor (busca automática, ou **Buscar participante**). Se não existir, informe nome, UF e município para cadastrá-lo na hora. A data de entrada/saída acompanha a emissão.
2. Ainda na aba **Nota**, logo abaixo do cabeçalho, ficam os **itens**: produto (código, descrição ou GTIN; **Buscar produto**), CFOP, quantidade, valor unitário, NCM, unidade, desconto, frete, seguro e outras despesas. **Adicionar item**, **Atualizar item selecionado** ou **Remover item selecionado**; a lista dos itens já lançados aparece embaixo.
3. Aba **Tributos**: escolha um item na lista e confira **CFOP, CST/CSOSN, base, alíquota e valor de ICMS**, ICMS-ST, IPI, PIS, COFINS e, para serviço, ISS. Ao alterar a **base** ou a **alíquota**, o valor do tributo é recalculado (base × alíquota); você pode corrigir o valor à mão depois. **Calcular tributos** preenche pelas alíquotas do produto o que estiver em branco (e, a partir de 2026, calcula também o IBS e a CBS). Clique em **Aplicar ao item** para guardar; os **totais da nota** (rodapé da janela) são recalculados, com o IBS, a CBS e o IS. Nas notas importadas de XML os tributos são só de leitura.
   - **IBS, CBS e Imposto Seletivo (reforma tributária):** no mesmo item, informe a **Classificação (cClassTrib)** (ou só o **CST IBS/CBS**) e clique em **Calcular IBS/CBS/IS**. O sistema usa as alíquotas do ano da nota (2026: CBS 0,9% e IBS 0,1%; 2027: CBS 11,2% e IBS 0,05% + 0,05%), aplica a **redução** da classificação, zera o IBS/CBS nos CST que não tributam (isenção, imunidade, diferimento, suspensão) e usa como **base** o valor do item sem o ICMS, o ISS, o PIS e a COFINS. Sem CST, vale 000 (tributação integral, classificação 000001). Para produtos com **Imposto Seletivo** (a partir de 2027: bebidas, cigarros, veículos, bens minerais...), informe o CST, a classificação e a **alíquota do IS** antes de calcular. Os valores podem ser corrigidos à mão.
   - **Credita IBS/CBS** (nota de entrada): marcado, o IBS e a CBS do item entram como crédito na apuração; a classificação sem crédito desmarca sozinha.
   - Ao gravar, o sistema avisa se a nota de 2027 em diante ainda tem **PIS/COFINS** (substituídos pela CBS) ou **IPI** (zerado, salvo Zona Franca de Manaus) destacados, ou itens sem o CST do IBS/CBS.
4. Aba **Parcelas**: escolha a **Forma de pagamento** ([cadastro](02-cadastros-comuns.md#formas-de-pagamento)) e as duplicatas são geradas pelos prazos dela, contados da emissão, sobre o total da nota. Se mudar os itens depois, clique em **Gerar pela forma** para refazer. Para parcelar à mão, informe o **número de parcelas**, o **primeiro vencimento** e o **intervalo em dias** e clique em **Gerar parcelas** (a última recebe a diferença de centavos; a soma tem de bater com o valor da nota). **Limpar parcelas** apaga a lista. Sem parcelas, a nota nova gera uma duplicata única *à vista* (veja [Duplicatas](#duplicatas-contas-a-pagar-e-a-receber)).
5. Aba **Escrituração**: centro de custo, contas de débito e crédito e observações (veja abaixo, nas notas importadas).
6. Confira o **total informado** (aba Nota, ao lado dos botões dos itens): precisa bater com a soma dos itens; em branco, vale a soma. Clique em **Gravar nota**.
7. Nota de entrada com itens de **ativo imobilizado** (CFOP 1.551, 2.551, 3.551, 1.406 ou 2.406, produto do tipo *08 – Ativo imobilizado* ou regra de bens): ao gravar a nota, o sistema prepara o cadastro dos bens no Contábil. Com a contabilização **On-line**, o *Cadastro de bens* abre já preenchido para você conferir e salvar (pode cancelar: o item fica na lista *Imobilizado › Bens a cadastrar das notas*). Com a contabilização **Em lote**, o item só fica na lista e é oferecido na *Contabilização em lote*. Se o Contábil não estiver instalado/aberto, o sistema só registra e avisa. O botão **Cadastrar bens do imobilizado** reabre o cadastro com os itens ainda pendentes da nota. Vale também para notas importadas por XML, por chave ou em lote. Veja [Bens a cadastrar das notas](03-contabil.md#bens-a-cadastrar-das-notas).

**Campos bloqueados?** Uma faixa no topo da janela explica o motivo:
- *Seu perfil só permite consultar*: peça ao administrador para marcar, no seu perfil, a permissão **Importar XML e digitar notas fiscais** (*Configurações › Usuários › Perfis*).
- *Nota contabilizada*: desfaça a contabilização para alterar, ou use **Novo lançamento** para lançar outra.
- *Nota aberta para consulta*: você abriu pelo botão **Consultar**; feche e use **Editar**.
- *Nota importada de XML* (faixa amarela): os dados do XML não mudam; só a escrituração (CFOP de entrada, CST, créditos de ICMS, IPI, PIS, COFINS e IBS/CBS, centro de custo, produto vinculado, contas e observações). Escolha o item na lista da aba **Nota** (ou da aba Tributos), ajuste na aba **Escrituração**, clique em **Aplicar ao item** e depois em **Gravar escrituração**. A coluna *Regra fiscal* mostra *Sem regra* nos itens que não encontraram regra; ao gravar a escrituração a marca sai.

### Reaplicar regras (notas de entrada)
Na tela de *Entrada*, **Reaplicar regras** refaz o CFOP, o CST e os créditos das notas **importadas e ainda não contabilizadas** do período filtrado, pelas regras de *Cadastros › Regras fiscais*. Itens sem regra que você já conferiu à mão são mantidos. Notas contabilizadas, ou de período fiscal fechado, não são alteradas.

### Importar uma nota (XML ou chave de acesso)
No alto da janela da nota há dois atalhos. Eles **gravam a nota como importada** (com o XML original e a chave de acesso, como na importação em lote) e abrem a nota para você conferir a escrituração. Os valores do XML não mudam; só a escrituração.

**Importar por XML:** clique em **Escolher arquivo...** e escolha o XML de uma nota. O fornecedor/cliente é cadastrado a partir do XML, se ainda não existir. Em notas de entrada, o CFOP de entrada, o CST e os créditos vêm das *Regras fiscais*; itens sem regra são avisados. A nota precisa ser da empresa selecionada, e da operação da tela (uma nota de saída do seu cliente abre em *Saída*). Se a nota (mesma chave de acesso) já estiver lançada, o sistema avisa e abre a que já existe, sem duplicar.

**Importar por chave de acesso:** cole a chave de 44 dígitos (pode ter espaços ou pontos) e clique em **Importar por chave**. O sistema confere o dígito verificador e então:
1. **procura a nota entre as já lançadas e os XMLs importados da empresa**; se achar, mostra a nota que já está no sistema (não duplica);
2. se não achar, e a empresa tiver **certificado digital** (e-CNPJ A1 ou A3, cadastrado em *Configurações › Certificados digitais* e marcado como padrão do responsável), **baixa o XML na SEFAZ** (Ambiente Nacional, a mesma consulta do Portal da NF-e). Se o certificado for A1, o sistema pede a senha;
3. sem certificado, o sistema avisa que a nota não está entre as importadas e que é preciso o certificado digital da empresa.

Se a SEFAZ devolver só o **resumo** da nota, ela ainda não foi manifestada: aparece o botão **Manifestar ciência**, que registra a *Ciência da operação* em nome da empresa e libera o XML completo. Aguarde alguns minutos e clique de novo em **Importar por chave**. Mensagens da SEFAZ (ex.: *137 – Nenhum documento localizado*, *656 – Consumo indevido*) aparecem como vieram; a SEFAZ limita o número de consultas por hora (se aparecer *656*, espere uma hora).

## Importação de XML (NF-e, NFC-e, NFS-e)

**Para que serve:** trazer as notas dos arquivos XML, sem digitar, de qualquer tipo ao mesmo tempo.
**Onde fica:** *Integrações › Importação de XML em lote* (atalho **Ctrl+I**).

### Um ou mais arquivos
Clique em **Importar arquivos...** e escolha os .xml ou um .zip.

### Importação em lote (pasta)
1. Digite ou **Procurar...** a pasta (ex.: `C:\XML\NotasFiscais`).
2. Marque **Incluir subpastas** se for o caso e clique em **Importar pasta**. Também aceita .zip.
3. O painel mostra o resultado por arquivo (importado, duplicado, recusado e o motivo). **Exportar resumo (CSV)...** guarda o relatório; **Cancelar importação** interrompe o lote.

### Regras
- Aceita NF-e e NFC-e (modelos 55 e 65), CT-e (modelo 57) e NFS-e no padrão nacional (ADN).
- O sentido é identificado pelo **CNPJ da empresa** (e dos estabelecimentos): é entrada se a empresa é o destinatário e saída se é o emitente. Notas de terceiros são recusadas.
- **Duplicidade:** a mesma chave de acesso não é importada duas vezes. Eventos de cancelamento são aplicados, mesmo que cheguem antes da nota.
- Clientes e fornecedores são criados ou vinculados automaticamente. Com **Completar o cadastro de fornecedores e clientes novos pela consulta do CNPJ (Receita)** marcado (vem marcado), o cadastro criado a partir do XML é completado como no botão *Buscar dados na Receita* do cadastro de clientes e fornecedores: nome fantasia, endereço completo, e-mail, telefone e inscrição estadual que faltarem. Os dados que vieram no XML são mantidos. Na janela da nota (Importar por XML ou por chave), o fornecedor novo aparece com os dados do XML para conferência e é cadastrado só ao gravar a nota.
- **Nota de outra empresa:** se o XML não for da empresa selecionada (o CNPJ dela não é o do emitente nem o do destinatário), a nota não é importada e aparece na lista como *Outra empresa*, com os CNPJs do emitente e do destinatário. Escolha a empresa certa no topo e importe de novo.
- Se acontecer um erro inesperado, o programa mostra o aviso, grava os detalhes em `%LOCALAPPDATA%\VorticisEmpresarial\logs\erros.log` e continua aberto.
- **Regras fiscais:** nas notas de **entrada**, cada item recebe o CFOP de entrada, o CST e os créditos conforme [Regras fiscais](#regras-fiscais); sem regra, vale a troca padrão (5 para 1, 6 para 2, 7 para 3) e o item fica marcado como *Sem regra* para conferência.
- **Duplicatas:** a cobrança da NF-e (parcelas com número, vencimento e valor) vira duplicatas a pagar (nota de entrada) ou a receber (nota de saída). Nota sem cobrança gera uma duplicata *à vista*, vencendo na emissão (os dias são configuráveis, e é possível desligar em *Cadastros › Regras de contabilização*, aba *Duplicatas e baixas*). NFC-e só gera duplicata se trouxer cobrança. Se a nota for cancelada, as duplicatas ainda abertas também são canceladas.

## Importar NFS-e

**Para que serve:** trazer só as notas de serviço (NFS-e).
**Onde fica:** *Integrações › Importar NFS-e*.
Funciona como a [importação de XML em lote](#importacao-de-xml-nf-e-nfc-e-nfs-e) (arquivos, .zip ou pasta), mas só aceita NFS-e; arquivos de outro tipo aparecem como *Ignorado* e podem ser importados em *Integrações › Importação de XML em lote*. Serviço prestado pela empresa é **saída**; serviço tomado é **entrada**.
**Layouts aceitos:** padrão nacional (ADN) e o layout **ABRASF básico** (arquivos com CompNfse/Nfse: prestador, tomador, valor do serviço, ISS e retenções). A nota ABRASF não tem chave de acesso: o sistema usa o CNPJ do prestador, o número e o código de verificação para não importar a mesma nota duas vezes. Se o município usa outro layout, digite a nota em *Lançamentos › Documentos fiscais*.

## Importar CT-e

**Para que serve:** trazer os conhecimentos de transporte eletrônicos (CT-e, modelo 57, arquivo procCTe).
**Onde fica:** *Integrações › Importar CT-e*.
1. Clique em **Importar arquivos...** ou informe a pasta e clique em **Importar pasta**.
2. O sistema lê a transportadora (emitente), o tomador, o remetente e o destinatário, o percurso, o valor da prestação, o CFOP, o ICMS e a chave de acesso, e guarda o XML.
3. Se a empresa é o **tomador** do serviço (quem paga o frete), o CT-e é **entrada** (a transportadora vira fornecedor e o ICMS destacado pode ser crédito). Se a empresa é a **transportadora** (emitente), é **saída** (o tomador vira cliente). Se não for nenhuma das duas, o CT-e é recusado.
4. O CT-e entra nos livros de entradas e saídas, no resumo por CFOP, na contabilização (pelas regras de contabilização) e, na EFD ICMS/IPI, nos registros **D100 e D190** (ICMS destacado nos créditos e débitos da apuração). Eventos de cancelamento atualizam o CT-e.
**Limitações:** a EFD-Contribuições ainda não traz o CT-e (registro D100 daquela escrituração).

## Importar SPED (EFD ICMS/IPI e EFD-Contribuições)

**Para que serve:** trazer os dados de uma escrituração feita em outro sistema: participantes, produtos, notas fiscais de entrada e saída (NF-e, NFC-e, NFS-e e CT-e), apuração e ajustes, inventário e o histórico de PIS e COFINS.
**Onde fica:** *Integrações › Importar SPED (EFD ICMS/IPI e EFD-Contribuições)*.
1. **1. Arquivo:** clique em **Procurar...** e escolha o arquivo .txt. O programa mostra o **Tipo reconhecido** (EFD ICMS/IPI ou EFD-Contribuições). Arquivos da ECD ou da ECF devem ser importados no módulo Contábil.
2. **2. O que importar:** tudo, só cadastros e notas, ou só os cadastros. Marque *Ignorar o que tiver erro e importar o restante* se aceitar que as notas com problema fiquem de fora (elas aparecem no relatório).
3. **Aba Prévia** (nada é gravado ainda): período, empresa do arquivo e, para cada tipo de registro, quantos são **novos**, quantos **já existem** (notas com a mesma chave, ou mesmo número, série e participante, não são duplicadas) e quantos têm **conflito**. Abaixo ficam os erros e avisos com a linha do arquivo. Se o CNPJ não for o da empresa ativa, a importação é bloqueada.
4. **3. Importar:** clique em **Importar** e confirme. A barra mostra o andamento; **Cancelar importação** desfaz tudo. Tudo é gravado numa só vez. O mesmo arquivo não pode ser importado duas vezes.
5. **Aba Relatório:** o que foi importado, o que já existia e o que teve erro; **Salvar relatório (CSV)** abre no Excel.
6. **Aba Histórico de importações:** selecione uma importação e clique em **Desfazer importação** para remover o que ela criou. Notas já contabilizadas ou em período fechado impedem o desfazer, e o programa explica o motivo.

## Consulta de CT-e

**Onde fica:** *Documentos fiscais › Consulta de CT-e*.
Escolha o período, o sentido (entradas, como tomador, ou saídas, como transportadora) e, se quiser, um texto (número, nome, CNPJ ou chave) e clique em **Consultar**. A lista mostra transportadora, tomador, remetente, destinatário, percurso, CFOP, valor, ICMS e situação, com o total no rodapé.

## Consulta de documentos

**Onde fica:** *Documentos fiscais › Consulta de documentos* (atalho **Ctrl+D**). O filtro de modelo inclui CT-e (57).
Filtre por período, CFOP e texto (número, nome, CNPJ ou chave) e clique em **Consultar**. Selecione uma nota para ver os itens e tributos. **Excluir selecionado** remove a nota e as suas duplicatas (desfaça antes a contabilização, se houver). Se alguma duplicata da nota já foi baixada, o sistema recusa: estorne as baixas em *Lançamentos › Duplicatas* e exclua depois.

## Regras de contabilização

**Para que serve:** ensinar o sistema em quais contas lançar cada tipo de nota.
**Onde fica:** *Cadastros › Regras de contabilização*.

A tela tem três abas:
- **Regras das notas**, com as abas **Entradas** (notas de compra e outras entradas) e **Saídas** (vendas e serviços prestados). Cada uma mostra e grava só as regras do seu sentido; escolha a aba antes de criar a regra.
- **Provisões de impostos**: as contas das provisões de cada tributo (veja [Contabilização das provisões de impostos](#contabilizacao-das-provisoes-de-impostos)).
- **Duplicatas e baixas**: duplicata à vista, contas e históricos das baixas (veja [Duplicatas](#duplicatas-contas-a-pagar-e-a-receber)).

Cada aba pede a sua permissão: as regras, a de contabilizar; as provisões, a de apurar tributos; as duplicatas, a de manter duplicatas.

1. Na aba desejada, clique em **Nova** (a ficha da regra abre numa janela por cima das listas; **Abrir ficha** ou duplo clique numa regra abrem a que está selecionada). Informe o **CFOP** (completo, como 5102, só o prefixo, como 51, ou `NFSE` para notas de serviço; vazio vale para todos).
2. Informe a **conta de débito** e a **de crédito** e, se quiser, o **participante** e o histórico (aceita as variáveis dos históricos padrão).
3. **Salvar** (a janela fecha). A regra nova entra no fim da lista da aba. **Excluir**, **Subir** e **Descer** ficam na tela.

Vale a regra **mais específica**: a que tem participante vence a que não tem; depois, o CFOP completo vence o prefixo (e o prefixo mais longo vence o mais curto); por último, a regra geral. Se duas regras forem igualmente específicas, vale a que estiver **mais acima na lista**. Para mudar a ordem, selecione a regra e use **Subir** ou **Descer**; a ordem fica gravada. Não há campo de prioridade para digitar.

## Centros de custo

**Para que serve:** separar as compras e as vendas por área da empresa (filial, loja, obra, departamento) e levar essa separação para a contabilidade.
**Onde fica:** *Cadastros › Centros de custo* (o **mesmo cadastro** do Contábil: o centro incluído aqui aparece lá, e vice-versa).

1. Ligue **Usar centros de custo** no cadastro da empresa (*Cadastros › Empresas*, aba *Contabilidade*) e, em *Cadastros › Parâmetros*, grupo *Lançamento das notas*, **Informar centro de custo nos itens das notas**.
2. Cadastre os centros em *Cadastros › Centros de custo* (veja como em [Centros de custo do Contábil](03-contabil.md#centros-de-custo)).
3. Na nota, aba **Escrituração**, escolha o centro de custo da nota ou de cada item. O do item vale; sem ele, vale o da nota.
4. Ao contabilizar, as partidas saem separadas por centro de custo. No Contábil, o resultado aparece em *Relatórios › Gerenciais › Contabilização por centro de custo* e nos demais relatórios gerenciais.

**Relatório:** *Relatórios › Resumos › Notas por centro de custo* mostra, no período, as notas e o valor das entradas e das saídas de cada centro (as sem centro aparecem em *Sem centro de custo*) e quanto já foi contabilizado. Informe o período e **Gerar**; exporta em PDF, Excel e CSV.

## Contabilizar notas fiscais

**Onde fica:** *Integrações › Contabilizar notas fiscais* (só aparece com a contabilização **em lote**).

### On-line ou em lote
Em *Cadastros › Parâmetros*, grupo *Lançamento das notas*, escolha como as notas vão para o Contábil:
- **Em lote** (*Contabilizar as notas on-line* desmarcado, o padrão): as notas ficam pendentes e você contabiliza depois, por período, nesta tela.
- Se a empresa usa o tipo de contabilização **Em lote** do Contábil (veja [Contabilização em lote](03-contabil.md#contabilizacao-em-lote)), as notas gravadas ou importadas e as baixas de duplicatas viram pendências nessa tela de lote, em vez de lançamentos na hora.
- **On-line** (marcado): cada nota é contabilizada assim que é gravada (digitação) ou importada (XML ou chave de acesso), pelas regras de contabilização. O menu *Contabilizar notas fiscais* fica escondido. Nota sem regra ou com período contábil fechado continua pendente, e o motivo aparece na mensagem da gravação ou no resumo da importação; corrija a regra e grave a nota de novo, ou volte para **em lote** para contabilizar as pendentes.

1. Escolha o período e clique em **Pré-visualizar**: aparecem as notas pendentes, com o lançamento previsto.
2. Marque as notas e clique em **Contabilizar marcadas**. São gerados os lançamentos no Contábil (documento = número da nota).
3. Para desfazer, marque as notas em *Notas contabilizadas no período* e clique em **Desfazer contabilização das marcadas**: o lançamento é estornado e a nota volta a pendente.

Quando a nota tem duplicatas e a empresa informou a **conta de fornecedores** (entrada) ou de **clientes** (saída) em *Cadastros › Regras de contabilização* (aba *Duplicatas e baixas*), essa conta passa a ser a contrapartida da nota: o fornecedor é creditado na compra e o cliente debitado na venda, com o código do fornecedor/cliente na partida. Uma regra específica daquele participante, ou as contas digitadas na própria nota, têm preferência.

**Erro comum:** nota sem regra aplicável ou período contábil fechado ficam sem contabilizar, com a mensagem na coluna *Situação*.

## Fechamento do período fiscal

**Para que serve:** travar o mês depois de escriturar as notas e apurar os impostos, para que nada mude depois de entregue a apuração (empresas do **regime normal**: Lucro Presumido ou Real).
**Onde fica:** *Apuração › Fechamento do período fiscal*.

1. Escolha o **Ano**. A lista mostra os 12 meses: situação (aberto/fechado), quantas notas há, quantas ainda **não foram contabilizadas** e quando e por quem o mês foi fechado.
2. Selecione o mês e clique em **Fechar mês**. Se houver notas não contabilizadas, o aviso aparece antes de confirmar.
3. Com o mês fechado, as notas com data de entrada/saída no mês **não podem ser incluídas, alteradas nem excluídas** (digitação, importação de XML ou pela chave). A contabilização das notas continua permitida.
4. Para corrigir algo, selecione o mês e clique em **Reabrir mês**. Mês com a DCTFWeb já transmitida não reabre: desfaça antes o registro da transmissão em [DCTFWeb (MIT)](#dctfweb-mit).
5. Com o mês fechado, prepare a DCTFWeb do mês em *Apuração › Obrigações › DCTFWeb (MIT)*.

**Atenção:** empresa do Simples Nacional não usa este fechamento (o período é encerrado pela apuração do DAS). O regime vem do cadastro da empresa.

## Duplicatas (contas a pagar e a receber)

**Para que serve:** controlar o que a empresa tem a pagar aos fornecedores e a receber dos clientes, parcela por parcela, e gerar a contabilização do pagamento e do recebimento. As duplicatas nascem das notas (importadas ou digitadas) e também podem ser lançadas à mão.
**Onde fica:** *Lançamentos › Duplicatas (contas a pagar e a receber)*.

**Antes de usar:** em *Cadastros › Regras de contabilização*, aba *Duplicatas e baixas*, informe os códigos das contas contábeis analíticas: fornecedores, clientes, juros pagos, multa paga, descontos obtidos, juros recebidos, multa recebida e descontos concedidos. As de juros, multa e desconto só são exigidas quando você usar esses valores numa baixa. Também ali você escolhe se a nota sem cobrança gera duplicata à vista e o texto do histórico dos lançamentos (aceita {DOCUMENTO}, {PARTICIPANTE}, {DATA} e {VALOR}).

### Consultar
1. Escolha *A pagar e a receber*, *A pagar* ou *A receber*, o período de **vencimento**, o fornecedor/cliente (nome, código ou CNPJ) e a **situação**. Marque **Somente vencidas** para ver só as que passaram do vencimento.
2. Clique em **Consultar**. Cada linha mostra vencimento, documento (número da nota/parcela), valor, saldo, situação e dias de atraso. O resumo acima da lista traz o saldo em aberto a pagar e a receber.

Situações: **Aberta** (sem baixa), **Parcial** (parte já paga ou recebida), **Liquidada** (saldo zero) e **Cancelada**.

### Incluir e alterar
1. Clique em **Nova duplicata**: a ficha abre numa janela por cima da lista. Escolha *A pagar* ou *A receber*, informe o fornecedor/cliente (como cadastrado), o número do documento, a parcela, a emissão, o vencimento e o valor. **Gravar** fecha a janela.
2. Para alterar, selecione a duplicata e clique em **Abrir ficha** (ou dê duplo clique na linha), mude os campos e **Gravar**. O valor só muda enquanto não há baixas. O vencimento não pode ser anterior à emissão.
3. **Cancelar duplicata** e **Excluir** ficam ao lado de *Nova duplicata*, na tela. O cancelamento vale para a que está aberta e sem baixas. **Excluir** vale para as lançadas à mão e sem baixas; as que vieram de nota são canceladas ou somem junto com a nota.

### Baixar (pagar ou receber)
1. Selecione a duplicata. Na área *Baixa*, confira a **data**, o **valor principal** (já vem com o saldo; digite menos para baixa parcial), os **juros**, a **multa** e o **desconto** (se houver) e o código da **conta do banco ou caixa**.
2. Clique em **Baixar selecionada**. O valor pago é principal + juros + multa - desconto.
3. O sistema gera o lançamento no Contábil. **Pagamento:** débito no fornecedor (principal), nos juros e na multa; crédito no desconto obtido e no banco. **Recebimento:** débito no banco e no desconto concedido; crédito no cliente (principal), nos juros e na multa recebidos. O documento do lançamento é o número da nota/parcela (ex.: 1234/001).
4. **Baixar várias:** marque as duplicatas na primeira coluna, informe a data e a conta do banco e clique em **Baixar marcadas**. Cada uma é baixada pelo saldo total; as que tiverem problema ficam indicadas e as outras são baixadas.

### Estornar uma baixa
Selecione a duplicata, escolha a baixa na lista *Baixas da duplicata selecionada* e clique em **Estornar baixa selecionada**. O lançamento contábil é estornado e o valor volta ao saldo. Se o período contábil estiver fechado, o estorno é recusado: reabra o período e tente de novo.

**Dicas e erros comuns:**
- "Informe a conta de fornecedores em Cadastros › Regras de contabilização, aba Duplicatas e baixas": falta configurar a conta antes da primeira baixa.
- O valor da baixa não pode passar do saldo; duplicata liquidada ou cancelada não recebe baixa.
- Perfis: quem tem a função de *manter duplicatas* inclui e altera; só quem tem a de *baixar duplicatas* baixa, estorna e concilia (Administrador e Contador; o Assistente só mantém).

## Conciliação de duplicatas

**Para que serve:** baixar duplicatas a partir do extrato bancário, sem digitar, e deixar a partida do banco marcada como conciliada.
**Onde fica:** *Lançamentos › Conciliação de duplicatas*.

**Antes:** importe o extrato do banco (OFX ou PDF) no Contábil, em *Integrações › Importar › Extratos bancários (OFX e PDF)*.

1. Informe o código da **conta contábil do banco** do extrato, o período e a **tolerância em dias** (quantos dias de diferença entre o vencimento e a data do banco ainda contam como a mesma operação).
2. Clique em **Sugerir**. A lista mostra pares *duplicata em aberto x movimento do extrato* que têm o mesmo valor do saldo, data próxima do vencimento e/ou o número do documento no histórico, com o motivo da sugestão. Saída do banco só combina com duplicata a pagar; entrada, com a receber.
3. Desmarque os pares que não quiser e clique em **Baixar marcadas**. A duplicata é baixada pelo saldo total, na data do extrato.

Como fica a contabilidade: se o extrato foi importado contra a própria conta de fornecedores/clientes, a baixa só registra o vínculo com o movimento (o lançamento do extrato já baixa a parcela). Se foi importado contra outra conta (por exemplo, "pagamentos a classificar"), a baixa lança contra essa conta e as duas partidas são conciliadas em *Lançamentos › Conciliações*. Estornar a baixa desfaz a conciliação e o movimento volta a ser sugerido.

## Relatórios de duplicatas

**Onde ficam:** *Relatórios › Duplicatas*. Todos aparecem na tela e exportam em PDF, Excel e CSV.

- **Duplicatas a pagar** e **Duplicatas a receber**: uma linha por parcela, com vencimento, documento, valor, valor baixado, saldo, situação e dias de atraso na **data-base**. Filtre por vencimento, fornecedor/cliente e situação (*Em aberto* ou *Todas*) e clique em **Gerar**.
- **Posição por vencimento**: saldo em aberto por fornecedor/cliente nas faixas *a vencer*, *vencidas de 1 a 30 dias*, *de 31 a 60*, *de 61 a 90* e *há mais de 90 dias*, na data-base escolhida. Escolha *Contas a pagar* ou *Contas a receber*.
- **Relatório de recebimentos** e **Relatório de pagamentos**: cada baixa feita no período (data, cliente/fornecedor, documento e parcela, principal, juros e multa, desconto, valor líquido e conta usada), com o total no fim. Informe o período das baixas e clique em **Gerar**.

## Apuração do Simples Nacional (DAS)

**Onde fica:** *Apuração › Simples Nacional (DAS)*.

1. Escolha a **competência**.
2. Informe a receita do mês por atividade, de um destes jeitos:
   - **Sugerir pelas notas**: preenche pelas notas de saída lançadas no sistema.
   - **Incluir receita manualmente** (quadro acima da lista): escolha a atividade (anexo), digite o **Valor**, marque ICMS-ST, monofásico, ISS retido ou exportação quando houver e clique em **Incluir receita**. Repita para cada atividade. Serve para empresas sem notas no sistema ou para completar o que veio das notas.
   - Para corrigir, clique no valor na lista e digite o novo; **Remover selecionada** tira uma linha e **Limpar receitas** apaga todas.
3. O **RBT12** (receita dos 12 meses anteriores) vem do histórico; informe também a *folha 12 meses* para o Fator R (anexo III ou V). Se a empresa é nova, o sistema proporcionaliza.
4. **Calcular**: mostra alíquota efetiva, valor do DAS e a repartição por tributo, com memória de cálculo.
5. **Salvar apuração**. **Contabilizar provisão** lança a despesa e o passivo (precisa das [contas das provisões](#contabilizacao-das-provisoes-de-impostos), em Cadastros › Regras de contabilização, aba Provisões de impostos); **Desfazer provisão** estorna.

**PGDAS-D:** o Vorticis calcula o DAS, mas a transmissão da declaração continua no portal do Simples Nacional (PGDAS-D). A integração direta exige o contrato do *Integra Contador* do SERPRO (chaves de acesso pagas) e procuração eletrônica; ela ainda não está disponível.

**Dica:** preencha antes o [Histórico de receita e folha](#historico-de-receita-e-folha) com os meses anteriores ao uso do sistema. Confira as tabelas de alíquotas com a legislação vigente.

## Lucro Presumido (IRPJ e CSLL)

**Onde fica:** *Apuração › Lucro Presumido (IRPJ e CSLL)*. Apuração **trimestral**.

1. Escolha o ano e o trimestre. **Sugerir pelas notas** preenche a receita por atividade (edite se precisar).
2. Informe outras receitas, IRRF a compensar, CSLL retida e outras deduções.
3. **Calcular**: presunção de 8%, 16% ou 32% (IRPJ) e 12% ou 32% (CSLL) por atividade; IRPJ de 15% mais adicional de 10% sobre o que passar de R$ 20 mil por mês; CSLL de 9%.
4. **Salvar apuração** e, se desejar, **Contabilizar provisão** (ou **Desfazer provisão**).

## Lucro Real (IRPJ, CSLL e LALUR)

**Onde fica:** *Apuração › Lucro Real (IRPJ, CSLL e LALUR)*. Apuração **trimestral**.

1. Escolha o trimestre e clique em **Preparar (contabilidade e LALUR)**: traz o lucro contábil das contas de resultado e os lançamentos do LALUR do ano.
2. Em *LALUR / LACS · Parte A*, inclua as **adições e exclusões** (descrição, valor, tributo, tipo e **código da ECF**) com **Incluir no LALUR/LACS**; **Excluir selecionado** remove.
3. Informe prejuízo fiscal e base negativa a compensar (limite de 30% do lucro), IRRF, CSLL retida e deduções. Os saldos iniciais da *Parte B* (prejuízos fiscais e base negativa) são gravados em **Gravar saldos iniciais**.
4. **Calcular**, **Salvar apuração** e, se desejar, **Contabilizar provisão**.

A *Parte B* mostra o razão dos prejuízos e da base negativa. O **código da ECF** de cada lançamento é obrigatório para gerar a ECF.

## PIS e COFINS

**Onde fica:** *Apuração › PIS e COFINS*.

1. Escolha a competência. **Sugerir pelas notas** preenche a receita da base e a base dos créditos (não cumulativo).
2. Marque **Excluir o ICMS da base (Tema 69/STF)**, se aplicável.
3. Informe PIS e COFINS retidos, **Calcular** e **Salvar apuração**. Cumulativo: 0,65% e 3%. Não cumulativo: 1,65% e 7,6%, com créditos sobre as entradas.
4. **Contabilizar provisão** / **Desfazer provisão** como nas demais apurações.

## IBS e CBS (reforma tributária)

**Para que serve:** apurar o **IBS** (estados e municípios), a **CBS** (União) e o **Imposto Seletivo (IS)** destacados nas notas fiscais, levar o saldo credor de um mês para o outro e lançar a compensação na contabilidade.
**Onde fica:** *Apuração › IBS e CBS (reforma tributária)*.

A partir de 2026 a NF-e traz, em cada item, o grupo **IBS/CBS**: o CST e a classificação tributária, a base de cálculo, o IBS da UF, o IBS do município e a CBS (e, de 2027 em diante, o Imposto Seletivo dos produtos prejudiciais à saúde ou ao meio ambiente). Ao importar o XML, ou ao digitar a nota, o Fiscal guarda esses valores junto com o item.

1. Abra *Apuração › IBS e CBS (reforma tributária)*.
2. Escolha a **Competência** e clique em **Apurar**. A tabela mostra os **débitos** das saídas, os **créditos** das entradas (só dos itens que creditam), o **saldo credor do mês anterior**, o **saldo a recolher** e o **saldo credor para o mês seguinte** de CBS e de IBS (UF, município e total), e o **Imposto Seletivo** das saídas (que não gera crédito).
3. Os avisos apontam as notas sem o grupo IBS/CBS (as de entrada não geram crédito), os itens marcados para não creditar e a CBS destacada diferente da esperada pela alíquota do ano.
4. De **2027** em diante, clique em **Encerrar** para fechar o mês: os saldos ficam gravados e o saldo credor passa para o mês seguinte. Com **Contabilizar a compensação** marcado, o sistema lança na contabilidade a parte dos débitos quitada com os créditos (débito em IBS/CBS a recolher e crédito em IBS/CBS a recuperar). Os meses são encerrados em ordem.
5. **Reabrir** desfaz o encerramento do último mês encerrado. Se a compensação foi contabilizada, estorne o lançamento na contabilidade.

**Contas contábeis:** em *Cadastros › Regras de contabilização*, aba **Provisões de impostos**, cadastre os tributos **IBS** e **CBS** (a conta de despesa é a do **crédito a recuperar**, no ativo, e a de passivo, a do tributo **a recolher**) e o **IS** (despesa e a recolher). De 2027 em diante, ao contabilizar as notas, o IBS e a CBS entram em partidas próprias: na venda, o cliente é debitado pelo valor da nota mais o IBS/CBS (cobrados "por fora") e o IBS/CBS vão para a conta a recolher; na compra, o crédito dos itens que creditam vai para a conta a recuperar e o fornecedor é creditado pelo total; o Imposto Seletivo da venda vai para a despesa e para a conta a recolher.

**Transição:** em **2026** (ano de teste) as alíquotas são de 0,9% de CBS e 0,1% de IBS, os valores são informativos e compensáveis com o PIS e a COFINS: a apuração não é encerrada nem contabilizada. Em **2027** e **2028** a CBS é a alíquota de referência menos 0,1 ponto (o Tribunal de Contas da União calculou a referência em 11,3%, o que dá 11,2%, e o Senado a fixa por resolução), o IBS é de 0,05% para a UF e 0,05% para o município, o **PIS e a COFINS deixam de existir**, o **IPI fica zerado** (salvo produtos incentivados da Zona Franca de Manaus) e começa o **Imposto Seletivo**. De 2029 a 2032 o ICMS e o ISS diminuem enquanto o IBS aumenta, até 2033. As alíquotas e as regras de cada ano ficam nos **parâmetros legais** do programa (chave REFORMA_IBS_CBS) e devem ser conferidas com a resolução do Senado e com os atos do Comitê Gestor. Esta apuração é um apoio: a apuração oficial é feita pelos sistemas da Receita Federal e do Comitê Gestor do IBS.

## Simples Nacional × IBS/CBS (reforma)

**Para que serve:** mostrar, ano a ano, quanto a empresa do Simples Nacional paga hoje (o DAS) e quanto pagaria com a reforma tributária, de **2026** (ano de teste) a **2033**, e comparar as duas escolhas que a reforma dá ao Simples.
**Onde fica:** *Apuração › Simples Nacional × IBS/CBS (reforma)*.

**As duas escolhas:**
- **A – tudo dentro do DAS:** a empresa continua pagando tudo no DAS. As partes de PIS e COFINS viram CBS e as de ICMS e ISS viram IBS, na mesma proporção, então o valor total tende a ficar igual. Atenção: o cliente só aproveita como crédito o IBS/CBS que está dentro do DAS, que é pouco.
- **B – IBS/CBS fora do DAS (regime regular):** a empresa continua no Simples para os outros tributos, mas paga o IBS e a CBS à parte, com a alíquota cheia sobre a venda, **descontando o crédito das compras**. O DAS diminui as partes de PIS/COFINS e de ICMS/ISS que já acabaram, e o cliente aproveita o crédito integral. Vale a pena, em geral, para quem vende para empresas e tem muitas compras com crédito.

**Como usar:**
1. Escolha a **Atividade** da empresa (ela define o anexo do Simples; nos serviços intelectuais o Fator R decide entre os anexos III e V).
2. Clique em **Trazer do histórico** para preencher a **Receita dos 12 meses** e a **Folha** (de *Apuração › Histórico de receita e folha*) e as **Compras com crédito** (das notas de entrada autorizadas). Se preferir, digite os valores.
3. Se a atividade tem alíquota reduzida na reforma, informe a **Redução de alíquota (%)**; ela vale só para a escolha B.
4. Clique em **Simular**. A primeira tabela resume a situação de hoje (anexo, faixa, alíquota efetiva, DAS anual e quanto dele é PIS, COFINS, ICMS e ISS). A segunda mostra, para cada ano, o imposto atual, o resultado de A e de B, a **diferença em reais e em %** sobre o imposto atual, o crédito que o cliente aproveita e qual escolha dá o **menor imposto**. A terceira mostra o cronograma usado.
5. Exporte em PDF, Excel ou CSV.

**Como ler:** em **2026** nada muda no valor pago: o IBS e a CBS são só informativos (0,9% de CBS e 0,1% de IBS), compensáveis com o PIS e a COFINS. Em **2027** o PIS e a COFINS acabam e a CBS entra. De **2029 a 2032** o ICMS e o ISS diminuem a cada ano enquanto o IBS aumenta, e em **2033** a reforma está completa. Um resultado de B **negativo** nas colunas de CBS ou IBS é saldo credor (as compras superam as vendas).

**Cuidados:** é uma estimativa. Usa a receita e as compras dos últimos 12 meses como se fossem iguais nos anos seguintes e as alíquotas de referência dos parâmetros legais do programa (CBS 8,8% e IBS 17,7%), que só valem depois de fixadas por lei e pelo Senado. As tabelas do Simples para a reforma ainda serão definidas pelo Comitê Gestor do Simples Nacional; por isso a escolha A mantém a carga de hoje. Consulte o contador responsável antes de decidir.

## Histórico de receita e folha

**Onde fica:** *Apuração › Histórico de receita e folha*.
Escolha o ano, **Carregar**, edite a receita bruta e a folha de cada mês anterior ao uso do sistema e **Salvar alterações**. Serve de base para o RBT12 do Simples e o Fator R.

## Planejamento tributário

**Para que serve:** comparar, com os mesmos números, quanto a empresa pagaria de tributos no ano no **Simples Nacional**, no **Lucro Presumido** e no **Lucro Real**, e ver qual regime tem a menor carga.
**Onde fica:** *Apuração › Planejamento tributário* e *Relatórios › Planejamento tributário* (a mesma tela: monta, compara, grava e exporta).

Ao abrir, a tela já traz a última simulação gravada (comparada na hora). Sem simulação, traz a receita e a folha do ano anterior do *Histórico de receita e folha*; sem histórico, mostra o que é preciso informar. **Comparar** sem nenhuma receita digitada também mostra essa orientação.

1. Escolha o **Ano** e dê uma **Descrição** à simulação (ex.: *Cenário base*, *Com nova filial*).
2. Na grade, digite a **receita bruta prevista para o ano** em cada tipo de receita (comércio, indústria, serviços de cada anexo, transporte de cargas). A coluna **Paga** mostra se o tipo paga ICMS ou ISS.
   - **Trazer do histórico** busca a receita e a folha do ano anterior em *Apuração › Histórico de receita e folha*; depois, separe a receita por tipo, se houver mais de um.
3. Informe a **Folha do ano** (salários e pró-labore), os **Encargos patronais sobre a folha (%)** (vem 20%, a contribuição patronal; some o RAT e os terceiros da empresa), os **Custos e despesas** (tudo o que reduz o lucro, sem a folha e sem os tributos), as **Compras com crédito de PIS/COFINS** e os percentuais de **ICMS** efetivo sobre as vendas e de **ISS** sobre os serviços.
4. Clique em **Comparar**. A tabela mostra, para cada regime, IRPJ, CSLL, PIS, COFINS, INSS patronal, IPI, ICMS e ISS do ano, o total e o percentual sobre a receita. Acima dela aparece o regime de **menor carga** e a economia em relação ao segundo melhor.
5. **Gravar simulação** guarda os dados (a de mesma descrição e ano é substituída). Em **Simulações gravadas** você abre uma simulação de novo; **Excluir simulação** apaga a escolhida.

Como cada regime é calculado:
- **Simples Nacional:** DAS de um mês médio (receita ÷ 12, com a receita do ano como RBT12 e a folha para o Fator R) × 12. No **Anexo IV** o INSS patronal é pago fora do DAS; acima do sublimite, o ICMS e o ISS também. Receita acima de R$ 4,8 milhões: *não permitido*.
- **Lucro Presumido:** IRPJ e CSLL trimestrais pelos percentuais de presunção de cada atividade; PIS 0,65% e COFINS 3%. Receita acima de R$ 78 milhões: *não permitido* (a empresa é obrigada ao Lucro Real).
- **Lucro Real:** lucro = receita − custos e despesas − folha − encargos − PIS/COFINS − ICMS − ISS; IRPJ e CSLL trimestrais; PIS 1,65% e COFINS 7,6% com crédito sobre as compras. Sem lucro, não há IRPJ nem CSLL.

As tabelas, alíquotas e limites vêm dos parâmetros legais vigentes no ano. É uma **estimativa** para apoiar a decisão: não considera retenções, incentivos, substituição tributária, produtos monofásicos nem as adições e exclusões do LALUR. Montar a simulação pede a permissão de apurar tributos ou de relatórios fiscais; gravar e excluir, a de apurar.

## Contabilização das provisões de impostos

**Onde fica:** *Cadastros › Regras de contabilização*, aba *Provisões de impostos*.
Para cada tributo informe a **conta de débito (despesa)** e a **conta de crédito (imposto a recolher)** e clique em **Salvar**; deixe as duas em branco para remover. São usadas por **Contabilizar provisão** nas apurações.

Só ficam liberados os tributos que a empresa tem, conforme o regime em *Cadastros › Empresas* (Dados cadastrais):
- **Simples Nacional e MEI:** o **DAS** (e o IBS e a CBS, para quem optar por recolhê-los fora do DAS).
- **Lucro Presumido, Real ou Arbitrado:** **IRPJ, CSLL, PIS e COFINS**; mais **ICMS** (empresa com inscrição estadual, comércio ou indústria), **IPI** (indústria) e **ISS** (atividade de serviços), conforme a inscrição e a atividade (CNAE).
- **Reforma tributária (todos os regimes acima e o Simples):** **IBS** e **CBS** (a conta de débito é a do **crédito a recuperar**, no ativo, e a de crédito, a do tributo **a recolher**) e, fora do Simples, **IS** (Imposto Seletivo: despesa e a recolher). De 2027 em diante elas são usadas na contabilização das notas e na compensação do encerramento da [apuração do IBS e da CBS](#ibs-e-cbs-reforma-tributaria).
Um tributo bloqueado que já tenha contas gravadas continua liberado, para você poder corrigir ou limpar. A tela *Contas dos tributos* deixou de existir: as mesmas contas agora são mantidas aqui.

## Retenções e guias

### Retenções (IRRF, CSRF, INSS, ISS)
**Onde fica:** *Apuração › Obrigações › Retenções (IRRF, CSRF, INSS, ISS)*.
1. Escolha o período e **Consultar**.
2. **Gerar das notas** cria as retenções (sofridas e efetuadas) a partir dos documentos.
3. Para lançar à mão, clique em **Nova retenção (manual)**: a ficha abre numa janela com o tributo, o sentido, a base de cálculo, o valor retido e o código da receita; **Incluir** grava e fecha a janela.
4. **Excluir selecionada** apaga uma retenção.

### Guias de recolhimento
**Onde fica:** *Apuração › Obrigações › Guias de recolhimento*.
1. Escolha as competências e **Consultar**.
2. **Gerar guias** cria DAS, DARF (com código de receita), DCTFWeb/GPS do INSS (informativo) e ISS a partir das apurações e retenções.
3. Selecione a guia e **Marcar como paga** (ou **Reabrir**). Guias manuais: **Nova guia (manual)** abre a ficha numa janela (**Incluir** grava e fecha) e **Excluir (manual)** apaga a selecionada.

Guias com vencimento próximo aparecem no calendário de obrigações e na tela inicial.

### DCTFWeb (MIT)

**Para que serve:** preparar a DCTFWeb do mês com os débitos dos tributos federais apurados (IRPJ, CSLL, PIS, COFINS e DARF lançados à mão), que são informados pelo **MIT** (Módulo de Inclusão de Tributos), e guardar o recibo da transmissão. Só depois da apuração encerrada.
**Onde fica:** *Apuração › Obrigações › DCTFWeb (MIT)* (empresas do Lucro Presumido ou Real).

1. Faça as apurações do mês e gere as guias em *Apuração › Obrigações › Guias de recolhimento*.
2. Encerre o mês em *Apuração › Fechamento do período fiscal*. Enquanto o mês estiver aberto, a declaração não pode ser preparada (a lista mostra *Apuração em aberto: feche o mês antes*).
3. Escolha o **Ano**, clique no mês na lista e em **Conferir débitos** para ver o que vai para a declaração, sem gravar.
4. **Preparar declaração** grava os débitos do mês: um por tributo e código de receita, com o total. Mês sem débitos fica como declaração *sem movimento*. As retenções (IRRF, CSRF, INSS retido) e o INSS da folha aparecem só para conferência, porque chegam à DCTFWeb pela EFD-Reinf e pelo eSocial.
5. Exporte a tabela (PDF, Excel ou CSV), lance os débitos no MIT da DCTFWeb no e-CAC e transmita com o certificado digital.
6. De volta aqui, informe o **Recibo da transmissão** e a data em **Transmitida em** e clique em **Registrar transmissão**.

Mês com a DCTFWeb transmitida não pode ser reaberto no fechamento do período fiscal. Para retificar: **Desfazer transmissão**, reabra o mês, corrija, prepare de novo, transmita a retificadora e registre o novo recibo. A transmissão é feita no portal da Receita (e-CAC): o envio direto pelo sistema depende da contratação do serviço Integra Contador (SERPRO).

## Relatórios fiscais

Todos têm tela, filtro de período e exportação em PDF, Excel e CSV.

- **Livro de Registro de Entradas** e **de Saídas** (*Relatórios › Livros*): uma linha por nota e CFOP; notas canceladas aparecem sem valores. Informe o período e **Gerar**.
- **Resumos** (*Relatórios › Resumos*):
  - **Resumo de vendas** e **Resumo de compras**: soma das notas autorizadas de saída (vendas) ou de entrada (compras) do período, com quantidade de notas, valor total, ICMS, IPI, PIS+COFINS e ISS. Escolha como agrupar: *Por nota*, *Por cliente/fornecedor* ou *Por mês*. Notas canceladas ou denegadas ficam de fora.
  - **Resumo por CFOP**: totais por CFOP e sentido.
- **Planejamento tributário**: informe os dados do ano e compare Simples × Presumido × Real, com exportação; veja [Planejamento tributário](#planejamento-tributario).
- **Produtos cadastrados** e **Itens das notas fiscais**: listagens com filtros.
- **Duplicatas a pagar**, **Duplicatas a receber** e **Posição por vencimento**: veja [Relatórios de duplicatas](#relatorios-de-duplicatas).

## Livros de apuração do ICMS e do IPI

**Para que serve:** conferir e imprimir a apuração mensal do ICMS e do IPI, com os mesmos valores que vão para a EFD ICMS/IPI.
**Onde fica:** *Relatórios › Livros e apuração › Livro de Apuração do ICMS* e *Livro de Apuração do IPI*.

1. Escolha o **mês** e o **ano**. No livro do ICMS, informe também o **saldo credor anterior**, se houver.
2. Clique em **Gerar**: aparecem os débitos pelas saídas e os créditos pelas entradas, separados por CFOP, e o resultado do mês (imposto a recolher ou saldo credor a transportar).
3. Use **Exportar** para PDF, Excel ou CSV. O PDF sai com o cabeçalho da empresa, como os demais livros.

Notas canceladas, denegadas e NFS-e não entram. No livro do IPI só entram os itens com situação tributária do IPI preenchida. Os ajustes lançados em *Lançamentos › Ajustes* (outros débitos, outros créditos, estornos, deduções e débitos especiais) aparecem nas linhas próprias e entram na apuração como na EFD.

## Relatórios de apuração do SPED Fiscal

**Onde ficam:** *Relatórios › Livros e apuração*. Todos escolhem o mês e o ano, têm o botão **Gerar** e exportam em PDF, Excel ou CSV (**Exportar**). Usam os mesmos números da EFD ICMS/IPI, mas não geram nem registram o arquivo.
- **Resumo da apuração do ICMS:** os campos do registro E110 (débitos, ajustes, estornos, créditos, saldo credor anterior, deduções, ICMS a recolher e saldo credor a transportar), já com os ajustes. Informe o saldo credor anterior, se houver.
- **Relação de ajustes da apuração:** os ajustes lançados entre dois meses, com filtro opcional por código, o tipo, o documento ou a observação, e o total por código.
- **Apuração do IPI:** as operações por CFOP e CST (E510), o débito, o crédito, os outros débitos e créditos, o saldo devedor ou credor (E520) e os ajustes (E530).
- **Apuração do ICMS-ST:** por UF, o valor retido nas saídas, os ajustes (E220), as deduções, o ICMS-ST a recolher e o saldo credor.
- **Conferência da EFD ICMS/IPI:** quatro partes: (1) quantidade de registros por bloco; (2) C190/D190 por CST, CFOP e alíquota; (3) confronto das notas com o E110 (débitos, créditos, cabeçalho × itens e saldo apurado), marcando *Confere* ou *DIVERGE*; (4) as pendências e avisos da validação. Use antes de gerar o arquivo.

## SPED EFD ICMS/IPI

**Para que serve:** gerar o arquivo da EFD ICMS/IPI (SPED Fiscal) do mês.
**Onde fica:** *Apuração › Obrigações › SPED › EFD ICMS/IPI (SPED Fiscal)*.

**Antes de gerar (configuração):** deixe o padrão da empresa em *Cadastros › Parâmetros*, grupo *EFD ICMS/IPI* (perfil, atividade, códigos de receita e dia de vencimento do ICMS): o assistente já abre preenchido. Lance também, se houver, os [outros lançamentos do SPED Fiscal](#outros-lancamentos-do-sped-fiscal).

1. **Passo 1**: empresa, mês/ano e, se for o caso, **escrituração retificadora**. Informe as opções: **perfil** (IND_PERFIL), **atividade**, **código de receita do ICMS** (ex.: 046-2 para o estado), do ICMS-ST (se houver), **dia de vencimento** e **saldo credor do mês anterior**. **Salvar estas opções como padrão da empresa** evita redigitar a cada mês.
2. **Passo 2**: pré-requisitos (cadastros incompletos, produtos sem NCM etc.).
3. **Passo 3**: validação prévia com o resumo da apuração (E110). Corrija os bloqueios.
4. **Passo 4**: assinatura. O padrão é **não assinar** (a assinatura oficial é feita no PVA, na transmissão).
5. **Passo 5**: **Gerar arquivo**, escolha a pasta e **Salvar arquivo** (`EFD_ICMS_IPI_<CNPJ>_<AAAAMM>.txt`).

No PVA: *Escrituração › Importar* e *Validar*; confira o resumo e então assine e transmita.
O arquivo traz as notas (bloco C), as contas de consumo (C500 e D500), os ajustes da apuração (E111, E112, E113, E220 e E530) e o inventário cadastrado (bloco H) da competência.
**Limitações:** sem DIFAL, CIAP (bloco G) e produção (bloco K). Os conhecimentos de transporte (CT-e) saem no D100/D190.

## Outros lançamentos do SPED Fiscal

**Para que serve:** lançar o que a EFD ICMS/IPI pede além das notas de mercadoria.
**Onde fica:** *Lançamentos* (contas de consumo, inventário e ajustes).

### Contas de energia, gás, água e comunicação
**Onde fica:** *Lançamentos › Contas de energia, gás, água e comunicação*.
Para as contas recebidas de energia elétrica (modelo 06), gás canalizado (28), água (29) e de comunicação/telecomunicação (21 e 22), que não vêm em XML de NF-e:
1. Cadastre a concessionária ou operadora em *Cadastros › Fornecedores*.
2. Clique em **Nova** (a ficha abre numa janela por cima da lista das contas; **Abrir ficha** ou duplo clique abrem a selecionada), escolha o **Modelo**, o **Fornecedor** e informe número, série, emissão, entrada e o **valor da nota**. O CFOP vem sugerido (energia 1253, comunicação 1303, água e gás 1556; altere se a atividade for outra).
3. Para aproveitar o crédito, informe o **CST** (ex.: 000), a **base** e a **alíquota** do ICMS: o valor do ICMS é calculado. Informe PIS e COFINS, se destacados. **Gravar** fecha a janela.
A nota sai no C500/C590 (energia, gás, água) ou no D500/D590 (comunicação) da EFD do mês da entrada, e o ICMS entra nos créditos da apuração (E110).

### Ajustes de entrada e de saída
**Onde fica:** *Lançamentos › Ajustes › Ajustes de entrada* e *Ajustes de saída*.
Os ajustes corrigem a apuração do mês. **Ajustes de entrada** aumentam o crédito (outros créditos e estornos de débito); **Ajustes de saída** aumentam o débito (outros débitos, estornos de crédito, deduções e débitos especiais). No IPI, entrada é crédito e saída é débito.
1. Escolha a **Competência** (a lista mostra os ajustes dela). Clique em **Novo**: a ficha do ajuste abre numa janela por cima da lista.
2. Escolha o **Imposto**: ICMS, ICMS-ST ou IPI.
3. Informe o **código do ajuste** (ou escolha um já cadastrado em *Código cadastrado*). ICMS e ICMS-ST usam a tabela 5.1.1 da sua UF, com 8 posições: UF + 0 (ICMS próprio) ou 1 (ICMS-ST) + tipo + 4 dígitos, como SP020099. O tipo (4ª posição) é mostrado ao lado: 0 outros débitos, 1 estorno de créditos, 2 outros créditos, 3 estorno de débitos, 4 deduções, 5 débitos especiais. O sistema não deixa lançar na tela errada: crédito vai em entrada, débito em saída. O código do IPI tem 3 posições.
4. Informe a **descrição** e o **valor**. A **observação** vai como informação complementar do ajuste.
5. Se o ajuste se refere a uma nota, abra *Documento fiscal relacionado* e informe o cliente ou fornecedor, o modelo, a série, o número e a data (só para ICMS).
6. **Gravar** (a janela fecha). Na tela, **Novo** abre a ficha em branco, **Abrir ficha** (ou duplo clique) altera o ajuste selecionado e **Excluir** apaga o ajuste selecionado.
Os ajustes entram no [Livro de Apuração do ICMS e do IPI](#livros-de-apuracao-do-icms-e-do-ipi) e na EFD ICMS/IPI: ICMS próprio em E111 (com E112 para a observação e E113 para o documento relacionado), ICMS-ST em E220 e IPI em E530. As deduções só abatem até o saldo devedor do mês.

### Códigos de ajuste
**Onde fica:** *Lançamentos › Ajustes › Códigos de ajuste*.
Cadastro simples dos códigos que a empresa usa. A tela mostra a lista dos códigos; **Novo**, **Abrir ficha** ou duplo clique abrem a ficha numa janela. Escolha o **Imposto**, informe o **código** e a **descrição** e clique em **Gravar** (a janela fecha). Os códigos cadastrados aparecem para escolha nas telas de ajustes de entrada e de saída.

### Inventário de estoque (bloco H)
**Onde fica:** *Lançamentos › Inventário de estoque (bloco H)*.
1. Clique em **Novo**, informe a **data do inventário** (ex.: 31/12) e a **conta contábil do estoque**. O campo **Informar na EFD de** vem sugerido: o inventário de 31/12 vai na EFD de **fevereiro** do ano seguinte.
2. Escolha cada **produto** (do cadastro de produtos), clique em **Adicionar item** e informe na grade a **quantidade** e o **valor unitário** (o valor do item é calculado).
3. **Gravar inventário**. Na EFD da competência escolhida, o bloco H sai com o H005 (data e total) e um H010 por produto; o cadastro do produto entra no 0200.

## SPED EFD-Contribuições

**Onde fica:** *Apuração › Obrigações › SPED › EFD-Contribuições (PIS/COFINS)*. Mesmo assistente de cinco passos.

1. No passo 1 informe a **natureza da pessoa jurídica**, a **atividade preponderante** e, para retificadora, o recibo da escrituração anterior. Salve as opções como padrão.
2. Siga os passos 2 a 5 como na EFD ICMS/IPI (arquivo `EFD_CONTRIBUICOES_<CNPJ>_<AAAAMM>.txt`).

Antes, faça a [apuração de PIS e COFINS](#pis-e-cofins) do mês. Ambas as telas têm a aba de **arquivos gerados**, com data, período e hash de cada geração.

## Contabilização

A integração das notas e das apurações com o Contábil é descrita em [Regras de contabilização](#regras-de-contabilizacao), [Contabilizar notas fiscais](#contabilizar-notas-fiscais) e [Contabilização das provisões de impostos](#contabilizacao-das-provisoes-de-impostos).
