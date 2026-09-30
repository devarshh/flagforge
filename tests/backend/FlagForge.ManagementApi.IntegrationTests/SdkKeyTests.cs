using System.Net;
using FlagForge.Application.Common;
using FlagForge.Application.SdkKeys;
using FlagForge.Domain;
using FlagForge.Testing;
using Microsoft.EntityFrameworkCore;

namespace FlagForge.ManagementApi.IntegrationTests;

public sealed class SdkKeyTests(FlagForgeFixture fixture) : ManagementApiTest(fixture)
{
    [Fact]
    public async Task Plaintext_is_returned_once_and_only_its_hash_is_stored()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var keysUrl = $"/api/v1/projects/{project.Key}/environments/{Development}/sdk-keys";

        var created = await admin.CreateSdkKeyAsync(project.Key, Development, Ct);
        var listJson = await (await admin.Client.GetAsync(keysUrl, Ct)).Content.ReadAsStringAsync(Ct);
        var getJson = await (await admin.Client.GetAsync($"{keysUrl}/{created.Id}", Ct)).Content.ReadAsStringAsync(Ct);

        created.PlaintextKey.ShouldStartWith("ffk_");
        created.PlaintextKey.Length.ShouldBe(4 + 43);
        created.KeyPrefix.ShouldBe(created.PlaintextKey[..12]);
        listJson.ShouldNotContain(created.PlaintextKey);
        getJson.ShouldNotContain(created.PlaintextKey);
        listJson.ShouldContain(created.KeyPrefix);

        await using var db = Fixture.CreateDbContext();
        var stored = await db.SdkKeys.SingleAsync(k => k.Id == created.Id, Ct);
        stored.KeyHash.ShouldBe(Hashing.Sha256Hex(created.PlaintextKey));
        (await db.AuditEntries.SingleAsync(a => a.Action == AuditActions.SdkKeyCreated, Ct)).After!.Value.GetRawText().ShouldNotContain(created.PlaintextKey);
    }

    [Fact]
    public async Task Revoking_marks_the_key_and_publishes_to_every_evaluation_pod()
    {
        var admin = await AsAsync(Role.Admin);
        var project = await admin.CreateProjectAsync(cancellationToken: Ct);
        var created = await admin.CreateSdkKeyAsync(project.Key, Development, Ct);
        await using var recorder = await RedisRecorder.StartAsync<SdkKeyRevokedMessage>(Fixture.RedisConnectionString, Channels.SdkKeyRevoked);

        var response = await admin.Client.DeleteAsync($"/api/v1/projects/{project.Key}/environments/{Development}/sdk-keys/{created.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await recorder.WaitForAsync(m => m.SdkKeyId == created.Id, TimeSpan.FromSeconds(5), Ct);
        var keys = await (await admin.Client.GetAsync($"/api/v1/projects/{project.Key}/environments/{Development}/sdk-keys", Ct))
            .ReadJsonAsync<IReadOnlyList<SdkKeyResponse>>(Ct);
        keys.Single().RevokedAt.ShouldNotBeNull();
    }
}
