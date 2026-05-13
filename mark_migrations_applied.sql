-- Mark all migrations as applied to database
-- Run this script when database tables already exist but __EFMigrationsHistory is empty

INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES
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
