# Stage 2 mapping profiles

Runtime remains on appsettings. No resolver registration or processing code changes.

## DDL

| Table | Key / purpose |
|---|---|
| MappingProfiles | bigint identity ProfileId; unique ProfileCode; positive ProfileVersion; BL/MANIFEST; LegacyFallback/ModelProfile; four structural names; SealedAtUtc; CreatedAtUtc/CreatedBy (ORIGINAL_LOGIN)/ChangeReference |
| MappingFields | PK (ProfileId, MapKind, CanonicalKey); FK profile; Field/Header/Bill/Container; AzureName |
| ModelMappingBindings | ModelId PK/FK ModelRegistry; ProfileId FK profile; permanent from insertion |

No cascading deletes. Canonical keys are 1?64 ASCII identifier characters, beginning with a letter. Azure/structural names are 1?128 ASCII letters/digits/underscore/dot/hyphen. Binary character checks exclude spaces/control characters. Maximum 256 fields per profile; no display tables or generic container fields.

## Seed and fingerprints

Frozen SQL contains complete effective maps, including constructor defaults. The embedded JSON is the corresponding pure-test fixture, never read from appsettings at migration time. SQL tests independently read actual SQL headers/rows and hydrate them.

| Existing producing model | ProfileCode / shape | Expected SHA-256 |
|---|---|---|
| bl-maersk-v1 | bl-maersk-v1 / ModelProfile | 5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556 |
| bl-v2 | bl-v2 / ModelProfile | 5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556 |
| bl-v3 | bl-v3 / ModelProfile | 5ff362b2805995e4fc1dc3fcceafb95a6cb3d93cffdba491a402a6d93af6f556 |
| manifest-v1 | manifest-v1 / ModelProfile | 87aa5155640c0e10dc2379621e36c290a7bc16905d6e5f201cd55466f3a8ef2d |
| manifest-v2 | manifest-v2 / ModelProfile | 9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480 |
| manifest-v3 | manifest-v3 / ModelProfile | 9183bc0d1e3bfe8aa0f5be24490e422f2a0d022bc5b6e97a1b8095feb560b480 |
| Global template (unbound) | legacy / LegacyFallback | c9a1b76d2f79ff08355d51d2d347238d803d5370f8c9a21988b27ba578ed0d17 |
| Other existing BL extractor | producing ModelId / LegacyFallback copy | c9a1b76d2f79ff08355d51d2d347238d803d5370f8c9a21988b27ba578ed0d17 |

The migration binds only existing matching extract registry rows. All seven templates exist even on an empty registry. Classifiers are not bound. Unknown MANIFEST models require explicit reviewed registration before stage 3/4 cutover.

## Enforcement

- Profile and field contents can be edited while unsealed. Bindings cannot be updated/deleted, including drafts.
- Existing activated/Active or recorded-use bindings are sealed during migration.
- Registry activation (including direct Active insert/update) seals the bound profile; missing activation timestamps are recorded. Recorded Jobs.ModelId, AzureOperations.ModelId and UsageLedger.ModelId seal profiles, including binding after historical use. JobRuns and JobResults have no independent producing-model column.
- Sealing persists after retirement or deletion/retention of processing rows. ActivatedAtUtc cannot be cleared once recorded.
- Profile UPDATE/DELETE and field INSERT/UPDATE/DELETE are rejected after sealing. All affected parents are locked; registry locks serialize usage with late binding. A deadlock rolls back one transaction; retry the whole operation, never a partial edit.
- Runtime has SELECT plus explicit mutation DENYs on all three tables. Model-team has INSERT/SELECT, draft profile/field UPDATE/DELETE, and binding UPDATE/DELETE DENYs. SealedAtUtc UPDATE is denied to model-team. Triggers execute through the dbo ownership chain.
- Bound document type and extract kind must agree. Switching a draft to LegacyFallback cannot strand Header/Bill rows.

## Activation guard deferred to stage 5

The **binding-required activation guard is deliberately absent**, including for newly registered extractors. deploy/_bl-activation.ps1 and deploy/_router-activation.ps1 use existing S14; on-prem 14/15 register/activate without creating mappings. Requiring a binding now would break those scripts and their SQL tests while runtime still uses appsettings. No activation scripts, S14 statements or accuracy gates were changed. Stage 5 must generate/register/bind atomically before enforcing the guard and rehearse these scripts/tests. Until then newly registered unbound models remain possible by design.

## Test handoff

MappingProfilesTests contains offline hash/script tests and disposable SQL tests for fresh migration, all seed fingerprints, Local/IIS-like registries, active seeds, implicit BL copies, sealing, role permissions, historical/late binding, invalid keys/limits and concurrent activation/edit. Every SQL fixture is disposed/dropped, including failure paths. SQL tests were authored but not executed by Codex (no database authorization).

Baseline parity and catalog export explicitly target 20261101000000_InitialSchema. Adoption still validates the unchanged baseline and then migrates to latest. InitialSchema migration, frozen SQL/resources and catalog were not regenerated.

Offline commands use NUGET_PACKAGES=D:\unifret\DocAi\.packages. Restore uses only that local directory as its source, with NuGetAudit=false; no package references were added. Build/test output remains in bin/obj.

Pure filter: Category!=Sql&Category!=SqlIntegration&Category!=Machine&FullyQualifiedName!~WebhookTransportTests&FullyQualifiedName!~CertificateIdentityTests&FullyQualifiedName!~DocAi.Api.Tests

The initial broad non-SQL test run accidentally included untagged loopback HTTP/TLS and machine certificate/ACL tests. It was stopped; subsequent verification uses the explicit offline filter above. No SQL tests or external service calls were intentionally run. Certificate/ACL failures from the initial run are environment-dependent and were not changed.
