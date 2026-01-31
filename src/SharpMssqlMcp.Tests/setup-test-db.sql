USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'SharpMssqlMcpTest')
BEGIN
    CREATE DATABASE SharpMssqlMcpTest;
END
GO

USE SharpMssqlMcpTest;
GO

-- Create Users table
IF OBJECT_ID('Users', 'U') IS NOT NULL DROP TABLE Users;
CREATE TABLE Users (
    Id INT PRIMARY KEY IDENTITY(1,1),
    Name NVARCHAR(100),
    Email NVARCHAR(100),
    Status NVARCHAR(20),
    CreatedAt DATETIME2 DEFAULT GETDATE()
);

-- Insert sample users
INSERT INTO Users (Name, Email, Status) VALUES 
('John Doe', 'john@example.com', 'Active'),
('Jane Smith', 'jane@example.com', 'Active'),
('Bob Johnson', 'bob@example.com', 'Inactive'),
('Alice Brown', 'alice@example.com', 'Active'),
('Charlie Davis', 'charlie@example.com', 'Pending');

-- Create Orders table
IF OBJECT_ID('Orders', 'U') IS NOT NULL DROP TABLE Orders;
CREATE TABLE Orders (
    OrderId INT PRIMARY KEY IDENTITY(1001,1),
    UserId INT FOREIGN KEY REFERENCES Users(Id),
    OrderDate DATETIME2 DEFAULT GETDATE(),
    Total DECIMAL(18,2),
    Status NVARCHAR(20)
);

-- Insert sample orders
INSERT INTO Orders (UserId, Total, Status) VALUES 
(1, 150.50, 'Pending'),
(1, 200.00, 'Shipped'),
(2, 75.25, 'Pending'),
(4, 300.00, 'Delivered'),
(2, 50.00, 'Pending');

GO

-- Create stored procedures
IF OBJECT_ID('sp_GetUserById', 'P') IS NOT NULL DROP PROCEDURE sp_GetUserById;
GO
CREATE PROCEDURE sp_GetUserById
    @id INT
AS
BEGIN
    SELECT * FROM Users WHERE Id = @id;
END
GO

IF OBJECT_ID('sp_GetUserOrders', 'P') IS NOT NULL DROP PROCEDURE sp_GetUserOrders;
GO
CREATE PROCEDURE sp_GetUserOrders
    @userId INT,
    @status NVARCHAR(20) = NULL
AS
BEGIN
    -- Result Set 1: User Info
    SELECT Id, Name, Email FROM Users WHERE Id = @userId;

    -- Result Set 2: Orders Info
    SELECT OrderId, OrderDate, Total, Status 
    FROM Orders 
    WHERE UserId = @userId AND (@status IS NULL OR Status = @status);
END
GO

IF OBJECT_ID('sp_GetUserCount', 'P') IS NOT NULL DROP PROCEDURE sp_GetUserCount;
GO
CREATE PROCEDURE sp_GetUserCount
    @status NVARCHAR(20),
    @count INT OUTPUT
AS
BEGIN
    SELECT @count = COUNT(*) FROM Users WHERE Status = @status;
END
GO
