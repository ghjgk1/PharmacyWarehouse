USE [PharmacyWarehouse]
GO

-- =============================================
-- Очищаем старые тестовые данные
-- =============================================
DECLARE @OldInventoryId INT;
DECLARE @OldTrackedBatch1Id INT;
DECLARE @OldTrackedBatch2Id INT;
DECLARE @OldUntrackedBatch1Id INT;
DECLARE @OldUntrackedBatch2Id INT;
DECLARE @OldUntrackedBatch3Id INT;
DECLARE @OldTrackedProduct1Id INT;
DECLARE @OldTrackedProduct2Id INT;
DECLARE @OldUntrackedProduct1Id INT;
DECLARE @OldUntrackedProduct2Id INT;
DECLARE @OldUntrackedProduct3Id INT;
DECLARE @OldSupplierId INT;

-- Получаем ID старых сущностей
SELECT @OldInventoryId = Id FROM Inventories WHERE Number = 'INV-TEST-001';
SELECT @OldTrackedBatch1Id = Id FROM Batches WHERE Series = 'AMX-2024-001';
SELECT @OldTrackedBatch2Id = Id FROM Batches WHERE Series = 'IBP-2024-001';
SELECT @OldUntrackedBatch1Id = Id FROM Batches WHERE Series = 'PRC-2024-001';
SELECT @OldUntrackedBatch2Id = Id FROM Batches WHERE Series = 'ASP-2024-001';
SELECT @OldUntrackedBatch3Id = Id FROM Batches WHERE Series = 'NOS-2024-001';
SELECT @OldTrackedProduct1Id = Id FROM Products WHERE Gtin = '04601234000001';
SELECT @OldTrackedProduct2Id = Id FROM Products WHERE Gtin = '04601234000002';
SELECT @OldUntrackedProduct1Id = Id FROM Products WHERE Gtin = '04601234000003';
SELECT @OldUntrackedProduct2Id = Id FROM Products WHERE Gtin = '04601234000004';
SELECT @OldUntrackedProduct3Id = Id FROM Products WHERE Gtin = '04601234000005';

-- ОЧИЩАЕМ ДОКУМЕНТЫ С Notes LIKE '%Инвентаризация INV-TEST-001%'
DECLARE @DocIds TABLE (Id INT);
INSERT INTO @DocIds SELECT Id FROM Documents WHERE Notes LIKE '%Инвентаризация INV-TEST-001%';
-- Сначала удаляем связанные MdlpDocuments
DELETE FROM MdlpDocumentHistory WHERE MdlpDocumentId IN (SELECT Id FROM MdlpDocuments WHERE DocumentId IN (SELECT Id FROM @DocIds));
DELETE FROM MdlpDocuments WHERE DocumentId IN (SELECT Id FROM @DocIds);
-- Затем удаляем DocumentLines
DELETE FROM DocumentLines WHERE DocumentId IN (SELECT Id FROM @DocIds);
-- Затем удаляем сами документы
DELETE FROM Documents WHERE Id IN (SELECT Id FROM @DocIds);

-- Удаляем InventoryLines
IF @OldInventoryId IS NOT NULL
BEGIN
    DELETE FROM InventoryScannedCodes WHERE InventoryLineId IN (SELECT Id FROM InventoryLines WHERE InventoryId = @OldInventoryId);
    DELETE FROM InventoryLines WHERE InventoryId = @OldInventoryId;
    DELETE FROM Inventories WHERE Id = @OldInventoryId;
END

-- Удаляем MdlpSgtins
IF @OldTrackedBatch1Id IS NOT NULL
BEGIN
    DELETE FROM MdlpSgtins WHERE BatchId = @OldTrackedBatch1Id;
END
IF @OldTrackedBatch2Id IS NOT NULL
BEGIN
    DELETE FROM MdlpSgtins WHERE BatchId = @OldTrackedBatch2Id;
END

-- Удаляем Batches
IF @OldTrackedBatch1Id IS NOT NULL
BEGIN
    DELETE FROM Batches WHERE Id = @OldTrackedBatch1Id;
END
IF @OldTrackedBatch2Id IS NOT NULL
BEGIN
    DELETE FROM Batches WHERE Id = @OldTrackedBatch2Id;
END
IF @OldUntrackedBatch1Id IS NOT NULL
BEGIN
    DELETE FROM Batches WHERE Id = @OldUntrackedBatch1Id;
END
IF @OldUntrackedBatch2Id IS NOT NULL
BEGIN
    DELETE FROM Batches WHERE Id = @OldUntrackedBatch2Id;
END
IF @OldUntrackedBatch3Id IS NOT NULL
BEGIN
    DELETE FROM Batches WHERE Id = @OldUntrackedBatch3Id;
END

-- =============================================
-- 0. Создаем тестового поставщика
-- =============================================
DECLARE @SupplierId INT;

IF EXISTS (SELECT 1 FROM Suppliers WHERE Name = N'ЗАО «МедФарм»')
BEGIN
    SELECT @SupplierId = Id FROM Suppliers WHERE Name = N'ЗАО «МедФарм»';
END

-- =============================================
-- 1. Создаем тестовые товары
-- =============================================
DECLARE @TrackedProduct1Id INT;
DECLARE @TrackedProduct2Id INT;
DECLARE @UntrackedProduct1Id INT;
DECLARE @UntrackedProduct2Id INT;
DECLARE @UntrackedProduct3Id INT;

-- Товар 1 (маркированный)
IF NOT EXISTS (SELECT 1 FROM Products WHERE Gtin = '04601234000001')
BEGIN
    INSERT INTO Products (Name, Manufacturer, Gtin, IsTracked, MinRemainder, IsActive, ReleaseForm)
    VALUES ('Амоксициллин 500мг (маркированный)', 'ФармТест', '04601234000001', 1, 5, 1, N'Таблетки');
    SET @TrackedProduct1Id = SCOPE_IDENTITY();
END
ELSE
BEGIN
    SELECT @TrackedProduct1Id = Id FROM Products WHERE Gtin = '04601234000001';
END

-- Товар 2 (маркированный)
IF NOT EXISTS (SELECT 1 FROM Products WHERE Gtin = '04601234000002')
BEGIN
    INSERT INTO Products (Name, Manufacturer, Gtin, IsTracked, MinRemainder, IsActive, ReleaseForm)
    VALUES ('Ибупрофен 200мг (маркированный)', 'ФармТест', '04601234000002', 1, 3, 1, N'Капсулы');
    SET @TrackedProduct2Id = SCOPE_IDENTITY();
END
ELSE
BEGIN
    SELECT @TrackedProduct2Id = Id FROM Products WHERE Gtin = '04601234000002';
END

-- Товар 3 (немаркированный)
IF NOT EXISTS (SELECT 1 FROM Products WHERE Gtin = '04601234000003')
BEGIN
    INSERT INTO Products (Name, Manufacturer, Gtin, IsTracked, MinRemainder, IsActive, ReleaseForm)
    VALUES ('Парацетамол 500мг (немаркированный)', 'ФармТест', '04601234000003', 0, 10, 1, N'Таблетки');
    SET @UntrackedProduct1Id = SCOPE_IDENTITY();
END
ELSE
BEGIN
    SELECT @UntrackedProduct1Id = Id FROM Products WHERE Gtin = '04601234000003';
END

-- Товар 4 (немаркированный)
IF NOT EXISTS (SELECT 1 FROM Products WHERE Gtin = '04601234000004')
BEGIN
    INSERT INTO Products (Name, Manufacturer, Gtin, IsTracked, MinRemainder, IsActive, ReleaseForm)
    VALUES ('Аспирин 100мг (немаркированный)', 'ФармТест', '04601234000004', 0, 15, 1, N'Таблетки');
    SET @UntrackedProduct2Id = SCOPE_IDENTITY();
END
ELSE
BEGIN
    SELECT @UntrackedProduct2Id = Id FROM Products WHERE Gtin = '04601234000004';
END

-- Товар 5 (немаркированный)
IF NOT EXISTS (SELECT 1 FROM Products WHERE Gtin = '04601234000005')
BEGIN
    INSERT INTO Products (Name, Manufacturer, Gtin, IsTracked, MinRemainder, IsActive, ReleaseForm)
    VALUES ('Но-шпа 40мг (немаркированный)', 'ФармТест', '04601234000005', 0, 8, 1, N'Таблетки');
    SET @UntrackedProduct3Id = SCOPE_IDENTITY();
END
ELSE
BEGIN
    SELECT @UntrackedProduct3Id = Id FROM Products WHERE Gtin = '04601234000005';
END

-- =============================================
-- 2. Создаем тестовые партии
-- =============================================
DECLARE @TrackedBatch1Id INT;
DECLARE @TrackedBatch2Id INT;
DECLARE @UntrackedBatch1Id INT;
DECLARE @UntrackedBatch2Id INT;
DECLARE @UntrackedBatch3Id INT;

-- Партия для товара 1
INSERT INTO Batches (ProductId, Series, ExpirationDate, ArrivalDate, Quantity, PurchasePrice, SellingPrice, IsActive, SupplierId)
VALUES (@TrackedProduct1Id, 'AMX-2024-001', DATEADD(YEAR, 2, GETDATE()), GETDATE(), 10, 150.00, 225.00, 1, @SupplierId);
SET @TrackedBatch1Id = SCOPE_IDENTITY();

-- Партия для товара 2
INSERT INTO Batches (ProductId, Series, ExpirationDate, ArrivalDate, Quantity, PurchasePrice, SellingPrice, IsActive, SupplierId)
VALUES (@TrackedProduct2Id, 'IBP-2024-001', DATEADD(YEAR, 1, GETDATE()), GETDATE(), 5, 80.00, 120.00, 1, @SupplierId);
SET @TrackedBatch2Id = SCOPE_IDENTITY();

-- Партия для товара 3
INSERT INTO Batches (ProductId, Series, ExpirationDate, ArrivalDate, Quantity, PurchasePrice, SellingPrice, IsActive, SupplierId)
VALUES (@UntrackedProduct1Id, 'PRC-2024-001', DATEADD(YEAR, 3, GETDATE()), GETDATE(), 20, 100.00, 150.00, 1, @SupplierId);
SET @UntrackedBatch1Id = SCOPE_IDENTITY();

-- Партия для товара 4
INSERT INTO Batches (ProductId, Series, ExpirationDate, ArrivalDate, Quantity, PurchasePrice, SellingPrice, IsActive, SupplierId)
VALUES (@UntrackedProduct2Id, 'ASP-2024-001', DATEADD(YEAR, 4, GETDATE()), GETDATE(), 25, 50.00, 75.00, 1, @SupplierId);
SET @UntrackedBatch2Id = SCOPE_IDENTITY();

-- Партия для товара 5
INSERT INTO Batches (ProductId, Series, ExpirationDate, ArrivalDate, Quantity, PurchasePrice, SellingPrice, IsActive, SupplierId)
VALUES (@UntrackedProduct3Id, 'NOS-2024-001', DATEADD(YEAR, 2, GETDATE()), GETDATE(), 12, 120.00, 180.00, 1, @SupplierId);
SET @UntrackedBatch3Id = SCOPE_IDENTITY();

-- =============================================
-- 3. Создаем DataMatrix коды для маркированных товаров
-- =============================================
-- Для товара 1 (Амоксициллин): SGTIN = GTIN (13 digits) + Serial (13 digits)
INSERT INTO MdlpSgtins (Sgtin, BatchId, Status, CreatedAt)
VALUES 
('046012340000011000000000001', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000002', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000003', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000004', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000005', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000006', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000007', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000008', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000009', @TrackedBatch1Id, 'InCirculation', GETDATE()),
('046012340000011000000000010', @TrackedBatch1Id, 'InCirculation', GETDATE());

-- Для товара 2 (Ибупрофен)
INSERT INTO MdlpSgtins (Sgtin, BatchId, Status, CreatedAt)
VALUES 
('046012340000021000000000001', @TrackedBatch2Id, 'InCirculation', GETDATE()),
('046012340000021000000000002', @TrackedBatch2Id, 'InCirculation', GETDATE()),
('046012340000021000000000003', @TrackedBatch2Id, 'InCirculation', GETDATE()),
('046012340000021000000000004', @TrackedBatch2Id, 'InCirculation', GETDATE()),
('046012340000021000000000005', @TrackedBatch2Id, 'InCirculation', GETDATE());

-- =============================================
-- 4. Создаем тестовый акт инвентаризации
-- =============================================
DECLARE @InventoryId INT;
DECLARE @DateNow DATE = CAST(GETDATE() AS DATE);
DECLARE @InventoryNumber NVARCHAR(50) = 'INV-TEST-001';

INSERT INTO Inventories (Number, InventoryDate, Status, CreatedBy, CreatedAt)
VALUES (@InventoryNumber, @DateNow, 'Draft', 'Тестовый пользователь', GETDATE());
SET @InventoryId = SCOPE_IDENTITY();

-- =============================================
-- 5. Добавляем 5 строк в акт инвентаризации
-- =============================================
DECLARE @Line1Id INT;
DECLARE @Line2Id INT;

-- Строка 1: Маркированный товар, НЕДОСТАТК (ожидаем 10, фактически 7 > разница -3)
INSERT INTO InventoryLines (InventoryId, BatchId, ExpectedQuantity, ActualQuantity, Notes)
VALUES (@InventoryId, @TrackedBatch1Id, 10, 7, 'Тест: Недостаток');
SET @Line1Id = SCOPE_IDENTITY();

-- Строка 2: Маркированный товар, ИЗЛИШЕК (ожидаем 5, фактически 7 > разница +2)
INSERT INTO InventoryLines (InventoryId, BatchId, ExpectedQuantity, ActualQuantity, Notes)
VALUES (@InventoryId, @TrackedBatch2Id, 5, 5, 'Тест: Излишек');
SET @Line2Id = SCOPE_IDENTITY();

-- Строка 3: Немаркированный товар, РАВНО (ожидаем 20, фактически 20)
INSERT INTO InventoryLines (InventoryId, BatchId, ExpectedQuantity, ActualQuantity, Notes)
VALUES (@InventoryId, @UntrackedBatch1Id, 20, 20, 'Тест: Совпадение');

-- Строка 4: Немаркированный товар, НЕДОСТАТК (ожидаем 25, фактически 20 > разница -5)
INSERT INTO InventoryLines (InventoryId, BatchId, ExpectedQuantity, ActualQuantity, Notes)
VALUES (@InventoryId, @UntrackedBatch2Id, 25, 20, 'Тест: Недостаток');

-- Строка 5: Немаркированный товар, ИЗЛИШЕК (ожидаем 12, фактически 15 > разница +3)
INSERT INTO InventoryLines (InventoryId, BatchId, ExpectedQuantity, ActualQuantity, Notes)
VALUES (@InventoryId, @UntrackedBatch3Id, 12, 15, 'Тест: Излишек');

-- =============================================
-- 6. Добавляем тестовые InventoryScannedCodes!
-- =============================================
INSERT INTO InventoryScannedCodes (InventoryLineId, Sgtin)
VALUES
(@Line1Id, '046012340000011000000000001'),
(@Line1Id, '046012340000011000000000002'),
(@Line1Id, '046012340000011000000000003'),
(@Line1Id, '046012340000011000000000004'),
(@Line1Id, '046012340000011000000000005'),
(@Line1Id, '046012340000011000000000006'),
(@Line1Id, '046012340000011000000000007');

INSERT INTO InventoryScannedCodes (InventoryLineId, Sgtin)
VALUES
(@Line2Id, '046012340000021000000000001'),
(@Line2Id, '046012340000021000000000002'),
(@Line2Id, '046012340000021000000000003'),
(@Line2Id, '046012340000021000000000004'),
(@Line2Id, '046012340000021000000000005');

-- =============================================
-- Выводим результат для тестирования
-- =============================================
PRINT '=============================================';
PRINT 'Тестовые данные созданы!';
PRINT '=============================================';
PRINT 'Акт инвентаризации: ' + @InventoryNumber + ' (ID: ' + CAST(@InventoryId AS NVARCHAR(10)) + ')';
PRINT '';
PRINT 'DataMatrix коды для маркированных товаров:';
PRINT '- Амоксициллин 500мг: 010460123400000121100000000001, 01046012340000012110000000000010, ...';
PRINT '- Ибупрофен 200мг: 010460123400000221100000000001, 010460123400000221100000000002, ...';
PRINT '';
PRINT 'Тестовые сканированные коды добавлены!';
PRINT '=============================================';
GO