# Licenças (guia do fornecedor)

O Vorticis é distribuído com todos os módulos, mas **cada módulo só entra com uma licença válida** do cliente. A licença é um arquivo de
texto (`licenca.vlic`) com o cliente, os módulos liberados, a validade e o número de usuários, **assinado** com a chave privada do
fornecedor (ECDSA P-256 com SHA-256). Os módulos conferem a assinatura com a chave pública embutida no programa
(`src/Vorticis.Domain/Licencas/chave-publica.pem`): arquivo alterado ou emitido com outra chave é recusado.

## Gerenciador de licenças (`VorticisLicencas.exe`)

Programa só do fornecedor (artefato **VorticisGerenciadorLicencas** do CI; não vai no instalador nem nos portáteis do cliente).

1. **Chave do fornecedor.** Na primeira vez, clique em **Importar chave privada...** e escolha o arquivo `vorticis-chave-privada.pem`
   (o par cuja chave pública está na versão atual da suíte). A faixa no topo confirma: *as licenças emitidas valem na versão atual*.
   Guarde uma cópia da chave privada em local seguro (pendrive, cofre de senhas): **quem tiver esse arquivo emite licenças**.
   Ela fica em `%APPDATA%\Vorticis\GerenciadorLicencas\chave-privada.pem` e **nunca** deve ir para o repositório.
2. **Emitir.** Informe o **cliente**, o **CNPJ/CPF**, marque os **módulos**, a **validade** e o **número de usuários** e clique em
   **Emitir licença**. O arquivo sai em `%APPDATA%\Vorticis\GerenciadorLicencas\licencas\<cliente>-<id>.vlic` e entra no histórico.
3. **Entregar.** Envie o arquivo ao cliente. Ele o escolhe na página *Licença de uso* do instalador (só os módulos licenciados ficam
   disponíveis) ou na tela de entrada do programa (botão **Instalar arquivo de licença...**).
4. **Renovar / ampliar.** Emita uma licença nova (outra validade ou mais módulos) e envie; o cliente instala por cima.

### Trocar a chave do fornecedor

**Criar novo par de chaves** gera outro par. Para valer, a suíte precisa da chave pública nova: **Exportar chave pública...**, substitua
`src/Vorticis.Domain/Licencas/chave-publica.pem`, faça o commit e gere a nova versão no CI. A partir dela, só as licenças emitidas com a
chave nova valem (reemita as dos clientes).

## Formato do arquivo

```
VORTICIS-LICENCA 1
ID=<identificador>
CLIENTE=<nome>
DOCUMENTO=<CNPJ ou CPF, só números>
MODULOS=contabil,fiscal,folha,escritorio,erp,condominio
EMITIDA_EM=AAAA-MM-DD
VALIDA_ATE=AAAA-MM-DD
USUARIOS=<n>
ASSINATURA=<Base64 da assinatura das linhas acima>
```

A linha `MODULOS` é lida pelo instalador (Inno Setup) para habilitar só os componentes licenciados; a validação de verdade (assinatura,
validade e módulo) é feita pelo programa ao entrar, em `LicencaInstalada` (`src/Vorticis.Desktop.Comum`). Onde o programa procura o
arquivo, nesta ordem: variável `VORTICIS_LICENCA`, pasta do programa, `%LOCALAPPDATA%\Vorticis`, `%PROGRAMDATA%\Vorticis`.
Os testes automatizados geram uma chave própria e uma licença com todos os módulos (`LicencasDesktopTests`).
