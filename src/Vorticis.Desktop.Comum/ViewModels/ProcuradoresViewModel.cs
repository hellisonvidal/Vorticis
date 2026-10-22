using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Vorticis.Application.Cadastros;
using Vorticis.Domain.Cadastros;
using Vorticis.Domain.Seguranca;

namespace Vorticis.Desktop.Comum.ViewModels;

public sealed record ProcuradorLinha(Procurador Procurador)
{
    public string Numero => Procurador.Id.ToString("0000");
    public string Nome => Procurador.Nome;
    public string CpfFormatado => Domain.Cadastros.Cpf.Formatar(Procurador.Cpf);
    public string Inicio => Procurador.VigenciaInicio.ToString("dd/MM/yyyy");
    public string Fim => Procurador.VigenciaFim?.ToString("dd/MM/yyyy") ?? "indeterminado";
    public string Situacao => Procurador.VigenteEm(DateOnly.FromDateTime(DateTime.Today)) ? "vigente" : "fora da vigência";
    public string Observacao => Procurador.Observacao ?? "";
}

/// <summary>
/// Cadastros › Procurador: pessoas com procuração para assinar a ECD e a ECF pela empresa (qualificação 309 – Procurador), com a vigência
/// da procuração. A procuração eletrônica em si precisa estar cadastrada no e-CAC para a transmissão.
/// </summary>
public partial class ProcuradoresViewModel(IContexto contexto) : CadastroViewModel(contexto)
{
    public ObservableCollection<ProcuradorLinha> Lista { get; } = [];

    [ObservableProperty] private ProcuradorLinha? _selecionado;
    [ObservableProperty] private long _editandoId;
    [ObservableProperty] private string _nome = "";
    [ObservableProperty] private string _cpf = "";
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _telefone = "";
    [ObservableProperty] private DateTime? _inicio;
    [ObservableProperty] private DateTime? _fim;
    [ObservableProperty] private string _observacao = "";
    [ObservableProperty] private string _resumo = "";

    public bool PodeEditar => Contexto.Pode(Permissao.EmpresaResponsaveisManter);
    public override bool PodeManter => PodeEditar;
    protected override string NomeRegistro => "procurador";
    protected override string? DescricaoSelecionado => Selecionado?.Procurador.Nome;

    public void AtualizarPermissoes()
    {
        OnPropertyChanged(nameof(PodeEditar));
        AtualizarBarra();
    }

    public async Task CarregarAsync()
    {
        AtualizarPermissoes();
        if (Contexto.App is not { } app || Contexto.EmpresaId is not { } empresa || Contexto.Sessao is not { } sessao) return;
        var g = NovaGeracao();
        var r = await app.Responsaveis.ListarProcuradoresAsync(empresa, sessao.Id);
        if (!EhAtual(g)) return;
        if (!r.Sucesso) { MostrarResultado(r, ""); return; }
        var id = Selecionado?.Procurador.Id;
        Lista.Clear();
        foreach (var p in r.Valor) Lista.Add(new ProcuradorLinha(p));
        Selecionado = Lista.FirstOrDefault(l => l.Procurador.Id == id);
        if (Selecionado is null) { Limpar(); Modo = ModoCadastro.Consulta; }
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        Resumo = r.Valor.Any(p => p.VigenteEm(hoje)) ? "Há procurador com procuração vigente hoje." : "Nenhum procurador com procuração vigente hoje.";
    }

    partial void OnSelecionadoChanged(ProcuradorLinha? value)
    {
        MostrarSelecionado();
        AtualizarBarra();
    }

    protected override void MostrarSelecionado()
    {
        if (Selecionado is not { } linha) { Limpar(); return; }
        var p = linha.Procurador;
        (EditandoId, Nome, Cpf, Email, Telefone, Observacao) = (p.Id, p.Nome, p.Cpf, p.Email ?? "", p.Telefone ?? "", p.Observacao ?? "");
        (Inicio, Fim) = (p.VigenciaInicio.ToDateTime(TimeOnly.MinValue), p.VigenciaFim?.ToDateTime(TimeOnly.MinValue));
    }

    protected override void PrepararInclusao() => Limpar();

    private void Limpar()
    {
        (EditandoId, Nome, Cpf, Email, Telefone, Observacao) = (0, "", "", "", "", "");
        (Inicio, Fim) = (DateTime.Today, null);
    }

    protected override async Task<bool> GravarAsync(bool inclusao)
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Inicio is not { } inicio) { Erros.Add("Informe o início da vigência da procuração."); return; }
            var procurador = new Procurador(inclusao ? 0 : EditandoId, Nome, Cpf, Email, Telefone, DateOnly.FromDateTime(inicio),
                Fim is { } f ? DateOnly.FromDateTime(f) : null, Observacao);
            var r = await app.Responsaveis.SalvarProcuradorAsync(empresa, procurador, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Modo = ModoCadastro.Consulta;
            Selecionado = null;
            await CarregarAsync();
            Selecionado = Lista.FirstOrDefault(l => l.Procurador.Id == r.Valor);
            Mensagem = inclusao ? "Procurador incluído." : "Procurador alterado.";
        }, Permissao.EmpresaResponsaveisManter);
        return ok;
    }

    protected override async Task<bool> ExcluirSelecionadoAsync()
    {
        var ok = false;
        await ExecutarAsync(async (app, empresa, usuario) =>
        {
            if (Selecionado is not { } linha) { Erros.Add("Selecione um procurador."); return; }
            var r = await app.Responsaveis.ExcluirProcuradorAsync(empresa, linha.Procurador.Id, usuario);
            if (!r.Sucesso) { MostrarResultado(r, ""); return; }
            ok = true;
            Selecionado = null;
            await CarregarAsync();
            Mensagem = "Procurador excluído.";
        }, Permissao.EmpresaResponsaveisManter);
        return ok;
    }
}
