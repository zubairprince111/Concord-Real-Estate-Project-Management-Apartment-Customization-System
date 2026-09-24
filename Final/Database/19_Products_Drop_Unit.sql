
-- USE Concord_Practicum;
GO

-- Unit (e.g. SqFt/Gallon/Piece) is gone from the Add Product form and every
-- customization-related display -- it never factored into any calculation
-- (ExtraCost is a flat lump-sum, never multiplied by Unit/quantity), so it was
-- pure clutter. No DEFAULT constraint on this column (unlike Quantity/HasParking
-- in earlier drops), so a plain drop is enough.
ALTER TABLE Products DROP COLUMN Unit;
GO
