using Calendare.Server.Utils;

namespace Server.Tests;

public class StringCryptorTest
{
    private static StringCryptor Create()
    {
        var sc = new StringCryptor("a secret passphrase");
        Assert.NotNull(sc);
        return sc;
    }

    [Fact]
    public static void CreateCryptor()
    {
        var _ = Create();
    }

    private static async Task<string> Base64Roundtrip(StringCryptor sc, string cleartext, CancellationToken ct)
    {
        var ciphertext = await sc.EncryptBase64Async(cleartext, ct);
        Assert.NotEmpty(ciphertext);
        var cleartextCheck = await sc.DecryptBase64Async(ciphertext, ct);
        Assert.Equal(cleartext, cleartextCheck);
        return ciphertext;
    }

    private static async Task<string> Base64UrlRoundtrip(StringCryptor sc, string cleartext, CancellationToken ct)
    {
        var ciphertext = await sc.EncryptBase64UrlAsync(cleartext, ct);
        Assert.NotEmpty(ciphertext);
        var cleartextCheck = await sc.DecryptBase64UrlAsync(ciphertext, ct);
        Assert.Equal(cleartext, cleartextCheck);
        return ciphertext;
    }

    [Theory]
    [InlineData("i'm a simple text!")]
    [InlineData("")]
    [InlineData("As90lKiH5BEHdPqUa7LjHjPcPC8QwJ64douYn0mTTNnbgA3JTuchTHqAHv+XGeJffQhYltkhXkGvv4BDUyzfcOz4PoTvCd/7KHIiV2tuASWPDtt6Wg0yQDxjroWiX1BfvKyg+Xh8JRTEqxa55PPJX7umI0n3iLI4aFTdO33iCtRm")]
    public async Task EncryptBase64(string cleartext)
    {
        var ct = CancellationToken.None;

        var sc1 = Create();
        var ciphertext1 = await Base64Roundtrip(sc1, cleartext, ct);
        Assert.False(string.IsNullOrEmpty(ciphertext1));

        var sc2 = Create();
        var ciphertext2 = await Base64Roundtrip(sc2, cleartext, ct);
        Assert.False(string.IsNullOrEmpty(ciphertext2));

        Assert.NotEqual(ciphertext1, ciphertext2);
        var cleartextCheck2 = await sc2.DecryptBase64Async(ciphertext1, ct);
        Assert.Equal(cleartext, cleartextCheck2);
        var cleartextCheck1 = await sc1.DecryptBase64Async(ciphertext2, ct);
        Assert.Equal(cleartext, cleartextCheck1);
    }

    [Theory]
    [InlineData("i'm a simple text!")]
    [InlineData("")]
    [InlineData("As90lKiH5BEHdPqUa7LjHjPcPC8QwJ64douYn0mTTNnbgA3JTuchTHqAHv+XGeJffQhYltkhXkGvv4BDUyzfcOz4PoTvCd/7KHIiV2tuASWPDtt6Wg0yQDxjroWiX1BfvKyg+Xh8JRTEqxa55PPJX7umI0n3iLI4aFTdO33iCtRm")]
    public async Task EncryptBase64Url(string cleartext)
    {
        var ct = CancellationToken.None;

        var sc1 = Create();
        var ciphertext1 = await Base64UrlRoundtrip(sc1, cleartext, ct);
        Assert.False(string.IsNullOrEmpty(ciphertext1));

        var sc2 = Create();
        var ciphertext2 = await Base64UrlRoundtrip(sc2, cleartext, ct);
        Assert.False(string.IsNullOrEmpty(ciphertext2));

        Assert.NotEqual(ciphertext1, ciphertext2);
        var cleartextCheck2 = await sc2.DecryptBase64UrlAsync(ciphertext1, ct);
        Assert.Equal(cleartext, cleartextCheck2);
        var cleartextCheck1 = await sc1.DecryptBase64UrlAsync(ciphertext2, ct);
        Assert.Equal(cleartext, cleartextCheck1);
    }

    [Theory]
    [InlineData("i'm a simple text!", false)]
    [InlineData("", true)]
    [InlineData(@"IYedyF9LUuCwhe6AUNo3gJCIwd1KtCCsTGD0sAnX07ZJud-9B-VSXqzvAq_nT7kJ6tjgE8VRPEEitMczvG54raBOgKV_Sr3uU_ynajsEFieVcax8z7HuGdqV8LgSwKaLSj6OsCVnJo7i5lo7W4rqDU9fr54gDf-4HQefaTfpyV_Bxp8V_-6nhOlLZmlAWY-UDfByBRmbF0BtqHlzDgCuONNmn-H0vYrERGZBS5JyQ0kqojwU39lmP4IezRCnRkLb", true)]
    [InlineData(@"IYedyF9LUuCwhe6AUNo3gJCIwd1KtCCsTGD0sAnX07ZJud-9B-VSXqzvAq_nT7kJ6tjgE8VRPEEitMczvG54raBOgKV_Sr3uU_ynajsEFieVcax8z7HuGdqV8LgSwKaLSj6OsCVnJo7i5lo7W4rqDU9fr54gDf-4HQefaTfpyV_Bxp8V_-6nhOlLZmlAWY-UDfByBRmbF0BtqHlzDgCuONNmn-H0vYrERGZBS5JyQ0kqojwU39lmP4IezRCnRk", false)]
    public async Task TryDecryptBase64Url(string ciphertext, bool isValid)
    {
        var ct = CancellationToken.None;

        var sc1 = Create();
        var (success, cleartext) = await sc1.TryDecryptBase64UrlAsync(ciphertext, ct);
        Assert.Equal(isValid, success);
        if (success)
        {
            Assert.NotNull(cleartext);
        }
        else
        {
            Assert.Null(cleartext);
        }
    }
}
