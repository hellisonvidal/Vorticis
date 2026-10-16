# 02 — Validação de partidas dobradas

A integridade é garantida em **duas camadas**:

1. **Aplicação** (`ServicoLancamento`) — valida tudo, devolve mensagens amigáveis e acumula todos os erros de uma vez.
2. **Banco** (triggers em [`db/001_core_contabil.sql`](../db/001_core_contabil.sql)) — última linha de defesa contra bug, importação malfeita ou acesso direto ao banco.

## 1. Regras

| # | Regra | Fundamento | Onde |
|---|---|---|---|
| R1 | Σ Débitos = Σ Créditos em todo lançamento efetivado | Método das partidas dobradas (ITG 2000) | App + constraint trigger diferido |
| R2 | Ao menos 1 partida a débito e 1 a crédito | idem | App + trigger |
| R3 | Valor > 0; o sinal é dado por `ind_dc`, nunca pelo valor | ECD I250 (VL_DC positivo + IND_DC) | `CHECK` |
| R4 | Só contas **analíticas**, ativas, da mesma empresa, existentes na data | ECD I050/I250 | Trigger |
| R5 | Centro de custo / participante quando a conta exigir | Política da empresa / ECD I250 | Trigger |
| R6 | Data dentro do exercício e em **período aberto** | Fechamento mensal | Trigger |
| R7 | Lançamento efetivado é **imutável**; correção só por estorno | ITG 2000 itens 31–36 (retificação por estorno, transferência ou complementação) | Triggers |
| R8 | Numeração sequencial, sem lacunas, atribuída só na efetivação | Livro Diário — ordem cronológica, sem intervalos/rasuras (CC art. 1.183) | App (sequência com lock) |
| R9 | Encerramento de resultado (`IND_LCTO = 'E'`) só na data final do exercício | ECD I200 | Trigger |
| R10 | Histórico obrigatório em toda partida | ECD I250.HIST | `NOT NULL` |
| R11 | Arredondamento: cálculo em `decimal`, arredondar **cada partida** a 2 casas e ajustar a diferença residual na maior partida antes de validar | Evita lançamentos "de R$ 0,01" rejeitados | App |

## 2. Por que *constraint trigger* `DEFERRABLE INITIALLY DEFERRED`

Um `CHECK` comum não enxerga várias linhas. Um trigger imediato falharia ao inserir a primeira partida (ainda desbalanceada). O trigger diferido roda **no COMMIT**, quando todas as partidas já existem — se a soma não fechar, a transação inteira é desfeita.

```sql
-- Trecho central (ver arquivo completo)
SELECT COALESCE(SUM(valor) FILTER (WHERE ind_dc = 'D'), 0),
       COALESCE(SUM(valor) FILTER (WHERE ind_dc = 'C'), 0)
  INTO v_deb, v_cred
  FROM contabil.partida WHERE lancamento_id = v_lanc_id;

IF v_deb <> v_cred THEN
    RAISE EXCEPTION 'Lançamento %: débitos (%) <> créditos (%), diferença %', ...;
END IF;
```

Rascunhos (`status = 'RASCUNHO'`) podem ficar desbalanceados enquanto o usuário digita; a validação dispara na transição para `EFETIVADO`.

## 3. Camada de aplicação (C#)

```csharp
public sealed record PartidaDto(string Conta, char DC, decimal Valor, string Historico,
                                string? CentroCusto = null, string? Participante = null);

public sealed class ServicoLancamento
{
    public async Task<Resultado<long>> Efetivar(long empresaId, DateOnly data,
        IReadOnlyList<PartidaDto> partidas, OrigemLancamento origem, long? documentoOrigemId,
        CancellationToken ct)
    {
        var erros = new List<string>();

        // R3 + R11: normalização monetária
        var linhas = partidas.Select(p => p with { Valor = Math.Round(p.Valor, 2, MidpointRounding.AwayFromZero) })
                             .ToList();
        if (linhas.Any(p => p.Valor <= 0)) erros.Add("Todas as partidas devem ter valor positivo.");
        if (linhas.Any(p => p.DC is not ('D' or 'C'))) erros.Add("Indicador D/C inválido.");

        // R1 + R2
        decimal deb = linhas.Where(p => p.DC == 'D').Sum(p => p.Valor);
        decimal cred = linhas.Where(p => p.DC == 'C').Sum(p => p.Valor);
        if (!linhas.Any(p => p.DC == 'D') || !linhas.Any(p => p.DC == 'C'))
            erros.Add("O lançamento precisa de ao menos um débito e um crédito.");
        if (deb != cred)
            erros.Add($"Débitos ({deb:N2}) diferentes de créditos ({cred:N2}). Diferença: {deb - cred:N2}.");

        // R4 + R5: carrega contas de uma vez
        var contas = await _plano.ObterPorCodigos(empresaId, linhas.Select(l => l.Conta), ct);
        foreach (var l in linhas)
        {
            if (!contas.TryGetValue(l.Conta, out var c)) { erros.Add($"Conta {l.Conta} inexistente."); continue; }
            if (!c.Analitica) erros.Add($"Conta {c.Codigo} é sintética.");
            if (!c.Ativa) erros.Add($"Conta {c.Codigo} inativa.");
            if (c.DataInclusao > data) erros.Add($"Conta {c.Codigo} não existia em {data:dd/MM/yyyy}.");
            if (c.ExigeCentroCusto && l.CentroCusto is null) erros.Add($"Conta {c.Codigo} exige centro de custo.");
            if (string.IsNullOrWhiteSpace(l.Historico)) erros.Add("Histórico obrigatório.");
        }

        // R6
        var periodo = await _periodos.Obter(empresaId, data, ct);
        if (periodo is null) erros.Add("Exercício/período não cadastrado.");
        else if (periodo.Fechado) erros.Add($"Período {data:MM/yyyy} fechado.");

        if (erros.Count > 0) return Resultado<long>.Falha(erros);

        // R8: transação + numeração sem lacunas (lock na linha do exercício)
        await using var tx = await _db.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        long numero = await _db.ExecuteScalarAsync<long>(
            "SELECT COALESCE(MAX(numero),0)+1 FROM contabil.lancamento " +
            "WHERE exercicio_id = @ex AND numero IS NOT NULL " +
            "/* precedido de SELECT ... FROM contabil.exercicio WHERE id=@ex FOR UPDATE */", ...);
        long id = await _repo.InserirCabecalho(..., status: Status.Rascunho);
        await _repo.InserirPartidas(id, linhas);
        await _repo.Efetivar(id, numero);            // muda status -> dispara triggers
        await _saldos.Acumular(id);                  // atualiza contabil.saldo_mensal
        await _auditoria.Registrar("lancamento", id, "EFETIVAR");
        await tx.CommitAsync(ct);                    // constraint trigger valida R1/R2 aqui
        return Resultado<long>.Ok(id);
    }

    // R7: estorno = novo lançamento espelhado; o original fica no Diário marcado como ESTORNADO
    public Task<Resultado<long>> Estornar(long lancamentoId, DateOnly data, string motivo, CancellationToken ct) => ...;
}
```

## 4. Testes

`db/tests/001_partidas_dobradas_test.sql` cobre 11 casos (aceite de lançamento balanceado, rejeição de desbalanceado, sem crédito, conta sintética, valor negativo, período fechado, data fora do exercício, alteração/exclusão de efetivado, e estorno zerando saldo). Para rodar:

```bash
createdb perseus_test
psql -v ON_ERROR_STOP=1 -d perseus_test -f db/001_core_contabil.sql -f db/tests/001_partidas_dobradas_test.sql
# saída final esperada: TODOS OS TESTES PASSARAM
```

## 5. Invariantes globais a verificar no fechamento

Além da regra por lançamento, o fechamento de período deve conferir:

```sql
-- (a) Balancete fecha: Σ saldos devedores = Σ saldos credores (analíticas)
-- (b) Ativo = Passivo + PL (após apuração do resultado)
-- (c) saldo_mensal == recalculado a partir das partidas (detecta corrupção do cache)
-- (d) toda conta analítica com movimento tem mapeamento referencial no ano (pré-requisito ECD/ECF)
-- (e) nenhuma lacuna em lancamento.numero no exercício
```
