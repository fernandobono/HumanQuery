-- Crea un usuario que SOLO puede leer Northwind.
-- La app ejecuta SQL escrito por una IA: con este usuario, un DELETE o un UPDATE es rechazado por la base.
-- Ejecutar conectado como sa (o un administrador).

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = 'ia_lector')
    CREATE LOGIN ia_lector WITH PASSWORD = 'IaLector#2026', CHECK_POLICY = OFF, DEFAULT_DATABASE = Northwind;
GO

USE Northwind;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = 'ia_lector')
    CREATE USER ia_lector FOR LOGIN ia_lector;
GO

ALTER ROLE db_datareader ADD MEMBER ia_lector;
GO

-- Prueba: esto tiene que dar error de permisos si el usuario quedó bien
-- EXECUTE AS LOGIN = 'ia_lector'; DELETE FROM Customers WHERE 1 = 0; REVERT;
