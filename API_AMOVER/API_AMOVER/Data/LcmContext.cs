using System;
using System.Collections.Generic;
using API_AMOVER.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace API_AMOVER.Data;

public partial class LcmContext : DbContext
{
    public LcmContext(DbContextOptions<LcmContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AspNetRole> AspNetRoles { get; set; }

    public virtual DbSet<AspNetRoleClaim> AspNetRoleClaims { get; set; }

    public virtual DbSet<AspNetUser> AspNetUsers { get; set; }

    public virtual DbSet<AspNetUserClaim> AspNetUserClaims { get; set; }

    public virtual DbSet<AspNetUserLogin> AspNetUserLogins { get; set; }

    public virtual DbSet<AspNetUserToken> AspNetUserTokens { get; set; }

    public virtual DbSet<Checklist> Checklists { get; set; }

    public virtual DbSet<ChecklistControlo> ChecklistControlos { get; set; }

    public virtual DbSet<ChecklistEmbalagem> ChecklistEmbalagems { get; set; }

    public virtual DbSet<ChecklistModelo> ChecklistModelos { get; set; }

    public virtual DbSet<ChecklistMontagem> ChecklistMontagems { get; set; }

    public virtual DbSet<Cliente> Clientes { get; set; }

    public virtual DbSet<Documento> Documentos { get; set; }

    public virtual DbSet<DocumentosModelo> DocumentosModelos { get; set; }

    public virtual DbSet<Encomenda> Encomendas { get; set; }

    public virtual DbSet<ModeloPecasFixa> ModeloPecasFixas { get; set; }

    public virtual DbSet<ModeloPecasSN> ModeloPecasSNs { get; set; }

    public virtual DbSet<ModelosMotum> ModelosMota { get; set; }

    public virtual DbSet<Mota> Motas { get; set; }

    public virtual DbSet<MotasPecasInfo> MotasPecasInfos { get; set; }

    public virtual DbSet<MotasPecasSN> MotasPecasSNs { get; set; }

    public virtual DbSet<OrdemProducao> OrdemProducaos { get; set; }

    public virtual DbSet<Peca> Pecas { get; set; }

    public virtual DbSet<Servico> Servicos { get; set; }

    public virtual DbSet<ServicosPecasAlterada> ServicosPecasAlteradas { get; set; }

    public virtual DbSet<UtilizadorMotum> UtilizadorMota { get; set; }

    public virtual DbSet<Utilizadore> Utilizadores { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AspNetRole>(entity =>
        {
            entity.HasIndex(e => e.NormalizedName, "RoleNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedName] IS NOT NULL)");

            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.NormalizedName).HasMaxLength(256);
        });

        modelBuilder.Entity<AspNetRoleClaim>(entity =>
        {
            entity.HasIndex(e => e.RoleId, "IX_AspNetRoleClaims_RoleId");

            entity.HasOne(d => d.Role).WithMany(p => p.AspNetRoleClaims).HasForeignKey(d => d.RoleId);
        });

        modelBuilder.Entity<AspNetUser>(entity =>
        {
            entity.HasIndex(e => e.NormalizedEmail, "EmailIndex");

            entity.HasIndex(e => e.NormalizedUserName, "UserNameIndex")
                .IsUnique()
                .HasFilter("([NormalizedUserName] IS NOT NULL)");

            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.NormalizedEmail).HasMaxLength(256);
            entity.Property(e => e.NormalizedUserName).HasMaxLength(256);
            entity.Property(e => e.UserName).HasMaxLength(256);

            entity.HasMany(d => d.Roles).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "AspNetUserRole",
                    r => r.HasOne<AspNetRole>().WithMany().HasForeignKey("RoleId"),
                    l => l.HasOne<AspNetUser>().WithMany().HasForeignKey("UserId"),
                    j =>
                    {
                        j.HasKey("UserId", "RoleId");
                        j.ToTable("AspNetUserRoles");
                        j.HasIndex(new[] { "RoleId" }, "IX_AspNetUserRoles_RoleId");
                    });
        });

        modelBuilder.Entity<AspNetUserClaim>(entity =>
        {
            entity.HasIndex(e => e.UserId, "IX_AspNetUserClaims_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserClaims).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserLogin>(entity =>
        {
            entity.HasKey(e => new { e.LoginProvider, e.ProviderKey });

            entity.HasIndex(e => e.UserId, "IX_AspNetUserLogins_UserId");

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserLogins).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<AspNetUserToken>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.LoginProvider, e.Name });

            entity.HasOne(d => d.User).WithMany(p => p.AspNetUserTokens).HasForeignKey(d => d.UserId);
        });

        modelBuilder.Entity<Checklist>(entity =>
        {
            entity.HasKey(e => e.IDChecklist);

            entity.ToTable("Checklist");

            entity.Property(e => e.Descricao).HasMaxLength(500);
            entity.Property(e => e.Nome).HasMaxLength(200);
        });

        modelBuilder.Entity<ChecklistControlo>(entity =>
        {
            entity.HasKey(e => e.IDChecklistControlo);

            entity.ToTable("ChecklistControlo");

            entity.HasIndex(e => e.IDChecklist, "IX_ChecklistControlo_IDChecklist");

            entity.HasIndex(e => e.IDOrdemProducao, "IX_ChecklistControlo_IDOrdemProducao");

            entity.HasOne(d => d.IDChecklistNavigation).WithMany(p => p.ChecklistControlos).HasForeignKey(d => d.IDChecklist);

            entity.HasOne(d => d.IDOrdemProducaoNavigation).WithMany(p => p.ChecklistControlos).HasForeignKey(d => d.IDOrdemProducao);
        });

        modelBuilder.Entity<ChecklistEmbalagem>(entity =>
        {
            entity.HasKey(e => e.IDChecklistEmbalagem);

            entity.ToTable("ChecklistEmbalagem");

            entity.HasIndex(e => e.IDChecklist, "IX_ChecklistEmbalagem_IDChecklist");

            entity.HasIndex(e => e.IDOrdemProducao, "IX_ChecklistEmbalagem_IDOrdemProducao");

            entity.HasOne(d => d.IDChecklistNavigation).WithMany(p => p.ChecklistEmbalagems).HasForeignKey(d => d.IDChecklist);

            entity.HasOne(d => d.IDOrdemProducaoNavigation).WithMany(p => p.ChecklistEmbalagems).HasForeignKey(d => d.IDOrdemProducao);
        });

        modelBuilder.Entity<ChecklistModelo>(entity =>
        {
            entity.HasIndex(e => e.IDChecklist, "IX_ChecklistModelos_IDChecklist");

            entity.HasIndex(e => e.IDModelo, "IX_ChecklistModelos_IDModelo");

            entity.HasOne(d => d.IDChecklistNavigation).WithMany(p => p.ChecklistModelos).HasForeignKey(d => d.IDChecklist);

            entity.HasOne(d => d.IDModeloNavigation).WithMany(p => p.ChecklistModelos).HasForeignKey(d => d.IDModelo);
        });

        modelBuilder.Entity<ChecklistMontagem>(entity =>
        {
            entity.HasKey(e => e.IDChecklistMontagem);

            entity.ToTable("ChecklistMontagem");

            entity.HasIndex(e => e.IDChecklist, "IX_ChecklistMontagem_IDChecklist");

            entity.HasIndex(e => e.IDOrdemProducao, "IX_ChecklistMontagem_IDOrdemProducao");

            entity.HasOne(d => d.IDChecklistNavigation).WithMany(p => p.ChecklistMontagems).HasForeignKey(d => d.IDChecklist);

            entity.HasOne(d => d.IDOrdemProducaoNavigation).WithMany(p => p.ChecklistMontagems).HasForeignKey(d => d.IDOrdemProducao);
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.HasKey(e => e.IDCliente);
        });

        modelBuilder.Entity<Documento>(entity =>
        {
            entity.HasKey(e => e.IDDocumento);

            entity.ToTable("Documento");
        });

        modelBuilder.Entity<DocumentosModelo>(entity =>
        {
            entity.HasKey(e => e.IDDocumentosModelo);

            entity.ToTable("DocumentosModelo");

            entity.HasIndex(e => e.IDModelo, "IX_DocumentosModelo_IDModelo");
            entity.HasIndex(e => e.IDDocumento, "IX_DocumentosModelo_IDDocumento");

            // Relação correta: DocumentosModelo -> Modelo (IDModelo)
            entity.HasOne(d => d.Modelo)
                .WithMany(p => p.DocumentosModelos)
                .HasForeignKey(d => d.IDModelo)
                .OnDelete(DeleteBehavior.ClientSetNull);

            // Relação correta: DocumentosModelo -> Documento (IDDocumento)
            entity.HasOne(d => d.Documento)
                .WithMany(p => p.DocumentosModelos)
                .HasForeignKey(d => d.IDDocumento)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<Encomenda>(entity =>
        {
            entity.HasKey(e => e.IDEncomenda);

            entity.HasIndex(e => e.IDCliente, "IX_Encomendas_IDCliente");

            entity.HasIndex(e => e.IDModelo, "IX_Encomendas_IDModelo");

            entity.HasOne(d => d.IDClienteNavigation).WithMany(p => p.Encomenda).HasForeignKey(d => d.IDCliente);

            entity.HasOne(d => d.IDModeloNavigation).WithMany(p => p.Encomenda).HasForeignKey(d => d.IDModelo);
        });

        modelBuilder.Entity<ModeloPecasFixa>(entity =>
        {
            entity.HasKey(e => e.IDMPF);

            entity.HasIndex(e => e.IDModelo, "IX_ModeloPecasFixas_IDModelo");

            entity.HasIndex(e => e.IDPeca, "IX_ModeloPecasFixas_IDPeca");

            entity.Property(e => e.EspecificacaoPadrao).HasMaxLength(100);

            entity.HasOne(d => d.IDModeloNavigation).WithMany(p => p.ModeloPecasFixas).HasForeignKey(d => d.IDModelo);

            entity.HasOne(d => d.IDPecaNavigation).WithMany(p => p.ModeloPecasFixas).HasForeignKey(d => d.IDPeca);
        });

        modelBuilder.Entity<ModeloPecasSN>(entity =>
        {
            entity.HasKey(e => e.IDModeloPSN);

            entity.ToTable("ModeloPecasSN");

            entity.HasIndex(e => e.IDModelo, "IX_ModeloPecasSN_IDModelo");

            entity.HasIndex(e => e.IDPeca, "IX_ModeloPecasSN_IDPeca");

            entity.HasIndex(e => e.ModeloMotaIDModelo, "IX_ModeloPecasSN_ModeloMotaIDModelo");

            entity.Property(e => e.EspecificacaoPadrao).HasMaxLength(100);

            entity.HasOne(d => d.IDModeloNavigation).WithMany(p => p.ModeloPecasSNIDModeloNavigations)
                .HasForeignKey(d => d.IDModelo)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.IDPecaNavigation).WithMany(p => p.ModeloPecasSNs).HasForeignKey(d => d.IDPeca);

            entity.HasOne(d => d.ModeloMotaIDModeloNavigation).WithMany(p => p.ModeloPecasSNModeloMotaIDModeloNavigations).HasForeignKey(d => d.ModeloMotaIDModelo);
        });

        modelBuilder.Entity<ModelosMotum>(entity =>
        {
            entity.HasKey(e => e.IDModelo);
        });

        modelBuilder.Entity<Mota>(entity =>
        {
            entity.HasKey(e => e.IDMota);

            entity.HasIndex(e => e.IDModelo, "IX_Motas_IDModelo");

            entity.HasIndex(e => e.IDOrdemProducao, "IX_Motas_IDOrdemProducao").IsUnique();

            entity.Property(e => e.NumeroIdentificacao).HasDefaultValue("");

            entity.HasOne(d => d.IDModeloNavigation).WithMany(p => p.Mota).HasForeignKey(d => d.IDModelo);

            entity.HasOne(d => d.IDOrdemProducaoNavigation).WithOne(p => p.Mota).HasForeignKey<Mota>(d => d.IDOrdemProducao).IsRequired(false);
        });

        modelBuilder.Entity<MotasPecasInfo>(entity =>
        {
            entity.HasKey(e => e.IDMotasPecasInfo);

            entity.ToTable("MotasPecasInfo");

            entity.HasIndex(e => e.IDMota, "IX_MotasPecasInfo_IDMota");

            entity.HasIndex(e => e.IDPeca, "IX_MotasPecasInfo_IDPeca");

            entity.HasOne(d => d.IDMotaNavigation).WithMany(p => p.MotasPecasInfos).HasForeignKey(d => d.IDMota);

            entity.HasOne(d => d.IDPecaNavigation).WithMany(p => p.MotasPecasInfos).HasForeignKey(d => d.IDPeca);
        });

        modelBuilder.Entity<MotasPecasSN>(entity =>
        {
            entity.HasKey(e => e.IDMotasPecasSN);

            entity.ToTable("MotasPecasSN");

            entity.HasIndex(e => e.IDMota, "IX_MotasPecasSN_IDMota");

            entity.HasIndex(e => e.IDPeca, "IX_MotasPecasSN_IDPeca");

            entity.HasOne(d => d.IDMotaNavigation).WithMany(p => p.MotasPecasSNs).HasForeignKey(d => d.IDMota);

            entity.HasOne(d => d.IDPecaNavigation).WithMany(p => p.MotasPecasSNs).HasForeignKey(d => d.IDPeca);
        });

        modelBuilder.Entity<OrdemProducao>(entity =>
        {
            entity.HasKey(e => e.IDOrdemProducao);

            entity.ToTable("OrdemProducao");

            entity.HasIndex(e => e.ClienteIDCliente, "IX_OrdemProducao_ClienteIDCliente");

            entity.HasIndex(e => e.EncomendaIDEncomenda, "IX_OrdemProducao_EncomendaIDEncomenda");

            entity.HasIndex(e => e.IDEncomenda, "IX_OrdemProducao_IDEncomenda");

            entity.HasIndex(e => e.ModeloMotaIDModelo, "IX_OrdemProducao_ModeloMotaIDModelo");

            entity.HasOne(d => d.ClienteIDClienteNavigation).WithMany(p => p.OrdemProducaos).HasForeignKey(d => d.ClienteIDCliente);

            entity.HasOne(d => d.EncomendaIDEncomendaNavigation).WithMany(p => p.OrdemProducaoEncomendaIDEncomendaNavigations).HasForeignKey(d => d.EncomendaIDEncomenda);

            entity.HasOne(d => d.IDEncomendaNavigation).WithMany(p => p.OrdemProducaoIDEncomendaNavigations)
                .HasForeignKey(d => d.IDEncomenda)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.ModeloMotaIDModeloNavigation).WithMany(p => p.OrdemProducaos).HasForeignKey(d => d.ModeloMotaIDModelo);
        });

        modelBuilder.Entity<Peca>(entity =>
        {
            entity.HasKey(e => e.IDPeca);
        });

        modelBuilder.Entity<Servico>(entity =>
        {
            entity.HasKey(e => e.IDServico);

            entity.ToTable("Servico");

            entity.HasIndex(e => e.IDMota, "IX_Servico_IDMota");

            entity.HasOne(d => d.IDMotaNavigation).WithMany(p => p.Servicos).HasForeignKey(d => d.IDMota);
        });

        modelBuilder.Entity<ServicosPecasAlterada>(entity =>
        {
            entity.HasIndex(e => e.IDMotasPecasSN, "IX_ServicosPecasAlteradas_IDMotasPecasSN");

            entity.HasIndex(e => e.IDServico, "IX_ServicosPecasAlteradas_IDServico");

            entity.HasOne(d => d.IDMotasPecasSNNavigation).WithMany(p => p.ServicosPecasAlterada)
                .HasForeignKey(d => d.IDMotasPecasSN)
                .OnDelete(DeleteBehavior.ClientSetNull);

            entity.HasOne(d => d.IDServicoNavigation).WithMany(p => p.ServicosPecasAlterada)
                .HasForeignKey(d => d.IDServico)
                .OnDelete(DeleteBehavior.ClientSetNull);
        });

        modelBuilder.Entity<UtilizadorMotum>(entity =>
        {
            entity.HasKey(e => e.IDUtilizadorMota);

            entity.HasIndex(e => e.IDMota, "IX_UtilizadorMota_IDMota");

            entity.HasIndex(e => e.IdUtilizador, "IX_UtilizadorMota_IdUtilizador");

            entity.HasOne(d => d.IDMotaNavigation).WithMany(p => p.UtilizadorMota).HasForeignKey(d => d.IDMota);

            entity.HasOne(d => d.IdUtilizadorNavigation).WithMany(p => p.UtilizadorMota).HasForeignKey(d => d.IdUtilizador);
        });

        modelBuilder.Entity<Utilizadore>(entity =>
        {
            entity.HasKey(e => e.IdUtilizador);

            entity.Property(e => e.Nome).HasMaxLength(100);
            entity.Property(e => e.Telefone).HasDefaultValue("");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
