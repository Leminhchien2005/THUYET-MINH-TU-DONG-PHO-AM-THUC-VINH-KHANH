# PowerShell script để apply migrations và mark history

$connectionString = "Server=maglev.proxy.rlwy.net;Port=55832;Database=railway;Uid=root;Pwd=LXSpLFmhVEXTJWohkcvWfvUOTtFHMYsy;"

Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "Database Maintenance & Migration Setup" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host ""

# Step 1: Check existing tables
Write-Host "Step 1: Kiểm tra tables hiện tại..." -ForegroundColor Yellow
$testQuery = @"
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA='railway'
ORDER BY TABLE_NAME;
"@

# Step 2: Mark migrations as applied
Write-Host "Step 2: Mark migrations as applied..." -ForegroundColor Yellow
$sqlScript = @"
-- Insert migration history if not exists
INSERT IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
('20260302002619_Init', '10.0.0'),
('20260308094940_IdentityInit', '10.0.0'),
('20260310000503_AddFullName', '10.0.0'),
('20260310001913_AddOwnerToPoi', '10.0.0'),
('20260310005900_InitIdentity', '10.0.0'),
('20260310030328_AddPoiStatus', '10.0.0'),
('20260311193901_AddPoiRequest', '10.0.0'),
('20260311195507_FixRadiusType', '10.0.0'),
('20260312080228_AddCreatedAtToPoiRequest', '10.0.0'),
('20260314043223_AddRejectReason', '10.0.0'),
('20260319003838_AddFoodTable', '10.0.0'),
('20260510AddNarrationLogs', '10.0.0'),
('20260511AddAudioAndOnlineTablesIfNotExists', '10.0.0');
"@

# Step 3: Check migration history
Write-Host "Step 3: Verify migrations..." -ForegroundColor Yellow
$verifyQuery = @"
SELECT MigrationId, ProductVersion FROM __EFMigrationsHistory ORDER BY MigrationId;
"@

Write-Host ""
Write-Host "✅ Script ready. Run the following steps in FoodStreetWeb directory:" -ForegroundColor Green
Write-Host ""
Write-Host "1. Mark existing migrations:" -ForegroundColor Cyan
Write-Host "   cd ..\FoodStreetWeb" -ForegroundColor White
Write-Host "   dotnet ef database update 0" -ForegroundColor White
Write-Host ""
Write-Host "2. Apply all migrations (including new ones):" -ForegroundColor Cyan
Write-Host "   dotnet ef database update" -ForegroundColor White
Write-Host ""
Write-Host "════════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "Or use MySQL directly to mark migrations:" -ForegroundColor Yellow
Write-Host ""
Write-Host "Replace with: " -ForegroundColor Cyan -NoNewline
Write-Host "mark_migrations_applied.sql" -ForegroundColor White
Write-Host ""
