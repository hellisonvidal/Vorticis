-- Configurações › Usuários: módulos que cada usuário pode usar (Contábil, Fiscal, Folha, Escritório, Gerencial, Condomínio).
-- Soma dos bits de ModulosEmpresa (1 Contábil, 2 Fiscal, 4 Folha, 8 Escritório, 16 Gerencial, 32 Condomínio, 64 Frota).
-- NULL = todos os módulos (os usuários que já existiam continuam com acesso a todos). Administradores acessam sempre todos.
-- Num módulo bloqueado o usuário não vê nenhuma empresa (a lista de empresas do módulo já filtra no banco).

ALTER TABLE core.usuario
    ADD COLUMN modulos SMALLINT CHECK (modulos IS NULL OR modulos BETWEEN 0 AND 127);
