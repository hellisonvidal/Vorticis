using Vorticis.Desktop.Comum.Modulos;
using Vorticis.Desktop.ViewModels;

namespace Vorticis.Desktop.Tests;

/// <summary>Consistência do menu do módulo com o casco: toda chave de menu e de atalho tem uma tela registrada.</summary>
public class MenusTests
{
    private static IEnumerable<ItemMenu> Folhas(IEnumerable<ItemMenu> itens) =>
        itens.SelectMany(i => i.Filhos is null ? [i] : Folhas(i.Filhos));

    [Fact]
    public void Toda_chave_de_menu_do_contabil_tem_tela_registrada()
    {
        var vm = new MainViewModel();
        var chaves = Folhas(vm.Menus).Where(i => !i.EhSeparador && i.Habilitado && i.Chave is not null && i.Chave != TelasComuns.Sair)
            .Select(i => i.Chave!).ToList();
        Assert.Contains(Telas.Lancamento, chaves);
        Assert.Contains(Telas.Empresas, chaves);
        Assert.All(chaves, c => Assert.True(vm.ConheceTela(c), c));
        Assert.All(vm.Atalhos.Where(a => a.Chave is not null), a => Assert.True(vm.ConheceTela(a.Chave!), a.Chave));
    }

    [Fact]
    public void Fiscal_tem_planejamento_tributario_em_Apuracao_e_em_Relatorios()
    {
        var vm = new Vorticis.Fiscal.Desktop.MainViewModel();
        Folhas(vm.Menus.Single(m => m.Titulo == "_Apuração").Filhos!).Single(i => i.Chave == Vorticis.Fiscal.Desktop.ViewModels.Telas.Planejamento);
        Folhas(vm.Menus.Single(m => m.Titulo == "_Relatórios").Filhos!).Single(i => i.Chave == Vorticis.Fiscal.Desktop.ViewModels.Telas.RelPlanejamento);
        var tela = new Vorticis.Fiscal.Desktop.ViewModels.PlanejamentoTributarioViewModel(vm) { Ano = 2027 };
        tela.Receitas[0].ValorTexto = "1.200.000,00";
        tela.Receitas[3].ValorTexto = "300000";
        (tela.FolhaTexto, tela.EncargosTexto, tela.IcmsTexto, tela.IssTexto) = ("120.000,00", "27,8", "5", "2,5");
        var e = tela.Entrada();
        Assert.Equal(1_500_000m, e.ReceitaTotal);
        Assert.Equal((120_000m, 27.8m, 5m, 2.5m), (e.Folha, e.EncargosPercentual, e.AliquotaIcms, e.AliquotaIss));
        Assert.Equal(Vorticis.Fiscal.Planejamento.TipoReceitaPlanejamento.ServicosFatorR, e.Receitas[1].Tipo);
        var outra = new Vorticis.Fiscal.Desktop.ViewModels.PlanejamentoTributarioViewModel(vm);
        outra.Preencher(new Vorticis.Fiscal.Planejamento.SimulacaoPlanejamento(1, 2027, "Cenário base", e, DateTimeOffset.Now));
        Assert.Equal(e, outra.Entrada() with { Receitas = e.Receitas });
        Assert.Equal(e.Receitas, outra.Entrada().Receitas);
        Assert.Equal("2027 · Cenário base", new Vorticis.Fiscal.Planejamento.SimulacaoPlanejamento(1, 2027, "Cenário base", e, DateTimeOffset.Now).ToString());
    }

    [Fact]
    public void Fiscal_tem_menu_comum_e_itens_em_breve_desabilitados()
    {
        MainViewModelBase vm = new Vorticis.Fiscal.Desktop.MainViewModel();
        Assert.StartsWith("Vorticis ", vm.Modulo.Nome);
        Assert.Equal("_Cadastros", vm.Menus[0].Titulo);
        Assert.Equal("_Ajuda", vm.Menus[^1].Titulo);
        var proprios = vm.Menus.Skip(1).SkipLast(2);
        Assert.NotEmpty(proprios);
        // item habilitado tem chave; o que falta fica "em breve" (desabilitado e sem chave)
        Assert.All(Folhas(proprios).Where(i => !i.EhSeparador), i => Assert.Equal(i.Chave is not null, i.Habilitado));
        Assert.All(Folhas(vm.Menus).Where(i => i.Chave is not null && i.Chave != TelasComuns.Sair), i => Assert.True(vm.ConheceTela(i.Chave!), i.Chave));
    }

    [Fact]
    public void Cada_modulo_tem_cor_e_icone_proprios_para_nao_confundir_os_sistemas()
    {
        MainViewModelBase[] modulos = [new MainViewModel(), new Vorticis.Fiscal.Desktop.MainViewModel(), new Vorticis.Folha.Desktop.MainViewModel(), new Vorticis.Escritorio.Desktop.MainViewModel(), new Vorticis.Erp.Desktop.MainViewModel(), new Vorticis.Condominio.Desktop.MainViewModel()];
        Assert.Equal(["#1B7F5A", "#D97706", "#7E22CE", "#1D4ED8", "#0891B2", "#BE123C"], modulos.Select(m => m.Modulo.CorDestaque));
        Assert.Equal(modulos.Length, modulos.Select(m => m.Modulo.CorBarra).Distinct().Count());
        Assert.Equal("#14532D", modulos[0].Modulo.CorBarra);                      // verde-escuro: não confunde com o azul do Escritório
        // a cor chega ao casco como pincel (faixa sob os menus, login e tela inicial)
        Assert.Equal(Avalonia.Media.Color.Parse("#D97706"), ((Avalonia.Media.ISolidColorBrush)modulos[1].PincelDestaque).Color);
        Assert.Equal(Avalonia.Media.Color.Parse("#3B0764"), ((Avalonia.Media.ISolidColorBrush)modulos[2].PincelBarra).Color);
        Assert.Same(modulos[1].PincelDestaque, modulos[1].Login.PincelDestaque);
        Assert.Same(modulos[2].PincelDestaque, modulos[2].Inicio.PincelDestaque);
        // um ícone por executável (fonte em instalador/icone, embutido no casco e usado pelo exe e pelo instalador)
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !Directory.Exists(Path.Combine(pasta.FullName, "instalador", "icone"))) pasta = pasta.Parent;
        Assert.NotNull(pasta);
        var tamanhos = modulos.Select(m => new FileInfo(Path.Combine(pasta.FullName, "instalador", "icone", $"{m.Modulo.NomeExecutavel}.ico")))
                              .Select(f => { Assert.True(f.Exists, f.Name); return File.ReadAllBytes(f.FullName); }).ToList();
        Assert.Equal(modulos.Length, tamanhos.Select(b => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b))).Distinct().Count());   // ícones diferentes
        // o instalador tem ícone próprio, diferente dos módulos, e as cores do gerador de ícones são as dos módulos
        var instalador = File.ReadAllBytes(Path.Combine(pasta.FullName, "instalador", "icone", "VorticisSuite.ico"));
        Assert.DoesNotContain(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(instalador)),
                              tamanhos.Select(b => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(b))));
        Assert.Contains(@"SetupIconFile=icone\VorticisSuite.ico", File.ReadAllText(Path.Combine(pasta.FullName, "instalador", "Vorticis.iss")));
        var gerador = File.ReadAllText(Path.Combine(pasta.FullName, "instalador", "icone", "gerar_icone.py"));
        Assert.All(modulos, m => Assert.Contains($"\"{m.Modulo.NomeExecutavel}\": (\"{m.Modulo.CorDestaque}\", \"{m.Modulo.CorBarra}\"", gerador));
    }

    [Fact]
    public void Usuarios_acessos_e_banco_de_dados_ficam_no_Escritorio_quando_instalado()
    {
        static List<string> Config(MainViewModelBase vm) => Folhas(Assert.Single(vm.Menus, m => m.Titulo == "Con_figurações").Filhos!).Where(i => i.Chave is not null).Select(i => i.Chave!).ToList();
        var anterior = MainViewModelBase.EscritorioInstalado;
        try
        {
            MainViewModelBase.EscritorioInstalado = () => true;
            var contabil = new MainViewModel();
            Assert.True(contabil.SistemaCentralizadoNoEscritorio);
            Assert.Equal([TelasComuns.Certificados, TelasComuns.Sair], Config(contabil));
            var esc = new Vorticis.Escritorio.Desktop.MainViewModel();
            Assert.False(esc.SistemaCentralizadoNoEscritorio);
            var menuEsc = Assert.Single(esc.Menus, m => m.Titulo == "Con_figurações").Filhos!;
            Assert.Equal("Usuários e controle de acesso", menuEsc[0].Titulo);
            var banco = Assert.Single(menuEsc, m => m.Titulo == "_Banco de dados");
            Assert.Equal([TelasComuns.Conexao, TelasComuns.Backup, TelasComuns.ConfiguracoesGerais], banco.Filhos!.Select(i => i.Chave));

            MainViewModelBase.EscritorioInstalado = () => false;   // sem o Escritório, cada módulo administra
            Assert.Superset(new HashSet<string>([TelasComuns.Usuarios, TelasComuns.Conexao, TelasComuns.Backup, TelasComuns.ConfiguracoesGerais]),
                            new HashSet<string>(Config(new MainViewModel())));
        }
        finally { MainViewModelBase.EscritorioInstalado = anterior; }
        var raiz = new DirectoryInfo(AppContext.BaseDirectory);
        while (raiz is not null && !File.Exists(Path.Combine(raiz.FullName, "instalador", "Vorticis.iss"))) raiz = raiz.Parent;
        var iss = File.ReadAllText(Path.Combine(raiz!.FullName, "instalador", "Vorticis.iss"));
        // a instalação abre a Suite ao final (ela começa pelo Escritório)
        Assert.Contains("VorticisSuite.exe\"; Description: \"{cm:LaunchProgram,Vorticis Suite Empresarial}\"; Flags: nowait postinstall skipifsilent; Components:", iss);
    }

    [Fact]
    public void Menu_tem_os_menus_comuns_em_volta_dos_do_modulo()
    {
        var titulos = new MainViewModel().Menus.Select(m => m.Titulo).ToList();
        Assert.Equal("_Cadastros", titulos[0]);
        Assert.Equal("Con_figurações", titulos[^2]);
        Assert.Equal("_Ajuda", titulos[^1]);
        Assert.Contains("_Lançamentos", titulos);
    }

    [Fact]
    public void Calendario_fica_em_Consultas_no_contabil_e_em_Obrigacoes_no_fiscal_e_na_folha()
    {
        var contabil = new MainViewModel();
        Assert.DoesNotContain(contabil.Menus, m => m.Titulo.Replace("_", "") == "Obrigações");
        var consultas = Assert.Single(contabil.Menus, m => m.Titulo.Replace("_", "") == "Consultas");
        Assert.Contains(Folhas(consultas.Filhos!), i => i.Chave == TelasComuns.Obrigacoes);
        MainViewModelBase[] outros = [new Vorticis.Fiscal.Desktop.MainViewModel(), new Vorticis.Folha.Desktop.MainViewModel()];
        foreach (var vm in outros)
        {
            Assert.DoesNotContain(vm.Menus, m => m.Titulo.Contains("genda"));
            var obrig = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Obrigações");
            Assert.Contains(Folhas(obrig.Filhos!), i => i.Chave == TelasComuns.Obrigacoes);
        }
        // o ERP (rotina da empresa, não do escritório contábil), o Condomínio não têm calendário de obrigações
        foreach (var sem in new MainViewModelBase[] { new Vorticis.Erp.Desktop.MainViewModel(), new Vorticis.Condominio.Desktop.MainViewModel() })
        {
            Assert.False(sem.Modulo.UsaCalendarioObrigacoes);
            Assert.DoesNotContain(Folhas(sem.Menus), i => i.Chave == TelasComuns.Obrigacoes);
            Assert.DoesNotContain(sem.Menus, m => m.Titulo.Replace("_", "") == "Obrigações");
        }
    }

    [Fact]
    public void Condominio_tem_rateio_no_financeiro_atas_nos_relatorios_e_aba_sindico_so_nele()
    {
        var vm = new Vorticis.Condominio.Desktop.MainViewModel();
        var fin = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Financeiro");
        Assert.Contains(Folhas(fin.Filhos!), i => i.Chave == Vorticis.Condominio.Desktop.ViewModels.CondominioTelas.Rateio && i.Titulo == "Rateio entre unidades");
        var rel = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Relatórios");
        Assert.Contains(Folhas(rel.Filhos!), i => i.Chave == Vorticis.Condominio.Desktop.ViewModels.CondominioTelas.Atas && i.Titulo == "Atas de Assembleia");
        Assert.True(vm.Empresas.MostraAbaSindico);
        // no Condomínio o cadastro de empresas aparece como "Condomínios" (mesmos dados)
        var cad = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Cadastros");
        Assert.Equal("Condomínios", Assert.Single(Folhas(cad.Filhos!), i => i.Chave == TelasComuns.Empresas).Titulo);
        Assert.Equal("Empresas", Assert.Single(Folhas(Assert.Single(new MainViewModel().Menus, m => m.Titulo.Replace("_", "") == "Cadastros").Filhos!),
                                               i => i.Chave == TelasComuns.Empresas).Titulo);
        Assert.False(new MainViewModel().Empresas.MostraAbaSindico);
        Assert.False(new Vorticis.Fiscal.Desktop.MainViewModel().Empresas.MostraAbaSindico);
    }

    [Fact]
    public void Sindico_assinatura_digital_exige_assinar_os_livros()
    {
        var s = new Vorticis.Condominio.Desktop.MainViewModel().Sindico;
        s.AssinaturaDigital = true;
        Assert.True(s.AssinaLivros);
        s.AssinaLivros = false;
        Assert.False(s.AssinaturaDigital);
    }

    [Fact]
    public void Erp_separa_contas_a_pagar_e_a_receber_e_agrupa_os_relatorios_financeiros_sem_transporte()
    {
        var vm = new Vorticis.Erp.Desktop.MainViewModel();
        Assert.DoesNotContain(vm.Menus, m => m.Titulo.Replace("_", "") == "Transporte");
        Assert.All(Folhas(vm.Menus).Where(i => i.Chave is not null && i.Chave != TelasComuns.Sair), i => Assert.True(vm.ConheceTela(i.Chave!), i.Chave));
        var fin = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Financeiro");
        Assert.Equal(["Contas a pagar", "Contas a receber", "Conciliação bancária"], fin.Filhos!.Where(i => !i.EhSeparador).Select(i => i.Titulo));
        var rel = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Relatórios");
        var folhasRel = Folhas(rel.Filhos!).Select(i => i.Chave).ToList();
        Assert.Contains(Vorticis.Fiscal.Desktop.ViewModels.Telas.ResumoVendas, folhasRel);
        Assert.Contains(Vorticis.Fiscal.Desktop.ViewModels.Telas.ResumoCompras, folhasRel);
        Assert.Contains(Vorticis.Fiscal.Desktop.ViewModels.Telas.RelRecebimentos, folhasRel);
        var subFin = Assert.Single(rel.Filhos!, m => m.Titulo.Replace("_", "") == "Financeiro");
        Assert.Contains(subFin.Filhos!, i => i.Chave == Vorticis.Fiscal.Desktop.ViewModels.Telas.RelDuplicatasPagar);
        // cada tela de contas mostra um tipo só
        var pagar = new Vorticis.Fiscal.Desktop.ViewModels.ContasPagarViewModel(vm);
        Assert.False(pagar.EscolheTipo);
        Assert.Equal("Contas a pagar", pagar.TituloTela);
    }

    [Fact]
    public void Fiscal_agrupa_os_resumos_em_Relatorios()
    {
        var vm = new Vorticis.Fiscal.Desktop.MainViewModel();
        var rel = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Relatórios");
        var resumos = Assert.Single(rel.Filhos!, m => m.Titulo.Replace("_", "") == "Resumos");
        Assert.Equal(["Resumo de vendas", "Resumo de compras", "Resumo por CFOP"], resumos.Filhos!.Select(i => i.Titulo));
    }

    [Fact]
    public void Contabil_exporta_ECD_e_ECF_em_Integracoes_sem_menu_Auditoria_nem_eSocial()
    {
        var vm = new MainViewModel();
        Assert.DoesNotContain(vm.Menus, m => m.Titulo.Replace("_", "") == "Auditoria");
        var integ = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Integrações");
        var exportar = Assert.Single(integ.Filhos!, m => m.Titulo.Replace("_", "") == "Exportar");
        var folhas = Folhas(exportar.Filhos!).ToList();
        Assert.Contains(folhas, i => i.Chave == Telas.EcdAssistente);
        Assert.Contains(folhas, i => i.Chave == Telas.EcfAssistente);
        Assert.DoesNotContain(folhas, i => i.Titulo.Contains("eSocial"));
    }

    [Fact]
    public void Todos_os_modulos_tem_Parametros_no_menu_Cadastros()
    {
        MainViewModelBase[] modulos = [new MainViewModel(), new Vorticis.Fiscal.Desktop.MainViewModel(), new Vorticis.Folha.Desktop.MainViewModel(), new Vorticis.Escritorio.Desktop.MainViewModel(), new Vorticis.Erp.Desktop.MainViewModel(), new Vorticis.Condominio.Desktop.MainViewModel()];
        foreach (var vm in modulos)
        {
            var cadastros = vm.Menus[0];
            Assert.Contains(cadastros.Filhos!, i => i.Titulo == "Parâmetros" && (i.Chave == TelasComuns.Parametros || i.Filhos?.Any(f => f.Chave == TelasComuns.Parametros) == true));
            Assert.True(vm.ConheceTela(TelasComuns.Parametros));
        }
    }

    [Fact]
    public void Menu_da_folha_tem_telas_registradas_e_so_o_que_falta_fica_em_breve()
    {
        var vm = new Vorticis.Folha.Desktop.MainViewModel();
        Assert.Equal("_Cadastros", vm.Menus[0].Titulo);
        Assert.Equal("_Ajuda", vm.Menus[^1].Titulo);
        var proprios = Folhas(vm.Menus.Skip(1).SkipLast(2)).Where(i => !i.EhSeparador).ToList();
        var habilitados = proprios.Where(i => i.Habilitado).ToList();
        Assert.All(habilitados, i => Assert.True(vm.ConheceTela(i.Chave!), i.Titulo));
        Assert.Contains(habilitados, i => i.Chave == "folha.esocial");
        Assert.Contains(habilitados, i => i.Chave == "folha.prolabore");
        // Funcionários é submenu de Cadastros; a folha não tem Clientes nem Fornecedores
        var cadastros = Folhas(vm.Menus.Take(1)).Where(i => !i.EhSeparador).ToList();
        Assert.Contains(cadastros, i => i.Chave == "folha.funcionarios");
        Assert.DoesNotContain(cadastros, i => i.Chave is TelasComuns.Clientes or TelasComuns.Fornecedores);
        Assert.True(vm.UsaPeriodo);
        Assert.Equal(["FGTS (GFD) (em breve)", "DCTFWeb e INSS (em breve)", "RAIS e DIRF (em breve)"], proprios.Where(i => !i.Habilitado).Select(i => i.Titulo));
        Assert.All(vm.Atalhos.Where(a => a.Chave is not null), a => Assert.True(vm.ConheceTela(a.Chave!), a.Chave));
        // Folha e Pró-labore são submenus de Cálculos
        var calculos = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Cálculos");
        Assert.Equal(["Folha", "Pró-labore"], calculos.Filhos!.Select(m => m.Titulo.Replace("_", "")));
        Assert.DoesNotContain(vm.Menus, m => m.Titulo.Replace("_", "") is "Folha" or "Pró-labore");
    }

    [Fact]
    public async Task Competencia_da_folha_e_escolhida_por_mes_e_ano_e_a_lista_filtra_as_fechadas()
    {
        var vm = new Vorticis.Folha.Desktop.MainViewModel();
        var folha = vm.FolhaMensal;
        folha.Competencia = new DateTime(2025, 7, 1);
        Assert.Equal(2025, folha.AnoCompetencia);
        Assert.Equal(12, Vorticis.Desktop.Comum.Views.SeletorCompetencia.Meses.Count);
        Assert.All(Vorticis.Desktop.Comum.Views.SeletorCompetencia.Anos, a => Assert.Equal(4, a.ToString().Length));   // ano sempre com 4 dígitos

        // sem banco nenhuma competência está fechada: a lista tem os meses recentes, e "Só fechadas" a esvazia
        await vm.RecarregarPeriodosAsync();
        Assert.Contains(vm.ListaPeriodos, p => p.Periodo == vm.PeriodoTrabalho);
        Assert.Equal(vm.PeriodoTrabalho, vm.PeriodoEscolhido!.Periodo);
        var outro = vm.ListaPeriodos.First(p => p.Periodo != vm.PeriodoTrabalho);
        vm.PeriodoEscolhido = outro;
        Assert.Equal(outro.Periodo, vm.PeriodoTrabalho);
        vm.SomenteFechados = true;
        await vm.RecarregarPeriodosAsync();
        Assert.Empty(vm.ListaPeriodos);
        Assert.Equal(outro.Periodo, vm.PeriodoTrabalho);         // o filtro não troca a competência em uso
        Assert.Equal("03/2026 (fechada)", new OpcaoPeriodo(new DateOnly(2026, 3, 1), true).Texto);
    }

    [Fact]
    public void Contabil_esconde_Gerenciais_sem_o_parametro_e_tem_Depreciacao_e_Bloqueio_de_lancamentos()
    {
        var vm = new MainViewModel();
        var rel = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Relatórios");
        var gerenciais = Assert.Single(rel.Filhos!, m => m.Titulo.Replace("_", "") == "Gerenciais");
        Assert.Equal(ModuloContabil.RecursoGerencial, gerenciais.Recurso);
        Assert.False(vm.RecursoAtivo(gerenciais.Recurso));                     // sem empresa (e com o parâmetro desligado) o menu fica escondido
        Assert.True(vm.RecursoAtivo(null));
        var deprec = Assert.Single(rel.Filhos!, m => m.Titulo.Replace("_", "") == "Depreciação");
        Assert.Equal([Telas.RelacaoBens, Telas.ValorDepreciado, Telas.ValorResidual, Telas.DepreciacaoPorGrupo], deprec.Filhos!.Select(i => i.Chave));
        Assert.Contains(vm.Menus[0].Filhos!, i => i.Chave == Telas.BloqueioLancamentos);
        // "Bloquear lançamentos até" saiu dos parâmetros; participantes ganhou grupo próprio e a contabilidade gerencial ficou no lugar
        var p = new ParametrosContabilViewModel(vm);
        Assert.DoesNotContain(p.Grupos.SelectMany(g => g.Campos), c => c.Chave == "dataBloqueio");
        Assert.Equal(["usaCentroCusto", "usaGerencial"], p.Grupos.Single(g => g.Titulo.StartsWith("Centros de custo")).Campos.Select(c => c.Chave));
        Assert.Equal(["usaParticipantes"], p.Grupos.Single(g => g.Titulo == "Participantes").Campos.Select(c => c.Chave));
        Assert.Equal(["dataBloqueio"], new BloqueioLancamentosViewModel(vm).Grupos.SelectMany(g => g.Campos).Select(c => c.Chave));
    }

    [Fact]
    public void Contas_bancarias_e_formas_de_pagamento_ficam_em_Cadastros_menos_no_Escritorio()
    {
        static List<string?> Cadastros(MainViewModelBase vm) => Folhas(vm.Menus[0].Filhos!).Select(i => i.Chave).ToList();
        Assert.Contains(TelasComuns.ContasBancarias, Cadastros(new MainViewModel()));
        Assert.Contains(Folhas(new MainViewModel().Menus[0].Filhos!), i => i.Titulo == "Bancos" && i.Chave == TelasComuns.ContasBancarias);
        Assert.DoesNotContain(TelasComuns.FormasPagamento, Cadastros(new MainViewModel()));
        foreach (MainViewModelBase vm in new MainViewModelBase[] { new Vorticis.Fiscal.Desktop.MainViewModel(), new Vorticis.Erp.Desktop.MainViewModel() })
        {
            Assert.Contains(TelasComuns.ContasBancarias, Cadastros(vm));
            Assert.Contains(TelasComuns.FormasPagamento, Cadastros(vm));
        }
        var esc = Cadastros(new Vorticis.Escritorio.Desktop.MainViewModel());
        Assert.DoesNotContain(TelasComuns.ContasBancarias, esc);
        Assert.DoesNotContain(TelasComuns.FormasPagamento, esc);
        // a tela de cadastro mostra a simulação dos prazos digitados
        var formas = new FormasPagamentoViewModel(new MainViewModel()) { Prazos = "30/60" };
        Assert.Contains("1/2 em", formas.Exemplo);
        Assert.Equal((2m, 30m, 30m), (formas.Parcelas!.Value, formas.PrimeiroVencimento!.Value, formas.Intervalo!.Value));   // prazos → campos
        formas.Parcelas = 4;
        Assert.Equal("30/60/90/120", formas.Prazos);                                                                        // campos → prazos
        formas.IncluirCommand.Execute(null);
        Assert.Equal("1", formas.Codigo);                                                                                   // código automático ao incluir
        formas.Prazos = "x";
        Assert.StartsWith("Prazos inválidos", formas.Exemplo);
        Assert.True(new Vorticis.Desktop.Comum.Views.TemplatesComuns().Match(formas));
    }

    [Fact]
    public void Cliente_do_escritorio_importa_dados_da_receita_e_tem_aba_de_honorarios()
    {
        var main = new Vorticis.Escritorio.Desktop.MainViewModel();
        var cli = new Vorticis.Escritorio.Desktop.ViewModels.ClientesEscritorioViewModel(main);
        Assert.True(cli.ModoClientes);
        Assert.True(cli.Honorarios.EmbutidoNoCliente);
        Assert.False(cli.Honorarios.EscolheCliente);                 // o cliente é o do cadastro, sem caixa de escolha
        Assert.Equal(["Simples Nacional", "Lucro Presumido", "Lucro Real", "MEI", "Lucro Arbitrado", "Imune/isenta"],
                     Vorticis.Escritorio.Desktop.ViewModels.PessoasViewModel.Regimes);
        cli.AplicarConsulta(new Vorticis.Application.Cadastros.DadosCnpj("41827365000171", "MERCADO BOM PRECO LTDA", "BOM PRECO", "2062", "4711302", "SP", "3550308",
            "São Paulo", new DateOnly(2020, 5, 4), "ATIVA", true, false, "01001000", "Rua das Flores", "10", null, "Centro", "a@b.com", null, "BrasilAPI",
            DateTimeOffset.Now));
        Assert.Equal(("MERCADO BOM PRECO LTDA", "BOM PRECO", "2062", "4711302"), (cli.Nome, cli.NomeFantasia, cli.NaturezaJuridica, cli.Cnae));
        Assert.Equal(("3550308", "SP", "Simples Nacional", "Rua das Flores"), (cli.CodMunicipioIbge, cli.Uf, cli.Regime, cli.Logradouro));
        Assert.Equal(new DateTime(2020, 5, 4), cli.InicioAtividades);
        Assert.Contains("BrasilAPI", cli.OrigemDados);
    }

    [Fact]
    public void Escritorio_tem_cadastros_proprios_contratos_e_relatorios_de_honorarios()
    {
        var vm = new Vorticis.Escritorio.Desktop.MainViewModel();
        var cadastros = Folhas(vm.Menus[0].Filhos!).Select(i => i.Chave).ToList();
        // Cadastros: Escritório (primeiro: o cadastro de empresas, aqui só os escritórios) e Clientes; sem fornecedores (o módulo é só do escritório)
        Assert.Equal(TelasComuns.Empresas, cadastros[0]);
        Assert.Equal("Escritório", vm.Menus[0].Filhos![0].Titulo);
        Assert.Equal("Clientes:", vm.RotuloEmpresaAtiva);
        Assert.Contains(Vorticis.Escritorio.Desktop.ViewModels.EscritorioTelas.Clientes, cadastros);
        Assert.Equal(["Escritório", "Clientes"], vm.Menus[0].Filhos!.Where(i => i.Chave is not null && i.Chave != TelasComuns.Parametros && i.Chave != TelasComuns.Menus).Select(i => i.Titulo));
        Assert.False(vm.ConheceTela("escritorio.fornecedores"));
        Assert.Equal([Vorticis.Escritorio.ModeloContrato.Cliente], Vorticis.Escritorio.Desktop.ViewModels.ContratosViewModel.Modelos.Select(m => m.Valor));
        Assert.Equal("Empresa:", new Vorticis.Fiscal.Desktop.MainViewModel().RotuloEmpresaAtiva);
        foreach (var compartilhado in new[] { TelasComuns.Contabilidade, TelasComuns.Clientes, TelasComuns.Fornecedores, TelasComuns.Socios })
            Assert.DoesNotContain(compartilhado, cadastros);
        Assert.False(vm.Modulo.UsaCalendarioObrigacoes);
        Assert.DoesNotContain(vm.Menus, m => m.Titulo.Replace("_", "") is "Honorários" or "Obrigações");
        Assert.DoesNotContain(vm.Menus, m => m.Titulo.Replace("_", "") is "Contratos" or "Escritório");
        var org = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Organização");
        Assert.Equal(["Tarefas e prazos", "Protocolos de documentos"], org.Filhos!.Select(i => i.Titulo));
        var rel = Assert.Single(vm.Menus, m => m.Titulo.Replace("_", "") == "Relatórios");
        Assert.Equal(["Gerador de contratos", "Carteira de clientes", "Relatório de honorários"], rel.Filhos!.Select(i => i.Titulo));
        Assert.DoesNotContain(Folhas(vm.Menus), i => i.Titulo == "Relatório de contratos");
        Assert.All(Folhas(vm.Menus).Where(i => i.Chave is not null && i.Chave != TelasComuns.Sair), i => Assert.True(vm.ConheceTela(i.Chave!), i.Chave));
    }
}
