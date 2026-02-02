-- Create tables
CREATE TABLE Users (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(100),
    Email VARCHAR(100),
    Status VARCHAR(20),
    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE Orders (
    Id SERIAL PRIMARY KEY,
    UserId INT REFERENCES Users(Id),
    Total DECIMAL(18, 2),
    OrderDate TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Seed data
INSERT INTO Users (Name, Email, Status) VALUES 
('Postgres User', 'pg@example.com', 'Active'),
('Jane Doe', 'jane@example.com', 'Inactive'),
('Bob Smith', 'bob@example.com', 'Active');

INSERT INTO Orders (UserId, Total) VALUES 
(1, 150.00),
(1, 250.00),
(2, 50.00);

-- Create simple function (analogous to procedure for testing)
CREATE OR REPLACE FUNCTION get_user_by_id(user_id INT)
RETURNS TABLE (id INT, name VARCHAR, email VARCHAR) AS $$
BEGIN
    RETURN QUERY SELECT u.Id, u.Name, u.Email FROM Users u WHERE u.Id = user_id;
END;
$$ LANGUAGE plpgsql;
