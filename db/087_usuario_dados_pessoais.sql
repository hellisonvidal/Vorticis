-- Configurações › Usuários: dados pessoais e de contato de cada usuário do escritório (o CPF já existia em core.usuario).
-- Todos opcionais; o e-mail e o telefone servem para contato e avisos, a admissão e o cargo para o controle interno.

ALTER TABLE core.usuario
    ADD COLUMN telefone      VARCHAR(20),
    ADD COLUMN celular       VARCHAR(20),
    ADD COLUMN email         VARCHAR(150),
    ADD COLUMN data_admissao DATE,
    ADD COLUMN cargo         VARCHAR(80),
    ADD COLUMN departamento  VARCHAR(80),
    ADD COLUMN observacao    VARCHAR(500);

ALTER TABLE core.usuario
    ADD CONSTRAINT usuario_cpf_digitos CHECK (cpf IS NULL OR cpf ~ '^[0-9]{11}$');
