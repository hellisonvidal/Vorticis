-- =============================================================================
-- 075 — Logo da empresa (compartilhada por todos os módulos) para o cabeçalho dos relatórios.
--   * core.empresa_logo: imagem PNG/JPG já redimensionada (até 512 KB) e a opção "Usar a logo nos relatórios".
-- =============================================================================
CREATE TABLE core.empresa_logo (
    empresa_id          BIGINT       PRIMARY KEY REFERENCES core.empresa(id) ON DELETE CASCADE,
    imagem              BYTEA        NOT NULL,
    usar_nos_relatorios BOOLEAN      NOT NULL DEFAULT TRUE,
    atualizado_em       TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ck_empresa_logo_tamanho CHECK (octet_length(imagem) BETWEEN 8 AND 524288)
);
