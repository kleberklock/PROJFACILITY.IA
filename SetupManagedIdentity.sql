-- =========================================================================
-- Script de Configuração de Permissões para Managed Identity no Azure SQL
-- =========================================================================
-- IMPORTANTE: 
-- 1. Execute este script logado no banco de dados "FacilityDB"
-- 2. Você DEVE estar logado no Azure SQL usando um administrador do Entra ID 
--    (Azure Active Directory Admin), não com o usuário e senha tradicionais.
-- 3. Substitua "[NOME_DO_SEU_APP_SERVICE]" pelo nome exato do seu App Service 
--    no Azure (exatamente como aparece no portal, que é a sua Identidade Gerida).

-- 1. Cria o usuário associado à Identidade Gerida do seu App Service
CREATE USER [NOME_DO_SEU_APP_SERVICE] FROM EXTERNAL PROVIDER;
GO

-- 2. Concede permissão de LEITURA de dados
ALTER ROLE db_datareader ADD MEMBER [NOME_DO_SEU_APP_SERVICE];
GO

-- 3. Concede permissão de ESCRITA de dados (INSERT, UPDATE, DELETE)
ALTER ROLE db_datawriter ADD MEMBER [NOME_DO_SEU_APP_SERVICE];
GO

-- 4. Concede permissão para ALTERAR O ESQUEMA (criar/alterar tabelas para o EF Migrations)
ALTER ROLE db_ddladmin ADD MEMBER [NOME_DO_SEU_APP_SERVICE];
GO
