-- =============================================
-- Create date: 31-12-2025
-- Description: Dummy view for demonstration purposes
-- Version:     1
-- =============================================
CREATE OR ALTER VIEW dbo.vw_DummyView
AS
SELECT 
    1 AS Id,
    'Dummy' AS Name,
    GETUTCDATE() AS CreatedAt;
