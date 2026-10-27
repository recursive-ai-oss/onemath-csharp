using System;
using System.Text;
using RecursiveAI.OneMath.HashMatrix;
using Xunit;

namespace RecursiveAI.OneMath.Tests;

public class HashMatrixTests
{
    [Fact]
    public void Registered_scenarios_include_meta_and_are_sorted()
    {
        var all = Scenarios.All();
        Assert.Contains(all, s => s.Name == "meta/bits");
        for (int i = 1; i < all.Length; i++)
        {
            Assert.True(string.CompareOrdinal(all[i - 1].Name, all[i].Name) < 0);
        }
    }

    [Fact]
    public void Sha256_matches_fips_known_answers()
    {
        // Public FIPS 180-4 examples: "abc" and the 448-bit message.
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(Encoding.ASCII.GetBytes("abc"))));
        var ctx = new ScenarioContext(1);
        ctx.Write(Encoding.ASCII.GetBytes("abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq"));
        Assert.Equal("248d6a61d20638b8e5c026930c3e6039a33ce45964ff2167f6ecedd419db06c1", Convert.ToHexStringLower(ctx.Finish()));
    }
}
